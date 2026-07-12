using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IpWidget.Services;

public enum SourceState { Pending, Ok, Failed }

public sealed class IpSourceResult
{
    public required string Name { get; init; }
    public string? Ip { get; set; }
    public long ElapsedMs { get; set; }
    public SourceState State { get; set; } = SourceState.Pending;
    public string? Error { get; set; }
}

/// <summary>
/// Queries several public "what is my IP" endpoints in parallel and reports
/// each result independently so the UI can stream them in.
/// </summary>
public sealed class IpService : IDisposable
{
    private readonly HttpClient _http;

    public IpService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8),
        };
        _http.DefaultRequestHeaders.Add("User-Agent", "IpWidget/1.0");
    }

    // name -> (url, extractor). Extractor pulls the ip out of the raw body.
    private static readonly (string Name, string Url, Func<string, string?> Extract)[] Sources =
    {
        ("ipify",       "https://api.ipify.org?format=json", b => Json(b, "ip")),
        ("ifconfig.me", "https://ifconfig.me/ip",            b => b.Trim()),
        ("icanhazip",   "https://icanhazip.com",             b => b.Trim()),
        ("ipinfo.io",   "https://ipinfo.io/json",            b => Json(b, "ip")),
        ("seeip",       "https://api.seeip.org/jsonip",      b => Json(b, "ip")),
        ("aws",         "https://checkip.amazonaws.com",     b => b.Trim()),
    };

    public IReadOnlyList<IpSourceResult> CreatePending()
    {
        var list = new List<IpSourceResult>(Sources.Length);
        foreach (var s in Sources)
            list.Add(new IpSourceResult { Name = s.Name });
        return list;
    }

    /// <summary>
    /// Fires all sources concurrently, invoking <paramref name="onResult"/> on the
    /// UI thread as each one settles. Returns the consensus IP (most common value).
    /// </summary>
    public async Task<string?> CheckAllAsync(
        IReadOnlyList<IpSourceResult> results,
        Action<IpSourceResult> onResult,
        CancellationToken ct = default)
    {
        var tasks = new List<Task>(Sources.Length);
        for (int i = 0; i < Sources.Length; i++)
        {
            var src = Sources[i];
            var slot = results[i];
            tasks.Add(QueryOne(src, slot, onResult, ct));
        }

        await Task.WhenAll(tasks);

        // consensus = most frequently reported ip among successful sources
        var tally = new Dictionary<string, int>();
        foreach (var r in results)
            if (r.State == SourceState.Ok && r.Ip is { Length: > 0 })
                tally[r.Ip] = tally.GetValueOrDefault(r.Ip) + 1;

        string? best = null;
        int bestCount = 0;
        foreach (var kv in tally)
            if (kv.Value > bestCount) { best = kv.Key; bestCount = kv.Value; }

        return best;
    }

    private async Task QueryOne(
        (string Name, string Url, Func<string, string?> Extract) src,
        IpSourceResult slot,
        Action<IpSourceResult> onResult,
        CancellationToken ct)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var body = await _http.GetStringAsync(src.Url, ct);
            var ip = src.Extract(body);
            slot.ElapsedMs = (long)System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            if (!string.IsNullOrWhiteSpace(ip) && System.Net.IPAddress.TryParse(ip, out _))
            {
                slot.Ip = ip;
                slot.State = SourceState.Ok;
            }
            else
            {
                slot.State = SourceState.Failed;
                slot.Error = "bad response";
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            slot.State = SourceState.Failed;
            slot.Error = "cancelled";
        }
        catch (Exception ex)
        {
            slot.ElapsedMs = (long)System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            slot.State = SourceState.Failed;
            slot.Error = ex is TaskCanceledException ? "timeout" : Short(ex.Message);
        }

        onResult(slot);
    }

    private static string? Json(string body, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty(prop, out var el) ? el.GetString() : null;
        }
        catch { return null; }
    }

    private static string Short(string s) => s.Length > 40 ? s[..40] + "…" : s;

    public void Dispose() => _http.Dispose();
}
