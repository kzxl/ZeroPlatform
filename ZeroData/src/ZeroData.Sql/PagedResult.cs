using System.Collections.Generic;

namespace ZeroData.Sql
{
    /// <summary>
    /// Represents a paginated result set with metadata.
    /// </summary>
    public class PagedResult<T>
    {
        /// <summary>The items for the current page.</summary>
        public List<T> Items { get; set; }

        /// <summary>Total number of records matching the query (before pagination).</summary>
        public int TotalCount { get; set; }

        /// <summary>Total number of pages.</summary>
        public int TotalPages { get; set; }

        /// <summary>Current page number (1-based).</summary>
        public int CurrentPage { get; set; }

        /// <summary>Number of items per page.</summary>
        public int PageSize { get; set; }

        /// <summary>True if there is a next page.</summary>
        public bool HasNext => CurrentPage < TotalPages;

        /// <summary>True if there is a previous page.</summary>
        public bool HasPrevious => CurrentPage > 1;
    }
}
