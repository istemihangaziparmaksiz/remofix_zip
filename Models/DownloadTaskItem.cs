using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RemofixZip.Models
{
    public enum DownloadStatus
    {
        Queued,
        Downloading,
        Completed,
        Failed,
        Canceled
    }

    public class DownloadTaskItem : INotifyPropertyChanged
    {
        private DownloadStatus _status = DownloadStatus.Queued;
        private double _progress;
        private long _downloadedBytes;
        private long _totalBytes;
        private string _speedText = string.Empty;
        private string _statusText = "Kuyrukta bekliyor";
        private string? _errorMessage;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string FileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string LocalDestinationPath { get; set; } = string.Empty;
        public ZipEntryItem Entry { get; set; } = new();
        public CancellationTokenSource? Cts { get; set; }
        public int ThreadCount { get; set; } = 4;

        public DownloadStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(IsCompleted));
                OnPropertyChanged(nameof(StatusBadgeBackground));
                OnPropertyChanged(nameof(StatusBadgeForeground));
            }
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); }
        }

        public string ProgressText => $"{_progress:F1}%";

        public long DownloadedBytes
        {
            get => _downloadedBytes;
            set { _downloadedBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(DownloadedSizeText)); }
        }

        public long TotalBytes
        {
            get => _totalBytes;
            set { _totalBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalSizeText)); }
        }

        public string DownloadedSizeText
        {
            get => ZipEntryItem.FormatBytes(_downloadedBytes);
            set { }
        }

        public string TotalSizeText
        {
            get => ZipEntryItem.FormatBytes(_totalBytes);
            set { }
        }

        public string SpeedText
        {
            get => _speedText;
            set { _speedText = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public bool IsActive => _status == DownloadStatus.Downloading;
        public bool IsCompleted => _status == DownloadStatus.Completed;

        public string StatusBadgeBackground => _status switch
        {
            DownloadStatus.Completed => "#052E16",
            DownloadStatus.Downloading => "#082F49",
            DownloadStatus.Failed => "#2A0A0E",
            DownloadStatus.Canceled => "#291503",
            _ => "#18181C"
        };

        public string StatusBadgeForeground => _status switch
        {
            DownloadStatus.Completed => "#34D399",
            DownloadStatus.Downloading => "#38BDF8",
            DownloadStatus.Failed => "#F87171",
            DownloadStatus.Canceled => "#FBBF24",
            _ => "#A1A1AA"
        };

        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
