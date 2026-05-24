using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.Services
{
    /// <summary>
    /// Maps a raw matching score to a Low / Moderate / High educational
    /// confidence band. Bands are deliberately hard-coded per model — this is
    /// presented as a *band*, never as clinical probability.
    ///
    /// Per spec 001-guided-diagnosis-ux (FR-007, FR-019).
    /// </summary>
    public static class ConfidenceBadgeService
    {
        // Boundaries documented in specs/001-guided-diagnosis-ux/data-model.md
        public const double JaccardModerate = 0.20;
        public const double JaccardHigh = 0.50;

        public const double CosineModerate = 0.20;
        public const double CosineHigh = 0.50;

        public const double NaiveBayesModerate = 0.15;
        public const double NaiveBayesHigh = 0.35;

        public const double EnsembleModerate = 0.20;
        public const double EnsembleHigh = 0.50;

        public static Confidence FromScore(SymptomCheckerService.DetectionModel model, double score)
        {
            switch (model)
            {
                case SymptomCheckerService.DetectionModel.NaiveBayes:
                    if (score < NaiveBayesModerate) return Confidence.Low;
                    return score < NaiveBayesHigh ? Confidence.Moderate : Confidence.High;
                case SymptomCheckerService.DetectionModel.Ensemble:
                    if (score < EnsembleModerate) return Confidence.Low;
                    return score < EnsembleHigh ? Confidence.Moderate : Confidence.High;
                case SymptomCheckerService.DetectionModel.Cosine:
                    if (score < CosineModerate) return Confidence.Low;
                    return score < CosineHigh ? Confidence.Moderate : Confidence.High;
                case SymptomCheckerService.DetectionModel.Jaccard:
                default:
                    if (score < JaccardModerate) return Confidence.Low;
                    return score < JaccardHigh ? Confidence.Moderate : Confidence.High;
            }
        }

        /// <summary>
        /// Translation key for the band label. UI calls TranslationService.T(key).
        /// </summary>
        public static string TranslationKey(Confidence band) => band switch
        {
            Confidence.High => "Confidence_High",
            Confidence.Moderate => "Confidence_Moderate",
            _ => "Confidence_Low"
        };
    }
}
