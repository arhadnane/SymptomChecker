namespace SymptomCheckerApp.Models
{
    public class ConditionMatch
    {
        public string Name { get; set; } = string.Empty;
        public double Score { get; set; }
        public int MatchCount { get; set; }
    }
}
