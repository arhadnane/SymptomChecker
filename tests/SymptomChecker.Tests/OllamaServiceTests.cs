using System.Collections.Generic;
using SymptomCheckerApp.Services;
using Xunit;

namespace SymptomChecker.Tests
{
    public class OllamaServiceTests
    {
        [Fact]
        public void ChoosePreferredModel_PrefersKimiFamilyWhenDefaultExactNameIsUnavailable()
        {
            var models = new List<string> { "llama3", "kimi-k2:latest", "mistral" };

            var selected = OllamaService.ChoosePreferredModel(models, "kimi-k2.6");

            Assert.Equal("kimi-k2:latest", selected);
        }

        [Fact]
        public void ParseDiagnosisResponse_ParsesStructuredJsonPayload()
        {
            const string raw = """
            {
              "diagnostic_assessment": "Les symptomes restent plus compatibles avec une pneumonie que les autres candidats algorithmiques.",
              "confidence": 0.82,
              "medications": [
                {
                  "name": "Paracetamol",
                  "category": "OTC",
                  "purpose": "Fievre et douleur",
                  "dosage": "500 mg toutes les 6 a 8 heures si besoin",
                  "warning": "Eviter en cas d'atteinte hepatique severe"
                }
              ],
              "red_flags": ["Essoufflement au repos", "SpO2 basse"],
              "self_care": "Hydratation et surveillance rapprochee.",
              "disclaimer": "Information educative uniquement."
            }
            """;

            var result = OllamaService.ParseDiagnosisResponse(raw, elapsedMs: 42);

            Assert.Equal("Les symptomes restent plus compatibles avec une pneumonie que les autres candidats algorithmiques.", result.DiagnosticAssessment);
            Assert.Equal(0.82, result.ConfidenceReinforcement);
            Assert.Single(result.Medications);
            Assert.Equal("Paracetamol", result.Medications[0].Name);
            Assert.Equal(2, result.RedFlags.Count);
            Assert.Equal("Hydratation et surveillance rapprochee.", result.SelfCareAdvice);
            Assert.Equal("Information educative uniquement.", result.Disclaimer);
            Assert.Equal(42, result.ElapsedMs);
        }
    }
}