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

        public OllamaService(string baseUrl = "http://localhost:11434", string model = "llama3")
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

            string symptomsStr = string.Join(", ", selectedSymptoms);
            string matchesStr = string.Join("\n", algorithmicMatches.Select(m =>
                $"  - {m.Name} (score: {m.Score:F3}, matched symptoms: {string.Join(", ", m.MatchedSymptoms)})"));

            var vitalsInfo = new StringBuilder();
            if (patientAge.HasValue) vitalsInfo.Append($"Age: {patientAge.Value} years. ");
            if (tempC.HasValue) vitalsInfo.Append($"Temperature: {tempC.Value:F1}°C. ");
            if (heartRate.HasValue) vitalsInfo.Append($"Heart rate: {heartRate.Value} bpm. ");
            if (respRate.HasValue) vitalsInfo.Append($"Respiratory rate: {respRate.Value}/min. ");
            if (spO2.HasValue) vitalsInfo.Append($"SpO2: {spO2.Value}%. ");

            string langInstruction = language.ToLowerInvariant() switch
            {
                "fr" => "Respond entirely in French.",
                "ar" => "Respond entirely in Arabic.",
                _ => "Respond in English."
            };

            string systemPrompt = $@"You are a medical education assistant integrated into a symptom checker application.
Your role is to provide EDUCATIONAL analysis only — never replace professional medical advice.
{langInstruction}

IMPORTANT RULES:
1. Always include a disclaimer that this is educational only.
2. For medication suggestions, clearly distinguish OTC (over-the-counter) from prescription-only.
3. Include basic dosage guidance for OTC medications when applicable.
4. Highlight red flags (symptoms requiring urgent medical attention).
5. Be factual, concise and evidence-based.

Respond in the following structured format (use these exact section headers):

## DIAGNOSTIC ASSESSMENT
[Your assessment of the algorithmic results and the symptoms]

## CONFIDENCE
[A number between 0 and 1 indicating how much you agree with the top algorithmic diagnosis]

## MEDICATIONS
For each medication, use this format (one per line):
- MEDICATION: [name] | CATEGORY: [OTC/Prescription] | PURPOSE: [why] | DOSAGE: [standard adult dosage if OTC] | WARNING: [key contraindications]

## RED FLAGS
- [List any symptoms or combinations that need urgent medical attention]

## SELF-CARE
[Brief self-care advice]

## DISCLAIMER
[Educational disclaimer]";

            string userPrompt = $@"Patient selected symptoms: {symptomsStr}

{(vitalsInfo.Length > 0 ? $"Vitals: {vitalsInfo}" : "")}

Algorithmic analysis results (from mathematical models):
{matchesStr}

Please analyze these symptoms and algorithmic results. Provide:
1. Your assessment of the diagnosis (reinforcing or questioning the algorithmic results)
2. Appropriate medication suggestions (clearly marking OTC vs prescription)
3. Any red flags
4. Self-care advice";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            try
            {
                var rawResponse = await ChatAsync(messages, 0.3, 1500, ct);
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
                "fr" => "Respond entirely in French.",
                "ar" => "Respond entirely in Arabic.",
                _ => "Respond in English."
            };

            string systemPrompt = $@"You are a medical education assistant. {langInstruction}
Provide EDUCATIONAL medication information for the given condition. NEVER replace professional medical advice.

Respond using this exact format:

## MEDICATIONS
For each medication:
- MEDICATION: [name] | CATEGORY: [OTC/Prescription] | PURPOSE: [why] | DOSAGE: [dosage if OTC] | WARNING: [key warnings]

## RED FLAGS
- [When to seek immediate medical help]

## SELF-CARE
[Self-care advice]

## DISCLAIMER
[Educational disclaimer]";

            var ageStr = patientAge.HasValue ? $" Patient age: {patientAge.Value} years." : "";
            string userPrompt = $"Condition: {conditionName}\nPresenting symptoms: {string.Join(", ", matchedSymptoms)}{ageStr}\n\nProvide educational medication information and self-care guidance.";

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            try
            {
                var rawResponse = await ChatAsync(messages, 0.3, 1200, ct);
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

        /// <summary>Parse the structured markdown response into an AiDiagnosisResult.</summary>
        private static AiDiagnosisResult ParseDiagnosisResponse(string raw, long elapsedMs)
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
