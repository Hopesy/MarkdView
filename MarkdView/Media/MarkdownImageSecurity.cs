using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MarkdView.Media;

internal static class MarkdownImageSecurity
{
    internal static bool TryCreateSafeImageUri(string? url, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(url) || url.Length > 4096
            || !Uri.TryCreate(url, UriKind.Absolute, out var candidate)
            || string.IsNullOrWhiteSpace(candidate.Host)
            || !string.IsNullOrEmpty(candidate.UserInfo)
            || (candidate.Scheme != Uri.UriSchemeHttp && candidate.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        if (candidate.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || candidate.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || candidate.Host.Equals("::1", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(candidate.Host, out var address) && IsPrivateAddress(address))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    internal static async Task EnsurePublicEndpointAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(uri.DnsSafeHost, out var literalAddress))
        {
            if (IsPrivateAddress(literalAddress))
            {
                throw new InvalidOperationException("图片地址指向本地或私有网络");
            }

            return;
        }

        IPAddress[] resolvedAddresses;
        try
        {
            resolvedAddresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 取消必须保留原始异常类型，调用方才能区分卸载/替换和真实加载失败。
            throw;
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException("无法解析图片地址", ex);
        }

        if (resolvedAddresses.Length == 0 || resolvedAddresses.Any(IsPrivateAddress))
        {
            throw new InvalidOperationException("图片地址解析到本地或私有网络");
        }
    }

    internal static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return IsPrivateAddress(address.MapToIPv4());
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 10
                || bytes[0] == 127
                || bytes[0] == 169 && bytes[1] == 254
                || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31
                || bytes[0] == 192 && bytes[1] == 168
                || bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127
                || bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0
                || bytes[0] == 198 && bytes[1] >= 18 && bytes[1] <= 19
                || bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2
                || bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100
                || bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113
                || bytes[0] == 0
                || bytes[0] >= 224;
        }

        return address.IsIPv6LinkLocal
            || address.IsIPv6SiteLocal
            || address.IsIPv6UniqueLocal
            || address.Equals(IPAddress.IPv6Loopback)
            || address.IsIPv6Multicast;
    }
}
