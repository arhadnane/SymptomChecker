using System.Collections.Generic;

namespace SymptomCheckerApp.Models
{
    public class SymptomSynonyms
    {
        public string Canonical { get; set; } = string.Empty;
        public List<string> Aliases { get; set; } = new List<string>();
    }

    public class SynonymDatabase
    {
        public List<SymptomSynonyms> Synonyms { get; set; } = new List<SymptomSynonyms>();
    }
}
