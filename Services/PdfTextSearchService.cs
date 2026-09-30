using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Freedeeeff.Models;
using UglyToad.PdfPig;

namespace Freedeeeff.Services
{
    public class SearchMatchItem
    {
        public int PageIndex { get; set; }
        public string MatchText { get; set; } = string.Empty;
        public string SurroundingSnippet { get; set; } = string.Empty;
        public Rect RelBounds { get; set; }
    }

    public class PdfTextSearchService
    {
        public List<SearchMatchItem> SearchInPdf(
            string filePath,
            string query,
            IDictionary<int, OcrPageResult>? cachedOcrResults = null)
        {
            var matches = new List<SearchMatchItem>();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || string.IsNullOrWhiteSpace(query))
            {
                return matches;
            }

            string cleanQuery = query.Trim();

            try
            {
                using var document = PdfDocument.Open(filePath);
                int pageCount = document.NumberOfPages;

                for (int p = 1; p <= pageCount; p++)
                {
                    int pageIndex0 = p - 1;
                    var page = document.GetPage(p);
                    string pageText = page.Text;

                    bool foundInTextStream = false;

                    if (!string.IsNullOrWhiteSpace(pageText) && pageText.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase))
                    {
                        foundInTextStream = true;
                        double pageW = page.Width;
                        double pageH = page.Height;

                        // Find matching words
                        var words = page.GetWords().ToList();
                        for (int i = 0; i < words.Count; i++)
                        {
                            var w = words[i];
                            if (w.Text.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase))
                            {
                                // In PdfPig, Y starts from bottom-left (standard PDF coordinates)
                                // Convert to top-left normalized coordinates for WPF
                                double relX = w.BoundingBox.Left / pageW;
                                double relY = (pageH - w.BoundingBox.Top) / pageH;
                                double relW = w.BoundingBox.Width / pageW;
                                double relH = w.BoundingBox.Height / pageH;

                                int startIdx = Math.Max(0, i - 3);
                                int endIdx = Math.Min(words.Count - 1, i + 3);
                                string snippet = string.Join(" ", words.Skip(startIdx).Take(endIdx - startIdx + 1).Select(x => x.Text));

                                matches.Add(new SearchMatchItem
                                {
                                    PageIndex = pageIndex0,
                                    MatchText = w.Text,
                                    SurroundingSnippet = snippet,
                                    RelBounds = new Rect(relX, relY, Math.Max(0.005, relW), Math.Max(0.005, relH))
                                });
                            }
                        }
                    }

                    // If not found in native text stream or native text stream was empty, search OCR cache
                    if (!foundInTextStream && cachedOcrResults != null && cachedOcrResults.TryGetValue(pageIndex0, out var ocrResult))
                    {
                        foreach (var line in ocrResult.Lines)
                        {
                            foreach (var word in line.Words)
                            {
                                if (word.Text.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase))
                                {
                                    matches.Add(new SearchMatchItem
                                    {
                                        PageIndex = pageIndex0,
                                        MatchText = word.Text,
                                        SurroundingSnippet = line.Text,
                                        RelBounds = word.RelBounds
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return matches;
        }
    }
}
