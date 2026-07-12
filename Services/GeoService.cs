using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IpWidget.Services;

public sealed class GeoInfo
{
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public string? City { get; set; }
    public string? Isp { get; set; }
    public string? Org { get; set; }
    public string? Domain { get; set; }
    public int Asn { get; set; }

    public bool IsHosting { get; set; }
    public string? HostingReason { get; set; }

    public string Location =>
        string.Join(", ", new[] { Country, City }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string Provider
    {
        get
        {
            var name = !string.IsNullOrWhiteSpace(Isp) ? Isp : Org;
            if (string.IsNullOrWhiteSpace(name)) return "неизвестный провайдер";
            return Asn > 0 ? $"{name} · AS{Asn}" : name!;
        }
    }
}

/// <summary>Resolves geo / ASN info for an IP and flags likely VPN/hosting ranges.</summary>
public sealed class GeoService : IDisposable
{
    private readonly HttpClient _http;

    // datacenter / VPN / hosting fingerprints seen in ISP/org/domain fields
    private static readonly string[] HostingMarkers =
    {
        "hosting", "host", "data center", "datacenter", "colocation", "colo",
        "cloud", "server", "vps", "vpn", "proxy", "dedicated", "cdn",
        "ovh", "hetzner", "digitalocean", "digital ocean", "linode", "vultr",
        "amazon", "aws", "google", "microsoft", "azure", "oracle", "alibaba",
        "m247", "leaseweb", "contabo", "choopa", "g-core", "gcore", "quadranet",
        "datacamp", "nforex", "packet", "scaleway", "upcloud", "kamatera",
        "hostwinds", "ionos", "namecheap", "godaddy", "psychz", "zenlayer",
    };

    public GeoService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _http.DefaultRequestHeaders.Add("User-Agent", "IpWidget/1.0");
    }

    public async Task<GeoInfo?> LookupAsync(string ip, CancellationToken ct = default)
    {
        try
        {
            var body = await _http.GetStringAsync($"https://ipwho.is/{ip}", ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var ok) &&
                ok.ValueKind == JsonValueKind.False)
                return null;

            var info = new GeoInfo
            {
                Country = Str(root, "country"),
                CountryCode = Str(root, "country_code"),
                City = Str(root, "city"),
            };

            if (root.TryGetProperty("connection", out var c) &&
                c.ValueKind == JsonValueKind.Object)
            {
                info.Isp = Str(c, "isp");
                info.Org = Str(c, "org");
                info.Domain = Str(c, "domain");
                if (c.TryGetProperty("asn", out var a) && a.ValueKind == JsonValueKind.Number)
                    info.Asn = a.GetInt32();
            }

            DetectHosting(info);
            return info;
        }
        catch
        {
            return null;
        }
    }

    private static void DetectHosting(GeoInfo info)
    {
        var haystack = $"{info.Isp} {info.Org} {info.Domain}".ToLowerInvariant();
        foreach (var marker in HostingMarkers)
        {
            if (haystack.Contains(marker))
            {
                info.IsHosting = true;
                info.HostingReason = marker;
                return;
            }
        }
        info.IsHosting = false;
    }

    private static string? Str(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    public void Dispose() => _http.Dispose();
}
