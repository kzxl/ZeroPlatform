using System;
using System.Collections.Generic;

namespace ZeroCharts.Axis
{
    /// <summary>
    /// Discrete categorical / nominal axis for bar charts, grouped histograms, and categorical data.
    /// Maps string category labels to equidistant screen slots.
    /// </summary>
    public class CategoryAxis
    {
        public string Title { get; set; } = string.Empty;
        private readonly List<string> _categories = new List<string>();

        public IReadOnlyList<string> Categories => _categories;
        public int Count => _categories.Count;

        public CategoryAxis(string title = "")
        {
            Title = title;
        }

        public void SetCategories(IEnumerable<string> categories)
        {
            _categories.Clear();
            if (categories != null)
            {
                foreach (var c in categories)
                {
                    if (!_categories.Contains(c))
                        _categories.Add(c);
                }
            }
        }

        public int EnsureCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return -1;
            int idx = _categories.IndexOf(category);
            if (idx < 0)
            {
                idx = _categories.Count;
                _categories.Add(category);
            }
            return idx;
        }

        public float GetSlotCenter(int index, float screenStart, float screenLength)
        {
            if (_categories.Count == 0) return screenStart + screenLength * 0.5f;
            float slotWidth = screenLength / _categories.Count;
            return screenStart + (index + 0.5f) * slotWidth;
        }

        public float GetSlotWidth(float screenLength)
        {
            if (_categories.Count == 0) return screenLength;
            return screenLength / _categories.Count;
        }

        public int GetCategoryAtScreen(float screenPos, float screenStart, float screenLength)
        {
            if (_categories.Count == 0 || screenPos < screenStart || screenPos > screenStart + screenLength)
                return -1;

            float slotWidth = GetSlotWidth(screenLength);
            int idx = (int)((screenPos - screenStart) / slotWidth);
            if (idx >= 0 && idx < _categories.Count) return idx;
            return -1;
        }
    }
}
