using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessAnalytics.Domain.Entities
{
    public enum DataSourceType { Sales = 1, Satisfaction = 2, Generic = 3 }
    public class DataSource
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public DataSourceType Type { get; set; }
        public string OwnerId { get; set; } = default!;
        public ApplicationUser? Owner { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<RawImport> Imports { get; set; } = new List<RawImport>();
        public ICollection<FactSales> FactSales { get; set; } = new List<FactSales>(); // NEW
    }
}
