using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Freedeeeff.Models.Annotations
{
    public enum AnnotationType
    {
        TextFill,
        Signature,
        Highlight,
        Pen,
        Rectangle,
        Ellipse,
        Line,
        Arrow,
        Redaction,
        Stamp,
        StickyNote
    }

    public enum RedactionType
    {
        Blackout,
        Whiteout
    }

    public enum SignatureMode
    {
        Drawn,
        Typed,
        Image
    }

    public class AnnotationItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int PageIndex { get; set; }
        public AnnotationType Type { get; set; }

        // Relative coordinates (0.0 to 1.0) on the page so zoom and resize stay resolution-independent
        public double RelX { get; set; }
        public double RelY { get; set; }
        public double RelWidth { get; set; }
        public double RelHeight { get; set; }

        // Text properties (for TextFill, StickyNote, Stamp, Typed Signature)
        public string Text { get; set; } = string.Empty;
        public string FontFamily { get; set; } = "Segoe UI";
        public double FontSize { get; set; } = 14;
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public string TextColorHex { get; set; } = "#000000";
        public string BackgroundColorHex { get; set; } = "Transparent";

        // Stroke & Shape properties
        public string StrokeColorHex { get; set; } = "#000000";
        public double StrokeThickness { get; set; } = 2.0;
        public double Opacity { get; set; } = 1.0;

        // Pen / Ink points (stored as relative points [0..1, 0..1])
        public List<Point> RelativePoints { get; set; } = new();

        // Signature properties
        public SignatureMode SignatureMode { get; set; } = SignatureMode.Drawn;
        public byte[]? ImageBytes { get; set; }

        // Redaction properties
        public RedactionType RedactionType { get; set; } = RedactionType.Blackout;

        // Stamp properties
        public string StampTitle { get; set; } = "APPROVED";
        public DateTime? StampDate { get; set; }

        // Selection & UI state (not persisted to final PDF)
        public bool IsSelected { get; set; }
    }
}
