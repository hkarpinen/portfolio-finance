using Client.Extensions;
using Finance.Application.Dtos;
using Finance.Application.Commands;
using Finance.Application.Queries;
using Finance.Application.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

[ApiController]
[Authorize]
[Route("api/finance/connections")]
public sealed class ConnectionsController : ControllerBase
{
    private readonly IFinancialConnectionManager _manager;
    private readonly IFinancialConnectionQuery _connectionQuery;
    private readonly ILogger<ConnectionsController> _logger;

    public ConnectionsController(
        IFinancialConnectionManager manager,
        IFinancialConnectionQuery connectionQuery,
        ILogger<ConnectionsController> logger)
    {
        _manager = manager;
        _connectionQuery = connectionQuery;
        _logger = logger;
    }

    /// <summary>Issues a single-use bank-link token. The SPA passes this straight to Plaid Link.</summary>
    [HttpPost("link-token")]
    public async Task<IActionResult> CreateLinkToken(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateLinkTokenAsync(userId.Value, ct);
        return Ok(result);
    }

    /// <summary>
    /// Receives the short-lived token from the bank-link flow's <c>onSuccess</c> callback,
    /// exchanges it for a long-lived credential, and persists the linked connection.
    /// Idempotent: re-linking the same institution overwrites the prior credential.
    /// </summary>
    [HttpPost("exchange")]
    public async Task<IActionResult> Exchange([FromBody] LinkConnectionCommand request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _manager.ExchangePublicTokenAsync(userId.Value, request, ct);
        return Ok(result);
    }

    /// <summary>List of every linked institution + accounts for the current user.</summary>
    [HttpGet]
    public async Task<IActionResult> ListConnections(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _connectionQuery.ListConnectionsAsync(userId.Value, ct);
        return Ok(result);
    }

    /// <summary>Manual trigger for a cursor-based incremental sync of a single linked connection.</summary>
    [HttpPost("{connectionId:guid}/sync")]
    public async Task<IActionResult> Sync(Guid connectionId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _manager.SyncAsync(userId.Value, new SyncConnectionCommand(connectionId), ct);
        return Ok(result);
    }

    /// <summary>Paginated listing of synced bank transactions for one linked connection.</summary>
    [HttpGet("{connectionId:guid}/transactions")]
    public async Task<IActionResult> ListTransactions(
        Guid connectionId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _connectionQuery.ListTransactionsAsync(
            userId.Value, new ListTransactionsParams(connectionId, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Re-runs recurring-stream detection and refreshes suggested bills/deposits.</summary>
    [HttpPost("{connectionId:guid}/suggestions/refresh")]
    public async Task<IActionResult> RefreshSuggestions(Guid connectionId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _manager.RefreshSuggestionsAsync(
            userId.Value, new RefreshSuggestionsCommand(connectionId), ct);
        return Ok(result);
    }

    /// <summary>Lists every detected recurring suggestion (across all linked connections).</summary>
    [HttpGet("suggestions")]
    public async Task<IActionResult> ListSuggestions(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _connectionQuery.ListRecurringSuggestionsAsync(userId.Value, ct);
        return Ok(result);
    }

    /// <summary>
    /// Promotes a detected suggestion to a tracked <c>IncomeSource</c> (inflow)
    /// or <c>Expense</c> (outflow). Idempotent — calling twice returns the same linked entity.
    /// </summary>
    [HttpPost("suggestions/{suggestionId:guid}/accept")]
    public async Task<IActionResult> AcceptSuggestion(Guid suggestionId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _manager.AcceptSuggestionAsync(
            userId.Value, new AcceptSuggestionCommand(suggestionId), ct);
        return Ok(result);
    }

    /// <summary>Removes a linked institution. Best-effort calls the provider's remove endpoint.</summary>
    [HttpDelete("{connectionId:guid}")]
    public async Task<IActionResult> Disconnect(Guid connectionId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _manager.DisconnectAsync(userId.Value, new DisconnectCommand(connectionId), ct);
        return NoContent();
    }

    // ── Account balance ───────────────────────────────────────────────────────

    /// <summary>Returns the summed available balance across all linked depository accounts.</summary>
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _connectionQuery.GetForUserAsync(userId.Value, ct);
        return Ok(result);
    }

    // ── Bank sync suggestions ─────────────────────────────────────────────────

    /// <summary>Lists non-linked bank transactions as actionable suggestions.</summary>
    [HttpGet("bank-sync-suggestions")]
    public async Task<IActionResult> ListBankSyncSuggestions([FromQuery] bool includeDismissed = false, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _connectionQuery.ListForUserAsync(userId.Value, includeDismissed, ct);
        return Ok(result);
    }

    /// <summary>Accepts a suggestion, creating a pre-filled Expense or IncomeSource.</summary>
    [HttpPost("bank-sync-suggestions/{suggestionId:guid}/accept")]
    public async Task<IActionResult> AcceptBankSyncSuggestion(Guid suggestionId, [FromBody] AcceptBankSyncSuggestionBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.AcceptBankSyncSuggestionAsync(
            userId.Value,
            new AcceptBankSyncSuggestionCommand(suggestionId, body.AsIncome, body.HouseholdId),
            ct);
        return Ok(result);
    }

    /// <summary>Dismisses a suggestion so it no longer appears in the default list.</summary>
    [HttpPost("bank-sync-suggestions/{suggestionId:guid}/dismiss")]
    public async Task<IActionResult> DismissBankSyncSuggestion(Guid suggestionId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.DismissBankSyncSuggestionAsync(
            userId.Value, new DismissBankSyncSuggestionCommand(suggestionId), ct);
        return NoContent();
    }

    /// <summary>
    /// Bank-link provider webhook receiver. UNAUTHENTICATED — Plaid signs requests with JWT
    /// in the <c>Plaid-Verification</c> header; production deployments should validate that
    /// signature. We gate on item-id existence so spoofed payloads are a no-op.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] WebhookPayload payload, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(payload.ItemId))
            return Ok();

        _logger.LogInformation(
            "Bank-link webhook received: type={Type} code={Code} item={ItemId}",
            payload.WebhookType, payload.WebhookCode, payload.ItemId);

        if (string.Equals(payload.WebhookType, "TRANSACTIONS", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(payload.WebhookCode, "SYNC_UPDATES_AVAILABLE", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(payload.WebhookCode, "INITIAL_UPDATE", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(payload.WebhookCode, "DEFAULT_UPDATE", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(payload.WebhookCode, "HISTORICAL_UPDATE", StringComparison.OrdinalIgnoreCase)))
        {
            await _manager.SyncByExternalItemIdAsync(payload.ItemId, ct);
        }
        return Ok();
    }
}

public sealed record AcceptBankSyncSuggestionBody(bool AsIncome, Guid? HouseholdId = null);
