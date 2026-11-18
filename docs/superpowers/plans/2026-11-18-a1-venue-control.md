# A1 Venue Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The platform operator can open any venue and use every venue screen with manager rights (logged), suspend/unsuspend venues, and manage venue staff (deactivate, reset password).

**Design (approved 2026-11-18):**
- **Open venue** = `POST /admin/venues/{id}/act` returns a 60-minute *access-only* token: `sub` = operator id, `kind` = staff, `role` = VenueManager, `venue_id` = the venue, plus `act_by` = operator id. No refresh token. Every existing `/venue/*` endpoint works unchanged; `CreatedByStaffId` / `CheckedInByStaffId` naturally record the operator (operators are `staff_members` rows).
- **Log:** a middleware writes one `operator_actions` row for every non-GET request made with an `act_by` token (operator, venue, method, path, status code, time). `GET /admin/venues/{id}/actions` returns the latest 50.
- **Suspend:** `Venue.Suspended`. Suspending also unpublishes and revokes the refresh tokens of that venue's staff. Suspended venue staff can't sign in or refresh; the manager can't publish while suspended. Unsuspend clears the flag only (the venue stays unpublished until someone publishes it). Existing bookings untouched.
- **Staff controls** on `/venue/staff/{id}`: set active, reset password (temporary, must change, sessions revoked). Same venue only, never yourself; a real venue manager may only act on DeskStaff, an operator (act_by) on anyone in the venue.
- **Admin:** Venues page shows status (Draft / Published / Suspended), **Open**, **Suspend / Unsuspend**, and a "Recent operator changes" disclosure. While a venue is open: a banner "Viewing {venue} as Midango operator — Exit" and the venue sidebar. Exit, reload, or a 401 on the acting token returns to the operator view. Staff page gets Deactivate/Reactivate and Reset password.

**Tech Stack:** .NET 10 minimal API + EF Core/Npgsql (snake_case); React 19 admin with typed EN/AR locales (`ar.ts` is `Record<MessageKey,string>`).

Project rules: **no automated tests** — verify by building and running. Bilingual EN + AR with RTL.

---

### Task 1: API

**Files:**
- Modify: `api/src/Midango.Api/Venues/Venue.cs` — add `public bool Suspended { get; set; }` with a doc comment.
- Modify: `api/src/Midango.Api/Identity/Auth.cs` — `AppClaims.ActBy = "act_by"`, `public static Guid? ActingOperatorId(this ClaimsPrincipal user)` (parses `act_by`), and

```csharp
/// What a platform operator gets when they open a venue: manager rights there, marked as the operator.
public static IReadOnlyList<Claim> ActingFor(StaffMember op, Guid venueId) =>
[
    new(Subject, op.Id.ToString()),
    new(Kind, "staff"),
    new(Role, nameof(StaffRole.VenueManager)),
    new(VenueId, venueId.ToString()),
    new(ActBy, op.Id.ToString()),
];
```

- Modify: `api/src/Midango.Api/Identity/TokenService.cs` — extract the JWT creation in `IssueAsync` into `public (string Token, DateTimeOffset ExpiresAt) CreateAccess(IReadOnlyList<Claim> claims, TimeSpan lifetime)` and use it from `IssueAsync` (lifetime `AccessLifetime`). Add `public static readonly TimeSpan ActingLifetime = TimeSpan.FromMinutes(60);`.
- Create: `api/src/Midango.Api/Platform/OperatorAction.cs`:

```csharp
namespace Midango.Api.Platform;

/// One change a platform operator made while a venue was open in the admin.
public sealed class OperatorAction
{
    public long Id { get; init; }
    public Guid OperatorId { get; init; }
    public Guid VenueId { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public int Status { get; init; }
    public DateTimeOffset At { get; init; }
}
```

  DbContext: `DbSet<OperatorAction> OperatorActions`; config: `Method` max 8, `Path` max 300, index `(VenueId, At)`, FK `OperatorId` → `StaffMember` restrict, FK `VenueId` → `Venue` cascade. `Id` is a database identity (`ValueGeneratedOnAdd`) — check the "Ids are created in code" loop at the end of `OnModelCreating` and exclude this `long` key from it if that loop forces `ValueGenerated.Never`.

- Create: `api/src/Midango.Api/Platform/OperatorEndpoints.cs` with `MapOperatorEndpoints` and `UseOperatorActionLog`:

```csharp
public static IApplicationBuilder UseOperatorActionLog(this IApplicationBuilder app) =>
    app.Use(async (http, next) =>
    {
        await next();
        if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method) || HttpMethods.IsOptions(http.Request.Method)) return;
        if (http.User.ActingOperatorId() is not { } op || http.User.StaffVenueId() is not { } venue) return;
        var db = http.RequestServices.GetRequiredService<MidangoDb>();
        db.ChangeTracker.Clear(); // don't re-save anything the endpoint left tracked
        var path = http.Request.Path.Value ?? "";
        db.OperatorActions.Add(new OperatorAction
        {
            OperatorId = op, VenueId = venue, Method = http.Request.Method,
            Path = path[..Math.Min(300, path.Length)],
            Status = http.Response.StatusCode,
            At = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(),
        });
        await db.SaveChangesAsync(CancellationToken.None);
    });
```

  Register in `Program.cs` right after `app.UseRateLimiter();`: `app.UseOperatorActionLog();` and `app.MapOperatorEndpoints();` after `app.MapLeadEndpoints();`.

  Endpoints (group `/admin/venues`, `Policies.PlatformOperator`). Acting tokens carry role VenueManager so they already fail `PlatformOperator` — keep it that way.
  - `POST /{id:guid}/act` → 404 "Venue not found." if missing; 409 "This venue is suspended. Unsuspend it before opening it." if suspended; load the operator (`db.Staff` by `user.SubjectId()`), `tokens.CreateAccess(AppClaims.ActingFor(op, id), TokenService.ActingLifetime)`, return `{ accessToken, expiresAt, venueId, nameEn, nameAr }`.
  - `POST /{id:guid}/suspend` → set `Suspended = true`, `Published = false`, save, then revoke refresh tokens of every staff of that venue (`db.RefreshTokens.Where(t => t.SubjectKind == SubjectKind.Staff && t.RevokedAt == null && db.Staff.Any(s => s.Id == t.SubjectId && s.VenueId == id)).ExecuteUpdateAsync(...)`). Returns the venue summary.
  - `POST /{id:guid}/unsuspend` → `Suspended = false`. Returns the venue summary.
  - `GET /{id:guid}/actions` → latest 50 for the venue joined with the operator's `DisplayName`: `{ at, operatorName, method, path, status }`.

- Modify: `api/src/Midango.Api/Identity/AdminEndpoints.cs`:
  - `VenueSummary` gains `bool Published, bool Suspended` (update both constructions; `POST /admin/venues` returns `false, false`).
  - Staff controls on the existing `venueStaff` group:

```csharp
public sealed record ActiveBody(bool Active);
public sealed record ResetPasswordBody(string? TemporaryPassword);

venueStaff.MapPut("/{id:guid}/active", async (Guid id, ActiveBody body, ClaimsPrincipal user, MidangoDb db, TokenService tokens, CancellationToken ct) =>
{
    if (await TargetAsync(db, user, id, ct) is not { } staff) return ApiErrors.Fail(404, "Staff member not found.");
    staff.Active = body.Active;
    await db.SaveChangesAsync(ct);
    if (!body.Active) await tokens.RevokeAllAsync(SubjectKind.Staff, staff.Id, ct);
    return Results.Ok(new StaffSummary(staff.Id, staff.Email, staff.DisplayName, staff.Role, staff.Active));
});

venueStaff.MapPost("/{id:guid}/reset-password", async (Guid id, ResetPasswordBody body, ClaimsPrincipal user, MidangoDb db, IPasswordHasher<StaffMember> hasher, TokenService tokens, CancellationToken ct) =>
{
    if (await TargetAsync(db, user, id, ct) is not { } staff) return ApiErrors.Fail(404, "Staff member not found.");
    if (body.TemporaryPassword is not { Length: >= StaffAccounts.MinPasswordLength } temp)
        return ApiErrors.Fail(400, $"Use at least {StaffAccounts.MinPasswordLength} characters.");
    staff.PasswordHash = hasher.HashPassword(staff, temp);
    staff.MustChangePassword = true;
    await db.SaveChangesAsync(ct);
    await tokens.RevokeAllAsync(SubjectKind.Staff, staff.Id, ct);
    return Results.NoContent();
});

/// Same venue, never yourself; a venue manager may change desk staff only, an operator anyone.
static Task<StaffMember?> TargetAsync(MidangoDb db, ClaimsPrincipal user, Guid id, CancellationToken ct)
{
    var venueId = user.StaffVenueId();
    var self = user.SubjectId();
    var operatorActing = user.ActingOperatorId() is not null;
    return db.Staff.SingleOrDefaultAsync(s => s.Id == id && s.Id != self && s.VenueId == venueId
        && (operatorActing || s.Role == StaffRole.DeskStaff), ct);
}
```

- Modify: `api/src/Midango.Api/Identity/StaffEndpoints.cs` login — after the password check, if `staff.VenueId` is set and that venue is `Suspended` → `ApiErrors.Fail(403, "This venue is suspended. Contact Midango.")`.
- Modify: `api/src/Midango.Api/Identity/TokenEndpoints.cs` refresh — staff claims only when `Active` **and** its venue (if any) is not suspended; otherwise the existing 401 path.
- Modify: `api/src/Midango.Api/Catalog/VenueEndpoints.cs` publish — if the venue is suspended → 409 "Midango has suspended this venue. Contact Midango to reopen it." (check before the existing checklist).
- Migration `VenueControl` (`dotnet build`, `dotnet ef migrations add VenueControl`, `dotnet build`; 0 errors, no new warnings).
- Commit: `git add api && git commit` — `feat(api): operators open venues, suspend them, and manage venue staff` + the co-author line.

### Task 2: Admin

**Files:** `admin/src/lib/api.ts`, `admin/src/lib/auth.tsx`, `admin/src/lib/types.ts`, `admin/src/layout/Shell.tsx`, `admin/src/pages/VenuesPage.tsx`, `admin/src/pages/StaffPage.tsx`, locales, CSS.

- [ ] `api.ts`: add `export function getAccessToken()`, a module flag `acting` with `setActing(on: boolean)` and `onActingEnded(handler)`. In `api()`: if the response is 401 and `acting` is on → call the acting-ended handler and throw `ApiError(401, …)` **without** calling `refreshSession` (the refresh cookie belongs to the operator).
- [ ] `auth.tsx`: context gains `acting: { venueId: string; nameEn: string; nameAr: string; expiresAt: string } | null`, `openVenue(id)`, `exitVenue()`.
  - `openVenue`: keep the operator token and operator `staff` in refs; call `POST /admin/venues/{id}/act`; `setAccessToken(result.accessToken)`; `setActing(true)`; set `staff` to `{ ...operator, role: 'VenueManager', venueId: id }`; set `acting`.
  - `exitVenue`: restore the operator token and `staff`, `setActing(false)`, clear `acting`. Register `onActingEnded(exitVenue)`.
  - `signOut` while acting: exit first, then the normal sign-out.
  - A full page reload naturally returns to the operator (acting state is memory-only).
- [ ] Callers navigate: Open → `/today`; Exit → `/venues`.
- [ ] `Shell.tsx`: when `acting`, render a full-width banner above the page content: venue name in the current language, "Viewing as Midango operator", and an **Exit** button. Nav is already driven by `staff.role`, so venue items appear. Banner uses `role="status"`, lime-tinted background with navy text, sticky top.
- [ ] `VenuesPage.tsx`: each venue row shows a status pill (Draft / Published / Suspended, using the existing pill/status-tag components), buttons **Open** (disabled when suspended), **Suspend** / **Unsuspend** (confirm with the existing `Dialog` component: "Suspend {venue}? It disappears from the app and its staff are signed out. Bookings stay."), and a `<details>` "Recent operator changes" that loads `GET /admin/venues/{id}/actions` when opened: time, operator name, a readable action (`method path`, status). Update the `VenueSummary` type with `published`, `suspended`.
- [ ] `StaffPage.tsx`: for each row other than the current user, when allowed (operator acting → all; manager → DeskStaff only): **Deactivate** / **Reactivate** (`PUT /venue/staff/{id}/active`) and **Reset password** (Dialog with a temporary password field, min length same as the create form; on success toast "Password reset. They must change it at next sign-in."). Inactive rows show an "Inactive" pill.
- [ ] Locale keys in both `en.ts` and `ar.ts` for every new string (natural Arabic).
- [ ] `npm run build && npm run lint` → 0 errors. Commit `git add admin` — `feat(admin): open, suspend and staff controls for venues` + co-author line.

### Task 3: Verify (controller)
- [ ] API script: act token works on `/venue` and `/venue/bookings`, is 403 on `/admin/settings`, can't refresh; a POST while acting logs a row, a GET doesn't; actions list returns it with the operator name; act on a suspended venue 409; suspend → hidden from `/venues/{id}`, staff login 403, staff refresh 401, publish 409; unsuspend → login works; manager can deactivate/reset desk staff, not another manager, not self; operator acting can reset the manager; reset → `mustChangePassword` on next login.
- [ ] Browser: operator opens a venue, books on the schedule, changes a price, sees the banner in EN and AR, exits, sees the changes in "Recent operator changes"; suspend/unsuspend; staff page reset + deactivate.
- [ ] Append results here, mark the roadmap, merge to main fast-forward.

---

## Verification (2026-11-18)

**API** (`a1-check.ps1`): **0 failures.** Checked:
- **Open venue:** `/act` returns the name and expiry; 404 for an unknown venue; 403 for a manager token and for an acting token. The acting token works on `/venue*`, gets 403 on `/admin/settings`, and can't refresh.
- **Change log:** exactly 4 rows for 4 writes and none for GETs. `/actions` is newest first with the operator's name and is 403 for a manager.
- **Staff controls:** a deactivated desk member can't sign in; reactivate works; a password under 10 characters gets 400; after a reset the next sign-in has `mustChangePassword`. A manager can't touch another manager or themselves (404). An acting operator can reset a manager.
- **Suspend:** unpublishes the venue and hides it from the app (404). Staff sign-in gets 403 "This venue is suspended", the staff session cookie can't refresh (401), act gets 409, and publish with an old acting token gets 409. Unsuspend lets staff sign in again and the venue stays unpublished.

**Browser (operator):**
- The Venues list shows the status pills.
- **Open** goes to Today, with the banner and the venue sidebar.
- On the Staff page the operator sees Deactivate and Reset password even for the manager. A reset from the dialog shows the toast.
- The Arabic version reads right to left. The banner's name was correct; that test venue's Arabic name happens to be "منشأة".
- The "Recent operator changes" log showed the reset with the operator's name.
- Suspend, in Arabic: the warning dialog, then a "موقوفة" (suspended) pill and Open disabled with the reason. Unsuspend gives a "مسودة" (draft) pill.

**Fix during review:** Exit landed on Sport categories, because the role guard's redirect ran after the Exit navigation. The navigation is now deferred one tick, and Exit lands on Venues.

**Known limit:** when the one-hour pass expires on a 401, the operator returns to their home (Sport categories), not Venues. Access tokens already issued to a suspended venue's staff stay valid for up to 15 minutes; refresh is blocked.
