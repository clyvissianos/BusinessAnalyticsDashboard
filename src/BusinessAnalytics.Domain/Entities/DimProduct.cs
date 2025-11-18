using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessAnalytics.Domain.Entities
{
    public class DimProduct
    {
        public int ProductKey { get; set; }  // PK, surrogate key
        public string ProductName { get; set; } = null!;
        public string? Category { get; set; }
        public string? SubCategory { get; set; }

        public ICollection<FactSales> Sales { get; set; } = new List<FactSales>();
    }
}
