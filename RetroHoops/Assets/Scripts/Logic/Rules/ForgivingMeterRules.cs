namespace CallerRetroBall.Logic
{
    /// <summary>I14: where the forgiving meter may be used (never where results are compared with other people).</summary>
    public static class ForgivingMeterRules
    {
        public static bool Allowed(GameMode mode, string contextId)
        {
            if (mode == GameMode.Daily || mode == GameMode.Demo || mode == GameMode.Tutorial) return false;
            if (!string.IsNullOrEmpty(contextId) && (contextId.StartsWith("live") || contextId.StartsWith("link"))) return false;
            return true;
        }
    }
}
