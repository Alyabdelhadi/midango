using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Midango.Api.Data;

namespace Midango.Api.Identity;

public sealed record TokenPair(string AccessToken, DateTimeOffset AccessExpiresAt, string RefreshToken);

public sealed class TokenService(MidangoDb db, JwtOptions jwt, TimeProvider clock)
{
    public static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    public async Task<TokenPair> IssueAsync(
        SubjectKind kind, Guid subjectId, IReadOnlyList<Claim> claims, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var rawRefresh = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        db.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = Hash(rawRefresh),
            SubjectKind = kind,
            SubjectId = subjectId,
            CreatedAt = now,
            ExpiresAt = now + RefreshLifetime,
        });
        await db.SaveChangesAsync(ct);

        var accessExpires = now + AccessLifetime;
        var access = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = accessExpires.UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(jwt.Key(), SecurityAlgorithms.HmacSha256),
        });
        return new TokenPair(access, accessExpires, rawRefresh);
    }

    /// Spends a refresh token: returns it if it was valid, otherwise null.
    /// A token that was already spent means it was copied or replayed, so every session of that subject is revoked.
    public async Task<RefreshToken?> SpendAsync(string rawRefresh, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = Hash(rawRefresh);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null) return null;

        if (token.RevokedAt is not null)
        {
            await RevokeAllAsync(token.SubjectKind, token.SubjectId, ct);
            return null;
        }
        if (token.ExpiresAt <= now) return null;

        token.RevokedAt = now;
        await db.SaveChangesAsync(ct);
        return token;
    }

    public Task RevokeAsync(string rawRefresh, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = Hash(rawRefresh);
        return db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    public Task RevokeAllAsync(SubjectKind kind, Guid subjectId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return db.RefreshTokens
            .Where(t => t.SubjectKind == kind && t.SubjectId == subjectId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
