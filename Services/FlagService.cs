using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace IpWidget.Services;

/// <summary>Fetches small country-flag PNGs from flagcdn.com and caches the bitmaps.</summary>
public sealed class FlagService : IDisposable
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, Bitmap?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FlagService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        _http.DefaultRequestHeaders.Add("User-Agent", "IpWidget/1.0");
    }

    public async Task<Bitmap?> GetAsync(string? countryCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return null;
        var cc = countryCode.Trim().ToLowerInvariant();

        if (_cache.TryGetValue(cc, out var cached)) return cached;

        try
        {
            var bytes = await _http.GetByteArrayAsync($"https://flagcdn.com/w80/{cc}.png", ct);
            using var ms = new MemoryStream(bytes);
            var bmp = new Bitmap(ms);
            _cache[cc] = bmp;
            return bmp;
        }
        catch
        {
            _cache[cc] = null;
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var b in _cache.Values) b?.Dispose();
        _http.Dispose();
    }
}
