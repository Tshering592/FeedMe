using System.Collections.Generic;

namespace FeedMe.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // FIX: Added DisplayOrder to satisfy controller/view queries
        public int DisplayOrder { get; set; } = 0;

        public virtual ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    }
}