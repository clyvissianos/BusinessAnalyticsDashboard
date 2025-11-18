using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessAnalytics.Domain.Entities
{
    public class FactSales
    {
        public int Id { get; set; } // identity surrogate key

        public int DataSourceId { get; set; }          // NEW  ⬅⬅
        public DataSource DataSource { get; set; } = default!;  // NEW  ⬅⬅

        public DateOnly Date { get; set; }
        public int DateKey { get; set; }

        public int ProductKey { get; set; }
        public int CustomerKey { get; set; }

        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }

        public DimDate? DimDate { get; set; }
        public DimProduct? Product { get; set; }
        public DimCustomer? Customer { get; set; }
    }
}
