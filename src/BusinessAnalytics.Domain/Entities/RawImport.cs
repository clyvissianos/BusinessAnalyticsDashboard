using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessAnalytics.Domain.Entities
{
    public enum ImportStatus { Staged = 0, Parsed = 1, Failed = 2 }
    public class RawImport
    {
        public int Id { get; set; }
        public int DataSourceId { get; set; }
        public DataSource DataSource { get; set; } = default!;
        public string OriginalFilePath { get; set; } = default!;
        public ImportStatus Status { get; set; } = ImportStatus.Staged;
        public int Rows { get; set; }
        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAtUtc { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
