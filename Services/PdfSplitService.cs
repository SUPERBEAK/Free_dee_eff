using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Freedeeeff.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Freedeeeff.Services
{
    public class PdfSplitService
    {
        public SplitResult SplitPdf(string sourceFilePath, SplitOptions options)
        {
            var result = new SplitResult();

            if (!File.Exists(sourceFilePath))
            {
                result.Success = false;
                result.Message = "Source file does not exist.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            {
                options.OutputDirectory = Path.GetDirectoryName(sourceFilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            Directory.CreateDirectory(options.OutputDirectory);

            string baseName = string.IsNullOrWhiteSpace(options.BaseFileName)
                ? Path.GetFileNameWithoutExtension(sourceFilePath)
                : options.BaseFileName;

            try
            {
                using var inputDoc = PdfReader.Open(sourceFilePath, PdfDocumentOpenMode.Import);
                int totalPages = inputDoc.PageCount;

                if (totalPages == 0)
                {
                    result.Success = false;
                    result.Message = "PDF contains no pages.";
                    return result;
                }

                switch (options.Mode)
                {
                    case SplitMode.AllPages:
                        for (int i = 0; i < totalPages; i++)
                        {
                            var outDoc = new PdfDocument();
                            outDoc.AddPage(inputDoc.Pages[i]);
                            string outPath = Path.Combine(options.OutputDirectory, $"{baseName}_page_{i + 1:D3}.pdf");
                            outDoc.Save(outPath);
                            result.GeneratedFiles.Add(outPath);
                        }
                        break;

                    case SplitMode.EveryNPages:
                        int batchSize = Math.Max(1, options.EveryNPagesCount);
                        int partNum = 1;
                        for (int i = 0; i < totalPages; i += batchSize)
                        {
                            var outDoc = new PdfDocument();
                            int end = Math.Min(i + batchSize, totalPages);
                            for (int p = i; p < end; p++)
                            {
                                outDoc.AddPage(inputDoc.Pages[p]);
                            }
                            string outPath = Path.Combine(options.OutputDirectory, $"{baseName}_part_{partNum:D2}_p{i + 1}-p{end}.pdf");
                            outDoc.Save(outPath);
                            result.GeneratedFiles.Add(outPath);
                            partNum++;
                        }
                        break;

                    case SplitMode.ByRanges:
                        var parsedRanges = ParsePageRanges(options.RangeString, totalPages);
                        if (parsedRanges.Count == 0)
                        {
                            result.Success = false;
                            result.Message = "No valid page ranges specified. (Format example: 1-3, 4, 5-8)";
                            return result;
                        }

                        int rangeIdx = 1;
                        foreach (var range in parsedRanges)
                        {
                            var outDoc = new PdfDocument();
                            foreach (int page1Based in range)
                            {
                                int page0Based = page1Based - 1;
                                if (page0Based >= 0 && page0Based < totalPages)
                                {
                                    outDoc.AddPage(inputDoc.Pages[page0Based]);
                                }
                            }

                            if (outDoc.PageCount > 0)
                            {
                                int startP = range.First();
                                int endP = range.Last();
                                string outPath = Path.Combine(options.OutputDirectory, $"{baseName}_range_{rangeIdx:D2}_p{startP}-p{endP}.pdf");
                                outDoc.Save(outPath);
                                result.GeneratedFiles.Add(outPath);
                                rangeIdx++;
                            }
                        }
                        break;

                    case SplitMode.SelectedPages:
                        if (options.SelectedPageIndices == null || options.SelectedPageIndices.Count == 0)
                        {
                            result.Success = false;
                            result.Message = "No pages were selected for extraction.";
                            return result;
                        }

                        var extractDoc = new PdfDocument();
                        foreach (int page0Based in options.SelectedPageIndices.OrderBy(x => x))
                        {
                            if (page0Based >= 0 && page0Based < totalPages)
                            {
                                extractDoc.AddPage(inputDoc.Pages[page0Based]);
                            }
                        }

                        if (extractDoc.PageCount > 0)
                        {
                            string outPath = Path.Combine(options.OutputDirectory, $"{baseName}_extracted.pdf");
                            extractDoc.Save(outPath);
                            result.GeneratedFiles.Add(outPath);
                        }
                        break;
                }

                result.Success = result.GeneratedFiles.Count > 0;
                result.Message = result.Success
                    ? $"Successfully generated {result.GeneratedFiles.Count} PDF file(s)."
                    : "No files generated.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error splitting PDF: {ex.Message}";
            }

            return result;
        }

        public bool MergePdfs(IEnumerable<string> sourceFiles, string destinationPath, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var outDoc = new PdfDocument();

                foreach (var file in sourceFiles)
                {
                    if (!File.Exists(file)) continue;

                    using var inDoc = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                    for (int i = 0; i < inDoc.PageCount; i++)
                    {
                        outDoc.AddPage(inDoc.Pages[i]);
                    }
                }

                if (outDoc.PageCount == 0)
                {
                    errorMessage = "No pages to merge.";
                    return false;
                }

                outDoc.Save(destinationPath);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static List<List<int>> ParsePageRanges(string rangeString, int maxPage)
        {
            var result = new List<List<int>>();
            if (string.IsNullOrWhiteSpace(rangeString)) return result;

            var parts = rangeString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var clean = part.Trim();
                if (clean.Contains('-'))
                {
                    var segs = clean.Split('-');
                    if (segs.Length == 2 && int.TryParse(segs[0].Trim(), out int start) && int.TryParse(segs[1].Trim(), out int end))
                    {
                        start = Math.Clamp(start, 1, maxPage);
                        end = Math.Clamp(end, 1, maxPage);
                        if (start <= end)
                        {
                            var list = new List<int>();
                            for (int p = start; p <= end; p++) list.Add(p);
                            if (list.Count > 0) result.Add(list);
                        }
                    }
                }
                else if (int.TryParse(clean, out int single))
                {
                    if (single >= 1 && single <= maxPage)
                    {
                        result.Add(new List<int> { single });
                    }
                }
            }

            return result;
        }
    }
}
