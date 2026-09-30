using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Freedeeeff.Models;
using Freedeeeff.Models.Annotations;
using Freedeeeff.ViewModels;
using Freedeeeff.Views;

namespace Freedeeeff.Controls
{
    public partial class PdfPageCanvas : UserControl
    {
        private PdfPageViewModel? _pageVm;
        private MainViewModel? _mainVm;

        private bool _isDrawing;
        private Point _startPoint;
        private readonly List<Point> _currentPenPoints = new();
        private AnnotationItem? _draggedAnnotation;
        private Point _dragOffset;

        public PdfPageCanvas()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (s, e) => FindMainViewModel();
        }

        private void FindMainViewModel()
        {
            var parent = VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                if (parent is FrameworkElement fe && fe.DataContext is MainViewModel mvm)
                {
                    _mainVm = mvm;
                    break;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PdfPageViewModel oldVm)
            {
                oldVm.PropertyChanged -= PageVm_PropertyChanged;
                oldVm.Annotations.CollectionChanged -= Annotations_CollectionChanged;
                oldVm.SearchHighlights.CollectionChanged -= SearchHighlights_CollectionChanged;
            }

            if (e.NewValue is PdfPageViewModel newVm)
            {
                _pageVm = newVm;
                _pageVm.PropertyChanged += PageVm_PropertyChanged;
                _pageVm.Annotations.CollectionChanged += Annotations_CollectionChanged;
                _pageVm.SearchHighlights.CollectionChanged += SearchHighlights_CollectionChanged;

                Width = _pageVm.Width;
                Height = _pageVm.Height;

                if (_pageVm.RenderedImage != null)
                {
                    ImgPage.Source = _pageVm.RenderedImage;
                }

                RebuildAllLayers();
            }
        }

        private void PageVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PdfPageViewModel.RenderedImage))
            {
                ImgPage.Source = _pageVm?.RenderedImage;
            }
            else if (e.PropertyName == nameof(PdfPageViewModel.OcrResult) || e.PropertyName == nameof(PdfPageViewModel.IsOcrOverlayVisible))
            {
                RebuildOcrLayer();
            }
        }

        private void Annotations_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildAnnotationsLayer();
        }

        private void SearchHighlights_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildSearchLayer();
        }

        private void RebuildAllLayers()
        {
            RebuildAnnotationsLayer();
            RebuildOcrLayer();
            RebuildSearchLayer();
        }

        private void RebuildAnnotationsLayer()
        {
            AnnotationsCanvas.Children.Clear();
            if (_pageVm == null) return;

            double pw = ActualWidth > 0 ? ActualWidth : _pageVm.Width;
            double ph = ActualHeight > 0 ? ActualHeight : _pageVm.Height;

            foreach (var item in _pageVm.Annotations)
            {
                var element = CreateVisualForAnnotation(item, pw, ph);
                if (element != null)
                {
                    AnnotationsCanvas.Children.Add(element);
                }
            }
        }

        private FrameworkElement? CreateVisualForAnnotation(AnnotationItem item, double pw, double ph)
        {
            double x = item.RelX * pw;
            double y = item.RelY * ph;
            double w = item.RelWidth * pw;
            double h = item.RelHeight * ph;

            FrameworkElement? visual = null;

            switch (item.Type)
            {
                case AnnotationType.TextFill:
                    var tb = new TextBlock
                    {
                        Text = item.Text,
                        FontFamily = new FontFamily(item.FontFamily),
                        FontSize = item.FontSize,
                        FontWeight = item.IsBold ? FontWeights.Bold : FontWeights.Normal,
                        FontStyle = item.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                        Foreground = (Brush)new BrushConverter().ConvertFromString(item.TextColorHex)!,
                        TextWrapping = TextWrapping.Wrap,
                        Width = Math.Max(20, w),
                        MinHeight = Math.Max(18, h)
                    };
                    if (!string.IsNullOrEmpty(item.BackgroundColorHex) && !item.BackgroundColorHex.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
                    {
                        var border = new Border
                        {
                            Background = (Brush)new BrushConverter().ConvertFromString(item.BackgroundColorHex)!,
                            Child = tb,
                            Padding = new Thickness(4, 2, 4, 2)
                        };
                        visual = border;
                    }
                    else
                    {
                        visual = tb;
                    }
                    break;

                case AnnotationType.Signature:
                    if (item.ImageBytes != null && item.ImageBytes.Length > 0)
                    {
                        try
                        {
                            var bi = new BitmapImage();
                            using (var ms = new MemoryStream(item.ImageBytes))
                            {
                                bi.BeginInit();
                                bi.CacheOption = BitmapCacheOption.OnLoad;
                                bi.StreamSource = ms;
                                bi.EndInit();
                            }
                            bi.Freeze();
                            visual = new Image { Source = bi, Width = Math.Max(30, w), Height = Math.Max(15, h), Stretch = Stretch.Uniform };
                        }
                        catch { }
                    }
                    else if (item.SignatureMode == SignatureMode.Typed)
                    {
                        visual = new TextBlock
                        {
                            Text = item.Text,
                            FontFamily = new FontFamily("Segoe Script"),
                            FontSize = Math.Max(16, item.FontSize),
                            FontStyle = FontStyles.Italic,
                            Foreground = (Brush)new BrushConverter().ConvertFromString(item.TextColorHex)!,
                            Width = Math.Max(30, w),
                            Height = Math.Max(15, h)
                        };
                    }
                    break;

                case AnnotationType.Highlight:
                    var hlBrush = (Brush)new BrushConverter().ConvertFromString(item.StrokeColorHex)!;
                    visual = new Rectangle
                    {
                        Width = Math.Max(2, w),
                        Height = Math.Max(2, h),
                        Fill = hlBrush,
                        Opacity = 0.35
                    };
                    break;

                case AnnotationType.Pen:
                    if (item.RelativePoints.Count > 1)
                    {
                        var pl = new Polyline
                        {
                            Stroke = (Brush)new BrushConverter().ConvertFromString(item.StrokeColorHex)!,
                            StrokeThickness = item.StrokeThickness,
                            StrokeLineJoin = PenLineJoin.Round,
                            StrokeStartLineCap = PenLineCap.Round,
                            StrokeEndLineCap = PenLineCap.Round
                        };
                        foreach (var pt in item.RelativePoints)
                        {
                            pl.Points.Add(new Point(pt.X * pw, pt.Y * ph));
                        }
                        // Polyline position is absolute to page
                        Canvas.SetLeft(pl, 0);
                        Canvas.SetTop(pl, 0);
                        pl.Tag = item;
                        AttachAnnotationMouseEvents(pl, item);
                        return pl;
                    }
                    break;

                case AnnotationType.Rectangle:
                    var r = new Rectangle
                    {
                        Width = Math.Max(2, w),
                        Height = Math.Max(2, h),
                        Stroke = (Brush)new BrushConverter().ConvertFromString(item.StrokeColorHex)!,
                        StrokeThickness = item.StrokeThickness
                    };
                    if (!string.IsNullOrEmpty(item.BackgroundColorHex) && !item.BackgroundColorHex.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
                    {
                        r.Fill = (Brush)new BrushConverter().ConvertFromString(item.BackgroundColorHex)!;
                    }
                    visual = r;
                    break;

                case AnnotationType.Ellipse:
                    var el = new Ellipse
                    {
                        Width = Math.Max(2, w),
                        Height = Math.Max(2, h),
                        Stroke = (Brush)new BrushConverter().ConvertFromString(item.StrokeColorHex)!,
                        StrokeThickness = item.StrokeThickness
                    };
                    if (!string.IsNullOrEmpty(item.BackgroundColorHex) && !item.BackgroundColorHex.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
                    {
                        el.Fill = (Brush)new BrushConverter().ConvertFromString(item.BackgroundColorHex)!;
                    }
                    visual = el;
                    break;

                case AnnotationType.Line:
                    var line = new Line
                    {
                        X1 = 0,
                        Y1 = 0,
                        X2 = w,
                        Y2 = h,
                        Stroke = (Brush)new BrushConverter().ConvertFromString(item.StrokeColorHex)!,
                        StrokeThickness = item.StrokeThickness
                    };
                    visual = line;
                    break;

                case AnnotationType.Redaction:
                    visual = new Rectangle
                    {
                        Width = Math.Max(2, w),
                        Height = Math.Max(2, h),
                        Fill = item.RedactionType == RedactionType.Whiteout ? Brushes.White : Brushes.Black
                    };
                    break;

                case AnnotationType.Stamp:
                    var stampBorder = new Border
                    {
                        BorderBrush = (Brush)new BrushConverter().ConvertFromString(item.StrokeColorHex)!,
                        BorderThickness = new Thickness(2.5),
                        CornerRadius = new CornerRadius(5),
                        Background = new SolidColorBrush(Color.FromArgb(20, 255, 0, 0)),
                        Padding = new Thickness(8, 4, 8, 4),
                        Width = Math.Max(80, w),
                        Height = Math.Max(36, h)
                    };

                    var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    sp.Children.Add(new TextBlock
                    {
                        Text = item.StampTitle.ToUpperInvariant(),
                        FontWeight = FontWeights.Bold,
                        FontSize = 14,
                        Foreground = stampBorder.BorderBrush,
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    string dStr = (item.StampDate ?? DateTime.Now).ToString("yyyy-MM-dd");
                    sp.Children.Add(new TextBlock
                    {
                        Text = dStr,
                        FontSize = 9,
                        Foreground = stampBorder.BorderBrush,
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    stampBorder.Child = sp;
                    visual = stampBorder;
                    break;
            }

            if (visual != null)
            {
                Canvas.SetLeft(visual, x);
                Canvas.SetTop(visual, y);
                visual.Tag = item;
                AttachAnnotationMouseEvents(visual, item);
            }

            return visual;
        }

        private void AttachAnnotationMouseEvents(FrameworkElement element, AnnotationItem item)
        {
            element.Cursor = Cursors.Hand;
            element.MouseDown += (s, e) =>
            {
                if (_mainVm == null || _pageVm == null) return;

                if (_mainVm.ActiveTool == ActiveToolMode.Eraser)
                {
                    _pageVm.RemoveAnnotation(item);
                    _mainVm.HasUnsavedChanges = true;
                    e.Handled = true;
                }
                else if (_mainVm.ActiveTool == ActiveToolMode.Select)
                {
                    _draggedAnnotation = item;
                    Point pos = e.GetPosition(AnnotationsCanvas);
                    double pw = ActualWidth > 0 ? ActualWidth : _pageVm.Width;
                    double ph = ActualHeight > 0 ? ActualHeight : _pageVm.Height;
                    _dragOffset = new Point(pos.X - item.RelX * pw, pos.Y - item.RelY * ph);
                    element.CaptureMouse();
                    e.Handled = true;
                }
            };

            element.MouseMove += (s, e) =>
            {
                if (_draggedAnnotation == item && element.IsMouseCaptured && _pageVm != null)
                {
                    Point pos = e.GetPosition(AnnotationsCanvas);
                    double pw = ActualWidth > 0 ? ActualWidth : _pageVm.Width;
                    double ph = ActualHeight > 0 ? ActualHeight : _pageVm.Height;

                    double newX = (pos.X - _dragOffset.X) / pw;
                    double newY = (pos.Y - _dragOffset.Y) / ph;

                    item.RelX = Math.Clamp(newX, 0, 1.0 - item.RelWidth);
                    item.RelY = Math.Clamp(newY, 0, 1.0 - item.RelHeight);

                    Canvas.SetLeft(element, item.RelX * pw);
                    Canvas.SetTop(element, item.RelY * ph);
                    _mainVm?.SetPropertyHelper();
                }
            };

            element.MouseUp += (s, e) =>
            {
                if (_draggedAnnotation == item && element.IsMouseCaptured)
                {
                    element.ReleaseMouseCapture();
                    _draggedAnnotation = null;
                    if (_mainVm != null) _mainVm.HasUnsavedChanges = true;
                    e.Handled = true;
                }
            };
        }

        private void RebuildOcrLayer()
        {
            OcrOverlayCanvas.Children.Clear();
            if (_pageVm?.OcrResult == null || !_pageVm.IsOcrOverlayVisible) return;

            double pw = ActualWidth > 0 ? ActualWidth : _pageVm.Width;
            double ph = ActualHeight > 0 ? ActualHeight : _pageVm.Height;

            foreach (var line in _pageVm.OcrResult.Lines)
            {
                foreach (var word in line.Words)
                {
                    var rect = word.RelBounds;
                    var border = new Border
                    {
                        Width = Math.Max(4, rect.Width * pw),
                        Height = Math.Max(4, rect.Height * ph),
                        Background = new SolidColorBrush(Color.FromArgb(1, 0, 120, 215)), // barely visible for hit testing
                        BorderBrush = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215)),
                        BorderThickness = new Thickness(0.5),
                        ToolTip = word.Text,
                        Cursor = Cursors.IBeam
                    };

                    Canvas.SetLeft(border, rect.X * pw);
                    Canvas.SetTop(border, rect.Y * ph);

                    border.MouseRightButtonUp += (s, e) =>
                    {
                        Clipboard.SetText(word.Text);
                        e.Handled = true;
                    };

                    OcrOverlayCanvas.Children.Add(border);
                }
            }
        }

        private void RebuildSearchLayer()
        {
            SearchOverlayCanvas.Children.Clear();
            if (_pageVm == null) return;

            double pw = ActualWidth > 0 ? ActualWidth : _pageVm.Width;
            double ph = ActualHeight > 0 ? ActualHeight : _pageVm.Height;

            foreach (var match in _pageVm.SearchHighlights)
            {
                var r = match.RelBounds;
                var rect = new Rectangle
                {
                    Width = Math.Max(4, r.Width * pw),
                    Height = Math.Max(4, r.Height * ph),
                    Fill = new SolidColorBrush(Color.FromArgb(150, 255, 235, 59)),
                    Stroke = Brushes.Orange,
                    StrokeThickness = 1
                };

                Canvas.SetLeft(rect, r.X * pw);
                Canvas.SetTop(rect, r.Y * ph);
                SearchOverlayCanvas.Children.Add(rect);
            }
        }

        private void InteractiveCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            FindMainViewModel();
            if (_mainVm == null || _pageVm == null) return;

            _startPoint = e.GetPosition(InteractiveCanvas);
            double pw = ActualWidth > 0 ? ActualWidth : _pageVm.Width;
            double ph = ActualHeight > 0 ? ActualHeight : _pageVm.Height;

            switch (_mainVm.ActiveTool)
            {
                case ActiveToolMode.TextFill:
                    ShowInPlaceEditor(_startPoint);
                    break;

                case ActiveToolMode.Signature:
                    PlaceSignature(_startPoint, pw, ph);
                    break;

                case ActiveToolMode.Stamp:
                    PlaceStamp(_startPoint, pw, ph);
                    break;

                case ActiveToolMode.Highlight:
                case ActiveToolMode.Rectangle:
                case ActiveToolMode.Ellipse:
                case ActiveToolMode.Redaction:
                    _isDrawing = true;
                    LivePreviewRect.Width = 0;
                    LivePreviewRect.Height = 0;
                    Canvas.SetLeft(LivePreviewRect, _startPoint.X);
                    Canvas.SetTop(LivePreviewRect, _startPoint.Y);

                    if (_mainVm.ActiveTool == ActiveToolMode.Highlight)
                    {
                        LivePreviewRect.Fill = (Brush)new BrushConverter().ConvertFromString(_mainVm.SelectedHighlightColor)!;
                        LivePreviewRect.Opacity = 0.35;
                        LivePreviewRect.Stroke = Brushes.Transparent;
                    }
                    else if (_mainVm.ActiveTool == ActiveToolMode.Redaction)
                    {
                        LivePreviewRect.Fill = _mainVm.SelectedRedactionType == RedactionType.Whiteout ? Brushes.White : Brushes.Black;
                        LivePreviewRect.Opacity = 0.9;
                        LivePreviewRect.Stroke = Brushes.Red;
                    }
                    else
                    {
                        LivePreviewRect.Fill = Brushes.Transparent;
                        LivePreviewRect.Opacity = 1.0;
                        LivePreviewRect.Stroke = (Brush)new BrushConverter().ConvertFromString(_mainVm.SelectedStrokeColor)!;
                    }

                    LivePreviewRect.Visibility = Visibility.Visible;
                    InteractiveCanvas.CaptureMouse();
                    break;

                case ActiveToolMode.Line:
                    _isDrawing = true;
                    LivePreviewLine.X1 = _startPoint.X;
                    LivePreviewLine.Y1 = _startPoint.Y;
                    LivePreviewLine.X2 = _startPoint.X;
                    LivePreviewLine.Y2 = _startPoint.Y;
                    LivePreviewLine.Stroke = (Brush)new BrushConverter().ConvertFromString(_mainVm.SelectedStrokeColor)!;
                    LivePreviewLine.StrokeThickness = _mainVm.SelectedStrokeThickness;
                    LivePreviewLine.Visibility = Visibility.Visible;
                    InteractiveCanvas.CaptureMouse();
                    break;

                case ActiveToolMode.Pen:
                    _isDrawing = true;
                    _currentPenPoints.Clear();
                    _currentPenPoints.Add(_startPoint);

                    LivePreviewPolyline.Points.Clear();
                    LivePreviewPolyline.Points.Add(_startPoint);
                    LivePreviewPolyline.Stroke = (Brush)new BrushConverter().ConvertFromString(_mainVm.SelectedStrokeColor)!;
                    LivePreviewPolyline.StrokeThickness = _mainVm.SelectedStrokeThickness;
                    LivePreviewPolyline.Visibility = Visibility.Visible;
                    InteractiveCanvas.CaptureMouse();
                    break;
            }
        }

        private void InteractiveCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing || _mainVm == null || _pageVm == null) return;

            Point current = e.GetPosition(InteractiveCanvas);

            switch (_mainVm.ActiveTool)
            {
                case ActiveToolMode.Highlight:
                case ActiveToolMode.Rectangle:
                case ActiveToolMode.Ellipse:
                case ActiveToolMode.Redaction:
                    double x = Math.Min(_startPoint.X, current.X);
                    double y = Math.Min(_startPoint.Y, current.Y);
                    double w = Math.Abs(current.X - _startPoint.X);
                    double h = Math.Abs(current.Y - _startPoint.Y);

                    Canvas.SetLeft(LivePreviewRect, x);
                    Canvas.SetTop(LivePreviewRect, y);
                    LivePreviewRect.Width = w;
                    LivePreviewRect.Height = h;
                    break;

                case ActiveToolMode.Line:
                    LivePreviewLine.X2 = current.X;
                    LivePreviewLine.Y2 = current.Y;
                    break;

                case ActiveToolMode.Pen:
                    _currentPenPoints.Add(current);
                    LivePreviewPolyline.Points.Add(current);
                    break;
            }
        }

        private void InteractiveCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawing || _mainVm == null || _pageVm == null) return;

            _isDrawing = false;
            InteractiveCanvas.ReleaseMouseCapture();

            Point current = e.GetPosition(InteractiveCanvas);
            double pw = ActualWidth > 0 ? ActualWidth : _pageVm.Width;
            double ph = ActualHeight > 0 ? ActualHeight : _pageVm.Height;

            double x = Math.Min(_startPoint.X, current.X);
            double y = Math.Min(_startPoint.Y, current.Y);
            double w = Math.Abs(current.X - _startPoint.X);
            double h = Math.Abs(current.Y - _startPoint.Y);

            LivePreviewRect.Visibility = Visibility.Collapsed;
            LivePreviewLine.Visibility = Visibility.Collapsed;
            LivePreviewPolyline.Visibility = Visibility.Collapsed;

            switch (_mainVm.ActiveTool)
            {
                case ActiveToolMode.Highlight:
                    if (w > 3 && h > 3)
                    {
                        var item = new AnnotationItem
                        {
                            Type = AnnotationType.Highlight,
                            RelX = x / pw,
                            RelY = y / ph,
                            RelWidth = w / pw,
                            RelHeight = h / ph,
                            StrokeColorHex = _mainVm.SelectedHighlightColor
                        };
                        _mainVm.AddAnnotationToPage(_pageVm.PageIndex, item);
                    }
                    break;

                case ActiveToolMode.Rectangle:
                    if (w > 3 && h > 3)
                    {
                        var item = new AnnotationItem
                        {
                            Type = AnnotationType.Rectangle,
                            RelX = x / pw,
                            RelY = y / ph,
                            RelWidth = w / pw,
                            RelHeight = h / ph,
                            StrokeColorHex = _mainVm.SelectedStrokeColor,
                            StrokeThickness = _mainVm.SelectedStrokeThickness
                        };
                        _mainVm.AddAnnotationToPage(_pageVm.PageIndex, item);
                    }
                    break;

                case ActiveToolMode.Ellipse:
                    if (w > 3 && h > 3)
                    {
                        var item = new AnnotationItem
                        {
                            Type = AnnotationType.Ellipse,
                            RelX = x / pw,
                            RelY = y / ph,
                            RelWidth = w / pw,
                            RelHeight = h / ph,
                            StrokeColorHex = _mainVm.SelectedStrokeColor,
                            StrokeThickness = _mainVm.SelectedStrokeThickness
                        };
                        _mainVm.AddAnnotationToPage(_pageVm.PageIndex, item);
                    }
                    break;

                case ActiveToolMode.Line:
                    if (Math.Abs(current.X - _startPoint.X) > 3 || Math.Abs(current.Y - _startPoint.Y) > 3)
                    {
                        var item = new AnnotationItem
                        {
                            Type = AnnotationType.Line,
                            RelX = _startPoint.X / pw,
                            RelY = _startPoint.Y / ph,
                            RelWidth = (current.X - _startPoint.X) / pw,
                            RelHeight = (current.Y - _startPoint.Y) / ph,
                            StrokeColorHex = _mainVm.SelectedStrokeColor,
                            StrokeThickness = _mainVm.SelectedStrokeThickness
                        };
                        _mainVm.AddAnnotationToPage(_pageVm.PageIndex, item);
                    }
                    break;

                case ActiveToolMode.Redaction:
                    if (w > 3 && h > 3)
                    {
                        var item = new AnnotationItem
                        {
                            Type = AnnotationType.Redaction,
                            RelX = x / pw,
                            RelY = y / ph,
                            RelWidth = w / pw,
                            RelHeight = h / ph,
                            RedactionType = _mainVm.SelectedRedactionType
                        };
                        _mainVm.AddAnnotationToPage(_pageVm.PageIndex, item);
                    }
                    break;

                case ActiveToolMode.Pen:
                    if (_currentPenPoints.Count > 1)
                    {
                        var relPts = _currentPenPoints.Select(p => new Point(p.X / pw, p.Y / ph)).ToList();
                        var item = new AnnotationItem
                        {
                            Type = AnnotationType.Pen,
                            RelativePoints = relPts,
                            StrokeColorHex = _mainVm.SelectedStrokeColor,
                            StrokeThickness = _mainVm.SelectedStrokeThickness
                        };
                        _mainVm.AddAnnotationToPage(_pageVm.PageIndex, item);
                    }
                    break;
            }
        }

        private void ShowInPlaceEditor(Point location)
        {
            if (_mainVm == null) return;

            Canvas.SetLeft(InPlaceTextBox, location.X);
            Canvas.SetTop(InPlaceTextBox, location.Y);
            InPlaceTextBox.Width = 200;
            InPlaceTextBox.Text = "";
            InPlaceTextBox.FontFamily = new FontFamily(_mainVm.SelectedFontFamily);
            InPlaceTextBox.FontSize = _mainVm.SelectedFontSize;
            InPlaceTextBox.FontWeight = _mainVm.IsBold ? FontWeights.Bold : FontWeights.Normal;
            InPlaceTextBox.FontStyle = _mainVm.IsItalic ? FontStyles.Italic : FontStyles.Normal;
            InPlaceTextBox.Foreground = (Brush)new BrushConverter().ConvertFromString(_mainVm.SelectedTextColor)!;

            InPlaceTextBox.Visibility = Visibility.Visible;
            InPlaceTextBox.Focus();
        }

        private void InPlaceTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitInPlaceText();
        }

        private void InPlaceTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                InPlaceTextBox.Visibility = Visibility.Collapsed;
            }
        }

        private void CommitInPlaceText()
        {
            if (InPlaceTextBox.Visibility != Visibility.Visible) return;
            string text = InPlaceTextBox.Text.Trim();
            InPlaceTextBox.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(text) || _mainVm == null || _pageVm == null) return;

            double pw = ActualWidth > 0 ? ActualWidth : _pageVm.Width;
            double ph = ActualHeight > 0 ? ActualHeight : _pageVm.Height;

            double x = Canvas.GetLeft(InPlaceTextBox);
            double y = Canvas.GetTop(InPlaceTextBox);

            var item = new AnnotationItem
            {
                Type = AnnotationType.TextFill,
                Text = text,
                RelX = x / pw,
                RelY = y / ph,
                RelWidth = Math.Max(80, InPlaceTextBox.ActualWidth) / pw,
                RelHeight = Math.Max(24, InPlaceTextBox.ActualHeight) / ph,
                FontFamily = _mainVm.SelectedFontFamily,
                FontSize = _mainVm.SelectedFontSize,
                IsBold = _mainVm.IsBold,
                IsItalic = _mainVm.IsItalic,
                TextColorHex = _mainVm.SelectedTextColor
            };

            _mainVm.AddAnnotationToPage(_pageVm.PageIndex, item);
        }

        private void PlaceSignature(Point location, double pw, double ph)
        {
            if (_mainVm == null || _pageVm == null) return;

            byte[]? sigBytes = _mainVm.ActiveSignatureImageBytes;
            if (sigBytes == null || sigBytes.Length == 0)
            {
                // Open signature pad dialog directly if no signature loaded!
                var dlg = new SignaturePadDialog { Owner = Window.GetWindow(this) };
                if (dlg.ShowDialog() == true && dlg.GeneratedImageBytes != null)
                {
                    sigBytes = dlg.GeneratedImageBytes;
                    _mainVm.ActiveSignatureImageBytes = sigBytes;
                    _mainVm.LoadSavedSignatures();
                }
                else
                {
                    return;
                }
            }

            double w = 160;
            double h = 60;
            double x = location.X - (w / 2);
            double y = location.Y - (h / 2);

            var item = new AnnotationItem
            {
                Type = AnnotationType.Signature,
                SignatureMode = SignatureMode.Image,
                ImageBytes = sigBytes,
                RelX = Math.Max(0, x) / pw,
                RelY = Math.Max(0, y) / ph,
                RelWidth = w / pw,
                RelHeight = h / ph
            };

            _mainVm.AddAnnotationToPage(_pageVm.PageIndex, item);
        }

        private void PlaceStamp(Point location, double pw, double ph)
        {
            if (_mainVm == null || _pageVm == null) return;

            double w = 140;
            double h = 48;
            double x = location.X - (w / 2);
            double y = location.Y - (h / 2);

            var item = new AnnotationItem
            {
                Type = AnnotationType.Stamp,
                StampTitle = _mainVm.SelectedStampTitle,
                StampDate = DateTime.Now,
                RelX = Math.Max(0, x) / pw,
                RelY = Math.Max(0, y) / ph,
                RelWidth = w / pw,
                RelHeight = h / ph,
                StrokeColorHex = "#D32F2F"
            };

            _mainVm.AddAnnotationToPage(_pageVm.PageIndex, item);
        }
    }

    public static class MainViewModelExtensions
    {
        public static void SetPropertyHelper(this MainViewModel vm)
        {
            vm.HasUnsavedChanges = true;
        }
    }
}
