using System.Net.Http.Json;

namespace Ard.Core;

/// <summary>
/// DNS lookups without <c>dig</c>, using DNS-over-HTTPS (ARD DNS discovery, §6.1).
/// Resolves TXT and SRV records that ARD uses to advertise catalogs and registries.
/// Falls back across multiple public DoH resolvers for resilience.
/// </summary>
public sealed class DnsOverHttps
{
    private static readonly string[] DefaultResolvers =
    {
        "https://dns.google/resolve",
        "https://cloudflare-dns.com/dns-query",
    };

    private readonly HttpClient _http;
    private readonly string[] _resolvers;

    /// <summary>
    /// <paramref name="resolvers"/> overrides the public DoH providers — point a local mock-DoH
    /// endpoint here for an offline, self-hosted hunt. Defaults to dns.google + Cloudflare.
    /// </summary>
    public DnsOverHttps(HttpClient http, IEnumerable<string>? resolvers = null)
    {
        _http = http;
        var list = resolvers?.ToArray();
        _resolvers = list is { Length: > 0 } ? list : DefaultResolvers;
    }

    private const int TypeTxt = 16;
    private const int TypeSrv = 33;

    /// <summary>Resolve TXT records for a name; returns the unquoted values, with multi-chunk ("a" "b") TXT records concatenated.</summary>
    public async Task<IReadOnlyList<string>> ResolveTxtAsync(string name, CancellationToken ct = default)
    {
        var answers = await QueryAsync(name, TypeTxt, ct);
        return answers
            .Where(a => a.Type == TypeTxt && a.Data is not null)
            .Select(a => Unquote(a.Data!))
            .ToList();
    }

    /// <summary>Resolve SRV records for a name, parsed into <see cref="SrvRecord"/> and ordered by priority then weight.</summary>
    public async Task<IReadOnlyList<SrvRecord>> ResolveSrvAsync(string name, CancellationToken ct = default)
    {
        var answers = await QueryAsync(name, TypeSrv, ct);
        var records = new List<SrvRecord>();
        foreach (var a in answers.Where(a => a.Type == TypeSrv && a.Data is not null))
        {
            // "0 0 443 ard-01943f89c6ed4fbc.azurewebsites.net."
            var parts = a.Data!.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 4
                && int.TryParse(parts[0], out var prio)
                && int.TryParse(parts[1], out var weight)
                && int.TryParse(parts[2], out var port))
            {
                records.Add(new SrvRecord(prio, weight, port, parts[3]));
            }
        }
        return records.OrderBy(r => r.Priority).ThenByDescending(r => r.Weight).ToList();
    }

    private async Task<IReadOnlyList<DohAnswer>> QueryAsync(string name, int type, CancellationToken ct)
    {
        Exception? last = null;
        foreach (var resolver in _resolvers)
        {
            try
            {
                var url = $"{resolver}?name={Uri.EscapeDataString(name)}&type={type}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Accept.ParseAdd("application/dns-json");
                using var resp = await _http.SendAsync(req, ct);
                resp.EnsureSuccessStatusCode();
                var doh = await resp.Content.ReadFromJsonAsync<DohResponse>(Json.Default, ct);
                if (doh is null) continue;
                // Status is the DNS RCODE: 0 = NOERROR, 3 = NXDOMAIN (authoritative "no such name").
                // Any other non-zero status (e.g. 2 = SERVFAIL) is a soft/transient failure at this
                // resolver — fail over to the next one instead of reporting an empty answer set.
                const int NoError = 0, NxDomain = 3;
                if (doh.Status != NoError && doh.Status != NxDomain) continue;
                return doh.Answer ?? new List<DohAnswer>();
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }
        throw new ArdException($"DNS-over-HTTPS lookup failed for {name} (type {type}).", last);
    }

    // TXT data may arrive wrapped in quotes, and long TXT records may be split into
    // multiple quoted chunks ("part1" "part2") that must be concatenated.
    internal static string Unquote(string data)
    {
        data = data.Trim();
        if (!data.Contains('"')) return data;
        var chunks = new List<string>();
        bool inQuote = false;
        var current = new System.Text.StringBuilder();
        foreach (var c in data)
        {
            if (c == '"') { inQuote = !inQuote; if (!inQuote) { chunks.Add(current.ToString()); current.Clear(); } }
            else if (inQuote) current.Append(c);
        }
        if (current.Length > 0) chunks.Add(current.ToString()); // flush text after an unterminated quote
        return chunks.Count > 0 ? string.Concat(chunks) : data.Trim('"');
    }
}
