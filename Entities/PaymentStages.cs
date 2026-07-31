namespace PollenForYouApi.Entities;

/// <summary>
/// Payment settlement stages, matching the refined schema.
/// </summary>
public static class PaymentStages
{
    public const string Downpayment = "Downpayment";
    public const string FinalBalance = "Final Balance";
    public const string FullPayment = "Full Payment";
}
