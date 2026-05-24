namespace SymptomCheckerApp.Models
{
    /// <summary>
    /// High-level UI mode: a guided wizard for non-clinical users (Patient) or
    /// the full power-user surface (Professional). Educational only — does not
    /// alter matching logic or safety constraints.
    /// </summary>
    public enum UiMode
    {
        Patient = 0,
        Professional = 1
    }
}
