using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using RemofixZip.Models;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace RemofixZip.Services
{
    public class RemoteZipService
    {
        private static readonly HttpClient HttpClientInstance;

        static RemoteZipService()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
            };
            HttpClientInstance = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
        }

        public async Task<RemoteZipResult> AnalyzeRemoteZipAsync(
            string url,
            string? cookieHeader = null,
            string? password = null,
            IProgress<string>? statusProgress = null,
            CancellationToken ct = default)
        {
            statusProgress?.Report("Sunucu bağlantısı ve HTTP Range desteği doğrulanıyor...");

            // 1. Send probe request: Range: bytes=0-0 to verify 206 Partial Content and get total size
            using var probeReq = new HttpRequestMessage(HttpMethod.Get, url);
            probeReq.Headers.Range = new RangeHeaderValue(0, 0);
            probeReq.Headers.UserAgent.ParseAdd(AppSettings.Current.UserAgent);
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                probeReq.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            }

            using var probeRes = await HttpClientInstance.SendAsync(probeReq, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (probeRes.StatusCode == System.Net.HttpStatusCode.OK)
            {
                throw new NotSupportedException("Bu dosya veya sunucu parçalı indirmeyi (HTTP Range) desteklemiyor.");
            }

            if (probeRes.StatusCode != System.Net.HttpStatusCode.PartialContent)
            {
                throw new HttpRequestException($"Sunucu HTTP Range isteğine beklenmeyen bir yanıt verdi: {(int)probeRes.StatusCode} {probeRes.ReasonPhrase}");
            }

            // Extract total length from Content-Range: bytes 0-0/123456789
            long totalSize = -1;
            if (probeRes.Content.Headers.ContentRange?.Length != null)
            {
                totalSize = probeRes.Content.Headers.ContentRange.Length.Value;
            }
            else if (probeRes.Content.Headers.ContentLength.HasValue)
            {
                totalSize = probeRes.Content.Headers.ContentLength.Value;
            }

            if (totalSize <= 0)
            {
                throw new NotSupportedException("Sunucu geçerli bir dosya boyutu bildirmedi.");
            }

            // Determine filename
            string fileName = "archive.zip";
            if (probeRes.Content.Headers.ContentDisposition?.FileName != null)
            {
                fileName = probeRes.Content.Headers.ContentDisposition.FileName.Trim('\"', '\'');
            }
            else
            {
                try
                {
                    var uri = new Uri(url);
                    string lastSeg = Path.GetFileName(uri.LocalPath);
                    if (!string.IsNullOrWhiteSpace(lastSeg))
                    {
                        fileName = lastSeg;
                    }
                }
                catch { }
            }

            string folderName = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrWhiteSpace(folderName)) folderName = "remote_archive";

            statusProgress?.Report($"Dosya boyutu: {ZipEntryItem.FormatBytes(totalSize)} | ZIP dizini (Central Directory) okunuyor...");

            // 2. Open seekable stream over HTTP Range
            var seekableStream = new HttpRangeSeekableStream(HttpClientInstance, url, totalSize, cookieHeader);

            var readerOptions = new ReaderOptions();
            if (!string.IsNullOrEmpty(password))
            {
                readerOptions.Password = password;
            }

            var archive = ZipArchive.OpenArchive(seekableStream, readerOptions);

            var flatEntries = new List<ZipEntryItem>();
            bool anyEncrypted = false;
            int totalFiles = 0;
            int totalFolders = 0;

            var rootNode = new ZipTreeNode
            {
                Name = fileName,
                FullPath = "",
                IsDirectory = true,
                IsExpanded = true
            };

            var folderDict = new Dictionary<string, ZipTreeNode>(StringComparer.OrdinalIgnoreCase)
            {
                [""] = rootNode
            };

            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();

                string entryKey = entry.Key?.Replace('\\', '/').Trim('/') ?? "";
                if (string.IsNullOrWhiteSpace(entryKey)) continue;

                bool isDir = entry.IsDirectory;
                if (isDir) totalFolders++; else totalFiles++;

                if (entry.IsEncrypted) anyEncrypted = true;

                var item = new ZipEntryItem
                {
                    Path = entryKey,
                    Name = Path.GetFileName(entryKey),
                    IsDirectory = isDir,
                    Size = entry.Size,
                    CompressedSize = entry.CompressedSize,
                    CompressionMethod = entry.CompressionType.ToString(),
                    IsEncrypted = entry.IsEncrypted,
                    ModifiedTime = entry.LastModifiedTime,
                    Crc = entry.Crc
                };
                flatEntries.Add(item);

                // Build tree
                string[] parts = entryKey.Split('/');
                string currentPath = "";
                ZipTreeNode currentParent = rootNode;

                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];
                    bool isLastPart = (i == parts.Length - 1);
                    currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";

                    if (isLastPart && !isDir)
                    {
                        // File node
                        var fileNode = new ZipTreeNode
                        {
                            Name = part,
                            FullPath = currentPath,
                            IsDirectory = false,
                            Item = item,
                            Parent = currentParent
                        };
                        currentParent.Children.Add(fileNode);
                    }
                    else
                    {
                        // Folder node
                        if (!folderDict.TryGetValue(currentPath, out var folderNode))
                        {
                            folderNode = new ZipTreeNode
                            {
                                Name = part,
                                FullPath = currentPath,
                                IsDirectory = true,
                                Parent = currentParent
                            };
                            folderDict[currentPath] = folderNode;
                            currentParent.Children.Add(folderNode);
                        }
                        currentParent = folderNode;
                    }
                }
            }

            statusProgress?.Report($"Analiz tamamlandı. {totalFiles} dosya, {totalFolders} klasör tespit edildi.");

            return new RemoteZipResult
            {
                Url = url,
                ArchiveFileName = fileName,
                ArchiveFolderName = folderName,
                TotalArchiveSize = totalSize,
                IsEncrypted = anyEncrypted,
                TotalFilesCount = totalFiles,
                TotalFoldersCount = totalFolders,
                RootNode = rootNode,
                FlatEntries = flatEntries,
                SeekableStream = seekableStream,
                Archive = archive
            };
        }

        public async Task DownloadFileEntryAsync(
            RemoteZipResult zipResult,
            ZipEntryItem entryItem,
            string destinationFilePath,
            string? password,
            int threadCount,
            IProgress<(long downloaded, long total, double speed)> progress,
            CancellationToken ct)
        {
            string? dir = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Acquire lock to avoid concurrent stream seeks on the same archive
            await zipResult.StreamLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var entry = zipResult.Archive?.Entries.FirstOrDefault(e =>
                    string.Equals(e.Key?.Replace('\\', '/').Trim('/'), entryItem.Path.Trim('/'), StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                {
                    throw new FileNotFoundException($"Arşiv içinde dosya bulunamadı: {entryItem.Path}");
                }

                // If encrypted and password provided, recreate archive entry reader with password if needed
                if (entry.IsEncrypted && !string.IsNullOrEmpty(password))
                {
                    zipResult.Archive?.Dispose();
                    zipResult.SeekableStream?.Seek(0, SeekOrigin.Begin);
                    var options = new ReaderOptions { Password = password };
                    zipResult.Archive = ZipArchive.OpenArchive(zipResult.SeekableStream!, options);
                    entry = zipResult.Archive.Entries.First(e =>
                        string.Equals(e.Key?.Replace('\\', '/').Trim('/'), entryItem.Path.Trim('/'), StringComparison.OrdinalIgnoreCase));
                }

                long totalBytes = entry.Size > 0 ? entry.Size : entry.CompressedSize;
                long totalRead = 0;
                var stopwatch = Stopwatch.StartNew();
                long lastReportBytes = 0;
                var lastReportTime = stopwatch.Elapsed;

                using (var sourceStream = entry.OpenEntryStream())
                using (var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
                {
                    byte[] buffer = new byte[64 * 1024];
                    int bytesRead;

                    while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                        totalRead += bytesRead;

                        var now = stopwatch.Elapsed;
                        var elapsedSec = (now - lastReportTime).TotalSeconds;
                        if (elapsedSec >= 0.25 || totalRead >= totalBytes)
                        {
                            double speed = (totalRead - lastReportBytes) / Math.Max(0.001, elapsedSec);
                            progress.Report((totalRead, totalBytes, speed));
                            lastReportBytes = totalRead;
                            lastReportTime = now;
                        }
                    }

                    await fileStream.FlushAsync(ct).ConfigureAwait(false);
                }

                progress.Report((totalBytes, totalBytes, 0));
            }
            finally
            {
                zipResult.StreamLock.Release();
            }
        }
    }
}
