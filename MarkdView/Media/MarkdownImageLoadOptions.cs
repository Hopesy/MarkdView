using System;

namespace MarkdView.Media;

public sealed class MarkdownImageLoadOptions
{
    public static readonly TimeSpan MaxTimeout = TimeSpan.FromMinutes(10);
    public const long MaxAllowedBytes = 256L * 1024 * 1024;
    public const int DefaultMaxDecodePixel = 1600;

    private int _maxDecodePixel = DefaultMaxDecodePixel;

    public MarkdownImageLoadOptions(TimeSpan timeout, long maxBytes)
    {
        Timeout = timeout > TimeSpan.Zero && timeout <= MaxTimeout
            ? timeout
            : throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "图片加载超时必须大于 0 且不超过 10 分钟。");
        MaxBytes = maxBytes > 0 && maxBytes <= MaxAllowedBytes
            ? maxBytes
            : throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, "图片大小限制必须在 1 到 256 MB 之间。");
    }

    public TimeSpan Timeout { get; }

    public long MaxBytes { get; }

    public int MaxDecodePixel
    {
        get => _maxDecodePixel;
        init
        {
            if (value <= 0 || value > 8192)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxDecodePixel), value, "图片解码尺寸必须在 1 到 8192 像素之间。");
            }

            _maxDecodePixel = value;
        }
    }
}
