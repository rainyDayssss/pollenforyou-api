namespace PollenForYouApi.Entities;

/// <summary>
/// Order state machine string values, matching the refined schema (varchar(30)).
/// </summary>
public static class OrderStatuses
{
    public const string Pending = "Pending";
    public const string InProduction = "In Production";
    public const string ReadyForDispatch = "Ready for Dispatch";
    public const string Dispatched = "Dispatched";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
}
