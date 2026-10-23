using System;
using System.Collections.Generic;
using System.Threading;
using RemofixZip.Models;
using SharpCompress.Archives;

namespace RemofixZip.Services
{
    public class RemoteZipResult : IDisposable
    {
        public readonly SemaphoreSlim StreamLock = new(1, 1);

        public string Url { get; set; } = string.Empty;
        public string ArchiveFileName { get; set; } = string.Empty;
        public string ArchiveFolderName { get; set; } = string.Empty;
        public long TotalArchiveSize { get; set; }
        public bool IsEncrypted { get; set; }
        public int TotalFilesCount { get; set; }
        public int TotalFoldersCount { get; set; }

        public ZipTreeNode RootNode { get; set; } = new();
        public List<ZipEntryItem> FlatEntries { get; set; } = new();

        public HttpRangeSeekableStream? SeekableStream { get; set; }
        public IArchive? Archive { get; set; }

        public string FormattedTotalSize => ZipEntryItem.FormatBytes(TotalArchiveSize);

        public void Dispose()
        {
            try
            {
                StreamLock.Dispose();
                Archive?.Dispose();
                SeekableStream?.Dispose();
            }
            catch { }
        }
    }
}
