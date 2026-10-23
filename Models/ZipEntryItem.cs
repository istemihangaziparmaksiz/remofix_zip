using System;

namespace RemofixZip.Models
{
    public class ZipEntryItem
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public long CompressedSize { get; set; }
        public string CompressionMethod { get; set; } = "Deflate";
        public bool IsEncrypted { get; set; }
        public DateTime? ModifiedTime { get; set; }
        public long Crc { get; set; }
        public long LocalHeaderOffset { get; set; }

        public string FormattedSize => IsDirectory ? "" : FormatBytes(Size);
        public string FormattedCompressedSize => IsDirectory ? "" : FormatBytes(CompressedSize);

        public string CompressionRatioText
        {
            get
            {
                if (IsDirectory || Size <= 0) return string.Empty;
                double ratio = 100.0 - ((double)CompressedSize / Size * 100.0);
                if (ratio < 0) ratio = 0;
                return $"%{ratio:F0}";
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "0 B";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }
}
