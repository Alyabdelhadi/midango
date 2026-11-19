using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Platform;

/// Writes every unhandled exception to error_entries, then lets the default handler answer with problem details.
public sealed class ErrorLogHandler(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ErrorLogHandler> log) : IExceptionHandler
{
    public const int KeepDays = 30;

    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        try
        {
            // A fresh scope: the request's DbContext may be the thing that failed.
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MidangoDb>();
            var now = clock.GetUtcNow();
            var path = http.Request.Path.Value ?? "";
            db.ErrorEntries.Add(new ErrorEntry
            {
                At = now,
                Method = http.Request.Method,
                Path = Cut(path, 300),
                ExceptionType = Cut(exception.GetType().FullName ?? "Exception", 200),
                Message = Cut(exception.Message, 2000),
                Detail = Cut(exception.ToString(), 16000),
                StaffId = http.User.FindFirstValue(AppClaims.Kind) == "staff" ? http.User.SubjectId() : null,
                TraceId = Cut(Activity.Current?.Id ?? http.TraceIdentifier, 64),
            });
            await db.SaveChangesAsync(CancellationToken.None);
            await db.ErrorEntries.Where(e => e.At < now.AddDays(-KeepDays)).ExecuteDeleteAsync(CancellationToken.None);
        }
        catch (Exception logFailure)
        {
            log.LogError(logFailure, "Could not record an error in the error log.");
        }
        return false;
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
}

public static class ErrorLogEndpoints
{
    public const int PageSize = 50;

    public sealed record ErrorDto(
        long Id, DateTimeOffset At, string Method, string Path, string ExceptionType, string Message, string Detail,
        string? StaffName, string TraceId);

    public sealed record ErrorPage(IReadOnlyList<ErrorDto> Items, int Total);

    public static void MapErrorLogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/errors", async (int? page, string? q, MidangoDb db, CancellationToken ct) =>
        {
            var query = db.ErrorEntries.AsNoTracking();
            if (q?.Trim() is { Length: > 0 } term)
            {
                var pattern = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
                query = query.Where(e => EF.Functions.ILike(e.Path, pattern) || EF.Functions.ILike(e.Message, pattern)
                                         || EF.Functions.ILike(e.ExceptionType, pattern));
            }
            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(e => e.At)
                .Skip((Math.Max(1, page ?? 1) - 1) * PageSize).Take(PageSize)
                .Select(e => new ErrorDto(
                    e.Id, e.At, e.Method, e.Path, e.ExceptionType, e.Message, e.Detail,
                    db.Staff.Where(s => s.Id == e.StaffId).Select(s => s.DisplayName).FirstOrDefault(), e.TraceId))
                .ToListAsync(ct);
            return Results.Ok(new ErrorPage(items, total));
        }).RequireAuthorization(Policies.PlatformOperator);
    }
}
