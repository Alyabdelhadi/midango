using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Midango")
    ?? throw new InvalidOperationException("ConnectionStrings:Midango is not configured.");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));

var app = builder.Build();

// Liveness + database reachability. Never returns the exception text to the caller.
app.MapGet("/health", async (NpgsqlDataSource db, ILogger<Program> log, CancellationToken ct) =>
{
    try
    {
        await using var cmd = db.CreateCommand("SELECT 1");
        await cmd.ExecuteScalarAsync(ct);
        return Results.Ok(new { status = "ok", database = "ok" });
    }
    catch (NpgsqlException ex)
    {
        log.LogError(ex, "Health check could not reach the database");
        return Results.Json(new { status = "degraded", database = "unreachable" }, statusCode: 503);
    }
});

app.Run();
