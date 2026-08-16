using Finance.Application.Ports;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Plaid;

internal sealed class PlaidWebhookVerifier : IBankWebhookVerifier
{
    private readonly HttpClient _http;
    private readonly PlaidOptions _options;
    private readonly ILogger<PlaidWebhookVerifier> _logger;
    private static readonly JsonWebTokenHandler _jwtHandler = new();

    public PlaidWebhookVerifier(HttpClient http, IOptions<PlaidOptions> options, ILogger<PlaidWebhookVerifier> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string verificationJwt, byte[] rawBody, CancellationToken ct = default)
    {
        JsonWebToken unvalidated;
        try { unvalidated = _jwtHandler.ReadJsonWebToken(verificationJwt); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plaid webhook: could not parse verification JWT");
            return false;
        }

        var kid = unvalidated.Kid;
        if (string.IsNullOrEmpty(kid))
        {
            _logger.LogWarning("Plaid webhook: verification JWT missing kid");
            return false;
        }

        var keyRequest = new { client_id = _options.ClientId, secret = _options.Secret, key_id = kid };
        HttpResponseMessage keyResponse;
        try
        {
            keyResponse = await _http.PostAsJsonAsync(
                $"{_options.BaseUrl}/webhook_verification_key/get", keyRequest, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plaid webhook: failed to fetch verification key");
            return false;
        }

        if (!keyResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Plaid webhook: key fetch returned {Status}", keyResponse.StatusCode);
            return false;
        }

        PlaidKeyResponse? keyPayload;
        try
        {
            keyPayload = await keyResponse.Content.ReadFromJsonAsync<PlaidKeyResponse>(
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plaid webhook: could not deserialize key response");
            return false;
        }

        if (keyPayload?.Key is null)
        {
            _logger.LogWarning("Plaid webhook: key response contained no key");
            return false;
        }

        var jwk = new JsonWebKey(JsonSerializer.Serialize(keyPayload.Key));
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = jwk,
        };

        var result = await _jwtHandler.ValidateTokenAsync(verificationJwt, validationParams);
        if (!result.IsValid)
        {
            _logger.LogWarning("Plaid webhook: JWT signature validation failed: {Error}", result.Exception?.Message);
            return false;
        }

        if (!result.Claims.TryGetValue("request_body_sha256", out var hashClaim) || hashClaim is not string bodyHash)
        {
            _logger.LogWarning("Plaid webhook: JWT missing request_body_sha256 claim");
            return false;
        }

        var actualHash = Convert.ToHexString(SHA256.HashData(rawBody)).ToLowerInvariant();
        if (actualHash != bodyHash)
        {
            _logger.LogWarning("Plaid webhook: body hash mismatch");
            return false;
        }

        return true;
    }

    private sealed record PlaidKeyResponse([property: JsonPropertyName("key")] PlaidJwk? Key);

    private sealed record PlaidJwk(
        [property: JsonPropertyName("alg")] string? Alg,
        [property: JsonPropertyName("crv")] string? Crv,
        [property: JsonPropertyName("kid")] string? Kid,
        [property: JsonPropertyName("kty")] string? Kty,
        [property: JsonPropertyName("use")] string? Use,
        [property: JsonPropertyName("x")]   string? X,
        [property: JsonPropertyName("y")]   string? Y,
        [property: JsonPropertyName("n")]   string? N,
        [property: JsonPropertyName("e")]   string? E,
        [property: JsonPropertyName("created_at")] long? CreatedAt,
        [property: JsonPropertyName("expired_at")] long? ExpiredAt
    );
}
