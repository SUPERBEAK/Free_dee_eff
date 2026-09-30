using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Freedeeeff.Models.Annotations;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Freedeeeff.Services
{
    public class PdfExportService
    {
        public bool ExportWithAnnotations(
            string sourcePdfPath,
            string targetPdfPath,
            IDictionary<int, List<AnnotationItem>> annotationsByPage)
        {
            try
            {
                // If writing to the same file, use a temporary file first
                bool isSameFile = string.Equals(Path.GetFullPath(sourcePdfPath), Path.GetFullPath(targetPdfPath), StringComparison.OrdinalIgnoreCase);
                string destination = isSameFile ? Path.GetTempFileName() : targetPdfPath;

                using (var doc = PdfReader.Open(sourcePdfPath, PdfDocumentOpenMode.Modify))
                {
                    for (int pageIndex = 0; pageIndex < doc.PageCount; pageIndex++)
                    {
                        if (!annotationsByPage.TryGetValue(pageIndex, out var annotations) || annotations.Count == 0)
                        {
                            continue;
                        }

                        var page = doc.Pages[pageIndex];
                        double pWidth = page.Width.Point;
                        double pHeight = page.Height.Point;

                        using var gfx = XGraphics.FromPdfPage(page);

                        foreach (var item in annotations)
                        {
                            DrawAnnotation(gfx, item, pWidth, pHeight);
                        }
                    }

                    doc.Save(destination);
                }

                if (isSameFile)
                {
                    File.Copy(destination, targetPdfPath, true);
                    File.Delete(destination);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void DrawAnnotation(XGraphics gfx, AnnotationItem item, double pWidth, double pHeight)
        {
            double x = item.RelX * pWidth;
            double y = item.RelY * pHeight;
            double w = item.RelWidth * pWidth;
            double h = item.RelHeight * pHeight;

            switch (item.Type)
            {
                case AnnotationType.TextFill:
                    DrawTextFill(gfx, item, x, y, w, h);
                    break;

                case AnnotationType.Signature:
                    DrawSignature(gfx, item, x, y, w, h, pWidth, pHeight);
                    break;

                case AnnotationType.Highlight:
                    DrawHighlight(gfx, item, x, y, w, h);
                    break;

                case AnnotationType.Pen:
                    DrawPenMarkup(gfx, item, pWidth, pHeight);
                    break;

                case AnnotationType.Rectangle:
                    DrawRectangle(gfx, item, x, y, w, h);
                    break;

                case AnnotationType.Ellipse:
                    DrawEllipse(gfx, item, x, y, w, h);
                    break;

                case AnnotationType.Line:
                    DrawLine(gfx, item, x, y, w, h);
                    break;

                case AnnotationType.Redaction:
                    DrawRedaction(gfx, item, x, y, w, h);
                    break;

                case AnnotationType.Stamp:
                    DrawStamp(gfx, item, x, y, w, h);
                    break;
            }
        }

        private void DrawTextFill(XGraphics gfx, AnnotationItem item, double x, double y, double w, double h)
        {
            if (!string.IsNullOrEmpty(item.BackgroundColorHex) && !item.BackgroundColorHex.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
            {
                var bg = ParseColor(item.BackgroundColorHex);
                gfx.DrawRectangle(new XSolidBrush(bg), x, y, w, h);
            }

            var fontStyle = XFontStyleEx.Regular;
            if (item.IsBold && item.IsItalic) fontStyle = XFontStyleEx.BoldItalic;
            else if (item.IsBold) fontStyle = XFontStyleEx.Bold;
            else if (item.IsItalic) fontStyle = XFontStyleEx.Italic;

            string family = string.IsNullOrWhiteSpace(item.FontFamily) ? "Arial" : item.FontFamily;
            double size = item.FontSize > 0 ? item.FontSize : 12;
            var font = new XFont(family, size, fontStyle);
            var brush = new XSolidBrush(ParseColor(item.TextColorHex));

            var rect = new XRect(x, y, Math.Max(w, 20), Math.Max(h, size * 1.5));
            gfx.DrawString(item.Text, font, brush, rect, XStringFormats.TopLeft);
        }

        private void DrawSignature(XGraphics gfx, AnnotationItem item, double x, double y, double w, double h, double pWidth, double pHeight)
        {
            if (item.ImageBytes != null && item.ImageBytes.Length > 0)
            {
                using var ms = new MemoryStream(item.ImageBytes);
                var xImage = XImage.FromStream(ms);
                gfx.DrawImage(xImage, x, y, Math.Max(w, 20), Math.Max(h, 20));
            }
            else if (item.SignatureMode == SignatureMode.Typed && !string.IsNullOrWhiteSpace(item.Text))
            {
                var font = new XFont("Segoe Script", item.FontSize > 0 ? item.FontSize : 24, XFontStyleEx.Italic);
                var brush = new XSolidBrush(ParseColor(item.TextColorHex));
                gfx.DrawString(item.Text, font, brush, new XRect(x, y, w, h), XStringFormats.Center);
            }
            else if (item.RelativePoints.Count > 1)
            {
                DrawPenMarkup(gfx, item, pWidth, pHeight);
            }
        }

        private void DrawHighlight(XGraphics gfx, AnnotationItem item, double x, double y, double w, double h)
        {
            var baseColor = ParseColor(item.StrokeColorHex);
            var hlColor = XColor.FromArgb((int)(0.4 * 255), baseColor.R, baseColor.G, baseColor.B);
            gfx.DrawRectangle(new XSolidBrush(hlColor), x, y, w, h);
        }

        private void DrawPenMarkup(XGraphics gfx, AnnotationItem item, double pWidth, double pHeight)
        {
            if (item.RelativePoints.Count < 2) return;

            var color = ParseColor(item.StrokeColorHex);
            var pen = new XPen(color, item.StrokeThickness);

            var pts = item.RelativePoints.Select(p => new XPoint(p.X * pWidth, p.Y * pHeight)).ToArray();
            gfx.DrawLines(pen, pts);
        }

        private void DrawRectangle(XGraphics gfx, AnnotationItem item, double x, double y, double w, double h)
        {
            var strokeColor = ParseColor(item.StrokeColorHex);
            var pen = new XPen(strokeColor, item.StrokeThickness);

            if (!string.IsNullOrEmpty(item.BackgroundColorHex) && !item.BackgroundColorHex.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
            {
                var fill = new XSolidBrush(ParseColor(item.BackgroundColorHex));
                gfx.DrawRectangle(pen, fill, x, y, w, h);
            }
            else
            {
                gfx.DrawRectangle(pen, x, y, w, h);
            }
        }

        private void DrawEllipse(XGraphics gfx, AnnotationItem item, double x, double y, double w, double h)
        {
            var strokeColor = ParseColor(item.StrokeColorHex);
            var pen = new XPen(strokeColor, item.StrokeThickness);

            if (!string.IsNullOrEmpty(item.BackgroundColorHex) && !item.BackgroundColorHex.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
            {
                var fill = new XSolidBrush(ParseColor(item.BackgroundColorHex));
                gfx.DrawEllipse(pen, fill, x, y, w, h);
            }
            else
            {
                gfx.DrawEllipse(pen, x, y, w, h);
            }
        }

        private void DrawLine(XGraphics gfx, AnnotationItem item, double x, double y, double w, double h)
        {
            var strokeColor = ParseColor(item.StrokeColorHex);
            var pen = new XPen(strokeColor, item.StrokeThickness);
            gfx.DrawLine(pen, x, y, x + w, y + h);
        }

        private void DrawRedaction(XGraphics gfx, AnnotationItem item, double x, double y, double w, double h)
        {
            var brush = item.RedactionType == RedactionType.Whiteout ? XBrushes.White : XBrushes.Black;
            gfx.DrawRectangle(brush, x, y, w, h);
        }

        private void DrawStamp(XGraphics gfx, AnnotationItem item, double x, double y, double w, double h)
        {
            var stampColor = ParseColor(item.StrokeColorHex);
            var pen = new XPen(stampColor, 2.5);
            var brush = new XSolidBrush(stampColor);

            gfx.DrawRoundedRectangle(pen, x, y, w, h, 6, 6);

            var titleFont = new XFont("Arial", 16, XFontStyleEx.Bold);
            string title = string.IsNullOrWhiteSpace(item.StampTitle) ? "APPROVED" : item.StampTitle.ToUpperInvariant();

            string dateStr = (item.StampDate ?? DateTime.Now).ToString("yyyy-MM-dd");
            var dateFont = new XFont("Arial", 9, XFontStyleEx.Regular);

            var titleRect = new XRect(x, y + 2, w, h * 0.65);
            var dateRect = new XRect(x, y + h * 0.60, w, h * 0.35);

            gfx.DrawString(title, titleFont, brush, titleRect, XStringFormats.Center);
            gfx.DrawString(dateStr, dateFont, brush, dateRect, XStringFormats.Center);
        }

        private static XColor ParseColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return XColors.Black;
            hex = hex.Trim().TrimStart('#');

            try
            {
                if (hex.Length == 6)
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return XColor.FromArgb(255, r, g, b);
                }
                else if (hex.Length == 8)
                {
                    byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                    return XColor.FromArgb(a, r, g, b);
                }
            }
            catch { }

            return XColors.Black;
        }
    }
}
