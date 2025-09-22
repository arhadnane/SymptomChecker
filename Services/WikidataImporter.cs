using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.Services
{
    public class WikidataImporter
    {
        private static readonly Uri Endpoint = new Uri("https://query.wikidata.org/sparql");

        private readonly HttpClient _http;

        public WikidataImporter(HttpClient? httpClient = null)
        {
            _http = httpClient ?? new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("SymptomChecker/1.0 (+https://example.local)");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/sparql-results+json");
        }

        public async Task<List<Condition>> FetchConditionsAsync(int limit = 200, CancellationToken ct = default)
        {
            // SPARQL: diseases (Q12136) with symptoms (P780); English labels
                        var query = $@"SELECT ?disease ?diseaseLabel ?symptomLabel WHERE {{
    ?disease wdt:P31/wdt:P279* wd:Q12136 .
    ?disease wdt:P780 ?symptom .
    SERVICE wikibase:label {{ bd:serviceParam wikibase:language ""en"". }}
}} LIMIT {limit}";

            var url = new Uri(Endpoint, $"?query={Uri.EscapeDataString(query)}");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var data = await JsonSerializer.DeserializeAsync<SparqlResult>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }, ct).ConfigureAwait(false);

            var comparer = StringComparer.OrdinalIgnoreCase;
            var map = new Dictionary<string, HashSet<string>>(comparer);

            if (data?.Results?.Bindings != null)
            {
                foreach (var b in data.Results.Bindings)
                {
                    var disease = b.DiseaseLabel?.Value?.Trim();
                    var symptom = b.SymptomLabel?.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(disease) || string.IsNullOrWhiteSpace(symptom)) continue;
                    if (!map.TryGetValue(disease, out var set))
                    {
                        set = new HashSet<string>(comparer);
                        map[disease] = set;
                    }
                    set.Add(ToTitleCase(symptom));
                }
            }

            var list = new List<Condition>();
            foreach (var kvp in map)
            {
                list.Add(new Condition
                {
                    Name = kvp.Key,
                    Symptoms = new List<string>(kvp.Value)
                });
            }
            return list;
        }

        private static string ToTitleCase(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p.Length == 1) parts[i] = p.ToUpperInvariant();
                else parts[i] = char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant();
            }
            return string.Join(' ', parts);
        }

        // DTOs for SPARQL JSON
        private class SparqlResult
        {
            public ResultContainer? Results { get; set; }
        }

        private class ResultContainer
        {
            public List<Binding>? Bindings { get; set; }
        }

        private class Binding
        {
            public ValueNode? DiseaseLabel { get; set; }
            public ValueNode? SymptomLabel { get; set; }
        }

        private class ValueNode
        {
            public string? Type { get; set; }
            public string? Value { get; set; }
        }
    }
}
