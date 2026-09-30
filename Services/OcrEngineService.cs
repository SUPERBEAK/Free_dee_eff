using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows;
using Freedeeeff.Models;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Freedeeeff.Services
{
    public class OcrEngineService
    {
        private OcrEngine? _ocrEngine;

        public bool IsSupported => OcrEngine.AvailableRecognizerLanguages.Count > 0;

        public IReadOnlyList<string> AvailableLanguages
        {
            get
            {
                var list = new List<string>();
                foreach (var lang in OcrEngine.AvailableRecognizerLanguages)
                {
                    list.Add($"{lang.DisplayName} ({lang.LanguageTag})");
                }
                return list;
            }
        }

        private OcrEngine GetEngine()
        {
            if (_ocrEngine == null)
            {
                _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (_ocrEngine == null && OcrEngine.AvailableRecognizerLanguages.Count > 0)
                {
                    _ocrEngine = OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
                }
            }

            if (_ocrEngine == null)
            {
                throw new InvalidOperationException("No OCR recognition language is available on this system.");
            }

            return _ocrEngine;
        }

        public async Task<OcrPageResult> RecognizePageAsync(byte[] bgraBytes, int width, int height, int pageIndex)
        {
            return await Task.Run(async () =>
            {
                var engine = GetEngine();

                // Create SoftwareBitmap from BGRA byte array
                var softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
                softwareBitmap.CopyFromBuffer(bgraBytes.AsBuffer());

                var result = await engine.RecognizeAsync(softwareBitmap);

                var pageResult = new OcrPageResult
                {
                    PageIndex = pageIndex,
                    FullText = result.Text
                };

                double pageW = Math.Max(1.0, width);
                double pageH = Math.Max(1.0, height);

                foreach (var line in result.Lines)
                {
                    var lineInfo = new OcrLineInfo
                    {
                        Text = line.Text,
                        RelBounds = new Rect(
                            line.Words.Count > 0 ? line.Words[0].BoundingRect.X / pageW : 0,
                            line.Words.Count > 0 ? line.Words[0].BoundingRect.Y / pageH : 0,
                            0, 0)
                    };

                    double minX = double.MaxValue, minY = double.MaxValue;
                    double maxX = double.MinValue, maxY = double.MinValue;

                    foreach (var word in line.Words)
                    {
                        var wordRect = new Rect(
                            word.BoundingRect.X / pageW,
                            word.BoundingRect.Y / pageH,
                            word.BoundingRect.Width / pageW,
                            word.BoundingRect.Height / pageH);

                        lineInfo.Words.Add(new OcrWordInfo
                        {
                            Text = word.Text,
                            RelBounds = wordRect
                        });

                        minX = Math.Min(minX, wordRect.Left);
                        minY = Math.Min(minY, wordRect.Top);
                        maxX = Math.Max(maxX, wordRect.Right);
                        maxY = Math.Max(maxY, wordRect.Bottom);
                    }

                    if (lineInfo.Words.Count > 0)
                    {
                        lineInfo.RelBounds = new Rect(minX, minY, Math.Max(0.001, maxX - minX), Math.Max(0.001, maxY - minY));
                    }

                    pageResult.Lines.Add(lineInfo);
                }

                return pageResult;
            });
        }
    }
}
