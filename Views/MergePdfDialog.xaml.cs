using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using Freedeeeff.Services;
using Microsoft.Win32;

namespace Freedeeeff.Views
{
    public class MergeItem
    {
        public string FullPath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FullPath);
    }

    public partial class MergePdfDialog : Window
    {
        public ObservableCollection<MergeItem> Items { get; } = new();
        private readonly PdfSplitService _splitService = new();

        public MergePdfDialog(string? initialFile = null)
        {
            InitializeComponent();
            LstFiles.ItemsSource = Items;

            if (!string.IsNullOrEmpty(initialFile) && File.Exists(initialFile))
            {
                Items.Add(new MergeItem { FullPath = initialFile });
                string dir = Path.GetDirectoryName(initialFile) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                TxtOutputPath.Text = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(initialFile)}_merged.pdf");
            }
        }

        private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF Documents (*.pdf)|*.pdf|All Files (*.*)|*.*",
                Multiselect = true,
                Title = "Select PDFs to Merge"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!Items.Any(x => x.FullPath.Equals(file, StringComparison.OrdinalIgnoreCase)))
                    {
                        Items.Add(new MergeItem { FullPath = file });
                    }
                }

                if (string.IsNullOrEmpty(TxtOutputPath.Text) && Items.Count > 0)
                {
                    string dir = Path.GetDirectoryName(Items[0].FullPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    TxtOutputPath.Text = Path.Combine(dir, "Merged_Document.pdf");
                }
            }
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = LstFiles.SelectedIndex;
            if (idx > 0)
            {
                var item = Items[idx];
                Items.RemoveAt(idx);
                Items.Insert(idx - 1, item);
                LstFiles.SelectedIndex = idx - 1;
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = LstFiles.SelectedIndex;
            if (idx >= 0 && idx < Items.Count - 1)
            {
                var item = Items[idx];
                Items.RemoveAt(idx);
                Items.Insert(idx + 1, item);
                LstFiles.SelectedIndex = idx + 1;
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            int idx = LstFiles.SelectedIndex;
            if (idx >= 0)
            {
                Items.RemoveAt(idx);
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            Items.Clear();
        }

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                Title = "Save Merged PDF As"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtOutputPath.Text = dialog.FileName;
            }
        }

        private void BtnMerge_Click(object sender, RoutedEventArgs e)
        {
            if (Items.Count < 2)
            {
                MessageBox.Show("Please add at least 2 PDF files to merge.", "Merge PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string outPath = TxtOutputPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(outPath))
            {
                MessageBox.Show("Please specify a destination file path.", "Merge PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool success = _splitService.MergePdfs(Items.Select(x => x.FullPath), outPath, out string error);
            if (success)
            {
                MessageBox.Show($"Successfully merged {Items.Count} PDFs into:\n{outPath}", "Merge Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show($"Merge failed: {error}", "Merge Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
