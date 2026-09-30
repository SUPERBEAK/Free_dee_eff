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

        public MainWindow(string? initialFilePath = null)
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;

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
    }
}