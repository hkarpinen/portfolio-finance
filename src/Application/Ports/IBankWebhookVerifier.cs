namespace Finance.Application.Ports;

/// <summary>
/// Proves an inbound bank webhook really came from the provider. Named for the job, not the
/// vendor: the other ports here say <c>Bank</c>, and Plaid is one implementation of that idea.
/// </summary>
public interface IBankWebhookVerifier
{
    // Returns false — never throws — on a missing header, an invalid JWT, or a body-hash mismatch.
    Task<bool> VerifyAsync(string verificationJwt, byte[] rawBody, CancellationToken ct = default);
}
