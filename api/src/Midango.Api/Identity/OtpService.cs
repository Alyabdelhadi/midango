using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;

namespace Midango.Api.Identity;

public enum OtpRequestResult
{
    Sent,
    TooSoon,
    TooMany,
    SenderUnavailable,
}

public sealed class OtpService(
    MidangoDb db, IConfiguration config, IHostEnvironment env, ILogger<OtpService> log, TimeProvider clock)
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    public const int MaxCodesPerHour = 5;
    public const int MaxAttempts = 5;

    public async Task<OtpRequestResult> RequestAsync(PhoneNumber phone, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hourAgo = now.AddHours(-1);
        var recent = await db.OtpChallenges
            .Where(c => c.PhoneE164 == phone.E164 && c.CreatedAt > hourAgo)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.CreatedAt)
            .ToListAsync(ct);
        if (recent.Count >= MaxCodesPerHour) return OtpRequestResult.TooMany;
        if (recent.Count > 0 && now - recent[0] < ResendCooldown) return OtpRequestResult.TooSoon;

        // ponytail: no SMS provider chosen yet. Development writes the code to the log; every other
        // environment refuses. Plug the provider (Unifonic / Taqnyat / Twilio) in here.
        if (!env.IsDevelopment()) return OtpRequestResult.SenderUnavailable;

        var id = Guid.CreateVersion7();
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        db.OtpChallenges.Add(new OtpChallenge
        {
            Id = id,
            PhoneE164 = phone.E164,
            CodeHash = Hash(id, code),
            CreatedAt = now,
            ExpiresAt = now + CodeLifetime,
        });
        await db.SaveChangesAsync(ct);

        log.LogWarning("DEV OTP for {Phone}: {Code}", phone.Masked, code);
        return OtpRequestResult.Sent;
    }

    /// Checks the code against the newest unexpired, unused challenge for this phone.
    public async Task<bool> VerifyAsync(PhoneNumber phone, string code, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var challenge = await db.OtpChallenges
            .Where(c => c.PhoneE164 == phone.E164 && c.ConsumedAt == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (challenge is null || challenge.Attempts >= MaxAttempts) return false;

        // ponytail: Attempts isn't concurrency-safe, so parallel guesses can win a few extra tries
        // out of 1,000,000. Add a row version if it is ever abused.
        challenge.Attempts++;
        var matches = CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(challenge.CodeHash), Convert.FromHexString(Hash(challenge.Id, code)));
        if (matches) challenge.ConsumedAt = now;
        await db.SaveChangesAsync(ct);
        return matches;
    }

    private string Hash(Guid challengeId, string code)
    {
        var key = config["Otp:HashKey"] ?? throw new InvalidOperationException("Otp:HashKey is not configured.");
        return Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes($"{challengeId:N}:{code}")));
    }
}
