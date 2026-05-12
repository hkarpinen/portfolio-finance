using Finance.Application.Ports;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Plaid;

/// <summary>
/// Wraps ASP.NET Data Protection to encrypt Plaid <c>access_token</c>s at rest.
/// We use a named purpose so rotating the protector for other secrets does not
/// invalidate every linked bank account.
/// </summary>
internal sealed class AccessTokenProtector : IConnectionTokenProtector
{
    private const string Purpose = "Finance.Plaid.AccessToken.v1";
    private readonly IDataProtector _protector;

    public AccessTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string accessToken) => _protector.Protect(accessToken);

    public string Unprotect(string encryptedAccessToken) => _protector.Unprotect(encryptedAccessToken);
}
