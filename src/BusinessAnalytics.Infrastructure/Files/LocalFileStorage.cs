using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessAnalytics.Infrastructure.Files
{
    public class LocalFileStorage : IFileStorage
    {
        private readonly string _root;
        public LocalFileStorage(IOptions<UploadOptions> options)
        {
            _root = string.IsNullOrWhiteSpace(options.Value.Root)
                ? Path.Combine(AppContext.BaseDirectory, "data", "uploads")
                : options.Value.Root;

            Directory.CreateDirectory(_root);
        }

        public async Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default)
        {
            var clean = Path.GetFileName(originalFileName);
            var fname = $"{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}_{clean}";
            var path = Path.Combine(_root, fname);

            await using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await content.CopyToAsync(fs, ct);
            return path;
        }
    }
}
