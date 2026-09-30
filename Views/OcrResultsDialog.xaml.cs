using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Freedeeeff.Views
{
    public partial class OcrResultsDialog : Window
    {
        public OcrResultsDialog(string fullText, int pageNumber = 1)
        {
            InitializeComponent();

            TxtContent.Text = fullText;

            int lines = fullText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            int words = fullText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            int chars = fullText.Length;

            TxtMetrics.Text = $"Page {pageNumber}: {lines} lines, {words} words, {chars} characters detected";
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtContent.Text))
            {
                Clipboard.SetText(TxtContent.Text);
                MessageBox.Show("OCR text copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = "Extracted_Text.txt",
                Title = "Export OCR Text"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, TxtContent.Text);
                    MessageBox.Show("Exported OCR text successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save text: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
