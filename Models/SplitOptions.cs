using System.Collections.Generic;

namespace Freedeeeff.Models
{
    public enum SplitMode
    {
        AllPages,       // Every page to separate file
        ByRanges,       // Custom ranges like 1-3, 4-7, 8
        EveryNPages,    // Chunks of N pages
        SelectedPages   // Extract selected thumbnail pages
    }

    public class SplitOptions
    {
        public SplitMode Mode { get; set; } = SplitMode.AllPages;
        public string RangeString { get; set; } = "1-2, 3-4";
        public int EveryNPagesCount { get; set; } = 1;
        public List<int> SelectedPageIndices { get; set; } = new();
        public string OutputDirectory { get; set; } = string.Empty;
        public string BaseFileName { get; set; } = "Document";
    }

    public class SplitResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> GeneratedFiles { get; set; } = new();
    }
}
