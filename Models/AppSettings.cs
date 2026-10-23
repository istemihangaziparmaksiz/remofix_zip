using System;
using System.IO;

namespace RemofixZip.Models
{
    public class AppSettings
    {
        public static AppSettings Current { get; } = new();

        public int DownloadThreads { get; set; } = 4;

        public string BaseDownloadDirectory { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "remofix_zip_download");

        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36 RemofixZip/1.0";
    }
}
