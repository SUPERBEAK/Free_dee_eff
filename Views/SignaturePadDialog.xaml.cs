using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Freedeeeff.Services;
using Microsoft.Win32;

namespace Freedeeeff.Views
{
    public partial class SignaturePadDialog : Window
    {
        public byte[]? GeneratedImageBytes { get; private set; }
        public bool ShouldSaveToLibrary { get; private set; }
        public string SignatureName { get; private set; } = "Signature";

        private byte[]? _uploadedBytes;
        private readonly SignatureStorageService _storageService = new();

        public SignaturePadDialog()
        {
            InitializeComponent();
            SetupInkCanvas();
        }

        private void SetupInkCanvas()
        {
            var da = new DrawingAttributes
            {
                Color = Colors.Black,
                Width = 3,
                Height = 3,
                FitToCurve = true
            };
            InkPad.DefaultDrawingAttributes = da;
        }

        private void CmbInkColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InkPad == null || CmbInkColor.SelectedItem is not ComboBoxItem item) return;
            string hex = item.Tag?.ToString() ?? "#000000";
            var color = (Color)ColorConverter.ConvertFromString(hex);
            InkPad.DefaultDrawingAttributes.Color = color;
        }

        private void BtnClearInk_Click(object sender, RoutedEventArgs e)
        {
            InkPad.Strokes.Clear();
        }

        private void TxtTypeName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtTypePreview == null) return;
            TxtTypePreview.Text = string.IsNullOrWhiteSpace(TxtTypeName.Text) ? "Your Signature" : TxtTypeName.Text;
        }

        private void CmbTypeFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtTypePreview == null || CmbTypeFont.SelectedItem is not ComboBoxItem item) return;
            string fontName = item.Content.ToString() ?? "Segoe Script";
            TxtTypePreview.FontFamily = new FontFamily(fontName);
        }

        private void BtnUploadImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All Files (*.*)|*.*",
                Title = "Select Signature Image"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _uploadedBytes = File.ReadAllBytes(dialog.FileName);
                    var bi = new BitmapImage();
                    using (var ms = new MemoryStream(_uploadedBytes))
                    {
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = ms;
                        bi.EndInit();
                    }
                    bi.Freeze();

                    ImgUploadPreview.Source = bi;
                    TxtUploadHint.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load image: {ex.Message}", "Image Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            ShouldSaveToLibrary = ChkSaveToLibrary.IsChecked == true;

            int selectedTab = TabModes.SelectedIndex;

            if (selectedTab == 0) // Draw
            {
                if (InkPad.Strokes.Count == 0)
                {
                    MessageBox.Show("Please draw your signature before applying.", "Signature Empty", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                GeneratedImageBytes = RenderInkCanvasToPng(InkPad);
                SignatureName = "Drawn Signature";
            }
            else if (selectedTab == 1) // Type
            {
                if (string.IsNullOrWhiteSpace(TxtTypeName.Text))
                {
                    MessageBox.Show("Please enter your name.", "Signature Empty", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                GeneratedImageBytes = RenderVisualToPng(TxtTypePreview, 400, 120);
                SignatureName = $"{TxtTypeName.Text.Trim()} (Typed)";
            }
            else if (selectedTab == 2) // Upload
            {
                if (_uploadedBytes == null || _uploadedBytes.Length == 0)
                {
                    MessageBox.Show("Please select an image file.", "No Image", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                GeneratedImageBytes = _uploadedBytes;
                SignatureName = "Imported Signature";
            }

            if (GeneratedImageBytes != null && ShouldSaveToLibrary)
            {
                _storageService.SaveSignature(SignatureName, GeneratedImageBytes);
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static byte[] RenderInkCanvasToPng(InkCanvas inkCanvas)
        {
            int w = Math.Max(100, (int)inkCanvas.ActualWidth);
            int h = Math.Max(50, (int)inkCanvas.ActualHeight);

            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(inkCanvas);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }

        private static byte[] RenderVisualToPng(FrameworkElement element, int width, int height)
        {
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(element);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
    }
}
