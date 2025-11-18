using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessAnalytics.Infrastructure.Files
{
    public interface IFileStorage
    {
        Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default);
    }
}
