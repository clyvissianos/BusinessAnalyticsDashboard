using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessAnalytics.Domain.Entities
{
    public class ImportResult
    {
        public int Rows { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        public ImportResult() { }

        public ImportResult(int rows, bool success, string? errorMessage = null)
        {
            Rows = rows;
            Success = success;
            ErrorMessage = errorMessage;
        }
    }
}
