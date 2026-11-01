using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ConsoleScout.Contracts;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace ConsoleScout.Scraper.Browser;

/// <summary>Keeps the scraper off loopback and private networks. The URLs it is given come from a model
/// reading untrusted pages, so without this a page could steer it into the router or a local service.</summary>
internal sealed class NavigationGuard(IOptions<ScraperSettings> options)
{
    private readonly bool _allowPrivate = options.Value.Fetch.AllowPrivateNetworks;

    // Hosts already resolved during this process; addresses rarely move between public and private.
    private readonly ConcurrentDictionary<string, bool> _hosts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Rejects anything but an absolute public http(s) URL, with a message meant for the model.</summary>
    public async Task<Uri> ValidateAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ScraperFailure(ScraperErrorCodes.BadRequest, $"'{url}' is not an absolute http or https URL.");
        }

        if (!await IsAllowedAsync(uri.IdnHost, ct))
        {
            throw new ScraperFailure(ScraperErrorCodes.Blocked, $"{uri.Host} is a local or private address and cannot be fetched.");
        }

        return uri;
    }

    /// <summary>Checks every request the page makes, redirects and subresources included.</summary>
    public Task AttachAsync(IPage page) =>
        _allowPrivate ? Task.CompletedTask : page.RouteAsync("**/*", async route =>
        {
            if (Uri.TryCreate(route.Request.Url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && !await IsAllowedAsync(uri.IdnHost, CancellationToken.None))
            {
                await route.AbortAsync("blockedbyclient");
                return;
            }

            await route.ContinueAsync();
        });

    /// <summary>For HttpClient: validates the address actually being connected to, so neither a
    /// redirect nor a DNS answer that changes between check and connect can slip past.</summary>
    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
        var allowed = _allowPrivate ? addresses : addresses.Where(IsPublic).ToArray();
        if (allowed.Length == 0)
        {
            throw new HttpRequestException($"{context.DnsEndPoint.Host} is a local or private address and cannot be fetched.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private async ValueTask<bool> IsAllowedAsync(string host, CancellationToken ct)
    {
        if (_allowPrivate)
        {
            return true;
        }

        if (_hosts.TryGetValue(host, out var known))
        {
            return known;
        }

        bool allowed;
        try
        {
            var addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await Dns.GetHostAddressesAsync(host, ct);
            allowed = addresses.Length > 0 && addresses.All(IsPublic);
        }
        catch (SocketException)
        {
            // Unresolvable: let the request fail on its own, with the browser's or HttpClient's message.
            return true;
        }

        _hosts[host] = allowed;
        return allowed;
    }

    private static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var first = address.GetAddressBytes()[0];
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || (first & 0xFE) == 0xFC); // fc00::/7, unique local
        }

        var b = address.GetAddressBytes();
        return b[0] switch
        {
            0 or 10 or 127 => false,
            100 => b[1] is < 64 or > 127,       // 100.64.0.0/10, carrier-grade NAT
            169 => b[1] != 254,                 // link-local
            172 => b[1] is < 16 or > 31,
            192 => b[1] != 168,
            >= 224 => false,                    // multicast and reserved
            _ => true,
        };
    }
}
