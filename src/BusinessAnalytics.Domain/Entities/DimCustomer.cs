using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessAnalytics.Domain.Entities
{
    public class DimCustomer
    {
        public int CustomerKey { get; set; }  // PK
        public string CustomerName { get; set; } = null!;
        public string? Email { get; set; }
        public string? Region { get; set; }

        public ICollection<FactSales> Sales { get; set; } = new List<FactSales>();
    }
}
