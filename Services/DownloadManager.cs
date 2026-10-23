using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using RemofixZip.Models;

namespace RemofixZip.Services
{
    public class DownloadManager
    {
        public static DownloadManager Instance { get; } = new();

        public ObservableCollection<DownloadTaskItem> Tasks { get; } = new();

        private readonly SemaphoreSlim _semaphore = new(4, 4);
        private readonly ConcurrentDictionary<string, string> _sessionPasswords = new(StringComparer.OrdinalIgnoreCase);
        private readonly RemoteZipService _zipService = new();

        public void SetSessionPassword(string url, string password)
        {
            _sessionPasswords[url] = password;
        }

        public string? GetSessionPassword(string url)
        {
            _sessionPasswords.TryGetValue(url, out var pass);
            return pass;
        }

        public void EnqueueDownload(RemoteZipResult zipResult, ZipEntryItem entry)
        {
            string destinationFolder = Path.Combine(AppSettings.Current.BaseDownloadDirectory, zipResult.ArchiveFolderName);
            string localFilePath = Path.Combine(destinationFolder, entry.Path.Replace('/', Path.DirectorySeparatorChar));

            var task = new DownloadTaskItem
            {
                FileName = entry.Name,
                RelativePath = entry.Path,
                LocalDestinationPath = localFilePath,
                Entry = entry,
                TotalBytes = entry.Size > 0 ? entry.Size : entry.CompressedSize,
                ThreadCount = AppSettings.Current.DownloadThreads,
                Status = DownloadStatus.Queued,
                Cts = new CancellationTokenSource()
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                Tasks.Insert(0, task);
            });

            _ = ProcessTaskAsync(zipResult, task);
        }

        private async Task ProcessTaskAsync(RemoteZipResult zipResult, DownloadTaskItem task)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (task.Cts?.IsCancellationRequested == true)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        task.Status = DownloadStatus.Canceled;
                        task.StatusText = "İptal edildi";
                    });
                    return;
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    task.Status = DownloadStatus.Downloading;
                    task.StatusText = "İndiriliyor...";
                });

                string? password = GetSessionPassword(zipResult.Url);

                var progress = new Progress<(long downloaded, long total, double speed)>(report =>
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        task.DownloadedBytes = report.downloaded;
                        if (report.total > 0)
                        {
                            task.TotalBytes = report.total;
                            task.Progress = Math.Min(100.0, (double)report.downloaded / report.total * 100.0);
                        }
                        if (report.speed > 0)
                        {
                            task.SpeedText = $"{report.speed / (1024.0 * 1024.0):F2} MB/s";
                        }
                    });
                });

                await _zipService.DownloadFileEntryAsync(
                    zipResult,
                    task.Entry,
                    task.LocalDestinationPath,
                    password,
                    task.ThreadCount,
                    progress,
                    task.Cts?.Token ?? CancellationToken.None).ConfigureAwait(false);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    task.DownloadedBytes = task.TotalBytes;
                    task.Progress = 100.0;
                    task.SpeedText = "";
                    task.StatusText = "Tamamlandı";
                    task.Status = DownloadStatus.Completed;
                });
            }
            catch (OperationCanceledException)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    task.Status = DownloadStatus.Canceled;
                    task.StatusText = "İptal edildi";
                    task.SpeedText = "";
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    task.Status = DownloadStatus.Failed;
                    task.StatusText = "Hata oluştu";
                    task.ErrorMessage = ex.Message;
                    task.SpeedText = "";
                });
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void CancelTask(DownloadTaskItem task)
        {
            try
            {
                task.Cts?.Cancel();
                task.Status = DownloadStatus.Canceled;
                task.StatusText = "İptal edildi";
            }
            catch { }
        }

        public void ClearFinished()
        {
            var finished = Tasks.Where(t => t.Status == DownloadStatus.Completed || t.Status == DownloadStatus.Canceled || t.Status == DownloadStatus.Failed).ToList();
            foreach (var item in finished)
            {
                Tasks.Remove(item);
            }
        }
    }
}
