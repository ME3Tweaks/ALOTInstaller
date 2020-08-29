using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Use HttpClient instead of WebClient, microsoft says! Oh yeah, we also removeda ll of hte useful things from webclient. Oh and we also
    /// made webclient CancelAsync() not actually cancel it. Cause you didn't really want to cancel that right?
    /// </summary>
    public class HttpClientDownloadWithProgress : IDisposable
    {
        private readonly string _downloadUrl;
        private readonly Stream _outStream;
        private readonly string _destinationFilePath;

        private HttpClient _httpClient;
        private CancellationToken cancellationToken;

        public delegate void ProgressChangedHandler(long? totalFileSize, long totalBytesDownloaded, double? progressPercentage);

        public event ProgressChangedHandler ProgressChanged;

        public HttpClientDownloadWithProgress(string downloadUrl, string destinationFilePath, CancellationTokenSource cancellationToken = null)
        {
            _downloadUrl = downloadUrl;
            _destinationFilePath = destinationFilePath;
            this.cancellationToken = cancellationToken.Token;
        }

        public HttpClientDownloadWithProgress(string downloadUrl, Stream outputStream, CancellationTokenSource cancellationToken = null)
        {
            _downloadUrl = downloadUrl;
            _outStream = outputStream;
            this.cancellationToken = cancellationToken.Token;
        }

        public async Task StartDownload()
        {
            _httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(5) };

            using var response = await _httpClient.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            await DownloadFileFromHttpResponseMessage(response, _outStream);
        }

        private async Task DownloadFileFromHttpResponseMessage(HttpResponseMessage response, Stream outStream)
        {
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            await ProcessContentStream(totalBytes, contentStream, outStream);
        }

        private async Task ProcessContentStream(long? totalDownloadSize, Stream contentStream, Stream outStream)
        {
            var totalBytesRead = 0L;
            var readCount = 0L;
            var buffer = new byte[8192];
            var isMoreToRead = true;
            Stream fileStream = null;
            try
            {
                fileStream = outStream ?? new FileStream(_destinationFilePath, FileMode.Create, FileAccess.Write,
                    FileShare.None, 8192, true);
                do
                {
                    var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0)
                    {
                        isMoreToRead = false;
                        TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                        continue;
                    }

                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);

                    totalBytesRead += bytesRead;
                    readCount += 1;

                    if (readCount % 100 == 0)
                        TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                } while (isMoreToRead);
            }
            catch (OperationCanceledException)
            {
                Log.Warning("Download canceled");
            }
            finally
            {
                if (outStream == null)
                {
                    fileStream?.Close();
                }
            }


        }

        private void TriggerProgressChanged(long? totalDownloadSize, long totalBytesRead)
        {
            if (ProgressChanged == null)
                return;

            double? progressPercentage = null;
            if (totalDownloadSize.HasValue)
                progressPercentage = Math.Round((double)totalBytesRead / totalDownloadSize.Value * 100, 2);

            ProgressChanged(totalDownloadSize, totalBytesRead, progressPercentage);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        public static string DownloadStringAwareOfEncoding(string url)
        {
            //var clientP = new HttpClientDownloadWithProgress (url)
            return "hi";
        }

        public void Cancel()
        {
            _httpClient?.CancelPendingRequests();
        }
    }
}
