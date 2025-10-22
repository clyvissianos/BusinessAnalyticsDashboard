using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessAnalytics.Domain.Entities
{
    public class DimDate
    {
        public int DateKey { get; set; } // YYYYMMDD
        public DateOnly Date { get; set; }
        public int Year { get; set; }
        public int Quarter { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = default!;
        public int Day { get; set; }
        public int IsoWeek { get; set; }

        public static int ToDateKey(DateOnly d) => d.Year * 10000 + d.Month * 100 + d.Day;
    }
}
