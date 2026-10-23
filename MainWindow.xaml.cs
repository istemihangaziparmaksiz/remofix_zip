using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using RemofixZip.Dialogs;
using RemofixZip.Models;
using RemofixZip.Services;

namespace RemofixZip
{
    public partial class MainWindow : Window
    {
        private readonly RemoteZipService _zipService = new();
        private RemoteZipResult? _currentZipResult;
        private CancellationTokenSource? _analyzeCts;
        private bool _isBrowserInitialized;

        private string? _lastPromptedUrl;
        private DateTime _lastPromptedTime = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();

            listDownloads.ItemsSource = DownloadManager.Instance.Tasks;
            DownloadManager.Instance.Tasks.CollectionChanged += Downloads_CollectionChanged;

            txtDownloadDir.Text = AppSettings.Current.BaseDownloadDirectory;

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await InitWebView2Async();
            }
            catch { }
        }

        private async Task InitWebView2Async()
        {
            if (_isBrowserInitialized) return;
            try
            {
                await wvBrowser.EnsureCoreWebView2Async();
                _isBrowserInitialized = true;

                // 1. Hook download starting event
                wvBrowser.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;

                // 2. Hook new window requests (target="_blank", window.open)
                wvBrowser.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

                // 3. Track URL changes
                wvBrowser.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;

                wvBrowser.CoreWebView2.Navigate("https://www.google.com");
                txtBrowserUrl.Text = "https://www.google.com";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebView2 initialization failed: {ex.Message}");
            }
        }

        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true; // Prevent launching in external system browser

            string targetUri = e.Uri;
            if (string.IsNullOrWhiteSpace(targetUri)) return;

            if (IsDirectZipUrl(targetUri))
            {
                string name = GetFileNameFromUrl(targetUri) ?? "archive.zip";
                PromptAndInspectZip(targetUri, name);
            }
            else
            {
                // Open new tab requests inside the current browser view
                wvBrowser.CoreWebView2.Navigate(targetUri);
                txtBrowserUrl.Text = targetUri;
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            txtBrowserUrl.Text = e.Uri;
        }

        private void CoreWebView2_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
        {
            string uri = e.DownloadOperation.Uri;
            string suggestedName = Path.GetFileName(e.ResultFilePath);
            string mime = e.DownloadOperation.MimeType ?? "";

            if (IsZipDownload(uri, suggestedName, mime))
            {
                // Cancel browser standard download
                e.Cancel = true;

                string fileName = !string.IsNullOrWhiteSpace(suggestedName)
                    ? suggestedName
                    : (GetFileNameFromUrl(uri) ?? "archive.zip");

                PromptAndInspectZip(uri, fileName);
            }
        }

        private void PromptAndInspectZip(string uri, string suggestedFileName)
        {
            // Debounce duplicate events within 2 seconds
            if (_lastPromptedUrl == uri && (DateTime.Now - _lastPromptedTime).TotalSeconds < 2)
            {
                return;
            }
            _lastPromptedUrl = uri;
            _lastPromptedTime = DateTime.Now;

            Dispatcher.Invoke(async () =>
            {
                var dlg = new ConfirmPromptDialog(suggestedFileName, uri)
                {
                    Owner = this
                };

                if (dlg.ShowDialog() == true)
                {
                    rbNavExplorer.IsChecked = true;
                    txtUrl.Text = uri;
                    string? cookies = await GetCookiesForUrlAsync(uri);
                    _ = StartAnalysisAsync(uri, cookies);
                }
            });
        }

        private bool IsDirectZipUrl(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return false;
            try
            {
                var u = new Uri(uri);
                string localPath = u.LocalPath.ToLowerInvariant();
                return localPath.EndsWith(".zip") || localPath.EndsWith(".zip64");
            }
            catch
            {
                return false;
            }
        }

        private bool IsZipDownload(string uri, string? suggestedName, string? mimeType)
        {
            if (!string.IsNullOrWhiteSpace(suggestedName))
            {
                string lower = suggestedName.ToLowerInvariant();
                if (lower.EndsWith(".zip") || lower.EndsWith(".zip64"))
                {
                    return true;
                }
            }

            if (IsDirectZipUrl(uri))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(mimeType))
            {
                string lowerMime = mimeType.ToLowerInvariant();
                if (lowerMime == "application/zip" ||
                    lowerMime == "application/x-zip-compressed" ||
                    lowerMime == "application/x-zip")
                {
                    return true;
                }
            }

            return false;
        }

        private string? GetFileNameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                string seg = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrWhiteSpace(seg)) return seg;
            }
            catch { }
            return null;
        }

        private async Task<string?> GetCookiesForUrlAsync(string url)
        {
            try
            {
                if (wvBrowser.CoreWebView2?.CookieManager != null)
                {
                    var cookies = await wvBrowser.CoreWebView2.CookieManager.GetCookiesAsync(url);
                    if (cookies != null && cookies.Count > 0)
                    {
                        return string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
                    }
                }
            }
            catch { }
            return null;
        }

        #region TextBox Auto Select All on Focus
        private void TxtBrowserUrl_GotFocus(object sender, RoutedEventArgs e)
        {
            txtBrowserUrl.SelectAll();
        }

        private void TxtBrowserUrl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!txtBrowserUrl.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                txtBrowserUrl.Focus();
                txtBrowserUrl.SelectAll();
            }
        }

        private void TxtUrl_GotFocus(object sender, RoutedEventArgs e)
        {
            txtUrl.SelectAll();
        }

        private void TxtUrl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!txtUrl.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                txtUrl.Focus();
                txtUrl.SelectAll();
            }
        }
        #endregion

        #region Navigation
        private void NavButton_Checked(object sender, RoutedEventArgs e)
        {
            if (panelExplorer == null) return;

            panelExplorer.Visibility = rbNavExplorer.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            panelBrowser.Visibility = rbNavBrowser.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            panelDownloads.Visibility = rbNavDownloads.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            panelSettings.Visibility = rbNavSettings.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            if (rbNavBrowser.IsChecked == true && !_isBrowserInitialized)
            {
                _ = InitWebView2Async();
            }
        }
        #endregion

        #region ZIP Explorer
        private void BtnPaste_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText().Trim();
                if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    txtUrl.Text = text;
                    txtUrl.CaretIndex = txtUrl.Text.Length;
                }
            }
        }

        private void TxtUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnAnalyze_Click(sender, e);
            }
        }

        private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            string url = txtUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Lütfen geçerli bir ZIP URL adresi girin.", "Remofix Zip", MessageBoxButton.OK, MessageBoxImage.Information);
                txtUrl.Focus();
                return;
            }

            string? cookies = await GetCookiesForUrlAsync(url);
            await StartAnalysisAsync(url, cookies);
        }

        private async Task StartAnalysisAsync(string url, string? cookieHeader = null)
        {
            _analyzeCts?.Cancel();
            _analyzeCts = new CancellationTokenSource();

            btnAnalyze.IsEnabled = false;
            ringAnalyze.Visibility = Visibility.Visible;
            ringAnalyze.IsActive = true;
            icoAnalyze.Visibility = Visibility.Collapsed;
            lblStatusLog.Text = "Analiz ediliyor...";

            try
            {
                _currentZipResult?.Dispose();
                _currentZipResult = null;

                var progress = new Progress<string>(msg =>
                {
                    lblStatusLog.Text = msg;
                });

                string? cachedPassword = DownloadManager.Instance.GetSessionPassword(url);

                var result = await _zipService.AnalyzeRemoteZipAsync(url, cookieHeader, cachedPassword, progress, _analyzeCts.Token);
                _currentZipResult = result;

                // Bind UI
                panelEmptyState.Visibility = Visibility.Collapsed;
                treeArchive.Visibility = Visibility.Visible;
                cardArchiveSummary.Visibility = Visibility.Visible;

                lblArchiveName.Text = result.ArchiveFileName;
                lblArchiveSize.Text = $"Boyut: {result.FormattedTotalSize}";
                lblArchiveCount.Text = $"{result.TotalFilesCount} Dosya, {result.TotalFoldersCount} Klasör";
                badgeEncrypted.Visibility = result.IsEncrypted ? Visibility.Visible : Visibility.Collapsed;

                treeArchive.ItemsSource = new List<ZipTreeNode> { result.RootNode };

                UpdateSelectionSummary();
                lblStatusLog.Text = "Hazır.";
            }
            catch (NotSupportedException ex)
            {
                MessageBox.Show(ex.Message, "Range Desteği Yok", MessageBoxButton.OK, MessageBoxImage.Warning);
                lblStatusLog.Text = "Sunucu parçalı indirmeyi desteklemiyor.";
            }
            catch (OperationCanceledException)
            {
                lblStatusLog.Text = "İşlem iptal edildi.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ZIP analizi sırasında bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                lblStatusLog.Text = "Hata oluştu.";
            }
            finally
            {
                btnAnalyze.IsEnabled = true;
                ringAnalyze.IsActive = false;
                ringAnalyze.Visibility = Visibility.Collapsed;
                icoAnalyze.Visibility = Visibility.Visible;
            }
        }

        private void TreeNode_CheckChanged(object sender, RoutedEventArgs e)
        {
            UpdateSelectionSummary();
        }

        private void UpdateSelectionSummary()
        {
            if (_currentZipResult == null)
            {
                lblSelectedSummary.Text = "Seçilen dosya yok";
                btnDownloadSelected.IsEnabled = false;
                return;
            }

            var selectedFiles = GetSelectedLeafNodes(_currentZipResult.RootNode);
            int count = selectedFiles.Count;
            long totalBytes = selectedFiles.Sum(f => f.Item?.Size ?? 0);

            if (count > 0)
            {
                lblSelectedSummary.Text = $"Seçilen: {count} dosya ({ZipEntryItem.FormatBytes(totalBytes)})";
                btnDownloadSelected.Content = $"Seçilenleri İndir ({count})";
                btnDownloadSelected.IsEnabled = true;
            }
            else
            {
                lblSelectedSummary.Text = "Seçilen dosya yok";
                btnDownloadSelected.Content = "Seçilenleri İndir";
                btnDownloadSelected.IsEnabled = false;
            }
        }

        private List<ZipTreeNode> GetSelectedLeafNodes(ZipTreeNode node)
        {
            var list = new List<ZipTreeNode>();
            CollectSelectedLeaves(node, list);
            return list;
        }

        private void CollectSelectedLeaves(ZipTreeNode node, List<ZipTreeNode> list)
        {
            if (!node.IsDirectory && node.IsChecked == true && node.Item != null)
            {
                list.Add(node);
            }
            foreach (var child in node.Children)
            {
                CollectSelectedLeaves(child, list);
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            _currentZipResult?.RootNode.SetChecked(true, updateChildren: true, updateParent: true);
            UpdateSelectionSummary();
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            _currentZipResult?.RootNode.SetChecked(false, updateChildren: true, updateParent: true);
            UpdateSelectionSummary();
        }

        private void BtnDownloadSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_currentZipResult == null) return;

            var selectedNodes = GetSelectedLeafNodes(_currentZipResult.RootNode);
            if (selectedNodes.Count == 0) return;

            // Password check: If any selected file is encrypted and no password remembered for this link
            bool hasEncrypted = selectedNodes.Any(n => n.Item?.IsEncrypted == true) || _currentZipResult.IsEncrypted;
            string? currentPassword = DownloadManager.Instance.GetSessionPassword(_currentZipResult.Url);

            if (hasEncrypted && string.IsNullOrEmpty(currentPassword))
            {
                var pwdDlg = new PasswordPromptDialog { Owner = this };
                if (pwdDlg.ShowDialog() == true)
                {
                    currentPassword = pwdDlg.EnteredPassword;
                    DownloadManager.Instance.SetSessionPassword(_currentZipResult.Url, currentPassword);
                }
                else
                {
                    // User canceled password dialog
                    return;
                }
            }

            // Enqueue all selected files
            foreach (var node in selectedNodes)
            {
                if (node.Item != null)
                {
                    DownloadManager.Instance.EnqueueDownload(_currentZipResult, node.Item);
                }
            }

            // Switch to Downloads tab to let user see progress
            rbNavDownloads.IsChecked = true;
        }
        #endregion

        #region Browser
        private void TxtBrowserUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnBrowserGo_Click(sender, e);
            }
        }

        private void BtnBrowserGo_Click(object sender, RoutedEventArgs e)
        {
            string url = txtBrowserUrl.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            try
            {
                wvBrowser.CoreWebView2?.Navigate(url);
            }
            catch { }
        }

        private void BtnBrowserBack_Click(object sender, RoutedEventArgs e)
        {
            if (wvBrowser.CanGoBack) wvBrowser.GoBack();
        }

        private void BtnBrowserForward_Click(object sender, RoutedEventArgs e)
        {
            if (wvBrowser.CanGoForward) wvBrowser.GoForward();
        }

        private void BtnBrowserRefresh_Click(object sender, RoutedEventArgs e)
        {
            wvBrowser.Reload();
        }

        private void BtnBrowserHome_Click(object sender, RoutedEventArgs e)
        {
            wvBrowser.CoreWebView2?.Navigate("https://www.google.com");
            txtBrowserUrl.Text = "https://www.google.com";
        }
        #endregion

        #region Downloads Management
        private void Downloads_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            int count = DownloadManager.Instance.Tasks.Count;
            txtNoDownloads.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;

            int activeCount = DownloadManager.Instance.Tasks.Count(t => t.Status == DownloadStatus.Downloading || t.Status == DownloadStatus.Queued);
            if (activeCount > 0)
            {
                badgeActiveDownloads.Visibility = Visibility.Visible;
                txtBadgeCount.Text = activeCount.ToString();
            }
            else
            {
                badgeActiveDownloads.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnCancelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DownloadTaskItem task)
            {
                DownloadManager.Instance.CancelTask(task);
            }
        }

        private void BtnShowInFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path && File.Exists(path))
            {
                try
                {
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                catch { }
            }
            else
            {
                BtnOpenDownloadFolder_Click(sender, e);
            }
        }

        private void BtnOpenDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = AppSettings.Current.BaseDownloadDirectory;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                Process.Start("explorer.exe", dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Klasör açılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClearFinishedDownloads_Click(object sender, RoutedEventArgs e)
        {
            DownloadManager.Instance.ClearFinished();
        }
        #endregion

        #region Settings
        private void CbThreads_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbThreads?.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int threads))
            {
                AppSettings.Current.DownloadThreads = threads;
            }
        }
        #endregion
    }
}
