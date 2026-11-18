using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;

namespace Midango.Api.Identity;

public static class Customers
{
    public const string BlockedMessage = "This account is blocked. Contact Midango.";

    /// 409 for a blocked customer, null otherwise. Every customer write path calls this
    /// (an access token issued before the block lives up to 15 minutes; reads stay allowed).
    public static async Task<IResult?> BlockedAsync(MidangoDb db, Guid customerId, CancellationToken ct) =>
        await db.Customers.AnyAsync(c => c.Id == customerId && c.BlockedAt != null, ct)
            ? ApiErrors.Fail(409, BlockedMessage)
            : null;
}
