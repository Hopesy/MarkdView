using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MarkdView.Media;

public sealed class HttpMarkdownImageLoader : IMarkdownImageLoader
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();
    private readonly HttpClient _httpClient;

    public HttpMarkdownImageLoader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? SharedHttpClient;
    }

    public async Task<BitmapSource> LoadAsync(Uri uri, MarkdownImageLoadOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(options);

        using var timeout = new CancellationTokenSource(options.Timeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        var token = linkedCancellation.Token;

        await MarkdownImageSecurity.EnsurePublicEndpointAsync(uri, token);
        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long contentLength && contentLength > options.MaxBytes)
        {
            throw new InvalidDataException("图片超过大小限制");
        }

        await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(chunk.AsMemory(), token).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > options.MaxBytes)
            {
                throw new InvalidDataException("图片超过大小限制");
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), token).ConfigureAwait(false);
        }

        buffer.Position = 0;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = buffer;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = options.MaxDecodePixel;
        bitmap.DecodePixelHeight = options.MaxDecodePixel;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }
}
