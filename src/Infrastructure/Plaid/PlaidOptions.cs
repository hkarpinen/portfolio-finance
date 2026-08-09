namespace Infrastructure.Plaid;

// Bound from appsettings:Plaid. WebhookUrl is registered with every link-token request — it is
// how Plaid learns where to push SYNC_UPDATES_AVAILABLE, so a wrong or missing value means
// updates only ever arrive on a manual sync.
public sealed class PlaidOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;

    // sandbox | development | production.
    public string Environment { get; set; } = "sandbox";

    public string[] CountryCodes { get; set; } = ["US"];

    // Requested at link time. Keep this minimal — every product widens consent scope and increases
    // per-item pricing. `transactions` covers both the sync endpoint and recurring detection.
    public string[] Products { get; set; } = ["transactions"];

    public string AppName { get; set; } = "Portfolio Finance";

    public string Language { get; set; } = "en";

    public string? WebhookUrl { get; set; }

    public string BaseUrl => Environment.ToLowerInvariant() switch
    {
        "production" => "https://production.plaid.com",
        "development" => "https://development.plaid.com",
        _ => "https://sandbox.plaid.com",
    };
}
