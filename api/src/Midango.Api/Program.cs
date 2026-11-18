using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Midango.Api.Bookings;
using Midango.Api.Catalog;
using Midango.Api.Data;
using Midango.Api.Identity;
using Midango.Api.Notifications;
using Midango.Api.Platform;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Midango")
    ?? throw new InvalidOperationException("ConnectionStrings:Midango is not configured.");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddDbContext<MidangoDb>(o => o.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt is not configured.");
jwt.Validate();
_ = builder.Configuration["Otp:HashKey"] ?? throw new InvalidOperationException("Otp:HashKey is not configured.");

builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher<StaffMember>, PasswordHasher<StaffMember>>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddProblemDetails();

// The Flutter web build calls the API from another origin in development. Mobile apps don't use CORS.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));
// Malformed request bodies are 400 in every environment (Development otherwise throws and returns 500).
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = false);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.Key(),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = AppClaims.Subject,
            RoleClaimType = AppClaims.Role,
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Customer, p => p.RequireClaim(AppClaims.Kind, "customer"))
    .AddPolicy(Policies.Staff, p => p.RequireClaim(AppClaims.Kind, "staff"))
    .AddPolicy(Policies.PlatformOperator, p => p
        .RequireClaim(AppClaims.Kind, "staff").RequireRole(nameof(StaffRole.PlatformOperator)))
    .AddPolicy(Policies.VenueManager, p => p
        .RequireClaim(AppClaims.Kind, "staff").RequireRole(nameof(StaffRole.VenueManager)))
    .AddPolicy(Policies.VenueStaff, p => p
        .RequireClaim(AppClaims.Kind, "staff").RequireRole(nameof(StaffRole.VenueManager), nameof(StaffRole.DeskStaff)));

// ponytail: partitioned by client IP. Behind a reverse proxy, add ForwardedHeaders first,
// or every user shares the proxy's bucket.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(Policies.AuthRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

// Push: real FCM when a service-account key is configured (never in the repo), otherwise the log.
var firebaseKey = builder.Configuration["Firebase:CredentialsPath"];
if (!string.IsNullOrWhiteSpace(firebaseKey) && File.Exists(firebaseKey))
    builder.Services.AddSingleton<IPushSender>(sp => new FcmPushSender(firebaseKey, sp.GetRequiredService<ILogger<FcmPushSender>>()));
else
    builder.Services.AddSingleton<IPushSender, LogPushSender>();
builder.Services.AddHostedService<ReminderService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseOperatorActionLog();

if (app.Environment.IsDevelopment())
{
    await using (var scope = app.Services.CreateAsyncScope())
    {
        await scope.ServiceProvider.GetRequiredService<MidangoDb>().Database.MigrateAsync();
    }
    await OperatorBootstrap.RunAsync(app.Services, app.Configuration);
}

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

app.MapTokenEndpoints();
app.MapCustomerEndpoints();
app.MapStaffEndpoints();
app.MapAdminEndpoints();
app.MapCategoryEndpoints();
app.MapVenueEndpoints();
app.MapPriceEndpoints();
app.MapAvailabilityEndpoints();
app.MapPublicVenueEndpoints();
app.MapBookingEndpoints();
app.MapStaffBookingEndpoints();
app.MapVenueInsightEndpoints();
app.MapDeskEndpoints();
app.MapReportEndpoints();
app.MapSettingsEndpoints();
app.MapDeviceEndpoints();
app.MapLeadEndpoints();
app.MapOperatorEndpoints();

app.Run();
