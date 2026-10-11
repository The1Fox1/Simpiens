namespace Simpiens.Cognition.Contracts
{
    public enum IntentResult : byte
    {
        InProgress,
        Success,
        TargetMissing,    // The entity we tried to interact with is gone
        PathBlocked,      // The physical path is indefinitely obstructed
        Aborted,          // Interrupted by player or higher-priority override
        Failed            // Transaction or action conditions could not be met
    }
}
