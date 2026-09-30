using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;

namespace Freedeeeff.Services
{
    public class PdfRenderService
    {
        private static readonly object _renderLock = new();

        public int GetPageCount(string filePath)
        {
            lock (_renderLock)
            {
                using var docReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(100, 100));
                return docReader.GetPageCount();
            }
        }

        public (int width, int height) GetPageSize(string filePath, int pageIndex)
        {
            lock (_renderLock)
            {
                using var docReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(1.0));
                using var pageReader = docReader.GetPageReader(pageIndex);
                return (pageReader.GetPageWidth(), pageReader.GetPageHeight());
            }
        }

        public BitmapSource RenderPage(string filePath, int pageIndex, double zoomFactor = 1.0)
        {
            lock (_renderLock)
            {
                // Baseline dimension factor is zoomFactor * 1.5 for crisp DPI
                double factor = Math.Clamp(zoomFactor * 1.5, 0.25, 5.0);
                using var docReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(factor));
                using var pageReader = docReader.GetPageReader(pageIndex);

                int width = pageReader.GetPageWidth();
                int height = pageReader.GetPageHeight();
                byte[] rawBytes = pageReader.GetImage();

                var bitmap = BitmapSource.Create(
                    width,
                    height,
                    96 * factor,
                    96 * factor,
                    PixelFormats.Bgra32,
                    null,
                    rawBytes,
                    width * 4);

                bitmap.Freeze();
                return bitmap;
            }
        }

        public BitmapSource RenderThumbnail(string filePath, int pageIndex, int targetWidth = 160)
        {
            lock (_renderLock)
            {
                // First get aspect ratio
                using (var probeDoc = DocLib.Instance.GetDocReader(filePath, new PageDimensions(1.0)))
                using (var probePage = probeDoc.GetPageReader(pageIndex))
                {
                    int origW = Math.Max(1, probePage.GetPageWidth());
                    int origH = Math.Max(1, probePage.GetPageHeight());
                    int targetHeight = (int)Math.Round((double)targetWidth * origH / origW);

                    using var docReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(targetWidth, targetHeight));
                    using var pageReader = docReader.GetPageReader(pageIndex);

                    int width = pageReader.GetPageWidth();
                    int height = pageReader.GetPageHeight();
                    byte[] rawBytes = pageReader.GetImage();

                    var bitmap = BitmapSource.Create(
                        width,
                        height,
                        96,
                        96,
                        PixelFormats.Bgra32,
                        null,
                        rawBytes,
                        width * 4);

                    bitmap.Freeze();
                    return bitmap;
                }
            }
        }

        public (byte[] bytes, int width, int height) RenderPageRawBgra(string filePath, int pageIndex, double scaling = 1.5)
        {
            lock (_renderLock)
            {
                using var docReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(scaling));
                using var pageReader = docReader.GetPageReader(pageIndex);
                int width = pageReader.GetPageWidth();
                int height = pageReader.GetPageHeight();
                byte[] rawBytes = pageReader.GetImage();
                return (rawBytes, width, height);
            }
        }
    }
}
