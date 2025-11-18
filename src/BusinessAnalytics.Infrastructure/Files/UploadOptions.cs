using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessAnalytics.Infrastructure.Files
{
    public class UploadOptions
    {
        // Root folder for uploads; default to {bin}/data/uploads if not supplied.
        public string Root { get; set; } = string.Empty;
        // Max allowed size in bytes (50 MB default)
        public long MaxBytes { get; set; } = 50L * 1024 * 1024;
    }
}
