using Finance.Application.Ports;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Plaid;

// The named purpose matters: rotating the protector for other secrets must not invalidate every
// linked bank account.
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
