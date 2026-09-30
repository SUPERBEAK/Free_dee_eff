using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Freedeeeff.Models;
using Freedeeeff.Models.Annotations;
using Freedeeeff.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Freedeeeff.Tests
{
    public class PdfServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _samplePdfPath;

        public PdfServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "Freedeeeff_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _samplePdfPath = Path.Combine(_tempDir, "Sample5Pages.pdf");
            CreateTestPdf(_samplePdfPath, 5);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { }
        }

        private static void CreateTestPdf(string filePath, int pageCount)
        {
            using var doc = new PdfDocument();
            for (int i = 1; i <= pageCount; i++)
            {
                var page = doc.AddPage();
                page.Width = XUnit.FromPoint(612);
                page.Height = XUnit.FromPoint(792);

                using var gfx = XGraphics.FromPdfPage(page);
                var font = new XFont("Arial", 20, XFontStyleEx.Bold);
                gfx.DrawString($"Freedeeeff Test Page {i}", font, XBrushes.Navy, new XRect(50, 50, 500, 50), XStringFormats.TopLeft);
                gfx.DrawString($"This is a sample document for testing split, merge, OCR, and annotations.", new XFont("Arial", 12, XFontStyleEx.Regular), XBrushes.Black, new XRect(50, 120, 500, 30), XStringFormats.TopLeft);
            }
            doc.Save(filePath);
        }

        [Fact]
        public void PdfSplit_AllPages_CreatesIndividualFiles()
        {
            var service = new PdfSplitService();
            var outDir = Path.Combine(_tempDir, "AllPagesSplit");

            var result = service.SplitPdf(_samplePdfPath, new SplitOptions
            {
                Mode = SplitMode.AllPages,
                OutputDirectory = outDir,
                BaseFileName = "SplitTest"
            });

            Assert.True(result.Success);
            Assert.Equal(5, result.GeneratedFiles.Count);

            foreach (var file in result.GeneratedFiles)
            {
                Assert.True(File.Exists(file));
                using var doc = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                Assert.Equal(1, doc.PageCount);
            }
        }

        [Fact]
        public void PdfSplit_ByRanges_CreatesCorrectSegments()
        {
            var service = new PdfSplitService();
            var outDir = Path.Combine(_tempDir, "RangesSplit");

            var result = service.SplitPdf(_samplePdfPath, new SplitOptions
            {
                Mode = SplitMode.ByRanges,
                RangeString = "1-2, 3-5",
                OutputDirectory = outDir,
                BaseFileName = "RangeDoc"
            });

            Assert.True(result.Success);
            Assert.Equal(2, result.GeneratedFiles.Count);

            using (var doc1 = PdfReader.Open(result.GeneratedFiles[0], PdfDocumentOpenMode.Import))
            {
                Assert.Equal(2, doc1.PageCount);
            }

            using (var doc2 = PdfReader.Open(result.GeneratedFiles[1], PdfDocumentOpenMode.Import))
            {
                Assert.Equal(3, doc2.PageCount);
            }
        }

        [Fact]
        public void PdfSplit_EveryNPages_SplitsIntoBatches()
        {
            var service = new PdfSplitService();
            var outDir = Path.Combine(_tempDir, "EveryNSplit");

            var result = service.SplitPdf(_samplePdfPath, new SplitOptions
            {
                Mode = SplitMode.EveryNPages,
                EveryNPagesCount = 2,
                OutputDirectory = outDir,
                BaseFileName = "BatchDoc"
            });

            Assert.True(result.Success);
            Assert.Equal(3, result.GeneratedFiles.Count); // 2 pages, 2 pages, 1 page

            using (var doc1 = PdfReader.Open(result.GeneratedFiles[0], PdfDocumentOpenMode.Import))
                Assert.Equal(2, doc1.PageCount);

            using (var doc2 = PdfReader.Open(result.GeneratedFiles[1], PdfDocumentOpenMode.Import))
                Assert.Equal(2, doc2.PageCount);

            using (var doc3 = PdfReader.Open(result.GeneratedFiles[2], PdfDocumentOpenMode.Import))
                Assert.Equal(1, doc3.PageCount);
        }

        [Fact]
        public void PdfSplit_SelectedPages_ExtractsProperly()
        {
            var service = new PdfSplitService();
            var outDir = Path.Combine(_tempDir, "SelectedSplit");

            var result = service.SplitPdf(_samplePdfPath, new SplitOptions
            {
                Mode = SplitMode.SelectedPages,
                SelectedPageIndices = new List<int> { 0, 2, 4 }, // Pages 1, 3, 5
                OutputDirectory = outDir,
                BaseFileName = "Extracted"
            });

            Assert.True(result.Success);
            Assert.Single(result.GeneratedFiles);

            using var doc = PdfReader.Open(result.GeneratedFiles[0], PdfDocumentOpenMode.Import);
            Assert.Equal(3, doc.PageCount);
        }

        [Fact]
        public void PdfSplit_MergePdfs_CombinesFilesCorrectly()
        {
            var service = new PdfSplitService();
            var file1 = Path.Combine(_tempDir, "DocA.pdf");
            var file2 = Path.Combine(_tempDir, "DocB.pdf");
            CreateTestPdf(file1, 2);
            CreateTestPdf(file2, 3);

            var mergedPath = Path.Combine(_tempDir, "MergedResult.pdf");
            bool ok = service.MergePdfs(new[] { file1, file2 }, mergedPath, out string error);

            Assert.True(ok, error);
            Assert.True(File.Exists(mergedPath));

            using var doc = PdfReader.Open(mergedPath, PdfDocumentOpenMode.Import);
            Assert.Equal(5, doc.PageCount);
        }

        [Fact]
        public void PdfPageService_RotateAndDeletePages()
        {
            var pageService = new PdfPageService();
            var modPath = Path.Combine(_tempDir, "RotatedDoc.pdf");

            bool rotateOk = pageService.RotatePages(_samplePdfPath, modPath, new[] { 0 }, 90);
            Assert.True(rotateOk);

            using (var doc = PdfReader.Open(modPath, PdfDocumentOpenMode.Import))
            {
                Assert.Equal(90, doc.Pages[0].Rotate);
            }

            var delPath = Path.Combine(_tempDir, "DeletedDoc.pdf");
            bool delOk = pageService.DeletePages(modPath, delPath, new[] { 1 });
            Assert.True(delOk);

            using (var doc = PdfReader.Open(delPath, PdfDocumentOpenMode.Import))
            {
                Assert.Equal(4, doc.PageCount);
            }
        }

        [Fact]
        public void PdfExportService_FlattensAnnotationsSuccessfully()
        {
            var exportService = new PdfExportService();
            var outPdf = Path.Combine(_tempDir, "AnnotatedDoc.pdf");

            var annotations = new Dictionary<int, List<AnnotationItem>>
            {
                [0] = new List<AnnotationItem>
                {
                    new AnnotationItem
                    {
                        Type = AnnotationType.TextFill,
                        Text = "Approved by Quality Assurance",
                        RelX = 0.1,
                        RelY = 0.3,
                        RelWidth = 0.4,
                        RelHeight = 0.05,
                        FontSize = 14,
                        TextColorHex = "#008800"
                    },
                    new AnnotationItem
                    {
                        Type = AnnotationType.Stamp,
                        StampTitle = "APPROVED",
                        StampDate = DateTime.Now,
                        RelX = 0.6,
                        RelY = 0.3,
                        RelWidth = 0.25,
                        RelHeight = 0.08,
                        StrokeColorHex = "#D32F2F"
                    },
                    new AnnotationItem
                    {
                        Type = AnnotationType.Highlight,
                        RelX = 0.1,
                        RelY = 0.15,
                        RelWidth = 0.8,
                        RelHeight = 0.04,
                        StrokeColorHex = "#FFFF00"
                    },
                    new AnnotationItem
                    {
                        Type = AnnotationType.Redaction,
                        RedactionType = RedactionType.Blackout,
                        RelX = 0.1,
                        RelY = 0.45,
                        RelWidth = 0.5,
                        RelHeight = 0.04
                    }
                }
            };

            bool success = exportService.ExportWithAnnotations(_samplePdfPath, outPdf, annotations);
            Assert.True(success);
            Assert.True(File.Exists(outPdf));

            using var doc = PdfReader.Open(outPdf, PdfDocumentOpenMode.Import);
            Assert.Equal(5, doc.PageCount);
        }

        [Fact]
        public void OcrEngineService_InitializesProperly()
        {
            var ocr = new OcrEngineService();
            Assert.True(ocr.IsSupported);
            Assert.NotEmpty(ocr.AvailableLanguages);
        }

        [Fact]
        public void PdfTextSearchService_FindsExpectedTerms()
        {
            var searchService = new PdfTextSearchService();
            var matches = searchService.SearchInPdf(_samplePdfPath, "Freedeeeff");

            Assert.NotEmpty(matches);
            Assert.Equal(5, matches.Count); // 1 per page across 5 pages
            Assert.Equal("Freedeeeff", matches[0].MatchText);
            Assert.True(matches[0].RelBounds.Width > 0);
            Assert.True(matches[0].RelBounds.Height > 0);
        }

        [Fact]
        public void PdfRenderService_RendersPageAndThumbnail()
        {
            var renderService = new PdfRenderService();
            int count = renderService.GetPageCount(_samplePdfPath);
            Assert.Equal(5, count);

            var (w, h) = renderService.GetPageSize(_samplePdfPath, 0);
            Assert.True(w > 0);
            Assert.True(h > 0);

            var pageBitmap = renderService.RenderPage(_samplePdfPath, 0, 1.0);
            Assert.NotNull(pageBitmap);
            Assert.True(pageBitmap.PixelWidth > 0);
            Assert.True(pageBitmap.PixelHeight > 0);
            Assert.True(pageBitmap.IsFrozen);

            var thumbBitmap = renderService.RenderThumbnail(_samplePdfPath, 0, 150);
            Assert.NotNull(thumbBitmap);
            Assert.InRange(thumbBitmap.PixelWidth, 148, 152);
            Assert.True(thumbBitmap.IsFrozen);
        }

        [Fact]
        public void PdfPageService_InsertBlankPageAndReorder()
        {
            var pageService = new PdfPageService();
            var blankPath = Path.Combine(_tempDir, "WithBlank.pdf");

            bool insertOk = pageService.InsertBlankPage(_samplePdfPath, blankPath, 1);
            Assert.True(insertOk);

            using (var doc = PdfReader.Open(blankPath, PdfDocumentOpenMode.Import))
            {
                Assert.Equal(6, doc.PageCount);
            }

            var reorderPath = Path.Combine(_tempDir, "Reordered.pdf");
            bool reorderOk = pageService.ReorderPages(_samplePdfPath, reorderPath, new[] { 4, 3, 2, 1, 0 });
            Assert.True(reorderOk);

            using (var doc = PdfReader.Open(reorderPath, PdfDocumentOpenMode.Import))
            {
                Assert.Equal(5, doc.PageCount);
            }
        }

        [Fact]
        public void GenerateSampleDocumentInWorkspace()
        {
            string rootPdf = Path.Combine("..", "..", "..", "..", "Sample_Document.pdf");
            using var doc = new PdfDocument();

            // Page 1: Cover & Intro
            var p1 = doc.AddPage();
            p1.Width = XUnit.FromPoint(612);
            p1.Height = XUnit.FromPoint(792);
            using (var g1 = XGraphics.FromPdfPage(p1))
            {
                g1.DrawRectangle(new XSolidBrush(XColor.FromArgb(240, 245, 250)), new XRect(0, 0, 612, 792));
                g1.DrawRectangle(new XSolidBrush(XColor.FromArgb(0, 120, 215)), new XRect(0, 0, 612, 10));
                g1.DrawString("FREEDEEEFF SAMPLE DOCUMENT", new XFont("Arial", 24, XFontStyleEx.Bold), XBrushes.Navy, new XRect(50, 60, 512, 40), XStringFormats.TopLeft);
                g1.DrawString("Master Services Agreement & Verification Form", new XFont("Arial", 14, XFontStyleEx.Regular), XBrushes.DarkGray, new XRect(50, 105, 512, 25), XStringFormats.TopLeft);

                g1.DrawString("1. Scope of Services", new XFont("Arial", 16, XFontStyleEx.Bold), XBrushes.Black, new XRect(50, 160, 512, 30), XStringFormats.TopLeft);
                g1.DrawString("This document serves as a test contract to verify the comprehensive capabilities of Freedeeeff:", new XFont("Arial", 11, XFontStyleEx.Regular), XBrushes.Black, new XRect(50, 195, 512, 20), XStringFormats.TopLeft);
                g1.DrawString("• Optical Character Recognition (OCR) for scanned text detection and copy", new XFont("Arial", 11, XFontStyleEx.Regular), XBrushes.Black, new XRect(70, 220, 492, 20), XStringFormats.TopLeft);
                g1.DrawString("• Text Fill tool for filling out blanks and form inputs", new XFont("Arial", 11, XFontStyleEx.Regular), XBrushes.Black, new XRect(70, 245, 492, 20), XStringFormats.TopLeft);
                g1.DrawString("• Interactive Signature tool with Drawn, Typed cursive, or Uploaded images", new XFont("Arial", 11, XFontStyleEx.Regular), XBrushes.Black, new XRect(70, 270, 492, 20), XStringFormats.TopLeft);
                g1.DrawString("• Highlighting, Ink Pen, Geometric Shapes, and Redaction boxes", new XFont("Arial", 11, XFontStyleEx.Regular), XBrushes.Black, new XRect(70, 295, 492, 20), XStringFormats.TopLeft);
                g1.DrawString("• PDF Splitting by ranges, individual pages, chunks, and page extraction", new XFont("Arial", 11, XFontStyleEx.Regular), XBrushes.Black, new XRect(70, 320, 492, 20), XStringFormats.TopLeft);

                g1.DrawString("Client Name: ____________________________________    Date: ________________", new XFont("Arial", 12, XFontStyleEx.Regular), XBrushes.Black, new XRect(50, 380, 512, 25), XStringFormats.TopLeft);
            }

            // Page 2: Terms & Confidentiality
            var p2 = doc.AddPage();
            p2.Width = XUnit.FromPoint(612);
            p2.Height = XUnit.FromPoint(792);
            using (var g2 = XGraphics.FromPdfPage(p2))
            {
                g2.DrawString("2. Terms, Conditions & Confidentiality", new XFont("Arial", 16, XFontStyleEx.Bold), XBrushes.Navy, new XRect(50, 60, 512, 30), XStringFormats.TopLeft);
                g2.DrawString("All proprietary information disclosed under this agreement shall remain strictly confidential.", new XFont("Arial", 11, XFontStyleEx.Regular), XBrushes.Black, new XRect(50, 100, 512, 20), XStringFormats.TopLeft);
                g2.DrawString("Sensitive customer data and banking identifiers should be protected or redacted prior to public release.", new XFont("Arial", 11, XFontStyleEx.Regular), XBrushes.Black, new XRect(50, 125, 512, 20), XStringFormats.TopLeft);
                g2.DrawString("Account ID (Confidential): ACCT-98234-X72-PRIVATE", new XFont("Arial", 12, XFontStyleEx.Bold), XBrushes.DarkRed, new XRect(50, 170, 512, 25), XStringFormats.TopLeft);
            }

            // Page 3: Signatures Page
            var p3 = doc.AddPage();
            p3.Width = XUnit.FromPoint(612);
            p3.Height = XUnit.FromPoint(792);
            using (var g3 = XGraphics.FromPdfPage(p3))
            {
                g3.DrawString("3. Acceptance and Execution", new XFont("Arial", 16, XFontStyleEx.Bold), XBrushes.Navy, new XRect(50, 60, 512, 30), XStringFormats.TopLeft);
                g3.DrawString("IN WITNESS WHEREOF, the parties hereto have executed this Agreement as of the date written below.", new XFont("Arial", 11, XFontStyleEx.Italic), XBrushes.Black, new XRect(50, 100, 512, 25), XStringFormats.TopLeft);

                // Authorized Signature Block
                g3.DrawRectangle(new XPen(XColors.LightGray, 1), new XRect(50, 180, 240, 120));
                g3.DrawString("Authorized Signature:", new XFont("Arial", 10, XFontStyleEx.Bold), XBrushes.Gray, new XRect(60, 190, 220, 20), XStringFormats.TopLeft);
                g3.DrawLine(new XPen(XColors.Gray, 1), 60, 260, 270, 260);
                g3.DrawString("Sign here using Freedeeeff Signature Tool", new XFont("Arial", 9, XFontStyleEx.Italic), XBrushes.LightGray, new XRect(60, 265, 220, 15), XStringFormats.TopLeft);

                // Approval Stamp Block
                g3.DrawRectangle(new XPen(XColors.LightGray, 1), new XRect(320, 180, 240, 120));
                g3.DrawString("Approval Stamp:", new XFont("Arial", 10, XFontStyleEx.Bold), XBrushes.Gray, new XRect(330, 190, 220, 20), XStringFormats.TopLeft);
                g3.DrawString("Place Stamp here using Freedeeeff Stamp Tool", new XFont("Arial", 9, XFontStyleEx.Italic), XBrushes.LightGray, new XRect(330, 265, 220, 15), XStringFormats.TopLeft);
            }

            doc.Save(rootPdf);
            Assert.True(File.Exists(rootPdf));
        }
    }
}
