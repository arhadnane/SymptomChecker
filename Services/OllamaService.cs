using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.Services
{
    /// <summary>
    /// Service layer for communicating with a local Ollama instance.
    /// Provides AI-powered diagnostic reinforcement and medication proposals.
    /// </summary>
    public class OllamaService : IDisposable
    {
        public const string DefaultBaseUrl = "http://localhost:11434";
        public const string DefaultModelName = "gemma4";
        private const int DiagnosisMaxTokens = 2600;
        private const int MedicationMaxTokens = 2200;

        private readonly HttpClient _http;
        private string _baseUrl;
        private string _model;
        private bool _disposed;

        /// <summary>Whether the last connectivity check succeeded.</summary>
        public bool IsAvailable { get; private set; }

        /// <summary>Currently selected model name.</summary>
        public string ModelName => _model;

        /// <summary>Base URL of the Ollama server.</summary>
        public string BaseUrl => _baseUrl;

        public OllamaService(string baseUrl = DefaultBaseUrl, string model = DefaultModelName)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _model = model;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        }

        public void SetModel(string model) => _model = model;
        public void SetBaseUrl(string url) => _baseUrl = url.TrimEnd('/');

        /// <summary>Ping the Ollama server to check availability.</summary>
        public async Task<bool> CheckAvailabilityAsync(CancellationToken ct = default)
        {
            try
            {
                var resp = await _http.GetAsync($"{_baseUrl}/api/tags", ct);
                IsAvailable = resp.IsSuccessStatusCode;
                return IsAvailable;
            }
            catch
            {
                IsAvailable = false;
                return false;
            }
        }

        /// <summary>List locally available models.</summary>
        public async Task<List<string>> ListModelsAsync(CancellationToken ct = default)
        {
            try
            {
                var resp = await _http.GetAsync($"{_baseUrl}/api/tags", ct);
                if (!resp.IsSuccessStatusCode) return new List<string>();
                var json = await resp.Content.ReadAsStringAsync(ct);
                var tags = JsonSerializer.Deserialize<OllamaTagsResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return tags?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Send a chat completion request to Ollama and return the raw assistant message.
        /// </summary>
        public async Task<string?> ChatAsync(List<OllamaChatMessage> messages, double temperature = 0.3,
            int maxTokens = 1500, CancellationToken ct = default)
        {
            var request = new OllamaChatRequest
            {
                Model = _model,
                Messages = messages,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = temperature,
                    NumPredict = maxTokens
                }
            };

            var payload = JsonSerializer.Serialize(request);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync($"{_baseUrl}/api/chat", content, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var respJson = await resp.Content.ReadAsStringAsync(ct);
            var chatResp = JsonSerializer.Deserialize<OllamaChatResponse>(respJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return chatResp?.Message?.Content;
        }

        internal static string? ChoosePreferredModel(IReadOnlyList<string> availableModels, string? preferredModel = null)
        {
            if (availableModels.Count == 0)
            {
                return null;
            }

            var requested = string.IsNullOrWhiteSpace(preferredModel)
                ? DefaultModelName
                : preferredModel.Trim();

            var exactMatch = availableModels.FirstOrDefault(model =>
                string.Equals(model, requested, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(exactMatch))
            {
                return exactMatch;
            }

            // If the requested value is a family name (e.g. "gemma4"), allow
            // selecting tagged variants like "gemma4:latest".
            var normalizedRequested = NormalizeModelName(requested);
            var familyMatch = availableModels.FirstOrDefault(model =>
                NormalizeModelName(model).StartsWith(normalizedRequested, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(familyMatch))
            {
                return familyMatch;
            }

            // Requested default not available: choose the first existing model.
            return availableModels[0];
        }

        private static string NormalizeModelName(string value)
        {
            var chars = value.Where(char.IsLetterOrDigit).ToArray();
            return new string(chars).ToLowerInvariant();
        }

        /// <summary>
        /// Run AI-powered diagnostic reinforcement given the selected symptoms and
        /// the algorithmic matches. Returns a structured result with assessment,
        /// medication proposals, and red flags.
        /// </summary>
        public async Task<AiDiagnosisResult> GetDiagnosisReinforcementAsync(
            IReadOnlyList<string> selectedSymptoms,
            IReadOnlyList<ConditionMatch> algorithmicMatches,
            string language = "en",
            double? patientAge = null,
            double? tempC = null,
            int? heartRate = null,
            int? respRate = null,
            int? spO2 = null,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            var vitalsInfo = new StringBuilder();
            if (patientAge.HasValue) vitalsInfo.Append($"Age: {patientAge.Value} years. ");
            if (tempC.HasValue) vitalsInfo.Append($"Temperature: {tempC.Value:F1}°C. ");
            if (heartRate.HasValue) vitalsInfo.Append($"Heart rate: {heartRate.Value} bpm. ");
            if (respRate.HasValue) vitalsInfo.Append($"Respiratory rate: {respRate.Value}/min. ");
            if (spO2.HasValue) vitalsInfo.Append($"SpO2: {spO2.Value}%. ");

            string langInstruction = language.ToLowerInvariant() switch
            {
                                "fr" => "Write all string values in French.",
                                "ar" => "Write all string values in Arabic.",
                                _ => "Write all string values in English."
            };

                        var groundedInput = JsonSerializer.Serialize(new
                        {
                                selected_symptoms = selectedSymptoms,
                                vitals = new
                                {
                                        age_years = patientAge,
                                        temperature_c = tempC,
                                        heart_rate_bpm = heartRate,
                                        respiratory_rate_min = respRate,
                                        spo2_percent = spO2,
                                        summary = vitalsInfo.ToString().Trim()
                                },
                                algorithmic_candidates = algorithmicMatches
                                        .Take(5)
                                        .Select(match => new
                                        {
                                                condition = match.Name,
                                                score = Math.Round(match.Score, 3),
                                                matched_symptoms = match.MatchedSymptoms
                                        })
                                        .ToList()
                        });

                                                string systemPrompt = $$"""
You are a medical education assistant integrated into a symptom checker application.
Your role is EDUCATIONAL only and must remain strictly grounded in the provided candidate conditions.
{{langInstruction}}

OUTPUT RULES:
1. Return exactly one valid JSON object.
2. Do not use markdown.
3. Do not wrap the JSON in code fences.
4. Do not mention any diagnosis that is not present in algorithmic_candidates.
5. If the evidence is weak or mixed, say so explicitly in diagnostic_assessment.
6. Medication suggestions must be conservative, educational, and clearly labeled OTC or Prescription.
7. red_flags must be based only on the reported symptoms, vitals, and candidate conditions.

Use exactly this JSON shape:
{
    "diagnostic_assessment": "string",
    "confidence": 0.0,
    "medications": [
        {
            "name": "string",
            "category": "OTC or Prescription",
            "purpose": "string",
            "dosage": "string",
            "warning": "string"
        }
    ],
    "red_flags": ["string"],
    "self_care": "string",
    "disclaimer": "string"
}
""";

                        string userPrompt = $@"Use only the following input JSON as evidence for your answer:
{groundedInput}";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            try
            {
                var rawResponse = await ChatAsync(messages, 0.2, DiagnosisMaxTokens, ct);
                sw.Stop();

                if (string.IsNullOrEmpty(rawResponse))
                {
                    return new AiDiagnosisResult
                    {
                        RawResponse = "",
                        DiagnosticAssessment = "Ollama did not return a response.",
                        ElapsedMs = sw.ElapsedMilliseconds
                    };
                }

                return ParseDiagnosisResponse(rawResponse, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                sw.Stop();
                throw;
            }
            catch (TaskCanceledException ex)
            {
                sw.Stop();
                return new AiDiagnosisResult
                {
                    RawResponse = ex.Message,
                    DiagnosticAssessment = "Ollama request timed out before the model returned an answer. Try again or choose a smaller/faster model.",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new AiDiagnosisResult
                {
                    RawResponse = ex.Message,
                    DiagnosticAssessment = $"Error communicating with Ollama: {ex.Message}",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
        }

        /// <summary>
        /// Ask Ollama for targeted medication recommendations for a specific condition.
        /// </summary>
        public async Task<AiDiagnosisResult> GetMedicationAdviceAsync(
            string conditionName,
            IReadOnlyList<string> matchedSymptoms,
            string language = "en",
            double? patientAge = null,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            string langInstruction = language.ToLowerInvariant() switch
            {
                                "fr" => "Write all string values in French.",
                                "ar" => "Write all string values in Arabic.",
                                _ => "Write all string values in English."
            };

                        var groundedInput = JsonSerializer.Serialize(new
                        {
                                condition = conditionName,
                                matched_symptoms = matchedSymptoms,
                                patient_age_years = patientAge
                        });

                                                string systemPrompt = $$"""
You are a medical education assistant. {{langInstruction}}
Provide EDUCATIONAL medication guidance grounded only in the provided condition and matched symptoms.

OUTPUT RULES:
1. Return exactly one valid JSON object.
2. Do not use markdown or code fences.
3. Do not discuss any diagnosis other than the provided condition.
4. Use an empty array when no medication is appropriate.

Use exactly this JSON shape:
{
    "diagnostic_assessment": "string",
    "confidence": 0.0,
    "medications": [
        {
            "name": "string",
            "category": "OTC or Prescription",
            "purpose": "string",
            "dosage": "string",
            "warning": "string"
        }
    ],
    "red_flags": ["string"],
    "self_care": "string",
    "disclaimer": "string"
}
""";

                        string userPrompt = $"Use only the following input JSON as evidence for your answer:\n{groundedInput}";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            try
            {
                var rawResponse = await ChatAsync(messages, 0.2, MedicationMaxTokens, ct);
                sw.Stop();

                if (string.IsNullOrEmpty(rawResponse))
                {
                    return new AiDiagnosisResult
                    {
                        RawResponse = "",
                        DiagnosticAssessment = "No response received.",
                        ElapsedMs = sw.ElapsedMilliseconds
                    };
                }

                return ParseDiagnosisResponse(rawResponse, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                sw.Stop();
                throw;
            }
            catch (TaskCanceledException ex)
            {
                sw.Stop();
                return new AiDiagnosisResult
                {
                    RawResponse = ex.Message,
                    DiagnosticAssessment = "Ollama request timed out before the model returned medication guidance. Try again or choose a smaller/faster model.",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new AiDiagnosisResult
                {
                    RawResponse = ex.Message,
                    DiagnosticAssessment = $"Error: {ex.Message}",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
        }

        /// <summary>Parse the structured JSON response into an AiDiagnosisResult, with markdown fallback.</summary>
        internal static AiDiagnosisResult ParseDiagnosisResponse(string raw, long elapsedMs)
        {
            if (TryParseDiagnosisJson(raw, elapsedMs, out var jsonResult))
            {
                return jsonResult;
            }

            return ParseDiagnosisMarkdownResponse(raw, elapsedMs);
        }

        private static bool TryParseDiagnosisJson(string raw, long elapsedMs, out AiDiagnosisResult result)
        {
            result = new AiDiagnosisResult();
            if (!TryExtractJsonPayload(raw, out var jsonPayload))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(jsonPayload);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                result = new AiDiagnosisResult
                {
                    RawResponse = raw,
                    ElapsedMs = elapsedMs,
                    DiagnosticAssessment = GetString(root, "diagnostic_assessment", "assessment", "diagnosticAssessment"),
                    ConfidenceReinforcement = GetNullableDouble(root, "confidence", "confidence_reinforcement", "confidenceReinforcement"),
                    SelfCareAdvice = GetString(root, "self_care", "selfCare"),
                    Disclaimer = GetString(root, "disclaimer")
                };

                if (root.TryGetProperty("medications", out var medsElement) && medsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var medElement in medsElement.EnumerateArray())
                    {
                        if (medElement.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var medication = new MedicationProposal
                        {
                            Name = GetString(medElement, "name", "medication"),
                            Category = GetString(medElement, "category"),
                            Purpose = GetString(medElement, "purpose"),
                            Dosage = GetString(medElement, "dosage"),
                            Warning = GetString(medElement, "warning")
                        };

                        if (!string.IsNullOrWhiteSpace(medication.Name))
                        {
                            result.Medications.Add(medication);
                        }
                    }
                }

                if (root.TryGetProperty("red_flags", out var redFlagsElement) && redFlagsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var redFlag in redFlagsElement.EnumerateArray())
                    {
                        if (redFlag.ValueKind == JsonValueKind.String)
                        {
                            var value = redFlag.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                result.RedFlags.Add(value);
                            }
                        }
                    }
                }

                return HasStructuredContent(result);
            }
            catch
            {
                result = new AiDiagnosisResult();
                return false;
            }
        }

        private static bool HasStructuredContent(AiDiagnosisResult result)
        {
            return !string.IsNullOrWhiteSpace(result.DiagnosticAssessment) ||
                   result.ConfidenceReinforcement.HasValue ||
                   result.Medications.Count > 0 ||
                   result.RedFlags.Count > 0 ||
                   !string.IsNullOrWhiteSpace(result.SelfCareAdvice) ||
                   !string.IsNullOrWhiteSpace(result.Disclaimer);
        }

        private static bool TryExtractJsonPayload(string raw, out string jsonPayload)
        {
            jsonPayload = string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var fencedMatch = Regex.Match(raw, "```(?:json)?\\s*(\\{[\\s\\S]*\\})\\s*```", RegexOptions.IgnoreCase);
            if (fencedMatch.Success)
            {
                jsonPayload = fencedMatch.Groups[1].Value.Trim();
                return true;
            }

            var trimmed = raw.Trim();
            if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                jsonPayload = trimmed;
                return true;
            }

            int firstBrace = trimmed.IndexOf('{');
            int lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                jsonPayload = trimmed.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
                return true;
            }

            return false;
        }

        private static string GetString(JsonElement element, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (TryGetPropertyIgnoreCase(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static double? GetNullableDouble(JsonElement element, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (!TryGetPropertyIgnoreCase(element, propertyName, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
                {
                    return Math.Clamp(number, 0, 1);
                }

                if (value.ValueKind == JsonValueKind.String &&
                    double.TryParse(value.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                {
                    return Math.Clamp(parsed, 0, 1);
                }
            }

            return null;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static AiDiagnosisResult ParseDiagnosisMarkdownResponse(string raw, long elapsedMs)
        {
            var result = new AiDiagnosisResult
            {
                RawResponse = raw,
                ElapsedMs = elapsedMs
            };

            // Split into sections by ## headers
            var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string currentSection = "";
            var sb = new StringBuilder();

            foreach (var line in raw.Split('\n'))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("## "))
                {
                    if (!string.IsNullOrEmpty(currentSection))
                        sections[currentSection] = sb.ToString().Trim();
                    currentSection = trimmed.Substring(3).Trim().ToUpperInvariant();
                    sb.Clear();
                }
                else
                {
                    sb.AppendLine(line);
                }
            }
            if (!string.IsNullOrEmpty(currentSection))
                sections[currentSection] = sb.ToString().Trim();

            // Parse DIAGNOSTIC ASSESSMENT
            if (sections.TryGetValue("DIAGNOSTIC ASSESSMENT", out var assessment))
                result.DiagnosticAssessment = assessment;
            else if (sections.TryGetValue("DIAGNOSTIC", out var diag))
                result.DiagnosticAssessment = diag;

            // Parse CONFIDENCE
            if (sections.TryGetValue("CONFIDENCE", out var confStr))
            {
                var match = Regex.Match(confStr, @"(0?\.\d+|1\.0|1|0)");
                if (match.Success && double.TryParse(match.Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var conf))
                {
                    result.ConfidenceReinforcement = Math.Clamp(conf, 0, 1);
                }
            }

            // Parse MEDICATIONS
            if (sections.TryGetValue("MEDICATIONS", out var medsSection))
            {
                foreach (var line in medsSection.Split('\n'))
                {
                    var t = line.Trim().TrimStart('-', '*', '•').Trim();
                    if (string.IsNullOrEmpty(t)) continue;
                    if (!t.Contains("MEDICATION:", StringComparison.OrdinalIgnoreCase) &&
                        !t.Contains("|")) continue;

                    var med = new MedicationProposal();
                    var parts = t.Split('|');
                    foreach (var part in parts)
                    {
                        var p = part.Trim();
                        if (p.StartsWith("MEDICATION:", StringComparison.OrdinalIgnoreCase))
                            med.Name = p.Substring("MEDICATION:".Length).Trim();
                        else if (p.StartsWith("CATEGORY:", StringComparison.OrdinalIgnoreCase))
                            med.Category = p.Substring("CATEGORY:".Length).Trim();
                        else if (p.StartsWith("PURPOSE:", StringComparison.OrdinalIgnoreCase))
                            med.Purpose = p.Substring("PURPOSE:".Length).Trim();
                        else if (p.StartsWith("DOSAGE:", StringComparison.OrdinalIgnoreCase))
                            med.Dosage = p.Substring("DOSAGE:".Length).Trim();
                        else if (p.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase))
                            med.Warning = p.Substring("WARNING:".Length).Trim();
                    }

                    if (!string.IsNullOrEmpty(med.Name))
                        result.Medications.Add(med);
                }
            }

            // Parse RED FLAGS
            if (sections.TryGetValue("RED FLAGS", out var flagsSection))
            {
                foreach (var line in flagsSection.Split('\n'))
                {
                    var t = line.Trim().TrimStart('-', '*', '•').Trim();
                    if (!string.IsNullOrEmpty(t))
                        result.RedFlags.Add(t);
                }
            }

            // Parse SELF-CARE
            if (sections.TryGetValue("SELF-CARE", out var care))
                result.SelfCareAdvice = care;
            else if (sections.TryGetValue("SELF CARE", out var care2))
                result.SelfCareAdvice = care2;

            // Parse DISCLAIMER
            if (sections.TryGetValue("DISCLAIMER", out var disc))
                result.Disclaimer = disc;

            return result;
        }

        /// <summary>
        /// Analyze an image using a multimodal vision model (e.g. LLaVA, llava-llama3, bakllava).
        /// Returns deduced symptoms, possible conditions, and observations.
        /// </summary>
        public async Task<ImageAnalysisResult> AnalyzeImageAsync(
            string base64Image,
            string language = "en",
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            string langInstruction = language.ToLowerInvariant() switch
            {
                "fr" => "Respond entirely in French.",
                "ar" => "Respond entirely in Arabic.",
                _ => "Respond in English."
            };

            string systemPrompt = $@"You are a medical image analysis assistant integrated into a symptom checker application.
Your role is to analyze medical images and identify visible symptoms and possible conditions for EDUCATIONAL purposes only.
{langInstruction}

You can analyze images of:
- Skin conditions (rashes, lesions, discoloration, swelling, acne, eczema, psoriasis, etc.)
- Throat/mouth (redness, swelling, white patches, ulcers, etc.)
- Eyes (redness, swelling, discharge, discoloration, etc.)
- Wounds, bruises, insect bites
- Any other visible medical condition

IMPORTANT RULES:
1. This is EDUCATIONAL only — never replace professional medical advice.
2. Be descriptive about what you observe in the image.
3. List specific symptoms that can be deduced from visual observation.
4. Suggest possible conditions but always recommend professional consultation.

Respond in the following structured format (use these EXACT section headers):

## BODY REGION
[The body region shown: skin, throat, eye, mouth, nail, scalp, ear, etc.]

## OBSERVATIONS
- [Visual observation 1]
- [Visual observation 2]
- [etc.]

## DEDUCED SYMPTOMS
- [symptom 1]
- [symptom 2]
- [etc.]

## POSSIBLE CONDITIONS
- [condition 1]
- [condition 2]
- [etc.]

## SEVERITY
[mild / moderate / severe]

## RECOMMENDATION
[Brief recommendation — e.g. self-care, see a dermatologist, urgent care, etc.]

## DISCLAIMER
[Educational disclaimer]";

            string userPrompt = "Please analyze this medical image. Identify the body region, describe what you observe, list the symptoms visible in the image, and suggest possible conditions. Keep symptom names simple and lowercase (e.g. 'skin rash', 'redness', 'swelling', 'itching', 'pain').";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt, Images = new List<string> { base64Image } }
            };

            try
            {
                var rawResponse = await ChatAsync(messages, 0.3, 2000, ct);
                sw.Stop();

                if (string.IsNullOrEmpty(rawResponse))
                {
                    return new ImageAnalysisResult
                    {
                        RawResponse = "",
                        Recommendation = "The vision model did not return a response. Make sure you are using a multimodal model (e.g. llava, bakllava, llava-llama3).",
                        ElapsedMs = sw.ElapsedMilliseconds
                    };
                }

                return ParseImageAnalysisResponse(rawResponse, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                sw.Stop();
                throw;
            }
            catch (TaskCanceledException ex)
            {
                sw.Stop();
                return new ImageAnalysisResult
                {
                    RawResponse = ex.Message,
                    Recommendation = "Ollama request timed out before the vision model returned an answer. Try again or choose a smaller/faster multimodal model.",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new ImageAnalysisResult
                {
                    RawResponse = ex.Message,
                    Recommendation = $"Error communicating with Ollama: {ex.Message}",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
        }

        /// <summary>Parse the structured image analysis response.</summary>
        private static ImageAnalysisResult ParseImageAnalysisResponse(string raw, long elapsedMs)
        {
            var result = new ImageAnalysisResult
            {
                RawResponse = raw,
                ElapsedMs = elapsedMs
            };

            var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string currentSection = "";
            var sb = new StringBuilder();

            foreach (var line in raw.Split('\n'))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("## "))
                {
                    if (!string.IsNullOrEmpty(currentSection))
                        sections[currentSection] = sb.ToString().Trim();
                    currentSection = trimmed.Substring(3).Trim().ToUpperInvariant();
                    sb.Clear();
                }
                else
                {
                    sb.AppendLine(line);
                }
            }
            if (!string.IsNullOrEmpty(currentSection))
                sections[currentSection] = sb.ToString().Trim();

            if (sections.TryGetValue("BODY REGION", out var region))
                result.BodyRegion = region.Trim();

            if (sections.TryGetValue("OBSERVATIONS", out var obs))
            {
                foreach (var line in obs.Split('\n'))
                {
                    var t = line.Trim().TrimStart('-', '*', '•').Trim();
                    if (!string.IsNullOrEmpty(t))
                        result.Observations.Add(t);
                }
            }

            if (sections.TryGetValue("DEDUCED SYMPTOMS", out var symp))
            {
                foreach (var line in symp.Split('\n'))
                {
                    var t = line.Trim().TrimStart('-', '*', '•').Trim();
                    if (!string.IsNullOrEmpty(t))
                        result.DeducedSymptoms.Add(t);
                }
            }

            if (sections.TryGetValue("POSSIBLE CONDITIONS", out var conds))
            {
                foreach (var line in conds.Split('\n'))
                {
                    var t = line.Trim().TrimStart('-', '*', '•').Trim();
                    if (!string.IsNullOrEmpty(t))
                        result.PossibleConditions.Add(t);
                }
            }

            if (sections.TryGetValue("SEVERITY", out var sev))
                result.Severity = sev.Trim();

            if (sections.TryGetValue("RECOMMENDATION", out var rec))
                result.Recommendation = rec.Trim();

            if (sections.TryGetValue("DISCLAIMER", out var disc))
                result.Disclaimer = disc.Trim();

            return result;
        }

        // ─────────────────────────────────────────────────────────────
        //  Blood Microscope Image Analysis
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Analyze a blood smear / blood under microscope image using a multimodal vision model.
        /// Returns cell morphology, differential count, abnormalities, and possible haematological conditions.
        /// </summary>
        public async Task<BloodAnalysisResult> AnalyzeBloodMicroscopeAsync(
            string base64Image,
            string language = "en",
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            string langInstruction = language.ToLowerInvariant() switch
            {
                "fr" => "Respond entirely in French.",
                "ar" => "Respond entirely in Arabic.",
                _ => "Respond in English."
            };

            string systemPrompt = $@"You are a haematology image analysis assistant integrated into a medical symptom checker.
Your role is to analyze blood smear / blood microscope images and identify cell morphology,
abnormalities, and possible haematological conditions for EDUCATIONAL purposes only.
{langInstruction}

You are an expert at reading peripheral blood smear images. You can identify:
- Red blood cells (erythrocytes): shape, size, colour, inclusions (e.g. target cells, sickle cells,
  spherocytes, schistocytes, rouleaux, Howell-Jolly bodies, basophilic stippling, polychromasia)
- White blood cells (leukocytes): neutrophils, lymphocytes, monocytes, eosinophils, basophils,
  blast cells, atypical lymphocytes, hyper-segmented neutrophils, toxic granulations
- Platelets: count estimate (adequate / decreased / increased), giant platelets, clumping
- Parasites: malaria (Plasmodium species, ring forms, trophozoites, gametocytes),
  Babesia, trypanosomes, microfilaria
- Other: nucleated red blood cells, Auer rods, circulating tumour cells

IMPORTANT RULES:
1. This is EDUCATIONAL only — never replace professional haematological interpretation.
2. Describe what you observe with haematology terminology.
3. Give an estimated differential count if individual WBCs are recognisable.
4. List symptoms that could correlate with the blood findings.
5. Always recommend professional laboratory confirmation.

Respond in the following structured format (use these EXACT section headers):

## STAIN TYPE
[Identified stain: Giemsa, Wright, May-Grünwald-Giemsa, unstained, unknown, etc.]

## MAGNIFICATION
[Estimated magnification: 40x, 100x (oil immersion), etc., or unknown]

## RBC FINDINGS
- [finding 1]
- [finding 2]

## WBC FINDINGS
- [finding 1]
- [finding 2]

## PLATELET FINDINGS
- [finding 1]

## OTHER FINDINGS
- [parasites, inclusions, artefacts, or 'None observed']

## DIFFERENTIAL COUNT
- [cell type]: [estimated percentage or count]

## ABNORMALITIES
- [abnormality 1]
- [abnormality 2]

## POSSIBLE CONDITIONS
- [condition 1]
- [condition 2]

## DEDUCED SYMPTOMS
- [symptom 1 — e.g. fatigue, pallor, bruising, fever]
- [symptom 2]

## SEVERITY
[normal / mild / moderate / severe]

## RECOMMENDATION
[Brief clinical recommendation]

## DISCLAIMER
[Educational disclaimer]";

            string userPrompt = "Please analyze this blood microscope image (peripheral blood smear). " +
                "Identify all visible cell types, describe their morphology, note any abnormalities, " +
                "estimate differential counts if possible, and suggest possible haematological conditions. " +
                "Keep symptom names simple and lowercase (e.g. 'fatigue', 'pallor', 'bruising', 'fever').";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt, Images = new List<string> { base64Image } }
            };

            try
            {
                var rawResponse = await ChatAsync(messages, 0.3, 2500, ct);
                sw.Stop();

                if (string.IsNullOrEmpty(rawResponse))
                {
                    return new BloodAnalysisResult
                    {
                        RawResponse = "",
                        Recommendation = "The vision model did not return a response. Make sure you are using a multimodal model (e.g. llava, bakllava, llava-llama3).",
                        ElapsedMs = sw.ElapsedMilliseconds
                    };
                }

                return ParseBloodAnalysisResponse(rawResponse, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                sw.Stop();
                throw;
            }
            catch (TaskCanceledException ex)
            {
                sw.Stop();
                return new BloodAnalysisResult
                {
                    RawResponse = ex.Message,
                    Recommendation = "Ollama request timed out before the blood analysis model returned an answer. Try again or choose a smaller/faster multimodal model.",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new BloodAnalysisResult
                {
                    RawResponse = ex.Message,
                    Recommendation = $"Error communicating with Ollama: {ex.Message}",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
        }

        /// <summary>Parse the structured blood analysis response.</summary>
        private static BloodAnalysisResult ParseBloodAnalysisResponse(string raw, long elapsedMs)
        {
            var result = new BloodAnalysisResult
            {
                RawResponse = raw,
                ElapsedMs = elapsedMs
            };

            var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string currentSection = "";
            var sb2 = new StringBuilder();

            foreach (var line in raw.Split('\n'))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("## "))
                {
                    if (!string.IsNullOrEmpty(currentSection))
                        sections[currentSection] = sb2.ToString().Trim();
                    currentSection = trimmed.Substring(3).Trim().ToUpperInvariant();
                    sb2.Clear();
                }
                else
                {
                    sb2.AppendLine(line);
                }
            }
            if (!string.IsNullOrEmpty(currentSection))
                sections[currentSection] = sb2.ToString().Trim();

            List<string> ParseBullets(string text)
            {
                var items = new List<string>();
                foreach (var line in text.Split('\n'))
                {
                    var t = line.Trim().TrimStart('-', '*', '•').Trim();
                    if (!string.IsNullOrEmpty(t))
                        items.Add(t);
                }
                return items;
            }

            if (sections.TryGetValue("STAIN TYPE", out var stain))
                result.StainType = stain.Trim();
            if (sections.TryGetValue("MAGNIFICATION", out var mag))
                result.Magnification = mag.Trim();
            if (sections.TryGetValue("RBC FINDINGS", out var rbc))
                result.RbcFindings = ParseBullets(rbc);
            if (sections.TryGetValue("WBC FINDINGS", out var wbc))
                result.WbcFindings = ParseBullets(wbc);
            if (sections.TryGetValue("PLATELET FINDINGS", out var plt))
                result.PlateletFindings = ParseBullets(plt);
            if (sections.TryGetValue("OTHER FINDINGS", out var other))
                result.OtherFindings = ParseBullets(other);
            if (sections.TryGetValue("DIFFERENTIAL COUNT", out var diff))
                result.DifferentialCount = ParseBullets(diff);
            if (sections.TryGetValue("ABNORMALITIES", out var abn))
                result.Abnormalities = ParseBullets(abn);
            if (sections.TryGetValue("POSSIBLE CONDITIONS", out var cond))
                result.PossibleConditions = ParseBullets(cond);
            if (sections.TryGetValue("DEDUCED SYMPTOMS", out var sym))
                result.DeducedSymptoms = ParseBullets(sym);
            if (sections.TryGetValue("SEVERITY", out var sev2))
                result.Severity = sev2.Trim();
            if (sections.TryGetValue("RECOMMENDATION", out var rec2))
                result.Recommendation = rec2.Trim();
            if (sections.TryGetValue("DISCLAIMER", out var disc2))
                result.Disclaimer = disc2.Trim();

            return result;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _http.Dispose();
                _disposed = true;
            }
        }
    }
}
