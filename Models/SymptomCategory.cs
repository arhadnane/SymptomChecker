using System.Collections.Generic;

namespace SymptomCheckerApp.Models
{
    public class SymptomCategory
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Keywords { get; set; } = new List<string>();
        // Optional explicit list; if empty, Keywords drive matching
        public List<string> Symptoms { get; set; } = new List<string>();
    }

    public class SymptomCategoryDatabase
    {
        public List<SymptomCategory> Categories { get; set; } = new List<SymptomCategory>();
    }
}
