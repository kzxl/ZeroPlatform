using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Headless mathematical model and state coordinator for data pagination.
    /// Provides zero-allocation page boundary math, windowed page list generation, and navigation events.
    /// </summary>
    public class PaginationModel
    {
        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalCount = 0;

        public event EventHandler<int>? PageChanged;
        public event EventHandler<int>? PageSizeChanged;
        public event EventHandler? StateChanged;

        public int CurrentPage
        {
            get => _currentPage;
            set => GoToPage(value);
        }

        public int PageSize
        {
            get => _pageSize;
            set => SetPageSize(value);
        }

        public int TotalCount
        {
            get => _totalCount;
            set
            {
                int sanitized = Math.Max(0, value);
                if (_totalCount != sanitized)
                {
                    _totalCount = sanitized;
                    int maxPages = TotalPages;
                    if (_currentPage > maxPages)
                    {
                        _currentPage = Math.Max(1, maxPages);
                    }
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public int TotalPages => _pageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling((double)_totalCount / _pageSize));

        public bool CanPrev => _currentPage > 1;
        public bool CanNext => _currentPage < TotalPages;
        public bool CanFirst => _currentPage > 1;
        public bool CanLast => _currentPage < TotalPages;

        public int StartItemIndex => _totalCount == 0 ? 0 : (_currentPage - 1) * _pageSize + 1;
        public int EndItemIndex => _totalCount == 0 ? 0 : Math.Min(_currentPage * _pageSize, _totalCount);

        public bool GoToPage(int page)
        {
            int target = Math.Max(1, Math.Min(TotalPages, page));
            if (target != _currentPage)
            {
                _currentPage = target;
                PageChanged?.Invoke(this, _currentPage);
                StateChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        public bool NextPage() => CanNext && GoToPage(_currentPage + 1);
        public bool PrevPage() => CanPrev && GoToPage(_currentPage - 1);
        public bool FirstPage() => CanFirst && GoToPage(1);
        public bool LastPage() => CanLast && GoToPage(TotalPages);

        public bool SetPageSize(int size)
        {
            int sanitized = Math.Max(1, size);
            if (sanitized != _pageSize)
            {
                _pageSize = sanitized;
                int maxPages = TotalPages;
                if (_currentPage > maxPages)
                {
                    _currentPage = Math.Max(1, maxPages);
                }
                PageSizeChanged?.Invoke(this, _pageSize);
                StateChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Generates a windowed list of page numbers to render as buttons.
        /// A value of -1 denotes an ellipsis ("...").
        /// </summary>
        public int[] GetVisiblePages(int maxButtons = 7)
        {
            int total = TotalPages;
            if (total <= maxButtons)
            {
                var list = new int[total];
                for (int i = 0; i < total; i++) list[i] = i + 1;
                return list;
            }

            var result = new List<int>(maxButtons);
            int cur = _currentPage;

            if (cur <= 4)
            {
                for (int i = 1; i <= 5; i++) result.Add(i);
                result.Add(-1); // ...
                result.Add(total);
            }
            else if (cur >= total - 3)
            {
                result.Add(1);
                result.Add(-1); // ...
                for (int i = total - 4; i <= total; i++) result.Add(i);
            }
            else
            {
                result.Add(1);
                result.Add(-1); // ...
                result.Add(cur - 1);
                result.Add(cur);
                result.Add(cur + 1);
                result.Add(-1); // ...
                result.Add(total);
            }

            return result.ToArray();
        }
    }
}
