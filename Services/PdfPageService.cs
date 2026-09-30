using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Freedeeeff.Services
{
    public class PdfPageService
    {
        public bool RotatePages(string inputPath, string outputPath, IEnumerable<int> pageIndices, int rotateAngleDelta)
        {
            try
            {
                using var doc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);
                var set = new HashSet<int>(pageIndices);

                for (int i = 0; i < doc.PageCount; i++)
                {
                    if (set.Contains(i))
                    {
                        int current = doc.Pages[i].Rotate;
                        int newAngle = (current + rotateAngleDelta) % 360;
                        if (newAngle < 0) newAngle += 360;
                        doc.Pages[i].Rotate = newAngle;
                    }
                }

                doc.Save(outputPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DeletePages(string inputPath, string outputPath, IEnumerable<int> pageIndicesToDelete)
        {
            try
            {
                using var inDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
                using var outDoc = new PdfDocument();

                var toDelete = new HashSet<int>(pageIndicesToDelete);

                for (int i = 0; i < inDoc.PageCount; i++)
                {
                    if (!toDelete.Contains(i))
                    {
                        outDoc.AddPage(inDoc.Pages[i]);
                    }
                }

                if (outDoc.PageCount == 0) return false;

                outDoc.Save(outputPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool InsertBlankPage(string inputPath, string outputPath, int afterPageIndex, double widthPoints = 612, double heightPoints = 792)
        {
            try
            {
                using var inDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
                using var outDoc = new PdfDocument();

                for (int i = 0; i < inDoc.PageCount; i++)
                {
                    outDoc.AddPage(inDoc.Pages[i]);
                    if (i == afterPageIndex)
                    {
                        var blank = outDoc.AddPage();
                        blank.Width = XUnit.FromPoint(widthPoints);
                        blank.Height = XUnit.FromPoint(heightPoints);
                    }
                }

                if (afterPageIndex >= inDoc.PageCount)
                {
                    var blank = outDoc.AddPage();
                    blank.Width = XUnit.FromPoint(widthPoints);
                    blank.Height = XUnit.FromPoint(heightPoints);
                }

                outDoc.Save(outputPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ReorderPages(string inputPath, string outputPath, IList<int> newOrderIndices)
        {
            try
            {
                using var inDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
                using var outDoc = new PdfDocument();

                foreach (var idx in newOrderIndices)
                {
                    if (idx >= 0 && idx < inDoc.PageCount)
                    {
                        outDoc.AddPage(inDoc.Pages[idx]);
                    }
                }

                if (outDoc.PageCount == 0) return false;

                outDoc.Save(outputPath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
