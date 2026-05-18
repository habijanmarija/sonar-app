namespace Sonar.Scenario
{
    /// Skill-level mode per dossier/05_feedback_script.md "Mode toggling" table.
    /// Set at session start and immutable for the session; surfaces in
    /// `custom_data.mode` on the session_start telemetry event.
    public enum SessionMode
    {
        Novice = 0,
        Intermediate = 1,
        Expert = 2,
    }
}
