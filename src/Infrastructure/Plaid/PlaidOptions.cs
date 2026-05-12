namespace Infrastructure.Plaid;

/// <summary>
/// Bound from <c>appsettings:Plaid</c>. The client id and secret come from the Plaid dashboard;
/// the environment selects which Plaid base URL we hit. Webhook URL is what we register with
/// every link-token request so Plaid knows where to push <c>SYNC_UPDATES_AVAILABLE</c>.
/// </summary>
public sealed class PlaidOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;

    /// <summary><c>sandbox</c> | <c>development</c> | <c>production</c>. Defaults to sandbox.</summary>
    public string Environment { get; set; } = "sandbox";

    /// <summary>Country codes Plaid Link should display institutions from.</summary>
    public string[] CountryCodes { get; set; } = ["US"];

    /// <summary>
    /// Products requested at link time. Keep this minimal — every product widens consent
    /// scope and increases per-item Plaid pricing. <c>transactions</c> covers both the
    /// sync endpoint and recurring detection.
    /// </summary>
    public string[] Products { get; set; } = ["transactions"];

    /// <summary>Display name shown to the end user inside Plaid Link.</summary>
    public string AppName { get; set; } = "Portfolio Finance";

    public string Language { get; set; } = "en";

    /// <summary>
    /// Public HTTPS URL Plaid will POST to when transactions update.
    /// Must terminate at our PlaidController.Webhook endpoint.
    /// </summary>
    public string? WebhookUrl { get; set; }

    public string BaseUrl => Environment.ToLowerInvariant() switch
    {
        "production" => "https://production.plaid.com",
        "development" => "https://development.plaid.com",
        _ => "https://sandbox.plaid.com",
    };
}
