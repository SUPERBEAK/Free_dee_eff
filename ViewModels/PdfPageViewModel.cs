using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Freedeeeff.Models;
using Freedeeeff.Models.Annotations;
using Freedeeeff.Services;

namespace Freedeeeff.ViewModels
{
    public partial class PdfPageViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _pageIndex;

        [ObservableProperty]
        private double _width = 612;

        [ObservableProperty]
        private double _height = 792;

        [ObservableProperty]
        private BitmapSource? _renderedImage;

        [ObservableProperty]
        private BitmapSource? _thumbnail;

        [ObservableProperty]
        private bool _isSelectedInSidebar;

        [ObservableProperty]
        private bool _isCurrentPage;

        [ObservableProperty]
        private OcrPageResult? _ocrResult;

        [ObservableProperty]
        private bool _isOcrOverlayVisible = true;

        public int PageNumber => PageIndex + 1;

        public ObservableCollection<AnnotationItem> Annotations { get; } = new();
        public ObservableCollection<SearchMatchItem> SearchHighlights { get; } = new();

        public PdfPageViewModel(int pageIndex, double width, double height)
        {
            _pageIndex = pageIndex;
            _width = width;
            _height = height;
        }

        public void AddAnnotation(AnnotationItem item)
        {
            item.PageIndex = PageIndex;
            Annotations.Add(item);
        }

        public void RemoveAnnotation(AnnotationItem item)
        {
            Annotations.Remove(item);
        }
    }
}
