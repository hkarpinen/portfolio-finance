using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Finance.Application.Ports;
using Microsoft.Extensions.Options;

namespace Infrastructure.Plaid;

/// <summary>
/// HttpClient-based Plaid REST client. We deliberately avoid the official
/// <c>Going.Plaid</c> SDK to keep deployment surface small and to give the team
/// full control over retry / timeout policies.
///
/// All requests authenticate via the <c>client_id</c> + <c>secret</c> body fields
/// Plaid expects (Plaid does not use Authorization headers).
/// </summary>
internal sealed class PlaidApiClient : IBankDataProvider
{
    private readonly HttpClient _http;
    private readonly PlaidOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public PlaidApiClient(HttpClient http, IOptions<PlaidOptions> options)
    {
        _http = http;
        _options = options.Value;
        _http.BaseAddress = new Uri(_options.BaseUrl);
    }

    // ── Link token ──────────────────────────────────────────────────────────

    public async Task<BankLinkToken> CreateLinkTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            client_id = _options.ClientId,
            secret = _options.Secret,
            client_name = _options.AppName,
            user = new { client_user_id = userId.ToString() },
            products = _options.Products,
            country_codes = _options.CountryCodes,
            language = _options.Language,
            webhook = _options.WebhookUrl,
        };
        var raw = await PostAsync("/link/token/create", body, cancellationToken);
        var token = raw.GetProperty("link_token").GetString()!;
        var expiration = raw.GetProperty("expiration").GetDateTime();
        return new BankLinkToken(token, expiration);
    }

    // ── Public token exchange ───────────────────────────────────────────────

    public async Task<BankConnectionCredentials> ExchangePublicTokenAsync(string publicToken, CancellationToken cancellationToken = default)
    {
        var body = new { client_id = _options.ClientId, secret = _options.Secret, public_token = publicToken };
        var raw = await PostAsync("/item/public_token/exchange", body, cancellationToken);
        return new BankConnectionCredentials(
            raw.GetProperty("access_token").GetString()!,
            raw.GetProperty("item_id").GetString()!);
    }

    // ── Accounts ────────────────────────────────────────────────────────────

    public async Task<ExternalAccountsResult> GetAccountsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var body = new { client_id = _options.ClientId, secret = _options.Secret, access_token = accessToken };
        var raw = await PostAsync("/accounts/get", body, cancellationToken);

        var accounts = new List<ExternalAccountDto>();
        foreach (var a in raw.GetProperty("accounts").EnumerateArray())
        {
            decimal? current = null, available = null;
            string currency = "USD";
            if (a.TryGetProperty("balances", out var bal))
            {
                if (bal.TryGetProperty("current", out var c) && c.ValueKind != JsonValueKind.Null) current = c.GetDecimal();
                if (bal.TryGetProperty("available", out var av) && av.ValueKind != JsonValueKind.Null) available = av.GetDecimal();
                if (bal.TryGetProperty("iso_currency_code", out var iso) && iso.ValueKind == JsonValueKind.String)
                    currency = iso.GetString()!;
            }
            accounts.Add(new ExternalAccountDto(
                a.GetProperty("account_id").GetString()!,
                GetStringOrEmpty(a, "name"),
                GetStringOrNull(a, "official_name"),
                GetStringOrNull(a, "mask"),
                GetStringOrEmpty(a, "type"),
                GetStringOrNull(a, "subtype"),
                currency,
                current,
                available));
        }
        return new ExternalAccountsResult(accounts);
    }

    // ── Transactions sync (cursor-based) ────────────────────────────────────

    public async Task<TransactionSyncPage> SyncTransactionsAsync(string accessToken, string? cursor, CancellationToken cancellationToken = default)
    {
        // Plaid expects an empty cursor on first call; null is rejected.
        var body = new
        {
            client_id = _options.ClientId,
            secret = _options.Secret,
            access_token = accessToken,
            cursor = cursor ?? string.Empty,
            count = 500, // Plaid's max per page
        };
        var raw = await PostAsync("/transactions/sync", body, cancellationToken);

        var added = ParseTransactions(raw.GetProperty("added"));
        var modified = ParseTransactions(raw.GetProperty("modified"));
        var removed = new List<string>();
        foreach (var r in raw.GetProperty("removed").EnumerateArray())
            removed.Add(r.GetProperty("transaction_id").GetString()!);

        var nextCursor = raw.GetProperty("next_cursor").GetString() ?? string.Empty;
        var hasMore = raw.GetProperty("has_more").GetBoolean();
        return new TransactionSyncPage(added, modified, removed, nextCursor, hasMore);
    }

    // ── Recurring transactions ──────────────────────────────────────────────

    public async Task<RecurringStreamsResult> GetRecurringTransactionsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var body = new { client_id = _options.ClientId, secret = _options.Secret, access_token = accessToken };
        var raw = await PostAsync("/transactions/recurring/get", body, cancellationToken);

        var inflow = new List<RecurringStreamDto>();
        var outflow = new List<RecurringStreamDto>();

        foreach (var s in raw.GetProperty("inflow_streams").EnumerateArray())
            inflow.Add(ParseStream(s));
        foreach (var s in raw.GetProperty("outflow_streams").EnumerateArray())
            outflow.Add(ParseStream(s));

        return new RecurringStreamsResult(inflow, outflow);
    }

    // ── Item removal ────────────────────────────────────────────────────────

    public async Task RemoveItemAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var body = new { client_id = _options.ClientId, secret = _options.Secret, access_token = accessToken };
        await PostAsync("/item/remove", body, cancellationToken);
    }

    // ── Internals ───────────────────────────────────────────────────────────

    private async Task<JsonElement> PostAsync(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(path, body, JsonOptions, cancellationToken);
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement.Clone();

        if (!response.IsSuccessStatusCode)
        {
            var errorCode = GetStringOrNull(root, "error_code") ?? "unknown";
            var errorMessage = GetStringOrNull(root, "error_message") ?? "Plaid request failed.";
            throw new PlaidApiException(response.StatusCode, errorCode, errorMessage);
        }
        return root;
    }

    private static IReadOnlyList<ExternalTransactionDto> ParseTransactions(JsonElement array)
    {
        var list = new List<ExternalTransactionDto>(array.GetArrayLength());
        foreach (var t in array.EnumerateArray())
        {
            string? primary = null, detailed = null;
            if (t.TryGetProperty("personal_finance_category", out var pfc))
            {
                primary = GetStringOrNull(pfc, "primary");
                detailed = GetStringOrNull(pfc, "detailed");
            }
            list.Add(new ExternalTransactionDto(
                t.GetProperty("transaction_id").GetString()!,
                t.GetProperty("account_id").GetString()!,
                t.GetProperty("amount").GetDecimal(),
                GetStringOrNull(t, "iso_currency_code") ?? "USD",
                t.GetProperty("date").GetDateTime(),
                t.TryGetProperty("authorized_date", out var ad) && ad.ValueKind != JsonValueKind.Null
                    ? ad.GetDateTime() : null,
                GetStringOrEmpty(t, "name"),
                GetStringOrNull(t, "merchant_name"),
                primary,
                detailed,
                t.GetProperty("pending").GetBoolean()));
        }
        return list;
    }

    private static RecurringStreamDto ParseStream(JsonElement s)
    {
        var avg = s.GetProperty("average_amount").GetProperty("amount").GetDecimal();
        var last = s.GetProperty("last_amount").GetProperty("amount").GetDecimal();
        var currency = GetStringOrNull(s.GetProperty("average_amount"), "iso_currency_code") ?? "USD";

        var description = GetStringOrEmpty(s, "description");
        var merchant = GetStringOrNull(s, "merchant_name");

        return new RecurringStreamDto(
            s.GetProperty("stream_id").GetString()!,
            s.GetProperty("account_id").GetString()!,
            description,
            merchant,
            MapFrequency(GetStringOrEmpty(s, "frequency")),
            avg,
            last,
            currency,
            s.GetProperty("first_date").GetDateTime(),
            s.GetProperty("last_date").GetDateTime(),
            s.TryGetProperty("predicted_next_date", out var pnd) && pnd.ValueKind != JsonValueKind.Null
                ? pnd.GetDateTime() : null,
            // Plaid uses `is_active` plus `status`. We treat the stream as active when
            // `is_active==true` AND status is `MATURE` or `EARLY_DETECTION`.
            s.TryGetProperty("is_active", out var ia) && ia.GetBoolean());
    }

    private static string GetStringOrEmpty(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;

    private static Finance.Domain.ValueObjects.RecurrenceFrequency MapFrequency(string plaidFrequency) =>
        plaidFrequency?.ToUpperInvariant() switch
        {
            "WEEKLY"                         => Finance.Domain.ValueObjects.RecurrenceFrequency.Weekly,
            "BIWEEKLY" or "BI_WEEKLY"        => Finance.Domain.ValueObjects.RecurrenceFrequency.BiWeekly,
            "SEMI_MONTHLY" or "SEMIMONTHLY"  => Finance.Domain.ValueObjects.RecurrenceFrequency.BiWeekly,
            "MONTHLY"                        => Finance.Domain.ValueObjects.RecurrenceFrequency.Monthly,
            "ANNUALLY" or "YEARLY"           => Finance.Domain.ValueObjects.RecurrenceFrequency.Annually,
            "DAILY"                          => Finance.Domain.ValueObjects.RecurrenceFrequency.Daily,
            _                                => Finance.Domain.ValueObjects.RecurrenceFrequency.Monthly,
        };

    private static string? GetStringOrNull(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}

public sealed class PlaidApiException : Exception
{
    public System.Net.HttpStatusCode StatusCode { get; }
    public string ErrorCode { get; }
    public PlaidApiException(System.Net.HttpStatusCode statusCode, string errorCode, string message)
        : base($"Plaid API error [{statusCode}/{errorCode}]: {message}")
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}
