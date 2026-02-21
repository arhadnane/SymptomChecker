using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SymptomCheckerApp.Models
{
    /// <summary>Request body for the Ollama /api/chat endpoint.</summary>
    public class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "llama3";

        [JsonPropertyName("messages")]
        public List<OllamaChatMessage> Messages { get; set; } = new();

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;

        [JsonPropertyName("options")]
        public OllamaOptions? Options { get; set; }
    }

    public class OllamaChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>Base64-encoded images for multimodal models (e.g. LLaVA).</summary>
        [JsonPropertyName("images")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Images { get; set; }
    }

    public class OllamaOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.3;

        [JsonPropertyName("num_predict")]
        public int NumPredict { get; set; } = 1024;
    }

    /// <summary>Response from the Ollama /api/chat endpoint.</summary>
    public class OllamaChatResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("message")]
        public OllamaChatMessage? Message { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("total_duration")]
        public long? TotalDuration { get; set; }
    }

    /// <summary>Response from the Ollama /api/tags endpoint (list models).</summary>
    public class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelInfo>? Models { get; set; }
    }

    public class OllamaModelInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("modified_at")]
        public string? ModifiedAt { get; set; }
    }

    /// <summary>Structured AI diagnosis result parsed from Ollama response.</summary>
    public class AiDiagnosisResult
    {
        public string RawResponse { get; set; } = string.Empty;
        public string DiagnosticAssessment { get; set; } = string.Empty;
        public List<MedicationProposal> Medications { get; set; } = new();
        public List<string> RedFlags { get; set; } = new();
        public string SelfCareAdvice { get; set; } = string.Empty;
        public string Disclaimer { get; set; } = string.Empty;
        public double? ConfidenceReinforcement { get; set; }
        public long ElapsedMs { get; set; }
    }

    public class MedicationProposal
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // OTC, Prescription-only, etc.
        public string Purpose { get; set; } = string.Empty;
        public string Dosage { get; set; } = string.Empty;
        public string Warning { get; set; } = string.Empty;
    }

    /// <summary>Result of AI-powered image analysis for symptom detection.</summary>
    public class ImageAnalysisResult
    {
        public string RawResponse { get; set; } = string.Empty;
        /// <summary>Body region detected (skin, throat, eye, etc.).</summary>
        public string BodyRegion { get; set; } = string.Empty;
        /// <summary>Visual observations from the image.</summary>
        public List<string> Observations { get; set; } = new();
        /// <summary>Symptoms deduced from visual analysis.</summary>
        public List<string> DeducedSymptoms { get; set; } = new();
        /// <summary>Possible conditions suggested by the image.</summary>
        public List<string> PossibleConditions { get; set; } = new();
        /// <summary>Severity assessment (mild, moderate, severe).</summary>
        public string Severity { get; set; } = string.Empty;
        /// <summary>Recommendation to see a doctor or self-care.</summary>
        public string Recommendation { get; set; } = string.Empty;
        public string Disclaimer { get; set; } = string.Empty;
        public long ElapsedMs { get; set; }
    }

    /// <summary>Result of AI-powered blood microscope image analysis.</summary>
    public class BloodAnalysisResult
    {
        public string RawResponse { get; set; } = string.Empty;
        /// <summary>Stain or preparation type detected (e.g. Giemsa, Wright, unstained).</summary>
        public string StainType { get; set; } = string.Empty;
        /// <summary>Magnification estimated from image context.</summary>
        public string Magnification { get; set; } = string.Empty;

        /// <summary>Red blood cell (erythrocyte) findings.</summary>
        public List<string> RbcFindings { get; set; } = new();
        /// <summary>White blood cell (leukocyte) findings.</summary>
        public List<string> WbcFindings { get; set; } = new();
        /// <summary>Platelet findings.</summary>
        public List<string> PlateletFindings { get; set; } = new();
        /// <summary>Other observations (parasites, inclusions, artefacts).</summary>
        public List<string> OtherFindings { get; set; } = new();

        /// <summary>Estimated differential count if identifiable.</summary>
        public List<string> DifferentialCount { get; set; } = new();
        /// <summary>Morphological abnormalities detected.</summary>
        public List<string> Abnormalities { get; set; } = new();

        /// <summary>Possible haematological conditions.</summary>
        public List<string> PossibleConditions { get; set; } = new();
        /// <summary>Deduced symptoms that may relate to the findings.</summary>
        public List<string> DeducedSymptoms { get; set; } = new();

        /// <summary>Severity (normal / mild / moderate / severe).</summary>
        public string Severity { get; set; } = string.Empty;
        /// <summary>Clinical recommendation.</summary>
        public string Recommendation { get; set; } = string.Empty;
        public string Disclaimer { get; set; } = string.Empty;
        public long ElapsedMs { get; set; }
    }
}
