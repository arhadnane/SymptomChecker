using System.Collections.Generic;

namespace SymptomCheckerApp.Models
{
    /// <summary>
    /// Snapshot of the user's currently entered vitals. Any field can be null
    /// (not provided). Educational only — values are never persisted as PHI.
    /// </summary>
    public readonly record struct VitalsSnapshot(
        double? TempC,
        int? HeartRate,
        int? RespRate,
        int? SystolicBP,
        int? DiastolicBP,
        int? SpO2);

    /// <summary>
    /// A single red-flag rule trigger. <see cref="Severity"/> is 1..5
    /// (higher = more urgent). Localized text is built by the UI layer from
    /// <see cref="MessageKey"/> via TranslationService.
    /// </summary>
    public sealed class RedFlag
    {
        public string Code { get; }
        public int Severity { get; }
        public string MessageKey { get; }

        public RedFlag(string code, int severity, string messageKey)
        {
            Code = code;
            Severity = severity;
            MessageKey = messageKey;
        }
    }

    /// <summary>
    /// Plain-language confidence band derived from a model score. Educational
    /// only — never to be presented as clinical certainty.
    /// </summary>
    public enum Confidence
    {
        Low = 0,
        Moderate = 1,
        High = 2
    }
}
