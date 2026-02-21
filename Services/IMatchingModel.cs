using System.Collections.Generic;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.Services
{
    /// <summary>
    /// Abstraction for symptom-condition matching algorithms.
    /// Each implementation computes scored matches between a set of selected symptoms
    /// and a collection of conditions.
    /// </summary>
    public interface IMatchingModel
    {
        /// <summary>Human-readable name used for display and serialization.</summary>
        string Name { get; }

        /// <summary>
        /// Compute scored matches for the selected symptoms against all conditions.
        /// </summary>
        /// <param name="selectedSymptoms">Case-insensitive set of selected symptom names.</param>
        /// <param name="conditions">All known conditions.</param>
        /// <param name="conditionSets">Pre-built per-condition symptom sets (cache).</param>
        /// <param name="vocabulary">Full vocabulary of unique symptoms.</param>
        /// <param name="threshold">Minimum score to include (0..1).</param>
        /// <param name="options">Optional model-specific parameters.</param>
        /// <returns>Unordered list of matches above threshold.</returns>
        List<ConditionMatch> ComputeMatches(
            HashSet<string> selectedSymptoms,
            IReadOnlyList<Condition> conditions,
            IReadOnlyDictionary<string, HashSet<string>> conditionSets,
            IReadOnlyList<string> vocabulary,
            double threshold,
            MatchingOptions? options = null);
    }

    /// <summary>
    /// Optional parameters that individual models may use.
    /// </summary>
    public class MatchingOptions
    {
        /// <summary>Temperature scaling for Naive Bayes softmax (default 1.0).</summary>
        public double? NaiveBayesTemperature { get; set; }
    }
}
