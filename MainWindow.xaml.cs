using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Freedeeeff.Controls;
using Freedeeeff.Services;
using Freedeeeff.ViewModels;
using Freedeeeff.Views;
using Wpf.Ui.Appearance;

namespace Freedeeeff
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        private readonly MainViewModel _vm;
        private bool _isSyncingScroll;

        public MainWindow(string? initialFilePath = null)
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;

            _vm.RequestFitWidthAction = ApplyFitWidth;
            _vm.RequestFitPageAction = ApplyFitPage;
            _vm.PropertyChanged += Vm_PropertyChanged;

            // Apply initial theme
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);

            UpdateToolOptionsPanels(ActiveToolMode.Select);

            if (!string.IsNullOrEmpty(initialFilePath) && File.Exists(initialFilePath))
            {
                Loaded += async (s, e) =>
                {
                    await _vm.OpenFileAsync(initialFilePath);
                };
            }
        }

        private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            var current = ApplicationThemeManager.GetAppTheme();
            var next = current == ApplicationTheme.Dark ? ApplicationTheme.Light : ApplicationTheme.Dark;
            ApplicationThemeManager.Apply(next);
        }

        private void Tool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string toolStr)
            {
                if (Enum.TryParse<ActiveToolMode>(toolStr, out var mode))
                {
                    _vm.ActiveTool = mode;
                    UpdateToolOptionsPanels(mode);
                }
            }
        }

        private void UpdateToolOptionsPanels(ActiveToolMode mode)
        {
            PanelTextOptions.Visibility = mode == ActiveToolMode.TextFill ? Visibility.Visible : Visibility.Collapsed;
            PanelStrokeOptions.Visibility = (mode == ActiveToolMode.Pen || mode == ActiveToolMode.Rectangle || mode == ActiveToolMode.Ellipse || mode == ActiveToolMode.Line) ? Visibility.Visible : Visibility.Collapsed;
            PanelHighlightOptions.Visibility = mode == ActiveToolMode.Highlight ? Visibility.Visible : Visibility.Collapsed;
            PanelRedactOptions.Visibility = mode == ActiveToolMode.Redaction ? Visibility.Visible : Visibility.Collapsed;
            PanelStampOptions.Visibility = mode == ActiveToolMode.Stamp ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CmbFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbFontFamily.SelectedItem is ComboBoxItem item && _vm != null)
            {
                _vm.SelectedFontFamily = item.Content.ToString() ?? "Arial";
            }
        }

        private void CmbFontSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbFontSize.SelectedItem is ComboBoxItem item && _vm != null)
            {
                if (double.TryParse(item.Content.ToString(), out double s))
                {
                    _vm.SelectedFontSize = s;
                }
            }
        }

        private void Style_Changed(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.IsBold = BtnBold.IsChecked == true;
            _vm.IsItalic = BtnItalic.IsChecked == true;
        }

        private void CmbTextColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbTextColor.SelectedItem is ComboBoxItem item && _vm != null)
            {
                _vm.SelectedTextColor = item.Tag?.ToString() ?? "#000000";
            }
        }

        private void CmbStrokeColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbStrokeColor.SelectedItem is ComboBoxItem item && _vm != null)
            {
                _vm.SelectedStrokeColor = item.Tag?.ToString() ?? "#0078D7";
            }
        }

        private void SliderThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_vm != null)
            {
                _vm.SelectedStrokeThickness = e.NewValue;
            }
        }

        private void CmbHighlightColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbHighlightColor.SelectedItem is ComboBoxItem item && _vm != null)
            {
                _vm.SelectedHighlightColor = item.Tag?.ToString() ?? "#FFFF00";
            }
        }

        private void CmbRedactionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbRedactionType.SelectedItem is ComboBoxItem item && _vm != null)
            {
                string text = item.Content.ToString() ?? "Blackout";
                _vm.SelectedRedactionType = text == "Whiteout" ? Models.Annotations.RedactionType.Whiteout : Models.Annotations.RedactionType.Blackout;
            }
        }

        private void CmbStampTitle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbStampTitle.SelectedItem is ComboBoxItem item && _vm != null)
            {
                _vm.SelectedStampTitle = item.Content.ToString() ?? "APPROVED";
            }
        }

        private void BtnOpenSignaturePad_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SignaturePadDialog { Owner = this };
            if (dlg.ShowDialog() == true && dlg.GeneratedImageBytes != null)
            {
                _vm.ActiveSignatureImageBytes = dlg.GeneratedImageBytes;
                _vm.LoadSavedSignatures();
                _vm.ActiveTool = ActiveToolMode.Signature;
                ToolSignature.IsChecked = true;
                UpdateToolOptionsPanels(ActiveToolMode.Signature);
            }
        }

        private void LstSignatures_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox lb && lb.SelectedItem is SavedSignature sig)
            {
                _vm.ActiveSignatureImageBytes = sig.ImageBytes;
                _vm.ActiveTool = ActiveToolMode.Signature;
                ToolSignature.IsChecked = true;
                UpdateToolOptionsPanels(ActiveToolMode.Signature);
            }
        }

        private void BtnSplitPdf_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_vm.CurrentFilePath)) return;

            var selected = _vm.Pages.Where(p => p.IsSelectedInSidebar).Select(p => p.PageIndex).ToList();
            var dlg = new SplitPdfDialog(_vm.CurrentFilePath, _vm.TotalPages, selected) { Owner = this };
            dlg.ShowDialog();
        }

        private void BtnMergePdf_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new MergePdfDialog(_vm.CurrentFilePath) { Owner = this };
            dlg.ShowDialog();
        }

        private void BtnInspectOcr_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.CurrentPageIndex < 0 || _vm.CurrentPageIndex >= _vm.Pages.Count) return;

            var page = _vm.Pages[_vm.CurrentPageIndex];
            if (page.OcrResult == null)
            {
                var prompt = MessageBox.Show($"Page {page.PageNumber} has not been OCR processed yet. Run OCR now?", "Run OCR", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (prompt == MessageBoxResult.Yes)
                {
                    _vm.OcrCurrentPageCommand.Execute(null);
                }
                return;
            }

            var dlg = new OcrResultsDialog(page.OcrResult.FullText, page.PageNumber) { Owner = this };
            dlg.ShowDialog();
        }

        private void BtnRotate_Click(object sender, RoutedEventArgs e)
        {
            _vm.RotateCurrentPage(90);
        }

        private void BtnDeletePage_Click(object sender, RoutedEventArgs e)
        {
            _vm.DeleteCurrentPage();
        }

        private void ContextMenu_Rotate_Click(object sender, RoutedEventArgs e)
        {
            _vm.RotateCurrentPage(90);
        }

        private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            _vm.DeleteCurrentPage();
        }

        private void ContextMenu_Extract_Click(object sender, RoutedEventArgs e)
        {
            BtnSplitPdf_Click(sender, e);
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true && _vm.CurrentPageIndex >= 0 && _vm.CurrentPageIndex < _vm.Pages.Count)
            {
                var page = _vm.Pages[_vm.CurrentPageIndex];
                if (page.RenderedImage != null)
                {
                    var visual = new DrawingVisual();
                    using (var dc = visual.RenderOpen())
                    {
                        dc.DrawImage(page.RenderedImage, new Rect(0, 0, printDlg.PrintableAreaWidth, printDlg.PrintableAreaHeight));
                    }
                    printDlg.PrintVisual(visual, $"Freedeeeff - Page {page.PageNumber}");
                }
            }
        }

        private void PdfScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Delta > 0)
                {
                    _vm.ZoomIn();
                }
                else if (e.Delta < 0)
                {
                    _vm.ZoomOut();
                }
                e.Handled = true;
            }
        }

        private void PdfScrollViewer_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void PdfScrollViewer_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var pdf = files.FirstOrDefault(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(pdf))
                {
                    _ = _vm.OpenFileAsync(pdf);
                }
            }
        }

        private void PdfPageCanvas_LayoutUpdated(object? sender, EventArgs e)
        {
            if (sender is PdfPageCanvas canvas && canvas.DataContext is PdfPageViewModel pageVm)
            {
                _vm.EnsurePageRendered(pageVm.PageIndex);
            }
        }

        private void TxtSearchQuery_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _vm.PerformSearch();
            }
        }

        private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentPageIndex))
            {
                if (!_isSyncingScroll)
                {
                    ScrollToPage(_vm.CurrentPageIndex);
                }
                if (_vm.CurrentPageIndex >= 0 && _vm.CurrentPageIndex < _vm.Pages.Count)
                {
                    SidebarPageListBox.ScrollIntoView(_vm.Pages[_vm.CurrentPageIndex]);
                }
            }
            else if (e.PropertyName == nameof(MainViewModel.IsDocumentLoaded))
            {
                if (_vm.IsDocumentLoaded)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        ApplyFitWidth();
                        ScrollToPage(0);
                    }, System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
        }

        public void ApplyFitWidth()
        {
            if (_vm == null || !_vm.IsDocumentLoaded || _vm.Pages.Count == 0) return;

            double viewportWidth = PdfScrollViewer.ActualWidth;
            if (viewportWidth <= 100)
            {
                Dispatcher.InvokeAsync(ApplyFitWidth, System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            // Available width accounts for left/right margins (24*2 = 48) and scrollbar (~18) + border (2)
            double availableWidth = Math.Max(100, viewportWidth - 68);
            int idx = Math.Clamp(_vm.CurrentPageIndex, 0, _vm.Pages.Count - 1);
            double pageWidth = _vm.Pages[idx].Width;
            if (pageWidth <= 0) pageWidth = 612;

            double targetZoom = Math.Clamp(Math.Round(availableWidth / pageWidth, 2), 0.25, 4.0);
            _vm.SetZoomDirect(targetZoom);
        }

        public void ApplyFitPage()
        {
            if (_vm == null || !_vm.IsDocumentLoaded || _vm.Pages.Count == 0) return;

            double viewportWidth = PdfScrollViewer.ActualWidth;
            double viewportHeight = PdfScrollViewer.ActualHeight;
            if (viewportWidth <= 100 || viewportHeight <= 100)
            {
                Dispatcher.InvokeAsync(ApplyFitPage, System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            double availableWidth = Math.Max(100, viewportWidth - 68);
            double availableHeight = Math.Max(100, viewportHeight - 48);
            int idx = Math.Clamp(_vm.CurrentPageIndex, 0, _vm.Pages.Count - 1);
            double pageWidth = _vm.Pages[idx].Width;
            double pageHeight = _vm.Pages[idx].Height;
            if (pageWidth <= 0) pageWidth = 612;
            if (pageHeight <= 0) pageHeight = 792;

            double zoomW = availableWidth / pageWidth;
            double zoomH = availableHeight / pageHeight;
            double targetZoom = Math.Clamp(Math.Round(Math.Min(zoomW, zoomH), 2), 0.25, 4.0);
            _vm.SetZoomDirect(targetZoom);
        }

        private void PdfScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_vm != null && _vm.IsFitWidthMode && _vm.IsDocumentLoaded && e.WidthChanged)
            {
                ApplyFitWidth();
            }
        }

        private void PdfScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingScroll || _vm == null || !_vm.IsDocumentLoaded || _vm.Pages.Count == 0) return;

            if (e.VerticalChange != 0 || e.ViewportHeightChange != 0)
            {
                UpdateCurrentPageFromScroll();
            }
        }

        private void UpdateCurrentPageFromScroll()
        {
            if (_vm == null || !_vm.IsDocumentLoaded || _vm.Pages.Count == 0) return;

            double viewportHeight = PdfScrollViewer.ViewportHeight;
            if (viewportHeight <= 0) return;

            int bestIndex = -1;
            double maxOverlap = -1;

            for (int i = 0; i < _vm.Pages.Count; i++)
            {
                var container = PdfPagesControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container == null) continue;

                try
                {
                    GeneralTransform transform = container.TransformToAncestor(PdfScrollViewer);
                    Point topPoint = transform.Transform(new Point(0, 0));
                    double top = topPoint.Y;
                    double bottom = top + container.ActualHeight;

                    double visibleTop = Math.Max(0, top);
                    double visibleBottom = Math.Min(viewportHeight, bottom);
                    double overlap = visibleBottom - visibleTop;

                    if (overlap > maxOverlap)
                    {
                        maxOverlap = overlap;
                        bestIndex = i;
                    }
                }
                catch
                {
                    // Ignore transient layout state
                }
            }

            if (bestIndex >= 0 && bestIndex != _vm.CurrentPageIndex)
            {
                _isSyncingScroll = true;
                try
                {
                    _vm.CurrentPageIndex = bestIndex;
                    if (bestIndex < _vm.Pages.Count)
                    {
                        SidebarPageListBox.ScrollIntoView(_vm.Pages[bestIndex]);
                    }
                }
                finally
                {
                    _isSyncingScroll = false;
                }
            }
        }

        private void ScrollToPage(int index)
        {
            if (_vm == null || index < 0 || index >= _vm.Pages.Count) return;

            var container = PdfPagesControl.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
            if (container != null)
            {
                _isSyncingScroll = true;
                try
                {
                    GeneralTransform transform = container.TransformToAncestor(PdfPagesControl);
                    Point target = transform.Transform(new Point(0, 0));
                    PdfScrollViewer.ScrollToVerticalOffset(Math.Max(0, target.Y - 12));
                }
                catch
                {
                    container.BringIntoView();
                }
                finally
                {
                    _isSyncingScroll = false;
                }
            }
            else
            {
                Dispatcher.InvokeAsync(() =>
                {
                    var c = PdfPagesControl.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                    if (c != null)
                    {
                        _isSyncingScroll = true;
                        try
                        {
                            GeneralTransform transform = c.TransformToAncestor(PdfPagesControl);
                            Point target = transform.Transform(new Point(0, 0));
                            PdfScrollViewer.ScrollToVerticalOffset(Math.Max(0, target.Y - 12));
                        }
                        catch
                        {
                            c.BringIntoView();
                        }
                        finally
                        {
                            _isSyncingScroll = false;
                        }
                    }
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
    }
}