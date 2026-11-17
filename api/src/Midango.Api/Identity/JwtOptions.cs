using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Midango.Api.Identity;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string SigningKey { get; init; } = "";

    public SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(SigningKey));

    public void Validate()
    {
        if (Issuer.Length == 0 || Audience.Length == 0 || Encoding.UTF8.GetByteCount(SigningKey) < 32)
            throw new InvalidOperationException(
                "Jwt:Issuer, Jwt:Audience and a Jwt:SigningKey of at least 32 bytes must be configured.");
    }
}
