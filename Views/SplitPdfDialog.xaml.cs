using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Freedeeeff.Models;
using Freedeeeff.Services;
using Microsoft.Win32;

namespace Freedeeeff.Views
{
    public partial class SplitPdfDialog : Window
    {
        private readonly string _sourcePdfPath;
        private readonly int _totalPages;
        private readonly List<int> _selectedPageIndices;
        private readonly PdfSplitService _splitService = new();
        private SplitResult? _lastResult;

        public SplitPdfDialog(string sourcePdfPath, int totalPages, IEnumerable<int>? selectedPageIndices = null)
        {
            InitializeComponent();

            _sourcePdfPath = sourcePdfPath;
            _totalPages = totalPages;
            _selectedPageIndices = selectedPageIndices?.ToList() ?? new List<int>();

            string fileName = Path.GetFileName(sourcePdfPath);
            TxtDocumentInfo.Text = $"Source: {fileName} ({totalPages} pages)";
            TxtFilePrefix.Text = Path.GetFileNameWithoutExtension(sourcePdfPath);

            string defaultDir = Path.GetDirectoryName(sourcePdfPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            TxtOutputDir.Text = Path.Combine(defaultDir, $"{Path.GetFileNameWithoutExtension(sourcePdfPath)}_split");

            if (_selectedPageIndices.Count == 0)
            {
                RbSelectedPages.IsEnabled = false;
                RbSelectedPages.Content = "Extract only currently selected pages (none selected)";
            }
            else
            {
                RbSelectedPages.Content = $"Extract only currently selected pages ({_selectedPageIndices.Count} selected)";
            }

            UpdatePreview();
        }

        private void Mode_Changed(object sender, RoutedEventArgs e)
        {
            if (TxtRanges == null || TxtEveryN == null) return;

            TxtRanges.IsEnabled = RbRanges.IsChecked == true;
            TxtEveryN.IsEnabled = RbEveryN.IsChecked == true;

            UpdatePreview();
        }

        private void TxtRanges_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();
        private void TxtEveryN_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();
        private void TxtFilePrefix_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

        private void UpdatePreview()
        {
            if (TxtPreview == null) return;
            string prefix = string.IsNullOrWhiteSpace(TxtFilePrefix.Text) ? "Document" : TxtFilePrefix.Text.Trim();

            if (RbAllPages.IsChecked == true)
            {
                TxtPreview.Text = $"Preview: Will create {_totalPages} files: {prefix}_page_001.pdf, {prefix}_page_002.pdf ... {prefix}_page_{_totalPages:D3}.pdf";
            }
            else if (RbRanges.IsChecked == true)
            {
                var ranges = PdfSplitService.ParsePageRanges(TxtRanges.Text, _totalPages);
                if (ranges.Count == 0)
                {
                    TxtPreview.Text = "Preview: Please enter valid page ranges (e.g. 1-3, 4-6, 7).";
                }
                else
                {
                    var fileNames = ranges.Select((r, i) => $"{prefix}_range_{i + 1:D2}_p{r.First()}-p{r.Last()}.pdf");
                    TxtPreview.Text = $"Preview ({ranges.Count} files): " + string.Join(", ", fileNames.Take(3)) + (ranges.Count > 3 ? "..." : "");
                }
            }
            else if (RbEveryN.IsChecked == true)
            {
                if (int.TryParse(TxtEveryN.Text, out int n) && n > 0)
                {
                    int parts = (int)Math.Ceiling((double)_totalPages / n);
                    TxtPreview.Text = $"Preview: Will create {parts} file(s) containing up to {n} pages each: {prefix}_part_01_p1-p{Math.Min(n, _totalPages)}.pdf ...";
                }
                else
                {
                    TxtPreview.Text = "Preview: Please enter a valid number of pages.";
                }
            }
            else if (RbSelectedPages.IsChecked == true)
            {
                TxtPreview.Text = $"Preview: Will create 1 file containing {_selectedPageIndices.Count} selected page(s): {prefix}_extracted.pdf";
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Destination Folder",
                InitialDirectory = Directory.Exists(TxtOutputDir.Text) ? TxtOutputDir.Text : Path.GetDirectoryName(_sourcePdfPath)
            };

            if (dialog.ShowDialog() == true)
            {
                TxtOutputDir.Text = dialog.FolderName;
            }
        }

        private async void BtnSplit_Click(object sender, RoutedEventArgs e)
        {
            string outDir = TxtOutputDir.Text.Trim();
            if (string.IsNullOrWhiteSpace(outDir))
            {
                MessageBox.Show("Please specify an output folder.", "Output Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var options = new SplitOptions
            {
                OutputDirectory = outDir,
                BaseFileName = TxtFilePrefix.Text.Trim()
            };

            if (RbAllPages.IsChecked == true)
            {
                options.Mode = SplitMode.AllPages;
            }
            else if (RbRanges.IsChecked == true)
            {
                options.Mode = SplitMode.ByRanges;
                options.RangeString = TxtRanges.Text.Trim();
            }
            else if (RbEveryN.IsChecked == true)
            {
                options.Mode = SplitMode.EveryNPages;
                if (!int.TryParse(TxtEveryN.Text, out int n) || n <= 0)
                {
                    MessageBox.Show("Please enter a valid number of pages per split.", "Invalid Number", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                options.EveryNPagesCount = n;
            }
            else if (RbSelectedPages.IsChecked == true)
            {
                options.Mode = SplitMode.SelectedPages;
                options.SelectedPageIndices = _selectedPageIndices;
            }

            BtnSplit.IsEnabled = false;
            ProgressSplit.Visibility = Visibility.Visible;
            TxtStatus.Text = "Splitting PDF...";

            _lastResult = await Task.Run(() => _splitService.SplitPdf(_sourcePdfPath, options));

            ProgressSplit.Visibility = Visibility.Collapsed;
            BtnSplit.IsEnabled = true;

            if (_lastResult.Success)
            {
                TxtStatus.Text = $"Success! Created {_lastResult.GeneratedFiles.Count} file(s).";
                BtnOpenFolder.Visibility = Visibility.Visible;
                MessageBox.Show($"Successfully split PDF into {_lastResult.GeneratedFiles.Count} file(s) in:\n{outDir}", "Split Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                TxtStatus.Text = $"Failed: {_lastResult.Message}";
                MessageBox.Show($"Could not split PDF:\n{_lastResult.Message}", "Split Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string dir = TxtOutputDir.Text.Trim();
            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
