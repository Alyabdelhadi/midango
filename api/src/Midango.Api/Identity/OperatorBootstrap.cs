using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;

namespace Midango.Api.Identity;

public static class OperatorBootstrap
{
    /// Development only: creates the first platform operator from configuration, so someone can sign in.
    public static async Task RunAsync(IServiceProvider services, IConfiguration config)
    {
        var email = StaffAccounts.NormaliseEmail(config["Bootstrap:OperatorEmail"]);
        var password = config["Bootstrap:OperatorPassword"];
        if (email is null || string.IsNullOrEmpty(password)) return;

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MidangoDb>();
        if (await db.Staff.AnyAsync(s => s.Role == StaffRole.PlatformOperator)) return;

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<StaffMember>>();
        var operatorAccount = new StaffMember
        {
            Email = email,
            DisplayName = "Midango Operator",
            Role = StaffRole.PlatformOperator,
        };
        operatorAccount.PasswordHash = hasher.HashPassword(operatorAccount, password);
        db.Staff.Add(operatorAccount);
        await db.SaveChangesAsync();
    }
}
