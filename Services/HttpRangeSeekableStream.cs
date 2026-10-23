using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace RemofixZip.Services
{
    public class HttpRangeSeekableStream : Stream
    {
        private readonly HttpClient _httpClient;
        private readonly string _url;
        private readonly long _length;
        private readonly string? _cookieHeader;
        private long _position;

        // Block cache to avoid tiny round-trips when reading central directory headers
        private byte[]? _cacheBuffer;
        private long _cacheStart = -1;
        private int _cacheLength = 0;
        private const int DefaultCacheBlockSize = 256 * 1024; // 256 KB cache block

        public HttpRangeSeekableStream(HttpClient httpClient, string url, long length, string? cookieHeader = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _url = url ?? throw new ArgumentNullException(nameof(url));
            _length = length;
            _cookieHeader = cookieHeader;
            _position = 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _length)
                    throw new ArgumentOutOfRangeException(nameof(value), "Pozisyon dosya sınırları dışında.");
                _position = value;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPos = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentException("Geçersiz SeekOrigin", nameof(origin))
            };

            if (newPos < 0 || newPos > _length)
                throw new IOException($"Seek pozisyonu dosya sınırları dışında: {newPos} / {_length}");

            _position = newPos;
            return _position;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_position >= _length) return 0;

            int toRead = (int)Math.Min(count, _length - _position);
            if (toRead <= 0) return 0;

            // Check if requested bytes are completely inside cache
            if (_cacheBuffer != null && _position >= _cacheStart && _position + toRead <= _cacheStart + _cacheLength)
            {
                int cacheOffset = (int)(_position - _cacheStart);
                Array.Copy(_cacheBuffer, cacheOffset, buffer, offset, toRead);
                _position += toRead;
                return toRead;
            }

            // Otherwise, fetch from remote server via HTTP Range
            long fetchStart = _position;
            long fetchEnd = Math.Min(_length - 1, fetchStart + Math.Max(toRead, DefaultCacheBlockSize) - 1);

            using var request = new HttpRequestMessage(HttpMethod.Get, _url);
            request.Headers.Range = new RangeHeaderValue(fetchStart, fetchEnd);
            if (!string.IsNullOrEmpty(_cookieHeader))
            {
                request.Headers.TryAddWithoutValidation("Cookie", _cookieHeader);
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // Server returned 200 OK instead of 206 Partial Content
                throw new NotSupportedException("Bu dosya / sunucu parçalı indirmeyi (HTTP Range) desteklemiyor.");
            }

            if (response.StatusCode != System.Net.HttpStatusCode.PartialContent)
            {
                throw new HttpRequestException($"HTTP Range isteği başarısız oldu: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            byte[] fetchedBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (fetchedBytes.Length == 0) return 0;

            // Store in cache
            _cacheBuffer = fetchedBytes;
            _cacheStart = fetchStart;
            _cacheLength = fetchedBytes.Length;

            int actualCopy = Math.Min(toRead, fetchedBytes.Length);
            Array.Copy(fetchedBytes, 0, buffer, offset, actualCopy);
            _position += actualCopy;
            return actualCopy;
        }

        public override void Flush() { }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
