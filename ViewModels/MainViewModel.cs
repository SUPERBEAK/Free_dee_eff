using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Freedeeeff.Models;
using Freedeeeff.Models.Annotations;
using Freedeeeff.Services;
using Microsoft.Win32;

namespace Freedeeeff.ViewModels
{
    public enum ActiveToolMode
    {
        Select,
        Hand,
        TextFill,
        Signature,
        Highlight,
        Pen,
        Rectangle,
        Ellipse,
        Line,
        Redaction,
        Stamp,
        Eraser
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly PdfRenderService _renderService = new();
        private readonly OcrEngineService _ocrService = new();
        private readonly PdfSplitService _splitService = new();
        private readonly PdfPageService _pageService = new();
        private readonly PdfExportService _exportService = new();
        private readonly PdfTextSearchService _searchService = new();
        private readonly SignatureStorageService _sigStorage = new();

        [ObservableProperty]
        private string? _currentFilePath;

        [ObservableProperty]
        private string _documentTitle = "Freedeeeff PDF Editor";

        [ObservableProperty]
        private bool _isDocumentLoaded;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentPageNumber))]
        private int _currentPageIndex;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentPageNumber))]
        private int _totalPages;

        public int CurrentPageNumber => TotalPages == 0 ? 0 : CurrentPageIndex + 1;

        [ObservableProperty]
        private bool _isFitWidthMode = true;

        public Action? RequestFitWidthAction { get; set; }
        public Action? RequestFitPageAction { get; set; }

        [ObservableProperty]
        private double _zoomLevel = 1.0;

        [ObservableProperty]
        private ActiveToolMode _activeTool = ActiveToolMode.Select;

        [ObservableProperty]
        private bool _isOcrBusy;

        [ObservableProperty]
        private string _ocrStatusMessage = "Ready";

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _hasUnsavedChanges;

        // Tool customization options
        [ObservableProperty]
        private string _selectedFontFamily = "Arial";

        [ObservableProperty]
        private double _selectedFontSize = 14;

        [ObservableProperty]
        private bool _isBold;

        [ObservableProperty]
        private bool _isItalic;

        [ObservableProperty]
        private string _selectedTextColor = "#000000";

        [ObservableProperty]
        private string _selectedStrokeColor = "#0078D7";

        [ObservableProperty]
        private double _selectedStrokeThickness = 2.5;

        [ObservableProperty]
        private string _selectedHighlightColor = "#FFFF00";

        [ObservableProperty]
        private RedactionType _selectedRedactionType = RedactionType.Blackout;

        [ObservableProperty]
        private string _selectedStampTitle = "APPROVED";

        // Active signature ready to place
        [ObservableProperty]
        private byte[]? _activeSignatureImageBytes;

        // Search in document
        [ObservableProperty]
        private bool _isSearchOpen;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private int _currentSearchMatchIndex = -1;

        [ObservableProperty]
        private int _totalSearchMatches;

        [ObservableProperty]
        private string _searchStatusText = string.Empty;

        // Collections
        public ObservableCollection<PdfPageViewModel> Pages { get; } = new();
        public ObservableCollection<SavedSignature> SavedSignatures { get; } = new();
        public List<SearchMatchItem> AllSearchMatches { get; } = new();

        // Undo / Redo history
        private readonly Stack<Action> _undoStack = new();
        private readonly Stack<Action> _redoStack = new();

        public MainViewModel()
        {
            LoadSavedSignatures();
        }

        public void LoadSavedSignatures()
        {
            SavedSignatures.Clear();
            foreach (var sig in _sigStorage.GetSavedSignatures())
            {
                SavedSignatures.Add(sig);
            }
        }

        [RelayCommand]
        public async Task OpenFileAsync(string? explicitPath = null)
        {
            string? filePath = explicitPath;

            if (string.IsNullOrEmpty(filePath))
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "PDF Documents (*.pdf)|*.pdf|All Files (*.*)|*.*",
                    Title = "Open PDF Document"
                };

                if (dialog.ShowDialog() != true) return;
                filePath = dialog.FileName;
            }

            if (!File.Exists(filePath)) return;

            try
            {
                StatusMessage = "Loading document...";
                CurrentFilePath = filePath;
                DocumentTitle = $"{Path.GetFileName(filePath)} - Freedeeeff";

                Pages.Clear();
                _undoStack.Clear();
                _redoStack.Clear();
                HasUnsavedChanges = false;

                int pageCount = _renderService.GetPageCount(filePath);
                TotalPages = pageCount;

                for (int i = 0; i < pageCount; i++)
                {
                    var (w, h) = _renderService.GetPageSize(filePath, i);
                    var pageVm = new PdfPageViewModel(i, w, h);
                    Pages.Add(pageVm);
                }

                IsDocumentLoaded = true;
                CurrentPageIndex = 0;
                IsFitWidthMode = true;
                StatusMessage = $"Loaded {pageCount} page(s).";

                // Trigger default fit to middle area width
                RequestFitWidthAction?.Invoke();

                // Render first few pages and thumbnails in background
                _ = RenderInitialPagesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open PDF: {ex.Message}", "Open PDF Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Error loading document.";
            }
        }

        private async Task RenderInitialPagesAsync()
        {
            if (string.IsNullOrEmpty(CurrentFilePath)) return;

            string path = CurrentFilePath;

            await Task.Run(() =>
            {
                for (int i = 0; i < Pages.Count; i++)
                {
                    int index = i;
                    var thumb = _renderService.RenderThumbnail(path, index, 140);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (index < Pages.Count)
                        {
                            Pages[index].Thumbnail = thumb;
                        }
                    });

                    // Render full resolution image for visible/initial pages
                    if (index < 5)
                    {
                        var rendered = _renderService.RenderPage(path, index, ZoomLevel);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (index < Pages.Count)
                            {
                                Pages[index].RenderedImage = rendered;
                            }
                        });
                    }
                }
            });
        }

        public void EnsurePageRendered(int pageIndex)
        {
            if (string.IsNullOrEmpty(CurrentFilePath) || pageIndex < 0 || pageIndex >= Pages.Count) return;
            var page = Pages[pageIndex];
            if (page.RenderedImage != null) return;

            string path = CurrentFilePath;
            double zoom = ZoomLevel;

            Task.Run(() =>
            {
                var img = _renderService.RenderPage(path, pageIndex, zoom);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    page.RenderedImage = img;
                });
            });
        }

        [RelayCommand]
        public async Task SaveAsync()
        {
            if (string.IsNullOrEmpty(CurrentFilePath)) return;
            await SaveInternalAsync(CurrentFilePath);
        }

        [RelayCommand]
        public async Task SaveAsAsync()
        {
            if (string.IsNullOrEmpty(CurrentFilePath)) return;

            var dialog = new SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = Path.GetFileNameWithoutExtension(CurrentFilePath) + "_edited.pdf",
                Title = "Save PDF As"
            };

            if (dialog.ShowDialog() == true)
            {
                await SaveInternalAsync(dialog.FileName);
                CurrentFilePath = dialog.FileName;
                DocumentTitle = $"{Path.GetFileName(dialog.FileName)} - Freedeeeff";
            }
        }

        private async Task SaveInternalAsync(string targetPath)
        {
            if (string.IsNullOrEmpty(CurrentFilePath)) return;

            try
            {
                StatusMessage = "Saving changes...";
                var annotationsByPage = new Dictionary<int, List<AnnotationItem>>();
                foreach (var page in Pages)
                {
                    if (page.Annotations.Count > 0)
                    {
                        annotationsByPage[page.PageIndex] = page.Annotations.ToList();
                    }
                }

                bool success = await Task.Run(() =>
                    _exportService.ExportWithAnnotations(CurrentFilePath, targetPath, annotationsByPage));

                if (success)
                {
                    HasUnsavedChanges = false;
                    StatusMessage = $"Saved successfully to {Path.GetFileName(targetPath)}";
                    MessageBox.Show("Document saved successfully!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = "Save failed.";
                    MessageBox.Show("Could not save the PDF. Please check if file is opened elsewhere.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Save error.";
                MessageBox.Show($"Error saving PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task OcrCurrentPageAsync()
        {
            if (string.IsNullOrEmpty(CurrentFilePath) || CurrentPageIndex < 0 || CurrentPageIndex >= Pages.Count) return;

            var page = Pages[CurrentPageIndex];
            try
            {
                IsOcrBusy = true;
                OcrStatusMessage = $"Recognizing page {page.PageNumber}...";
                StatusMessage = $"Running Windows OCR on page {page.PageNumber}...";

                var (rawBgra, w, h) = _renderService.RenderPageRawBgra(CurrentFilePath, page.PageIndex, 1.5);
                var ocrResult = await _ocrService.RecognizePageAsync(rawBgra, w, h, page.PageIndex);

                page.OcrResult = ocrResult;
                page.IsOcrOverlayVisible = true;

                OcrStatusMessage = $"OCR Complete: {ocrResult.Lines.Count} lines detected";
                StatusMessage = $"OCR finished for page {page.PageNumber} ({ocrResult.Lines.Count} lines).";
            }
            catch (Exception ex)
            {
                OcrStatusMessage = "OCR Failed";
                StatusMessage = "OCR error.";
                MessageBox.Show($"OCR recognition failed: {ex.Message}", "OCR Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                IsOcrBusy = false;
            }
        }

        [RelayCommand]
        public async Task OcrAllPagesAsync()
        {
            if (string.IsNullOrEmpty(CurrentFilePath)) return;

            try
            {
                IsOcrBusy = true;
                for (int i = 0; i < Pages.Count; i++)
                {
                    var page = Pages[i];
                    OcrStatusMessage = $"OCR {i + 1}/{Pages.Count}...";
                    StatusMessage = $"Running OCR on page {i + 1} of {Pages.Count}...";

                    var (rawBgra, w, h) = _renderService.RenderPageRawBgra(CurrentFilePath, i, 1.5);
                    var ocrResult = await _ocrService.RecognizePageAsync(rawBgra, w, h, i);
                    page.OcrResult = ocrResult;
                    page.IsOcrOverlayVisible = true;
                }

                OcrStatusMessage = "All pages OCR complete!";
                StatusMessage = "Document-wide OCR complete.";
                MessageBox.Show("OCR completed for all pages in the document!", "OCR Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"OCR error: {ex.Message}", "OCR Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsOcrBusy = false;
            }
        }

        [RelayCommand]
        public void ToggleSearch()
        {
            IsSearchOpen = !IsSearchOpen;
            if (!IsSearchOpen)
            {
                ClearSearchHighlights();
            }
        }

        [RelayCommand]
        public void PerformSearch()
        {
            if (string.IsNullOrWhiteSpace(CurrentFilePath) || string.IsNullOrWhiteSpace(SearchQuery))
            {
                ClearSearchHighlights();
                SearchStatusText = "No query entered";
                return;
            }

            ClearSearchHighlights();

            var ocrDict = Pages
                .Where(p => p.OcrResult != null)
                .ToDictionary(p => p.PageIndex, p => p.OcrResult!);

            var hits = _searchService.SearchInPdf(CurrentFilePath, SearchQuery, ocrDict);
            AllSearchMatches.Clear();
            AllSearchMatches.AddRange(hits);
            TotalSearchMatches = hits.Count;

            if (hits.Count > 0)
            {
                foreach (var hit in hits)
                {
                    if (hit.PageIndex < Pages.Count)
                    {
                        Pages[hit.PageIndex].SearchHighlights.Add(hit);
                    }
                }

                CurrentSearchMatchIndex = 0;
                NavigateToMatch(0);
                SearchStatusText = $"1 of {hits.Count} matches";
            }
            else
            {
                CurrentSearchMatchIndex = -1;
                SearchStatusText = "No matches found";
            }
        }

        [RelayCommand]
        public void NextSearchMatch()
        {
            if (AllSearchMatches.Count == 0) return;
            CurrentSearchMatchIndex = (CurrentSearchMatchIndex + 1) % AllSearchMatches.Count;
            NavigateToMatch(CurrentSearchMatchIndex);
            SearchStatusText = $"{CurrentSearchMatchIndex + 1} of {AllSearchMatches.Count} matches";
        }

        [RelayCommand]
        public void PreviousSearchMatch()
        {
            if (AllSearchMatches.Count == 0) return;
            CurrentSearchMatchIndex = (CurrentSearchMatchIndex - 1 + AllSearchMatches.Count) % AllSearchMatches.Count;
            NavigateToMatch(CurrentSearchMatchIndex);
            SearchStatusText = $"{CurrentSearchMatchIndex + 1} of {AllSearchMatches.Count} matches";
        }

        private void NavigateToMatch(int index)
        {
            if (index < 0 || index >= AllSearchMatches.Count) return;
            var hit = AllSearchMatches[index];
            CurrentPageIndex = hit.PageIndex;
        }

        private void ClearSearchHighlights()
        {
            AllSearchMatches.Clear();
            TotalSearchMatches = 0;
            CurrentSearchMatchIndex = -1;
            foreach (var page in Pages)
            {
                page.SearchHighlights.Clear();
            }
        }

        // Page navigation commands
        [RelayCommand]
        public void PreviousPage()
        {
            if (CurrentPageIndex > 0)
            {
                CurrentPageIndex--;
            }
        }

        [RelayCommand]
        public void NextPage()
        {
            if (CurrentPageIndex < Pages.Count - 1)
            {
                CurrentPageIndex++;
            }
        }

        // Zoom commands
        [RelayCommand]
        public void ZoomIn()
        {
            IsFitWidthMode = false;
            SetZoom(ZoomLevel + 0.15);
        }

        [RelayCommand]
        public void ZoomOut()
        {
            IsFitWidthMode = false;
            SetZoom(ZoomLevel - 0.15);
        }

        [RelayCommand]
        public void ZoomReset()
        {
            IsFitWidthMode = false;
            SetZoom(1.0);
        }

        [RelayCommand]
        public void ZoomFitWidth()
        {
            IsFitWidthMode = true;
            RequestFitWidthAction?.Invoke();
        }

        [RelayCommand]
        public void ZoomFitPage()
        {
            IsFitWidthMode = false;
            RequestFitPageAction?.Invoke();
        }

        public void SetZoom(double val)
        {
            ZoomLevel = Math.Clamp(Math.Round(val, 2), 0.25, 4.0);
            StatusMessage = $"Zoom: {(int)(ZoomLevel * 100)}%";
            RefreshRenderedPagesAtCurrentZoom();
        }

        public void SetZoomDirect(double val)
        {
            ZoomLevel = Math.Clamp(Math.Round(val, 2), 0.25, 4.0);
            StatusMessage = $"Zoom: {(int)(ZoomLevel * 100)}%";
            RefreshRenderedPagesAtCurrentZoom();
        }

        private void RefreshRenderedPagesAtCurrentZoom()
        {
            if (string.IsNullOrEmpty(CurrentFilePath) || Pages.Count == 0) return;

            string path = CurrentFilePath;
            double zoom = ZoomLevel;
            int current = CurrentPageIndex;

            Task.Run(() =>
            {
                int start = Math.Max(0, current - 1);
                int end = Math.Min(Pages.Count - 1, current + 1);

                for (int i = start; i <= end; i++)
                {
                    int index = i;
                    var img = _renderService.RenderPage(path, index, zoom);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (index < Pages.Count && Math.Abs(ZoomLevel - zoom) < 0.01)
                        {
                            Pages[index].RenderedImage = img;
                        }
                    });
                }
            });
        }

        // Tool selection
        [RelayCommand]
        public void SetTool(string toolName)
        {
            if (Enum.TryParse<ActiveToolMode>(toolName, out var mode))
            {
                ActiveTool = mode;
                StatusMessage = $"Tool: {mode}";
            }
        }

        // Annotation helpers
        public void AddAnnotationToPage(int pageIndex, AnnotationItem item)
        {
            if (pageIndex < 0 || pageIndex >= Pages.Count) return;
            var page = Pages[pageIndex];
            page.AddAnnotation(item);
            HasUnsavedChanges = true;

            // Undo action
            _undoStack.Push(() =>
            {
                page.RemoveAnnotation(item);
            });
            _redoStack.Clear();
        }

        [RelayCommand]
        public void Undo()
        {
            if (_undoStack.Count > 0)
            {
                var action = _undoStack.Pop();
                action();
                StatusMessage = "Undo action performed.";
            }
        }

        // Split & Merge Actions
        public SplitResult ExecuteSplit(SplitOptions options)
        {
            if (string.IsNullOrEmpty(CurrentFilePath))
            {
                return new SplitResult { Success = false, Message = "No document is currently loaded." };
            }

            return _splitService.SplitPdf(CurrentFilePath, options);
        }

        public bool ExecuteMerge(IEnumerable<string> files, string destPath, out string error)
        {
            return _splitService.MergePdfs(files, destPath, out error);
        }

        // Page manipulation
        public bool RotateCurrentPage(int angle = 90)
        {
            if (string.IsNullOrEmpty(CurrentFilePath) || CurrentPageIndex < 0 || CurrentPageIndex >= Pages.Count) return false;
            string path = CurrentFilePath;
            bool ok = _pageService.RotatePages(path, path, new[] { CurrentPageIndex }, angle);
            if (ok)
            {
                // Refresh rendered image & thumbnail
                Pages[CurrentPageIndex].RenderedImage = _renderService.RenderPage(path, CurrentPageIndex, ZoomLevel);
                Pages[CurrentPageIndex].Thumbnail = _renderService.RenderThumbnail(path, CurrentPageIndex, 140);
                StatusMessage = $"Rotated page {CurrentPageIndex + 1} by {angle}°";
            }
            return ok;
        }

        public bool DeleteCurrentPage()
        {
            if (string.IsNullOrEmpty(CurrentFilePath) || Pages.Count <= 1)
            {
                MessageBox.Show("Cannot delete the only page in the document.", "Delete Page", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            int toDelete = CurrentPageIndex;
            string path = CurrentFilePath;
            bool ok = _pageService.DeletePages(path, path, new[] { toDelete });
            if (ok)
            {
                Pages.RemoveAt(toDelete);
                TotalPages = Pages.Count;
                for (int i = 0; i < Pages.Count; i++)
                {
                    Pages[i].PageIndex = i;
                }
                CurrentPageIndex = Math.Clamp(toDelete, 0, Pages.Count - 1);
                StatusMessage = $"Deleted page {toDelete + 1}";
            }
            return ok;
        }
    }
}
