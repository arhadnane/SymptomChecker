using SymptomCheckerApp.Models;
using SymptomCheckerApp.Services;
using Xunit;

namespace SymptomChecker.Tests
{
    /// <summary>
    /// Spec 001-guided-diagnosis-ux T050 — ConfidenceBadgeService boundary tests.
    /// </summary>
    public class ConfidenceBadgeServiceTests
    {
        [Theory]
        [InlineData(0.00, Confidence.Low)]
        [InlineData(0.19, Confidence.Low)]
        [InlineData(0.20, Confidence.Moderate)]
        [InlineData(0.49, Confidence.Moderate)]
        [InlineData(0.50, Confidence.High)]
        [InlineData(1.00, Confidence.High)]
        public void Jaccard_BucketsAtBoundaries(double score, Confidence expected)
        {
            Assert.Equal(expected, ConfidenceBadgeService.FromScore(SymptomCheckerService.DetectionModel.Jaccard, score));
        }

        [Theory]
        [InlineData(0.19, Confidence.Low)]
        [InlineData(0.20, Confidence.Moderate)]
        [InlineData(0.50, Confidence.High)]
        public void Cosine_BucketsAtBoundaries(double score, Confidence expected)
        {
            Assert.Equal(expected, ConfidenceBadgeService.FromScore(SymptomCheckerService.DetectionModel.Cosine, score));
        }

        [Theory]
        [InlineData(0.14, Confidence.Low)]
        [InlineData(0.15, Confidence.Moderate)]
        [InlineData(0.34, Confidence.Moderate)]
        [InlineData(0.35, Confidence.High)]
        [InlineData(0.99, Confidence.High)]
        public void NaiveBayes_BucketsAtBoundaries(double score, Confidence expected)
        {
            Assert.Equal(expected, ConfidenceBadgeService.FromScore(SymptomCheckerService.DetectionModel.NaiveBayes, score));
        }

        [Theory]
        [InlineData(0.19, Confidence.Low)]
        [InlineData(0.20, Confidence.Moderate)]
        [InlineData(0.50, Confidence.High)]
        public void Ensemble_BucketsAtBoundaries(double score, Confidence expected)
        {
            Assert.Equal(expected, ConfidenceBadgeService.FromScore(SymptomCheckerService.DetectionModel.Ensemble, score));
        }

        [Fact]
        public void TranslationKey_MatchesBand()
        {
            Assert.Equal("Confidence_Low", ConfidenceBadgeService.TranslationKey(Confidence.Low));
            Assert.Equal("Confidence_Moderate", ConfidenceBadgeService.TranslationKey(Confidence.Moderate));
            Assert.Equal("Confidence_High", ConfidenceBadgeService.TranslationKey(Confidence.High));
        }
    }
}
