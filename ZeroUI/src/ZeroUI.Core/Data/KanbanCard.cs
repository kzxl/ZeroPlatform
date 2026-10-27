using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Data
{
    public class KanbanCard
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Color { get; set; }
        public string Tag { get; set; }
    }
    
    public class KanbanColumn
    {
        public string Title { get; set; }
        public List<KanbanCard> Cards { get; set; } = new List<KanbanCard>();
    }
}
