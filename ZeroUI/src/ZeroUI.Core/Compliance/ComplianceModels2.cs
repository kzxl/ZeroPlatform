namespace ZeroUI.Core.Compliance
{
    public class ChecklistItem
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public bool IsRequired { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}