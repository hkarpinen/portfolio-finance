using System.Text.Json;
using Client.Extensions;
using Finance.Application.Dtos;
using Finance.Application.Commands;
using Finance.Application.Queries;
using Finance.Application.Ports;
using Infrastructure.Plaid;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Client.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("api")]
[Route("api/finance/connections")]
public sealed class ConnectionsController : ControllerBase
{
    private readonly IBankConnections _manager;
    private readonly IFinancialConnectionQuery _connectionQuery;
    private readonly ILogger<ConnectionsController> _logger;

    private readonly IPlaidWebhookVerifier _webhookVerifier;

    public ConnectionsController(
        IBankConnections manager,
        IFinancialConnectionQuery connectionQuery,
        ILogger<ConnectionsController> logger,
        IPlaidWebhookVerifier webhookVerifier)
    {
        _manager = manager;
        _connectionQuery = connectionQuery;
        _logger = logger;
        _webhookVerifier = webhookVerifier;
    }

    // The link token is single-use.
    [HttpPost("link-token")]
    public async Task<IActionResult> CreateLinkToken(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateLinkTokenAsync(userId.Value, ct);
        return Ok(result);
    }

    // Idempotent: re-linking the same institution overwrites the prior credential.
    [HttpPost("exchange")]
    public async Task<IActionResult> Exchange([FromBody] LinkConnectionCommand request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _manager.ExchangePublicTokenAsync(userId.Value, request, ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> ListConnections(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _connectionQuery.ListConnectionsAsync(userId.Value, ct);
        return Ok(result);
    }

    [HttpPost("{connectionId:guid}/sync")]
    public async Task<IActionResult> Sync(Guid connectionId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _manager.SyncAsync(userId.Value, new SyncConnectionCommand(connectionId), ct);
        return Ok(result);
    }

    [HttpGet("{connectionId:guid}/transactions")]
    public async Task<IActionResult> ListTransactions(
        Guid connectionId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _connectionQuery.ListTransactionsAsync(
            userId.Value, new ListTransactionsParams(connectionId, page, pageSize), ct);
        return Ok(result);
    }

    [HttpPost("{connectionId:guid}/suggestions/refresh")]
    public async Task<IActionResult> RefreshSuggestions(Guid connectionId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _manager.RefreshSuggestionsAsync(
            userId.Value, new RefreshSuggestionsCommand(connectionId), ct);
        return Ok(result);
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> ListSuggestions(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _connectionQuery.ListRecurringSuggestionsAsync(userId.Value, ct);
        return Ok(result);
    }

    // Idempotent — calling twice returns the same linked entity.
    [HttpPost("suggestions/{suggestionId:guid}/accept")]
    public async Task<IActionResult> AcceptSuggestion(Guid suggestionId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _manager.AcceptSuggestionAsync(
            userId.Value, new AcceptSuggestionCommand(suggestionId), ct);
        return Ok(result);
    }

    // The provider's remove endpoint is called best-effort; a failure there does not block removal.
    [HttpDelete("{connectionId:guid}")]
    public async Task<IActionResult> Disconnect(Guid connectionId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _manager.DisconnectAsync(userId.Value, new DisconnectCommand(connectionId), ct);
        return NoContent();
    }

    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _connectionQuery.GetForUserAsync(userId.Value, ct);
        return Ok(result);
    }

    [HttpGet("bank-sync-suggestions")]
    public async Task<IActionResult> ListBankSyncSuggestions([FromQuery] bool includeDismissed = false, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _connectionQuery.ListForUserAsync(userId.Value, includeDismissed, ct);
        return Ok(result);
    }

    [HttpPost("bank-sync-suggestions/{suggestionId:guid}/accept")]
    public async Task<IActionResult> AcceptBankSyncSuggestion(Guid suggestionId, [FromBody] AcceptBankSyncSuggestionBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.AcceptBankSyncSuggestionAsync(
            userId.Value,
            new AcceptBankSyncSuggestionCommand(suggestionId, body.AsIncome, body.GroupId),
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
    /// Bank-link provider webhook receiver. Verifies the <c>Plaid-Verification</c> JWT signature
    /// and body hash before processing any payload.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        // Read the raw body for signature verification before model binding consumes it
        Request.EnableBuffering();
        byte[] rawBody;
        using (var ms = new MemoryStream())
        {
            await Request.Body.CopyToAsync(ms, ct);
            rawBody = ms.ToArray();
        }
        Request.Body.Position = 0;

        if (!Request.Headers.TryGetValue("Plaid-Verification", out var verificationHeader)
            || string.IsNullOrEmpty(verificationHeader))
        {
            _logger.LogWarning("Plaid webhook: missing Plaid-Verification header");
            return Unauthorized();
        }

        if (!await _webhookVerifier.VerifyAsync(verificationHeader!, rawBody, ct))
            return Unauthorized();

        var payload = JsonSerializer.Deserialize<WebhookPayload>(rawBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload is null || string.IsNullOrEmpty(payload.ItemId))
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

public sealed record AcceptBankSyncSuggestionBody(bool AsIncome, Guid? GroupId = null);
