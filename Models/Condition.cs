using System.Collections.Generic;

namespace SymptomCheckerApp.Models
{
    public class Condition
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Symptoms { get; set; } = new();
        // Optional educational fields
        public List<string>? Treatments { get; set; }
        public List<string>? Treatments_Fr { get; set; }
        public List<string>? Treatments_Ar { get; set; }
        public List<string>? Medications { get; set; }
        public List<string>? Medications_Fr { get; set; }
        public List<string>? Medications_Ar { get; set; }
        public string? CareAdvice { get; set; }
        public string? CareAdvice_Fr { get; set; }
        public string? CareAdvice_Ar { get; set; }
    }

    public class ConditionDatabase
    {
        public List<Condition> Conditions { get; set; } = new();
    }
}
