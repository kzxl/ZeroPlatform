namespace ZeroUI.Core.Media {
    public enum PdfAnnotationMode { Highlight, Stamp, FreeText }
    public class PdfAnnotation {
        public int Page { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public PdfAnnotationMode Type { get; set; }
        public string Text { get; set; } = "";
    }
}