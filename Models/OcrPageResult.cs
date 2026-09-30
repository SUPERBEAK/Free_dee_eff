using System;
using System.Collections.Generic;
using System.Windows;

namespace Freedeeeff.Models
{
    public class OcrWordInfo
    {
        public string Text { get; set; } = string.Empty;
        // Normalized page bounds (0..1)
        public Rect RelBounds { get; set; }
    }

    public class OcrLineInfo
    {
        public string Text { get; set; } = string.Empty;
        public Rect RelBounds { get; set; }
        public List<OcrWordInfo> Words { get; set; } = new();
    }

    public class OcrPageResult
    {
        public int PageIndex { get; set; }
        public string FullText { get; set; } = string.Empty;
        public List<OcrLineInfo> Lines { get; set; } = new();
        public bool HasText => !string.IsNullOrWhiteSpace(FullText);
    }
}
