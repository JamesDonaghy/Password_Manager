namespace PasswordManager
{
    /// <summary>
    /// Decides whether an account entry counts as "stale" - hasn't been updated in a
    /// while, or has no recorded update at all - and provides the visual/explanatory
    /// details for flagging it in the UI. Pure and stateless: takes an Account, returns
    /// an answer, no dependency on the grid or anything else.
    /// </summary>
    public static class StaleEntryPolicy
    {
        // ~6 months - a sensible default, easy to tune if it doesn't feel right in practice.
        private const int ThresholdDays = 180;

        public static readonly System.Drawing.Color HighlightColor = System.Drawing.Color.LightYellow;

        public static bool IsStale(Account account)
        {
            return account.ModifiedAt == null || (System.DateTime.Now - account.ModifiedAt.Value).TotalDays > ThresholdDays;
        }

        public static string ExplanationText =>
            $"This entry has no recorded update, or hasn't been changed in over {ThresholdDays} days - consider reviewing it.";
    }
}