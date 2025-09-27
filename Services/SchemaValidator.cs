using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NJsonSchema;

namespace SymptomCheckerApp.Services
{
    public static class SchemaValidator
    {
        public static async Task<string?> ValidateAsync(string jsonPath, string schemaPath)
        {
            try
            {
                if (!File.Exists(jsonPath)) return $"Data file not found: {jsonPath}";
                if (!File.Exists(schemaPath)) return $"Schema file not found: {schemaPath}";
                var json = await File.ReadAllTextAsync(jsonPath);
                using var doc = JsonDocument.Parse(json);
                var schemaJson = await File.ReadAllTextAsync(schemaPath);
                var schema = await JsonSchema.FromJsonAsync(schemaJson);
                var errors = schema.Validate(doc.RootElement.ToString());
                if (errors.Count == 0) return null;
                using var sw = new StringWriter();
                sw.WriteLine($"Validation errors for {Path.GetFileName(jsonPath)}:");
                foreach (var e in errors)
                {
                    sw.WriteLine($" - {e.Path}: {e.Kind} ({e.ToString()})");
                }
                return sw.ToString();
            }
            catch (Exception ex)
            {
                return $"Exception validating {jsonPath}: {ex.Message}";
            }
        }
    }
}
