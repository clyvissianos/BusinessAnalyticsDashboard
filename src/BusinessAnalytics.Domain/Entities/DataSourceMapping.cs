using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessAnalytics.Domain.Entities
{
    public class DataSourceMapping
    {
        public int Id { get; set; }
        public int DataSourceId { get; set; }
        public DataSource DataSource { get; set; } = default!;
        public string SheetName { get; set; } = "";     // for Excel
        public string Culture { get; set; } = "el-GR";  // parsing culture
        public string Kind { get; set; } = "Sales";     // "Sales" | "Generic" | future kinds
        public string ColumnMapJson { get; set; } = ""; // serialized map (see below)
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
