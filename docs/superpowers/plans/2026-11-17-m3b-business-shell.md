# M3b Business Shell & Catalog Screens Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MIDANGO Business becomes usable in a browser, in English and Arabic. Staff sign in (and replace a temporary password). The Midango operator manages sport categories, creates venues and adds venue managers. A venue manager sets up the venue (details, opening hours, courts), publishes it, and adds desk staff. Desk staff can view the venue.

**Architecture:**
- **API:** web staff sessions keep the refresh token in an HttpOnly, Secure, SameSite=Strict cookie (`?session=cookie`), so page scripts never see it. The mobile/body-token flow from M2 is unchanged.
- **Admin:** Vite + React 19 + `react-router` 7. All API calls go to `/api/*`. Vite proxies that to `http://localhost:5300` in development, and a reverse proxy does the same in production, so everything is same-origin and no CORS is needed.
- **i18n:** two typed dictionaries (TypeScript fails the build if a key is missing in Arabic), plus `name()` to pick `nameEn`/`nameAr`.
- **UI building blocks:** native `<dialog>` for dialogs, a fetch wrapper that refreshes the access token once on 401, and M1 tokens and components.

**Tech Stack:** React 19 · react-router 7 · lucide-react · TypeScript · .NET 10 API (M2/M3a)

**Project rules:** No automated tests. Every task ends with `npm run build` (TypeScript + Vite) or `dotnet build`. Task 6 is a browser walkthrough done by the controller. Run commands in PowerShell. Keep .NET packages on 10.x.

**Decisions this plan locks in:**

| Topic | Decision | Why |
|---|---|---|
| Web session | Refresh token only in an HttpOnly cookie; the access token only in memory; a reload restores the session through `/api/auth/refresh` | An injected script can't steal a long-lived token |
| Concurrent refreshes | One shared in-flight refresh promise | Refresh tokens are single-use; a second spend triggers M2's replay protection and signs the user out everywhere |
| Navigation | Sidebar shows only what works for the role. Operator: Sport categories, Venues. Manager: Venue, Staff. Desk staff: Venue (read-only) | Research rule: no dead controls |
| Venue setup | One Venue page with a publish checklist at the top (address, map location, hours, courts) instead of a multi-step wizard | Same guidance, one screen, nothing to keep in sync |
| Map location | Latitude/longitude fields with a "copy from Google Maps" hint | A map picker is its own piece of work. These two fields hold exactly what it would produce |
| Sport icons | Operator picks from 16 Lucide icons (`src/lib/sportIcons.tsx`); the app (M8) maps the same keys | One icon family (D9), predictable on both clients |
| Arabic inputs | Arabic name/address inputs are always `dir="rtl" lang="ar"`, whatever the interface language | Typing Arabic in an LTR box is error-prone |
| Server messages | API error titles are English for now and shown as-is | Arabic server messages are a later API task (noted in the roadmap) |

---

## File structure

```
api/src/Midango.Api/Identity/
├── SessionCookie.cs          new: write/clear the cookie, decide body vs cookie
├── TokenEndpoints.cs         modified: refresh/logout read the cookie when the body is empty
└── StaffEndpoints.cs         modified: login/password change can answer with a cookie

admin/
├── package.json              + react-router
├── vite.config.ts            + /api proxy
└── src/
    ├── App.tsx               routes
    ├── index.css             + @import ui.css
    ├── ui.css                shell, pages, tables, forms, dialog, notices
    ├── locales/en.ts, ar.ts  dictionaries
    ├── lib/
    │   ├── api.ts            fetch wrapper, refresh, ApiError
    │   ├── i18n.tsx          I18nProvider, useI18n (t, name, lang)
    │   ├── auth.tsx          AuthProvider, useAuth
    │   ├── useLoad.ts        load-and-reload hook
    │   ├── types.ts          API types + key maps
    │   └── sportIcons.tsx    SPORT_ICONS, SportIcon
    ├── components/           Field, Notice, Pill, Dialog, PageHeader, LanguageToggle (+ existing Button, StatusTag)
    ├── app/guards.tsx        RequireStaff, RequireRole, HomeRedirect
    ├── layout/Shell.tsx      sidebar + outlet
    └── pages/
        ├── SignInPage.tsx, ChangePasswordPage.tsx
        ├── CategoriesPage.tsx, CategoryDialog.tsx
        ├── VenuesPage.tsx, StaffForm.tsx, StaffPage.tsx
        └── venue/VenuePage.tsx, PublishPanel.tsx, DetailsSection.tsx, HoursSection.tsx, CourtsSection.tsx
```

---

### Task 1: Cookie sessions for staff on the web (API)

**Files:**
- Create: `api/src/Midango.Api/Identity/SessionCookie.cs`
- Modify: `api/src/Midango.Api/Identity/TokenEndpoints.cs`, `api/src/Midango.Api/Identity/StaffEndpoints.cs`

- [ ] **Step 1: Create `Identity/SessionCookie.cs`**

```csharp
namespace Midango.Api.Identity;

/// Web clients ask for "?session=cookie": the refresh token then travels only in an HttpOnly cookie,
/// so page scripts can never read it. Mobile clients keep receiving it in the body.
public static class SessionCookie
{
    public const string Name = "mg_refresh";

    public sealed record SessionResponse(string AccessToken, DateTimeOffset AccessExpiresAt, string? RefreshToken);

    public static bool Requested(HttpContext http) => http.Request.Query["session"] == "cookie";

    public static void Write(HttpContext http, TokenPair pair) =>
        http.Response.Cookies.Append(Name, pair.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow + TokenService.RefreshLifetime,
        });

    public static void Clear(HttpContext http) =>
        http.Response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });

    /// The refresh token for the response body: null when it went into the cookie instead.
    public static string? Deliver(HttpContext http, TokenPair pair)
    {
        if (!Requested(http)) return pair.RefreshToken;
        Write(http, pair);
        return null;
    }
}
```

- [ ] **Step 2: Replace `Identity/TokenEndpoints.cs`**

```csharp
using System.Security.Claims;
using Midango.Api.Data;

namespace Midango.Api.Identity;

public static class TokenEndpoints
{
    public sealed record RefreshBody(string? RefreshToken);

    public static void MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth");

        // Mobile sends the token in the body; the web sends an empty body and the cookie comes along.
        auth.MapPost("/refresh", async (
            RefreshBody body, HttpContext http, TokenService tokens, MidangoDb db, CancellationToken ct) =>
        {
            var fromCookie = string.IsNullOrEmpty(body.RefreshToken);
            var raw = fromCookie ? http.Request.Cookies[SessionCookie.Name] : body.RefreshToken;
            if (string.IsNullOrEmpty(raw)) return ApiErrors.Fail(401, "Please sign in again.");

            var spent = await tokens.SpendAsync(raw, ct);
            if (spent is null)
            {
                if (fromCookie) SessionCookie.Clear(http);
                return ApiErrors.Fail(401, "Please sign in again.");
            }

            // Claims are rebuilt from the database, so a role change or deactivation takes effect here.
            IReadOnlyList<Claim>? claims = spent.SubjectKind switch
            {
                SubjectKind.Customer => await db.Customers.FindAsync([spent.SubjectId], ct) is { } customer
                    ? AppClaims.For(customer)
                    : null,
                SubjectKind.Staff => await db.Staff.FindAsync([spent.SubjectId], ct) is { Active: true } staff
                    ? AppClaims.For(staff)
                    : null,
                _ => null,
            };
            if (claims is null)
            {
                if (fromCookie) SessionCookie.Clear(http);
                return ApiErrors.Fail(401, "Please sign in again.");
            }

            var pair = await tokens.IssueAsync(spent.SubjectKind, spent.SubjectId, claims, ct);
            if (!fromCookie) return Results.Ok(pair);

            SessionCookie.Write(http, pair);
            return Results.Ok(new SessionCookie.SessionResponse(pair.AccessToken, pair.AccessExpiresAt, null));
        });

        auth.MapPost("/logout", async (RefreshBody body, HttpContext http, TokenService tokens, CancellationToken ct) =>
        {
            var raw = string.IsNullOrEmpty(body.RefreshToken) ? http.Request.Cookies[SessionCookie.Name] : body.RefreshToken;
            if (!string.IsNullOrEmpty(raw)) await tokens.RevokeAsync(raw, ct);
            SessionCookie.Clear(http);
            return Results.NoContent();
        });
    }
}
```

- [ ] **Step 3: Edit `Identity/StaffEndpoints.cs`**

Three changes, nothing else:

1. Make the refresh token optional in the response record:

```csharp
    public sealed record StaffSignInResponse(
        string AccessToken, DateTimeOffset AccessExpiresAt, string? RefreshToken, bool MustChangePassword);
```

2. In the `/staff/auth/login` handler, add `HttpContext http` as the first lambda parameter, and return:

```csharp
            var pair = await tokens.IssueAsync(SubjectKind.Staff, staff!.Id, AppClaims.For(staff), ct);
            return Results.Ok(new StaffSignInResponse(
                pair.AccessToken, pair.AccessExpiresAt, SessionCookie.Deliver(http, pair), staff.MustChangePassword));
```

3. In the `/staff/me/password` handler, add `HttpContext http` as the first lambda parameter, and end with:

```csharp
            var pair = await tokens.IssueAsync(SubjectKind.Staff, staff.Id, AppClaims.For(staff), ct);
            return Results.Ok(new StaffSignInResponse(
                pair.AccessToken, pair.AccessExpiresAt, SessionCookie.Deliver(http, pair), false));
```

- [ ] **Step 4: Build**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango
dotnet build api/Midango.sln
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 5: Verify the cookie flow with curl**

Start the API (as in M2 Task 5: `Start-Process dotnet … --urls http://localhost:5300`, wait for `/health`), then:

```powershell
$body = '{"email":"operator@midango.local","password":"Operator!2026dev"}'
$login = curl.exe -s -i -X POST "http://localhost:5300/staff/auth/login?session=cookie" -H "Content-Type: application/json" -d $body
$login | Select-String 'HTTP/|set-cookie|refreshToken'
$cookie = ($login | Select-String 'mg_refresh=([^;]+)').Matches[0].Groups[1].Value

$r1 = curl.exe -s -i -X POST http://localhost:5300/auth/refresh -H "Content-Type: application/json" -H "Cookie: mg_refresh=$cookie" -d '{}'
$r1 | Select-String 'HTTP/|"refreshToken"'
$cookie2 = ($r1 | Select-String 'mg_refresh=([^;]+)').Matches[0].Groups[1].Value

"--- replaying the first cookie (expect 401 and a cleared cookie):"
curl.exe -s -i -X POST http://localhost:5300/auth/refresh -H "Content-Type: application/json" -H "Cookie: mg_refresh=$cookie" -d '{}' | Select-String 'HTTP/|set-cookie'
"--- the rotated cookie is now dead too (replay protection):"
curl.exe -s -o NUL -w "%{http_code}`n" -X POST http://localhost:5300/auth/refresh -H "Content-Type: application/json" -H "Cookie: mg_refresh=$cookie2" -d '{}'
"--- body flow unchanged (mobile):"
curl.exe -s -X POST http://localhost:5300/staff/auth/login -H "Content-Type: application/json" -d $body | Select-String '"refreshToken":"'
```

Expected:
- Login: `HTTP/1.1 200`, a `set-cookie: mg_refresh=…; expires=…; path=/; secure; samesite=strict; httponly` line, and the body contains `"refreshToken":null`.
- First refresh: `200` with `"refreshToken":null` and a new cookie.
- Replay: `401` and a `set-cookie: mg_refresh=; expires=Thu, 01 Jan 1970…` line.
- Rotated cookie: `401`.
- Plain login (no `?session=cookie`) still returns a real `"refreshToken":"…"` in the body.

Then also run the M2 check script again (it lives in the controller's scratchpad; the controller does this). Stop the API.

- [ ] **Step 6: Commit**

```powershell
git add api
git commit -m "feat(api): web staff sessions keep the refresh token in an HttpOnly cookie"
```

---

### Task 2: Admin foundations: routing, proxy, API client, i18n, shared UI

**Files:**
- Modify: `admin/package.json` (via npm), `admin/vite.config.ts`, `admin/src/index.css`
- Create: `admin/src/ui.css`, `admin/src/locales/en.ts`, `admin/src/locales/ar.ts`, `admin/src/lib/api.ts`, `admin/src/lib/i18n.tsx`, `admin/src/lib/useLoad.ts`, `admin/src/lib/types.ts`, `admin/src/components/Field.tsx`, `Notice.tsx`, `Pill.tsx`, `Dialog.tsx`, `PageHeader.tsx`, `LanguageToggle.tsx`

- [ ] **Step 1: Install the router**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango\admin
npm install react-router@7
```

- [ ] **Step 2: Add the API proxy to `admin/vite.config.ts`**

Keep the existing `plugins` entry exactly as generated, and add `server`:

```ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    // Same-origin API: the session cookie works without CORS. Production uses a reverse proxy the same way.
    proxy: {
      '/api': {
        target: 'http://localhost:5300',
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
})
```

If the generated file's plugin import differs (e.g. `@vitejs/plugin-react-swc`), keep the generated import and only add `server`.

- [ ] **Step 3: Create `admin/src/lib/api.ts`**

```ts
/** All calls go to /api, which Vite (dev) and the reverse proxy (production) forward to the .NET API. */
export class ApiError extends Error {
  readonly status: number;
  readonly missing?: string[];

  constructor(status: number, message: string, missing?: string[]) {
    super(message);
    this.status = status;
    this.missing = missing;
  }
}

let accessToken: string | null = null;
let sessionEnded: () => void = () => {};
let refreshing: Promise<boolean> | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function onSessionEnded(handler: () => void) {
  sessionEnded = handler;
}

/** Trades the HttpOnly session cookie for a new access token. Concurrent callers share one request:
 *  refresh tokens are single-use, and spending one twice signs the user out everywhere. */
export function refreshSession(): Promise<boolean> {
  refreshing ??= (async () => {
    try {
      const res = await fetch('/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: '{}',
      });
      if (!res.ok) {
        accessToken = null;
        return false;
      }
      accessToken = ((await res.json()) as { accessToken: string }).accessToken;
      return true;
    } finally {
      refreshing = null;
    }
  })();
  return refreshing;
}

type Options = { method?: 'GET' | 'POST' | 'PUT'; body?: unknown };

export async function api<T>(path: string, options: Options = {}): Promise<T> {
  const send = () =>
    fetch(`/api${path}`, {
      method: options.method ?? 'GET',
      headers: {
        'Content-Type': 'application/json',
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      },
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    });

  let res = await send();
  if (res.status === 401 && accessToken !== null) {
    if (await refreshSession()) res = await send();
    else sessionEnded();
  }
  if (!res.ok) {
    const problem = (await res.json().catch(() => ({}))) as { title?: string; missing?: string[] };
    throw new ApiError(res.status, problem.title ?? `Request failed (${res.status})`, problem.missing);
  }
  return (res.status === 204 ? undefined : await res.json()) as T;
}

export function errorText(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
```

- [ ] **Step 4: Create `admin/src/locales/en.ts`**

```ts
export const en = {
  appBusiness: 'Business',
  navLabel: 'Main',
  navCategories: 'Sport categories',
  navVenues: 'Venues',
  navVenue: 'Venue',
  navStaff: 'Staff',
  signIn: 'Sign in',
  signInTitle: 'Sign in to Midango Business',
  signOut: 'Sign out',
  email: 'Email',
  password: 'Password',
  loading: 'Loading…',
  changePasswordTitle: 'Choose your own password',
  changePasswordIntro: 'You signed in with a temporary password. Choose a new one to continue.',
  currentPassword: 'Temporary password',
  newPassword: 'New password',
  confirmPassword: 'Repeat new password',
  passwordTooShort: 'Use at least 10 characters.',
  passwordsDontMatch: 'The two new passwords are different.',
  savePassword: 'Save password',
  rolePlatformOperator: 'Midango operator',
  roleVenueManager: 'Venue manager',
  roleDeskStaff: 'Desk staff',
  categoriesIntro: 'Sports customers can book. A hidden sport stays on existing courts but disappears from the app.',
  newCategory: 'New sport',
  editCategory: 'Edit sport',
  noCategories: 'No sports yet. Add the first one.',
  sport: 'Sport',
  bookingLengths: 'Booking lengths',
  courtFeatures: 'Court features',
  status: 'Status',
  actions: 'Actions',
  minutesShort: '{m} min',
  shownInApp: 'Shown in app',
  hidden: 'Hidden',
  edit: 'Edit',
  nameEn: 'Name (English)',
  nameAr: 'Name (Arabic)',
  icon: 'Icon',
  durationsRule: 'Pick 1 to 4 lengths.',
  moveUp: 'Move up',
  moveDown: 'Move down',
  remove: 'Remove',
  addFeature: 'Add feature',
  sortOrder: 'Order in the app',
  showInApp: 'Show in the app',
  cancel: 'Cancel',
  save: 'Save',
  saved: 'Saved.',
  newVenue: 'New venue',
  noVenues: 'No venues yet.',
  addManager: 'Add manager',
  addManagerTo: 'Add a manager to {venue}',
  managerAdded: 'Manager added. Give them the temporary password; they choose their own when they first sign in.',
  displayName: 'Name',
  temporaryPassword: 'Temporary password',
  temporaryPasswordHint: 'At least 10 characters. They must change it when they first sign in.',
  addDeskStaff: 'Add desk staff',
  noStaff: 'No staff yet.',
  staffAdded: 'Staff member added. Give them the temporary password.',
  role: 'Role',
  active: 'Active',
  inactive: 'Inactive',
  venueDetails: 'Venue details',
  addressEn: 'Address (English)',
  addressAr: 'Address (Arabic)',
  latitude: 'Latitude',
  longitude: 'Longitude',
  locationHint: 'In Google Maps, right-click the venue and click the coordinates to copy them.',
  openingHours: 'Opening hours',
  day: 'Day',
  openThisDay: 'Open',
  opensAt: 'Opens',
  closesAt: 'Closes',
  closesNextDay: 'Closes the next day',
  closedDay: 'Closed',
  daySunday: 'Sunday',
  dayMonday: 'Monday',
  dayTuesday: 'Tuesday',
  dayWednesday: 'Wednesday',
  dayThursday: 'Thursday',
  dayFriday: 'Friday',
  daySaturday: 'Saturday',
  courts: 'Courts',
  court: 'Court',
  addCourt: 'Add court',
  editCourt: 'Edit court',
  noCourts: 'No courts yet.',
  setting: 'Setting',
  indoor: 'Indoor',
  outdoor: 'Outdoor',
  courtOpen: 'Open',
  courtClosed: 'Closed',
  courtOpenForBooking: 'Open for booking',
  hiddenSport: 'Hidden sport',
  noSportsYet: "Midango hasn't added any sports yet.",
  appVisibility: 'In the Midango app',
  liveInApp: 'Live',
  notPublished: 'Not published',
  publish: 'Publish',
  unpublish: 'Unpublish',
  finishChecklist: 'Finish the checklist first.',
  publishedNotice: 'Your venue is live in the app.',
  unpublishedNotice: 'Your venue is hidden from the app.',
  missingAddress: 'Address in English and Arabic',
  missingLocation: 'Map location',
  missingHours: 'Opening hours',
  missingCourts: 'At least one open court',
  readOnlyVenue: 'Only your venue manager can change these details.',
};

export type MessageKey = keyof typeof en;
```

- [ ] **Step 5: Create `admin/src/locales/ar.ts`**

```ts
import type { MessageKey } from './en';

export const ar: Record<MessageKey, string> = {
  appBusiness: 'الأعمال',
  navLabel: 'الرئيسية',
  navCategories: 'فئات الرياضة',
  navVenues: 'المنشآت',
  navVenue: 'المنشأة',
  navStaff: 'الموظفون',
  signIn: 'تسجيل الدخول',
  signInTitle: 'تسجيل الدخول إلى ميدانغو للأعمال',
  signOut: 'تسجيل الخروج',
  email: 'البريد الإلكتروني',
  password: 'كلمة المرور',
  loading: 'جارٍ التحميل…',
  changePasswordTitle: 'اختر كلمة مرور خاصة بك',
  changePasswordIntro: 'سجّلت الدخول بكلمة مرور مؤقتة. اختر كلمة مرور جديدة للمتابعة.',
  currentPassword: 'كلمة المرور المؤقتة',
  newPassword: 'كلمة المرور الجديدة',
  confirmPassword: 'أعد كتابة كلمة المرور الجديدة',
  passwordTooShort: 'استخدم 10 أحرف على الأقل.',
  passwordsDontMatch: 'كلمتا المرور الجديدتان غير متطابقتين.',
  savePassword: 'حفظ كلمة المرور',
  rolePlatformOperator: 'مشغّل ميدانغو',
  roleVenueManager: 'مدير المنشأة',
  roleDeskStaff: 'موظف الاستقبال',
  categoriesIntro: 'الرياضات التي يمكن للعملاء حجزها. الرياضة المخفية تبقى على الملاعب الحالية لكنها تختفي من التطبيق.',
  newCategory: 'رياضة جديدة',
  editCategory: 'تعديل الرياضة',
  noCategories: 'لا توجد رياضات بعد. أضف أول رياضة.',
  sport: 'الرياضة',
  bookingLengths: 'مدد الحجز',
  courtFeatures: 'مزايا الملعب',
  status: 'الحالة',
  actions: 'الإجراءات',
  minutesShort: '{m} دقيقة',
  shownInApp: 'ظاهرة في التطبيق',
  hidden: 'مخفية',
  edit: 'تعديل',
  nameEn: 'الاسم (بالإنجليزية)',
  nameAr: 'الاسم (بالعربية)',
  icon: 'الأيقونة',
  durationsRule: 'اختر من مدة إلى أربع مدد.',
  moveUp: 'نقل لأعلى',
  moveDown: 'نقل لأسفل',
  remove: 'إزالة',
  addFeature: 'إضافة ميزة',
  sortOrder: 'الترتيب في التطبيق',
  showInApp: 'إظهار في التطبيق',
  cancel: 'إلغاء',
  save: 'حفظ',
  saved: 'تم الحفظ.',
  newVenue: 'منشأة جديدة',
  noVenues: 'لا توجد منشآت بعد.',
  addManager: 'إضافة مدير',
  addManagerTo: 'إضافة مدير إلى {venue}',
  managerAdded: 'تمت إضافة المدير. أعطه كلمة المرور المؤقتة، وسيختار كلمته الخاصة عند أول تسجيل دخول.',
  displayName: 'الاسم',
  temporaryPassword: 'كلمة مرور مؤقتة',
  temporaryPasswordHint: '10 أحرف على الأقل. يجب تغييرها عند أول تسجيل دخول.',
  addDeskStaff: 'إضافة موظف استقبال',
  noStaff: 'لا يوجد موظفون بعد.',
  staffAdded: 'تمت إضافة الموظف. أعطه كلمة المرور المؤقتة.',
  role: 'الدور',
  active: 'نشط',
  inactive: 'غير نشط',
  venueDetails: 'تفاصيل المنشأة',
  addressEn: 'العنوان (بالإنجليزية)',
  addressAr: 'العنوان (بالعربية)',
  latitude: 'خط العرض',
  longitude: 'خط الطول',
  locationHint: 'في خرائط Google، انقر بزر الفأرة الأيمن على المنشأة ثم انقر على الإحداثيات لنسخها.',
  openingHours: 'ساعات العمل',
  day: 'اليوم',
  openThisDay: 'مفتوح',
  opensAt: 'يفتح',
  closesAt: 'يغلق',
  closesNextDay: 'يغلق في اليوم التالي',
  closedDay: 'مغلق',
  daySunday: 'الأحد',
  dayMonday: 'الاثنين',
  dayTuesday: 'الثلاثاء',
  dayWednesday: 'الأربعاء',
  dayThursday: 'الخميس',
  dayFriday: 'الجمعة',
  daySaturday: 'السبت',
  courts: 'الملاعب',
  court: 'الملعب',
  addCourt: 'إضافة ملعب',
  editCourt: 'تعديل الملعب',
  noCourts: 'لا توجد ملاعب بعد.',
  setting: 'النوع',
  indoor: 'داخلي',
  outdoor: 'خارجي',
  courtOpen: 'مفتوح',
  courtClosed: 'مغلق',
  courtOpenForBooking: 'متاح للحجز',
  hiddenSport: 'رياضة مخفية',
  noSportsYet: 'لم تُضف ميدانغو أي رياضة بعد.',
  appVisibility: 'في تطبيق ميدانغو',
  liveInApp: 'منشورة',
  notPublished: 'غير منشورة',
  publish: 'نشر',
  unpublish: 'إلغاء النشر',
  finishChecklist: 'أكمل القائمة أولاً.',
  publishedNotice: 'منشأتك الآن ظاهرة في التطبيق.',
  unpublishedNotice: 'منشأتك الآن مخفية من التطبيق.',
  missingAddress: 'العنوان بالإنجليزية والعربية',
  missingLocation: 'الموقع على الخريطة',
  missingHours: 'ساعات العمل',
  missingCourts: 'ملعب واحد مفتوح على الأقل',
  readOnlyVenue: 'يمكن لمدير المنشأة فقط تغيير هذه التفاصيل.',
};
```

- [ ] **Step 6: Create `admin/src/lib/i18n.tsx`**

```tsx
import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { ar } from '../locales/ar';
import { en, type MessageKey } from '../locales/en';

export type Lang = 'en' | 'ar';

const dictionaries: Record<Lang, Record<MessageKey, string>> = { en, ar };

type I18n = {
  lang: Lang;
  setLang: (lang: Lang) => void;
  t: (key: MessageKey, vars?: Record<string, string | number>) => string;
  /** The name in the interface language, from any { nameEn, nameAr } record. */
  name: (item: { nameEn: string; nameAr: string }) => string;
};

const I18nContext = createContext<I18n | null>(null);

function storedLang(): Lang {
  try {
    return localStorage.getItem('mg-lang') === 'ar' ? 'ar' : 'en';
  } catch {
    return 'en';
  }
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>(storedLang);

  useEffect(() => {
    document.documentElement.lang = lang;
    document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
  }, [lang]);

  const setLang = useCallback((next: Lang) => {
    setLangState(next);
    try {
      localStorage.setItem('mg-lang', next);
    } catch {
      // Storage blocked (private mode): the choice lasts for this visit only.
    }
  }, []);

  const t = useCallback(
    (key: MessageKey, vars?: Record<string, string | number>) => {
      let text = dictionaries[lang][key];
      for (const [k, v] of Object.entries(vars ?? {})) text = text.replaceAll(`{${k}}`, String(v));
      return text;
    },
    [lang],
  );

  const name = useCallback(
    (item: { nameEn: string; nameAr: string }) => (lang === 'ar' ? item.nameAr : item.nameEn),
    [lang],
  );

  return <I18nContext.Provider value={{ lang, setLang, t, name }}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18n {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error('useI18n must be used inside I18nProvider');
  return ctx;
}
```

- [ ] **Step 7: Create `admin/src/lib/useLoad.ts`**

```ts
import { useCallback, useEffect, useState } from 'react';
import { errorText } from './api';

/** Runs `load` on mount and on reload(). Keeps the last good data while reloading. */
export function useLoad<T>(load: () => Promise<T>) {
  const [data, setData] = useState<T>();
  const [error, setError] = useState<string>();
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let live = true;
    load().then(
      (result) => {
        if (live) {
          setData(result);
          setError(undefined);
        }
      },
      (err: unknown) => {
        if (live) setError(errorText(err));
      },
    );
    return () => {
      live = false;
    };
    // `load` is a new closure every render; `version` is what triggers a reload.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [version]);

  const reload = useCallback(() => setVersion((v) => v + 1), []);
  return { data, error, reload, setData };
}
```

- [ ] **Step 8: Create `admin/src/lib/types.ts`**

```ts
import type { MessageKey } from '../locales/en';

export type Role = 'PlatformOperator' | 'VenueManager' | 'DeskStaff';

export type Staff = {
  id: string;
  email: string;
  displayName: string;
  role: Role;
  venueId: string | null;
  mustChangePassword: boolean;
};

export type StaffSummary = { id: string; email: string; displayName: string; role: Role; active: boolean };

export type Feature = { id: string; nameEn: string; nameAr: string };

export type Category = {
  id: string;
  nameEn: string;
  nameAr: string;
  iconKey: string;
  durationsMinutes: number[];
  sortOrder: number;
  active: boolean;
  features: Feature[];
};

export type VenueSummary = { id: string; nameEn: string; nameAr: string };

export type Day = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';
export const DAYS: Day[] = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

/** Times are "HH:mm:ss" on the wire. */
export type Hours = { day: Day; opens: string; closes: string };

export type Court = {
  id: string;
  categoryId: string;
  nameEn: string;
  nameAr: string;
  indoor: boolean;
  featureIds: string[];
  sortOrder: number;
  active: boolean;
};

export type CourtInput = Omit<Court, 'id'>;

export type MissingItem = 'address' | 'location' | 'hours' | 'courts';
export const CHECKLIST: MissingItem[] = ['address', 'location', 'hours', 'courts'];

export type Venue = {
  id: string;
  nameEn: string;
  nameAr: string;
  addressEn: string | null;
  addressAr: string | null;
  latitude: number | null;
  longitude: number | null;
  timeZoneId: string;
  published: boolean;
  hours: Hours[];
  courts: Court[];
  missingForPublish: MissingItem[];
};

export const ROLE_KEY: Record<Role, MessageKey> = {
  PlatformOperator: 'rolePlatformOperator',
  VenueManager: 'roleVenueManager',
  DeskStaff: 'roleDeskStaff',
};

export const DAY_KEY: Record<Day, MessageKey> = {
  Sunday: 'daySunday',
  Monday: 'dayMonday',
  Tuesday: 'dayTuesday',
  Wednesday: 'dayWednesday',
  Thursday: 'dayThursday',
  Friday: 'dayFriday',
  Saturday: 'daySaturday',
};

export const MISSING_KEY: Record<MissingItem, MessageKey> = {
  address: 'missingAddress',
  location: 'missingLocation',
  hours: 'missingHours',
  courts: 'missingCourts',
};
```

- [ ] **Step 9: Create the shared components**

`admin/src/components/Field.tsx`:

```tsx
import type { ReactNode } from 'react';

export function Field({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <label className="mg-field">
      <span className="mg-field-label">{label}</span>
      {children}
      {hint && <span className="mg-field-hint">{hint}</span>}
    </label>
  );
}
```

`admin/src/components/Notice.tsx`:

```tsx
import type { ReactNode } from 'react';

export function Notice({ tone, children }: { tone: 'error' | 'success' | 'info'; children: ReactNode }) {
  return (
    <div className={`mg-notice mg-notice--${tone}`} role={tone === 'error' ? 'alert' : 'status'}>
      {children}
    </div>
  );
}
```

`admin/src/components/Pill.tsx`:

```tsx
import type { ReactNode } from 'react';

/** A status label. Always carries words, never colour alone. */
export function Pill({ tone = 'neutral', children }: { tone?: 'neutral' | 'success' | 'warning'; children: ReactNode }) {
  return <span className={`mg-pill mg-pill--${tone}`}>{children}</span>;
}
```

`admin/src/components/Dialog.tsx`:

```tsx
import { useEffect, useId, useRef, type ReactNode } from 'react';

/** Native <dialog>: the browser provides the focus trap, Esc and the backdrop.
 *  Children mount only while open, so every opening starts with a fresh form. */
export function Dialog({
  open,
  title,
  onClose,
  children,
}: {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (open && !dialog.open) dialog.showModal();
    if (!open && dialog.open) dialog.close();
  }, [open]);

  return (
    <dialog ref={ref} className="mg-dialog" aria-labelledby={titleId} onClose={onClose}>
      <h2 id={titleId}>{title}</h2>
      {open && children}
    </dialog>
  );
}
```

`admin/src/components/PageHeader.tsx`:

```tsx
import type { ReactNode } from 'react';

export function PageHeader({ title, action }: { title: string; action?: ReactNode }) {
  return (
    <header className="page-header">
      <h1>{title}</h1>
      {action}
    </header>
  );
}
```

`admin/src/components/LanguageToggle.tsx`:

```tsx
import { useI18n } from '../lib/i18n';
import { Button } from './Button';

export function LanguageToggle() {
  const { lang, setLang } = useI18n();
  const other = lang === 'en' ? 'ar' : 'en';
  return (
    <Button variant="text" lang={other} onClick={() => setLang(other)}>
      {other === 'ar' ? 'العربية' : 'English'}
    </Button>
  );
}
```

- [ ] **Step 10: Create `admin/src/ui.css` and import it**

Add `@import "./ui.css";` to `admin/src/index.css` directly below `@import "./theme/tokens.css";`. Then create `admin/src/ui.css`:

```css
.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  margin: -1px;
  padding: 0;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
  border: 0;
}

/* Shell */
.shell { display: grid; grid-template-columns: 232px minmax(0, 1fr); min-height: 100vh; }
.shell-nav {
  position: sticky; top: 0; height: 100vh;
  display: flex; flex-direction: column; gap: 16px;
  padding: 20px 12px;
  background: var(--mg-white);
  border-inline-end: 1px solid var(--mg-line);
}
.shell-brand { display: flex; align-items: center; gap: 10px; padding-inline: 8px; font: var(--mg-text-title-m); color: var(--mg-navy); }
.shell-nav nav { display: grid; gap: 4px; }
.shell-link {
  display: flex; align-items: center; gap: 10px;
  padding: 8px 10px; border-radius: var(--mg-radius);
  color: var(--mg-slate); text-decoration: none; font: var(--mg-text-label);
}
.shell-link:hover { background: var(--mg-cloud); }
.shell-link.active { background: var(--mg-lime); color: var(--mg-navy); }
.shell-user { margin-block-start: auto; display: grid; gap: 2px; padding: 12px 8px 0; border-block-start: 1px solid var(--mg-line); }
.shell-user-name { font: var(--mg-text-label); color: var(--mg-navy); }
.shell-user-role { font: var(--mg-text-caption); color: var(--mg-muted); }
.shell-user-actions { display: flex; flex-wrap: wrap; gap: 8px; margin-block-start: 6px; }
.shell-main { padding: 24px 32px 64px; min-width: 0; }

@media (max-width: 900px) {
  .shell { grid-template-columns: 1fr; }
  .shell-nav {
    position: static; height: auto;
    flex-direction: row; flex-wrap: wrap; align-items: center;
    padding: 12px 16px;
    border-inline-end: 0; border-block-end: 1px solid var(--mg-line);
  }
  .shell-nav nav { display: flex; flex-wrap: wrap; }
  .shell-user { margin: 0 0 0 auto; margin-inline-start: auto; padding: 0; border: 0; }
  .shell-main { padding: 16px 16px 48px; }
}

/* Pages and cards */
.page { display: grid; gap: 20px; max-width: 1100px; }
.page-header { display: flex; align-items: center; justify-content: space-between; gap: 16px; flex-wrap: wrap; }
.page-header h1 { font: var(--mg-text-title-l); }
.page-intro, .empty { margin: 0; color: var(--mg-muted); }
.page-loading { padding: 24px; color: var(--mg-muted); }
.card { display: grid; gap: 16px; padding: 20px; background: var(--mg-white); border: 1px solid var(--mg-line); border-radius: 12px; }
.card h2 { font: var(--mg-text-title-m); }
.card-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; }

/* Tables */
.mg-table { width: 100%; border-collapse: collapse; background: var(--mg-white); border: 1px solid var(--mg-line); }
.mg-table th, .mg-table td { padding: 10px 12px; text-align: start; vertical-align: middle; border-block-end: 1px solid var(--mg-line); }
.mg-table th { font: var(--mg-text-label); color: var(--mg-muted); background: var(--mg-cloud); }
.mg-table tr:last-child td { border-block-end: 0; }
.mg-table .numeric { font: var(--mg-text-numeric); }
.row-actions { text-align: end; white-space: nowrap; }
.cell-icon { display: inline-flex; align-items: center; gap: 8px; }
@media (max-width: 640px) { .mg-table { display: block; overflow-x: auto; } }

/* Forms */
.mg-field { display: grid; gap: 4px; min-width: 0; }
.mg-field-label { font: var(--mg-text-label); color: var(--mg-navy); }
.mg-field-hint { font: var(--mg-text-caption); color: var(--mg-muted); }
input:not([type='checkbox']):not([type='radio']), select {
  height: var(--mg-control-height); min-width: 0;
  padding-inline: 10px;
  border: 1px solid #c9d0db; border-radius: var(--mg-radius);
  background: var(--mg-white); color: var(--mg-navy); font: var(--mg-text-body);
}
input:disabled, select:disabled { background: var(--mg-cloud); color: var(--mg-muted); }
input[type='checkbox'], input[type='radio'] { width: 16px; height: 16px; accent-color: var(--mg-navy); }
.form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 12px 16px; min-width: 0; margin: 0; padding: 0; border: 0; }
.form-actions { display: flex; justify-content: flex-end; flex-wrap: wrap; gap: 8px; }
.mg-fieldset { display: grid; gap: 10px; min-width: 0; margin: 0; padding: 12px; border: 1px solid var(--mg-line); border-radius: var(--mg-radius); }
.mg-fieldset legend { padding-inline: 4px; font: var(--mg-text-label); color: var(--mg-navy); }
.check-row { display: flex; align-items: center; gap: 8px; color: var(--mg-navy); }
.chip-row { display: flex; flex-wrap: wrap; gap: 8px; }
.check-chip {
  display: inline-flex; align-items: center; gap: 6px;
  padding: 6px 10px; border: 1px solid var(--mg-line); border-radius: 999px;
  background: var(--mg-white); font: var(--mg-text-label); cursor: pointer;
}
.check-chip:has(input:checked) { background: var(--mg-lime); border-color: var(--mg-lime); color: var(--mg-navy); }
.icon-grid { display: grid; grid-template-columns: repeat(auto-fill, 44px); gap: 8px; }
.icon-option {
  position: relative; display: grid; place-items: center; width: 44px; height: 44px;
  border: 1px solid var(--mg-line); border-radius: var(--mg-radius); color: var(--mg-navy); cursor: pointer;
}
.icon-option input { position: absolute; opacity: 0; pointer-events: none; }
.icon-option:has(input:checked) { background: var(--mg-lime); border-color: var(--mg-lime); }
.icon-option:has(input:focus-visible) { outline: 3px solid var(--mg-cyan-ink); outline-offset: 2px; }
.feature-row { display: grid; grid-template-columns: 1fr 1fr auto auto auto; gap: 8px; align-items: center; }
@media (max-width: 640px) { .feature-row { grid-template-columns: 1fr 1fr; } }

/* Hours */
.hours-table td { padding-block: 6px; }
.hours-note { font: var(--mg-text-caption); color: var(--mg-muted); }

/* Publish checklist */
.checklist { display: grid; gap: 6px; margin: 0; padding: 0; list-style: none; }
.checklist .done { color: var(--mg-navy); }
.checklist .todo { color: var(--mg-warning); }

/* Notices, pills, toasts */
.mg-notice { padding: 10px 14px; border-radius: var(--mg-radius); font: var(--mg-text-body); }
.mg-notice--error { background: var(--mg-error-bg); color: var(--mg-error); }
.mg-notice--success { background: var(--mg-success-bg); color: var(--mg-navy); }
.mg-notice--info { background: var(--mg-cloud); color: var(--mg-slate); border: 1px solid var(--mg-line); }
.toast-region { position: fixed; inset-block-end: 16px; inset-inline-end: 16px; z-index: 10; max-width: min(420px, calc(100vw - 32px)); }
.mg-pill { display: inline-block; padding: 3px 9px; border-radius: 999px; font: var(--mg-text-caption); white-space: nowrap; }
.mg-pill--neutral { background: var(--mg-disabled-bg); color: var(--mg-slate); }
.mg-pill--success { background: var(--mg-success-bg); color: var(--mg-navy); }
.mg-pill--warning { background: var(--mg-warning-bg); color: var(--mg-warning); }

/* Dialog */
.mg-dialog {
  width: min(640px, calc(100vw - 32px)); max-height: calc(100vh - 48px);
  padding: 24px; border: 0; border-radius: 12px;
  color: var(--mg-slate); box-shadow: 0 12px 40px rgb(7 19 41 / 0.2);
}
.mg-dialog::backdrop { background: rgb(7 19 41 / 0.45); }
.mg-dialog h2 { margin-block-end: 16px; font: var(--mg-text-title-m); }
.dialog-form { display: grid; gap: 16px; }

/* Sign-in */
.auth-page { display: grid; place-items: center; min-height: 100vh; padding: 16px; }
.auth-card { display: grid; gap: 14px; width: min(380px, 100%); padding: 28px; background: var(--mg-white); border: 1px solid var(--mg-line); border-radius: 12px; }
.auth-card h1 { font: var(--mg-text-title-l); }
.auth-card p { margin: 0; color: var(--mg-muted); }
```

- [ ] **Step 11: Build and commit**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango\admin
npm run build
npm run lint
cd ..
git add admin
git commit -m "feat(admin): router, /api proxy, API client with cookie refresh, EN/AR dictionaries, shared UI"
```

Expected: build succeeds (the new files compile even though nothing uses them yet); lint reports no errors.

---

### Task 3: Operator screens: sport categories and venues

**Files:**
- Create: `admin/src/lib/sportIcons.tsx`, `admin/src/pages/CategoriesPage.tsx`, `admin/src/pages/CategoryDialog.tsx`, `admin/src/pages/StaffForm.tsx`, `admin/src/pages/VenuesPage.tsx`

- [ ] **Step 1: Create `admin/src/lib/sportIcons.tsx`**

```tsx
import {
  Activity, Award, Bike, CircleDot, Dumbbell, Flag, Footprints, Goal,
  Medal, Mountain, Target, Timer, Trophy, Users, Volleyball, Waves,
  type LucideIcon,
} from 'lucide-react';

/** The icons an operator can give a sport. Keys are Lucide names; the Flutter app maps the same keys (M8). */
export const SPORT_ICONS: Record<string, LucideIcon> = {
  volleyball: Volleyball,
  goal: Goal,
  trophy: Trophy,
  dumbbell: Dumbbell,
  target: Target,
  'circle-dot': CircleDot,
  bike: Bike,
  waves: Waves,
  mountain: Mountain,
  footprints: Footprints,
  medal: Medal,
  award: Award,
  flag: Flag,
  timer: Timer,
  activity: Activity,
  users: Users,
};

export const SPORT_ICON_KEYS = Object.keys(SPORT_ICONS);

export function SportIcon({ iconKey, size }: { iconKey: string; size: number }) {
  const Icon = SPORT_ICONS[iconKey] ?? CircleDot;
  return <Icon size={size} aria-hidden="true" />;
}
```

If an icon name doesn't exist in the installed `lucide-react`, replace it with the nearest existing one and keep the key matching its Lucide kebab-case name. Report the change.

- [ ] **Step 2: Create `admin/src/pages/CategoryDialog.tsx`**

```tsx
import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Dialog } from '../components/Dialog';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { SPORT_ICON_KEYS, SportIcon } from '../lib/sportIcons';
import type { Category } from '../lib/types';

type FeatureDraft = { id?: string; nameEn: string; nameAr: string };

const DURATIONS = [30, 60, 90, 120, 150, 180, 210, 240];

export function CategoryDialog({
  category,
  nextSortOrder,
  onClose,
  onSaved,
}: {
  category: Category | 'new' | null;
  nextSortOrder: number;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { t } = useI18n();
  return (
    <Dialog open={category !== null} title={category === 'new' ? t('newCategory') : t('editCategory')} onClose={onClose}>
      {category !== null && (
        <CategoryForm
          initial={category === 'new' ? undefined : category}
          nextSortOrder={nextSortOrder}
          onCancel={onClose}
          onSaved={onSaved}
        />
      )}
    </Dialog>
  );
}

function CategoryForm({
  initial,
  nextSortOrder,
  onCancel,
  onSaved,
}: {
  initial?: Category;
  nextSortOrder: number;
  onCancel: () => void;
  onSaved: () => void;
}) {
  const { t } = useI18n();
  const [nameEn, setNameEn] = useState(initial?.nameEn ?? '');
  const [nameAr, setNameAr] = useState(initial?.nameAr ?? '');
  const [iconKey, setIconKey] = useState(initial?.iconKey ?? SPORT_ICON_KEYS[0]);
  const [durations, setDurations] = useState<number[]>(initial?.durationsMinutes ?? [60, 90]);
  const [sortOrder, setSortOrder] = useState(initial?.sortOrder ?? nextSortOrder);
  const [active, setActive] = useState(initial?.active ?? true);
  const [features, setFeatures] = useState<FeatureDraft[]>(initial?.features ?? []);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  const toggleDuration = (minutes: number) =>
    setDurations((d) => (d.includes(minutes) ? d.filter((x) => x !== minutes) : [...d, minutes].sort((a, b) => a - b)));

  const updateFeature = (index: number, patch: Partial<FeatureDraft>) =>
    setFeatures((list) => list.map((f, n) => (n === index ? { ...f, ...patch } : f)));

  const moveFeature = (index: number, by: -1 | 1) =>
    setFeatures((list) => {
      const target = index + by;
      if (target < 0 || target >= list.length) return list;
      const next = [...list];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (durations.length < 1 || durations.length > 4) {
      setError(t('durationsRule'));
      return;
    }
    setBusy(true);
    setError(undefined);
    const body = { nameEn, nameAr, iconKey, durationsMinutes: durations, sortOrder, active, features };
    try {
      if (initial) await api(`/admin/categories/${initial.id}`, { method: 'PUT', body });
      else await api('/admin/categories', { method: 'POST', body });
      onSaved();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="dialog-form" onSubmit={submit}>
      {error && <Notice tone="error">{error}</Notice>}

      <div className="form-grid">
        <Field label={t('nameEn')}>
          <input required maxLength={60} value={nameEn} onChange={(e) => setNameEn(e.target.value)} />
        </Field>
        <Field label={t('nameAr')}>
          <input required maxLength={60} dir="rtl" lang="ar" value={nameAr} onChange={(e) => setNameAr(e.target.value)} />
        </Field>
      </div>

      <fieldset className="mg-fieldset">
        <legend>{t('icon')}</legend>
        <div className="icon-grid">
          {SPORT_ICON_KEYS.map((key) => (
            <label key={key} className="icon-option" title={key}>
              <input type="radio" name="icon" value={key} checked={iconKey === key} onChange={() => setIconKey(key)} />
              <SportIcon iconKey={key} size={22} />
              <span className="visually-hidden">{key}</span>
            </label>
          ))}
        </div>
      </fieldset>

      <fieldset className="mg-fieldset">
        <legend>{t('bookingLengths')}</legend>
        <div className="chip-row">
          {DURATIONS.map((minutes) => (
            <label key={minutes} className="check-chip">
              <input type="checkbox" checked={durations.includes(minutes)} onChange={() => toggleDuration(minutes)} />
              {t('minutesShort', { m: minutes })}
            </label>
          ))}
        </div>
        <span className="mg-field-hint">{t('durationsRule')}</span>
      </fieldset>

      <fieldset className="mg-fieldset">
        <legend>{t('courtFeatures')}</legend>
        {features.map((feature, index) => (
          <div key={feature.id ?? `new-${index}`} className="feature-row">
            <input
              aria-label={t('nameEn')}
              placeholder={t('nameEn')}
              required
              maxLength={60}
              value={feature.nameEn}
              onChange={(e) => updateFeature(index, { nameEn: e.target.value })}
            />
            <input
              aria-label={t('nameAr')}
              placeholder={t('nameAr')}
              required
              maxLength={60}
              dir="rtl"
              lang="ar"
              value={feature.nameAr}
              onChange={(e) => updateFeature(index, { nameAr: e.target.value })}
            />
            <Button variant="text" aria-label={t('moveUp')} disabled={index === 0} onClick={() => moveFeature(index, -1)}>
              ↑
            </Button>
            <Button
              variant="text"
              aria-label={t('moveDown')}
              disabled={index === features.length - 1}
              onClick={() => moveFeature(index, 1)}
            >
              ↓
            </Button>
            <Button variant="text" onClick={() => setFeatures((list) => list.filter((_, n) => n !== index))}>
              {t('remove')}
            </Button>
          </div>
        ))}
        <div>
          <Button variant="secondary" onClick={() => setFeatures((list) => [...list, { nameEn: '', nameAr: '' }])}>
            {t('addFeature')}
          </Button>
        </div>
      </fieldset>

      <div className="form-grid">
        <Field label={t('sortOrder')}>
          <input type="number" min={0} value={sortOrder} onChange={(e) => setSortOrder(Number(e.target.value))} />
        </Field>
        <label className="check-row">
          <input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} />
          {t('showInApp')}
        </label>
      </div>

      <div className="form-actions">
        <Button variant="secondary" onClick={onCancel}>
          {t('cancel')}
        </Button>
        <Button type="submit" disabled={busy}>
          {t('save')}
        </Button>
      </div>
    </form>
  );
}
```

- [ ] **Step 3: Create `admin/src/pages/CategoriesPage.tsx`**

```tsx
import { useState } from 'react';
import { Button } from '../components/Button';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { api } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { SportIcon } from '../lib/sportIcons';
import type { Category } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { CategoryDialog } from './CategoryDialog';

export function CategoriesPage() {
  const { t, name } = useI18n();
  const categories = useLoad(() => api<Category[]>('/admin/categories'));
  const [editing, setEditing] = useState<Category | 'new' | null>(null);
  const list = categories.data;

  return (
    <div className="page">
      <PageHeader title={t('navCategories')} action={<Button onClick={() => setEditing('new')}>{t('newCategory')}</Button>} />
      <p className="page-intro">{t('categoriesIntro')}</p>
      {categories.error && <Notice tone="error">{categories.error}</Notice>}
      {list?.length === 0 && <p className="empty">{t('noCategories')}</p>}
      {list && list.length > 0 && (
        <table className="mg-table">
          <thead>
            <tr>
              <th>{t('sport')}</th>
              <th>{t('bookingLengths')}</th>
              <th>{t('courtFeatures')}</th>
              <th>{t('status')}</th>
              <th>
                <span className="visually-hidden">{t('actions')}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {list.map((category) => (
              <tr key={category.id}>
                <td>
                  <span className="cell-icon">
                    <SportIcon iconKey={category.iconKey} size={18} />
                    {name(category)}
                  </span>
                </td>
                <td className="numeric">{category.durationsMinutes.map((m) => t('minutesShort', { m })).join(' · ')}</td>
                <td>{category.features.map((f) => name(f)).join(', ') || '—'}</td>
                <td>
                  <Pill tone={category.active ? 'success' : 'neutral'}>{category.active ? t('shownInApp') : t('hidden')}</Pill>
                </td>
                <td className="row-actions">
                  <Button variant="text" onClick={() => setEditing(category)}>
                    {t('edit')}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <CategoryDialog
        category={editing}
        nextSortOrder={(list?.length ?? 0) + 1}
        onClose={() => setEditing(null)}
        onSaved={() => {
          setEditing(null);
          categories.reload();
        }}
      />
    </div>
  );
}
```

- [ ] **Step 4: Create `admin/src/pages/StaffForm.tsx`**

```tsx
import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import type { Role } from '../lib/types';

/** Creates a staff account with a temporary password. `role` is sent only where the endpoint takes one. */
export function StaffForm({
  path,
  role,
  onCancel,
  onSaved,
}: {
  path: string;
  role?: Role;
  onCancel: () => void;
  onSaved: () => void;
}) {
  const { t } = useI18n();
  const [email, setEmail] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [temporaryPassword, setTemporaryPassword] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await api(path, { method: 'POST', body: { email, displayName, temporaryPassword, ...(role ? { role } : {}) } });
      onSaved();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="dialog-form" onSubmit={submit}>
      {error && <Notice tone="error">{error}</Notice>}
      <Field label={t('displayName')}>
        <input required maxLength={80} value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
      </Field>
      <Field label={t('email')}>
        <input type="email" required autoComplete="off" value={email} onChange={(e) => setEmail(e.target.value)} />
      </Field>
      <Field label={t('temporaryPassword')} hint={t('temporaryPasswordHint')}>
        <input
          required
          minLength={10}
          autoComplete="new-password"
          value={temporaryPassword}
          onChange={(e) => setTemporaryPassword(e.target.value)}
        />
      </Field>
      <div className="form-actions">
        <Button variant="secondary" onClick={onCancel}>
          {t('cancel')}
        </Button>
        <Button type="submit" disabled={busy}>
          {t('save')}
        </Button>
      </div>
    </form>
  );
}
```

- [ ] **Step 5: Create `admin/src/pages/VenuesPage.tsx`**

```tsx
import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Dialog } from '../components/Dialog';
import { Field } from '../components/Field';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { api, errorText } from '../lib/api';
import { useI18n } from '../lib/i18n';
import type { VenueSummary } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { StaffForm } from './StaffForm';

export function VenuesPage() {
  const { t, name } = useI18n();
  const venues = useLoad(() => api<VenueSummary[]>('/admin/venues'));
  const [creating, setCreating] = useState(false);
  const [managerFor, setManagerFor] = useState<VenueSummary | null>(null);
  const [notice, setNotice] = useState<string>();
  const list = venues.data;

  return (
    <div className="page">
      <PageHeader title={t('navVenues')} action={<Button onClick={() => setCreating(true)}>{t('newVenue')}</Button>} />
      {notice && <Notice tone="success">{notice}</Notice>}
      {venues.error && <Notice tone="error">{venues.error}</Notice>}
      {list?.length === 0 && <p className="empty">{t('noVenues')}</p>}
      {list && list.length > 0 && (
        <table className="mg-table">
          <thead>
            <tr>
              <th>{t('nameEn')}</th>
              <th>{t('nameAr')}</th>
              <th>
                <span className="visually-hidden">{t('actions')}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {list.map((venue) => (
              <tr key={venue.id}>
                <td>{venue.nameEn}</td>
                <td dir="rtl" lang="ar">
                  {venue.nameAr}
                </td>
                <td className="row-actions">
                  <Button variant="text" onClick={() => setManagerFor(venue)}>
                    {t('addManager')}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <Dialog open={creating} title={t('newVenue')} onClose={() => setCreating(false)}>
        <NewVenueForm
          onCancel={() => setCreating(false)}
          onSaved={() => {
            setCreating(false);
            venues.reload();
          }}
        />
      </Dialog>

      <Dialog
        open={managerFor !== null}
        title={t('addManagerTo', { venue: managerFor ? name(managerFor) : '' })}
        onClose={() => setManagerFor(null)}
      >
        {managerFor && (
          <StaffForm
            path={`/admin/venues/${managerFor.id}/staff`}
            role="VenueManager"
            onCancel={() => setManagerFor(null)}
            onSaved={() => {
              setManagerFor(null);
              setNotice(t('managerAdded'));
            }}
          />
        )}
      </Dialog>
    </div>
  );
}

function NewVenueForm({ onCancel, onSaved }: { onCancel: () => void; onSaved: () => void }) {
  const { t } = useI18n();
  const [nameEn, setNameEn] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await api('/admin/venues', { method: 'POST', body: { nameEn, nameAr } });
      onSaved();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="dialog-form" onSubmit={submit}>
      {error && <Notice tone="error">{error}</Notice>}
      <Field label={t('nameEn')}>
        <input required maxLength={120} value={nameEn} onChange={(e) => setNameEn(e.target.value)} />
      </Field>
      <Field label={t('nameAr')}>
        <input required maxLength={120} dir="rtl" lang="ar" value={nameAr} onChange={(e) => setNameAr(e.target.value)} />
      </Field>
      <div className="form-actions">
        <Button variant="secondary" onClick={onCancel}>
          {t('cancel')}
        </Button>
        <Button type="submit" disabled={busy}>
          {t('save')}
        </Button>
      </div>
    </form>
  );
}
```

- [ ] **Step 6: Build, lint, commit**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango\admin
npm run build
npm run lint
cd ..
git add admin
git commit -m "feat(admin): operator screens for sport categories, venues and venue managers"
```

---

### Task 4: Venue screens: details, hours, courts, publishing, staff

**Files:**
- Create: `admin/src/pages/venue/VenuePage.tsx`, `PublishPanel.tsx`, `DetailsSection.tsx`, `HoursSection.tsx`, `CourtsSection.tsx`, `admin/src/pages/StaffPage.tsx`

The `useAuth` hook is created in Task 5. To keep this task building on its own, `VenuePage` receives `canEdit` as a prop; Task 5's route passes it.

- [ ] **Step 1: Create `admin/src/pages/venue/PublishPanel.tsx`**

```tsx
import { Button } from '../../components/Button';
import { Pill } from '../../components/Pill';
import { useI18n } from '../../lib/i18n';
import { CHECKLIST, MISSING_KEY, type Venue } from '../../lib/types';

export function PublishPanel({
  venue,
  canEdit,
  onPublish,
  onUnpublish,
}: {
  venue: Venue;
  canEdit: boolean;
  onPublish: () => void;
  onUnpublish: () => void;
}) {
  const { t } = useI18n();
  return (
    <section className="card">
      <div className="card-head">
        <h2>{t('appVisibility')}</h2>
        <Pill tone={venue.published ? 'success' : 'neutral'}>{venue.published ? t('liveInApp') : t('notPublished')}</Pill>
      </div>
      {!venue.published && (
        <ul className="checklist">
          {CHECKLIST.map((item) => {
            const done = !venue.missingForPublish.includes(item);
            return (
              <li key={item} className={done ? 'done' : 'todo'}>
                {done ? '✓' : '○'} {t(MISSING_KEY[item])}
              </li>
            );
          })}
        </ul>
      )}
      {canEdit && (
        <div>
          {venue.published ? (
            <Button variant="secondary" onClick={onUnpublish}>
              {t('unpublish')}
            </Button>
          ) : (
            <Button
              onClick={onPublish}
              disabledReason={venue.missingForPublish.length > 0 ? t('finishChecklist') : undefined}
            >
              {t('publish')}
            </Button>
          )}
        </div>
      )}
    </section>
  );
}
```

- [ ] **Step 2: Create `admin/src/pages/venue/DetailsSection.tsx`**

```tsx
import { useState, type FormEvent } from 'react';
import { Button } from '../../components/Button';
import { Field } from '../../components/Field';
import { useI18n } from '../../lib/i18n';
import type { Venue } from '../../lib/types';

export type DetailsInput = {
  nameEn: string;
  nameAr: string;
  addressEn: string;
  addressAr: string;
  latitude: number | null;
  longitude: number | null;
  timeZoneId: string;
};

export function DetailsSection({
  venue,
  canEdit,
  onSave,
}: {
  venue: Venue;
  canEdit: boolean;
  onSave: (input: DetailsInput) => Promise<void>;
}) {
  const { t } = useI18n();
  const [draft, setDraft] = useState({
    nameEn: venue.nameEn,
    nameAr: venue.nameAr,
    addressEn: venue.addressEn ?? '',
    addressAr: venue.addressAr ?? '',
    latitude: venue.latitude?.toString() ?? '',
    longitude: venue.longitude?.toString() ?? '',
  });
  const set = (key: keyof typeof draft) => (value: string) => setDraft((d) => ({ ...d, [key]: value }));
  const toNumber = (value: string) => (value.trim() === '' ? null : Number(value));

  function submit(e: FormEvent) {
    e.preventDefault();
    void onSave({
      nameEn: draft.nameEn,
      nameAr: draft.nameAr,
      addressEn: draft.addressEn,
      addressAr: draft.addressAr,
      latitude: toNumber(draft.latitude),
      longitude: toNumber(draft.longitude),
      timeZoneId: venue.timeZoneId,
    });
  }

  return (
    <section className="card">
      <h2>{t('venueDetails')}</h2>
      <form className="dialog-form" onSubmit={submit}>
        <fieldset className="form-grid" disabled={!canEdit}>
          <Field label={t('nameEn')}>
            <input required maxLength={120} value={draft.nameEn} onChange={(e) => set('nameEn')(e.target.value)} />
          </Field>
          <Field label={t('nameAr')}>
            <input required maxLength={120} dir="rtl" lang="ar" value={draft.nameAr} onChange={(e) => set('nameAr')(e.target.value)} />
          </Field>
          <Field label={t('addressEn')}>
            <input maxLength={200} value={draft.addressEn} onChange={(e) => set('addressEn')(e.target.value)} />
          </Field>
          <Field label={t('addressAr')}>
            <input maxLength={200} dir="rtl" lang="ar" value={draft.addressAr} onChange={(e) => set('addressAr')(e.target.value)} />
          </Field>
          <Field label={t('latitude')} hint={t('locationHint')}>
            <input
              type="number"
              step="any"
              min={-90}
              max={90}
              inputMode="decimal"
              value={draft.latitude}
              onChange={(e) => set('latitude')(e.target.value)}
            />
          </Field>
          <Field label={t('longitude')}>
            <input
              type="number"
              step="any"
              min={-180}
              max={180}
              inputMode="decimal"
              value={draft.longitude}
              onChange={(e) => set('longitude')(e.target.value)}
            />
          </Field>
        </fieldset>
        {canEdit && (
          <div className="form-actions">
            <Button type="submit">{t('save')}</Button>
          </div>
        )}
      </form>
    </section>
  );
}
```

- [ ] **Step 3: Create `admin/src/pages/venue/HoursSection.tsx`**

```tsx
import { useState, type FormEvent } from 'react';
import { Button } from '../../components/Button';
import { useI18n } from '../../lib/i18n';
import { DAY_KEY, DAYS, type Day, type Hours, type Venue } from '../../lib/types';

type Row = { open: boolean; opens: string; closes: string };

function initialRows(venue: Venue): Record<Day, Row> {
  const rows = {} as Record<Day, Row>;
  for (const day of DAYS) {
    const hours = venue.hours.find((h) => h.day === day);
    rows[day] = hours
      ? { open: true, opens: hours.opens.slice(0, 5), closes: hours.closes.slice(0, 5) }
      : { open: false, opens: '16:00', closes: '23:00' };
  }
  return rows;
}

export function HoursSection({
  venue,
  canEdit,
  onSave,
}: {
  venue: Venue;
  canEdit: boolean;
  onSave: (hours: Hours[]) => Promise<void>;
}) {
  const { t } = useI18n();
  const [rows, setRows] = useState(() => initialRows(venue));
  const update = (day: Day, patch: Partial<Row>) => setRows((r) => ({ ...r, [day]: { ...r[day], ...patch } }));

  function submit(e: FormEvent) {
    e.preventDefault();
    void onSave(
      DAYS.filter((day) => rows[day].open).map((day) => ({
        day,
        opens: `${rows[day].opens}:00`,
        closes: `${rows[day].closes}:00`,
      })),
    );
  }

  return (
    <section className="card">
      <h2>{t('openingHours')}</h2>
      <form className="dialog-form" onSubmit={submit}>
        <fieldset className="mg-fieldset" disabled={!canEdit}>
          <legend className="visually-hidden">{t('openingHours')}</legend>
          <table className="mg-table hours-table">
            <thead>
              <tr>
                <th>{t('day')}</th>
                <th>{t('openThisDay')}</th>
                <th>{t('opensAt')}</th>
                <th>{t('closesAt')}</th>
              </tr>
            </thead>
            <tbody>
              {DAYS.map((day) => {
                const row = rows[day];
                return (
                  <tr key={day}>
                    <td>{t(DAY_KEY[day])}</td>
                    <td>
                      <input
                        type="checkbox"
                        aria-label={`${t(DAY_KEY[day])}: ${t('openThisDay')}`}
                        checked={row.open}
                        onChange={(e) => update(day, { open: e.target.checked })}
                      />
                    </td>
                    {row.open ? (
                      <>
                        <td>
                          <input
                            type="time"
                            required
                            aria-label={`${t(DAY_KEY[day])}: ${t('opensAt')}`}
                            value={row.opens}
                            onChange={(e) => update(day, { opens: e.target.value })}
                          />
                        </td>
                        <td>
                          <input
                            type="time"
                            required
                            aria-label={`${t(DAY_KEY[day])}: ${t('closesAt')}`}
                            value={row.closes}
                            onChange={(e) => update(day, { closes: e.target.value })}
                          />{' '}
                          {row.closes <= row.opens && <span className="hours-note">{t('closesNextDay')}</span>}
                        </td>
                      </>
                    ) : (
                      <td colSpan={2} className="hours-note">
                        {t('closedDay')}
                      </td>
                    )}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </fieldset>
        {canEdit && (
          <div className="form-actions">
            <Button type="submit">{t('save')}</Button>
          </div>
        )}
      </form>
    </section>
  );
}
```

- [ ] **Step 4: Create `admin/src/pages/venue/CourtsSection.tsx`**

```tsx
import { useState, type FormEvent } from 'react';
import { Button } from '../../components/Button';
import { Dialog } from '../../components/Dialog';
import { Field } from '../../components/Field';
import { Notice } from '../../components/Notice';
import { Pill } from '../../components/Pill';
import { errorText } from '../../lib/api';
import { useI18n } from '../../lib/i18n';
import { SportIcon } from '../../lib/sportIcons';
import type { Category, Court, CourtInput, Venue } from '../../lib/types';

export function CourtsSection({
  venue,
  categories,
  canEdit,
  onSave,
}: {
  venue: Venue;
  categories: Category[];
  canEdit: boolean;
  /** Throws on failure so the dialog can show the error next to the form. */
  onSave: (courtId: string | null, input: CourtInput) => Promise<void>;
}) {
  const { t, name } = useI18n();
  const [editing, setEditing] = useState<Court | 'new' | null>(null);
  const categoryById = new Map(categories.map((c) => [c.id, c]));

  const featureNames = (court: Court) => {
    const category = categoryById.get(court.categoryId);
    const names = court.featureIds
      .map((id) => category?.features.find((f) => f.id === id))
      .filter((f) => f !== undefined)
      .map((f) => name(f));
    return names.join(', ') || '—';
  };

  return (
    <section className="card">
      <div className="card-head">
        <h2>{t('courts')}</h2>
        {canEdit && (
          <Button
            variant="secondary"
            onClick={() => setEditing('new')}
            disabledReason={categories.length === 0 ? t('noSportsYet') : undefined}
          >
            {t('addCourt')}
          </Button>
        )}
      </div>

      {venue.courts.length === 0 ? (
        <p className="empty">{t('noCourts')}</p>
      ) : (
        <table className="mg-table">
          <thead>
            <tr>
              <th>{t('court')}</th>
              <th>{t('sport')}</th>
              <th>{t('setting')}</th>
              <th>{t('courtFeatures')}</th>
              <th>{t('status')}</th>
              {canEdit && (
                <th>
                  <span className="visually-hidden">{t('actions')}</span>
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {venue.courts.map((court) => {
              const category = categoryById.get(court.categoryId);
              return (
                <tr key={court.id}>
                  <td>{name(court)}</td>
                  <td>
                    {category ? (
                      <span className="cell-icon">
                        <SportIcon iconKey={category.iconKey} size={16} />
                        {name(category)}
                      </span>
                    ) : (
                      t('hiddenSport')
                    )}
                  </td>
                  <td>{court.indoor ? t('indoor') : t('outdoor')}</td>
                  <td>{featureNames(court)}</td>
                  <td>
                    <Pill tone={court.active ? 'success' : 'warning'}>{court.active ? t('courtOpen') : t('courtClosed')}</Pill>
                  </td>
                  {canEdit && (
                    <td className="row-actions">
                      <Button variant="text" onClick={() => setEditing(court)}>
                        {t('edit')}
                      </Button>
                    </td>
                  )}
                </tr>
              );
            })}
          </tbody>
        </table>
      )}

      <Dialog open={editing !== null} title={editing === 'new' ? t('addCourt') : t('editCourt')} onClose={() => setEditing(null)}>
        {editing !== null && (
          <CourtForm
            court={editing === 'new' ? null : editing}
            categories={categories}
            nextSortOrder={venue.courts.length + 1}
            onCancel={() => setEditing(null)}
            onSave={async (input) => {
              await onSave(editing === 'new' ? null : editing.id, input);
              setEditing(null);
            }}
          />
        )}
      </Dialog>
    </section>
  );
}

function CourtForm({
  court,
  categories,
  nextSortOrder,
  onCancel,
  onSave,
}: {
  court: Court | null;
  categories: Category[];
  nextSortOrder: number;
  onCancel: () => void;
  onSave: (input: CourtInput) => Promise<void>;
}) {
  const { t, name } = useI18n();
  const [draft, setDraft] = useState<CourtInput>(
    court
      ? {
          categoryId: court.categoryId,
          nameEn: court.nameEn,
          nameAr: court.nameAr,
          indoor: court.indoor,
          featureIds: court.featureIds,
          sortOrder: court.sortOrder,
          active: court.active,
        }
      : {
          categoryId: categories[0]?.id ?? '',
          nameEn: '',
          nameAr: '',
          indoor: false,
          featureIds: [],
          sortOrder: nextSortOrder,
          active: true,
        },
  );
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const category = categories.find((c) => c.id === draft.categoryId);
  const set = (patch: Partial<CourtInput>) => setDraft((d) => ({ ...d, ...patch }));
  const toggleFeature = (id: string) =>
    set({ featureIds: draft.featureIds.includes(id) ? draft.featureIds.filter((x) => x !== id) : [...draft.featureIds, id] });

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await onSave(draft);
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <form className="dialog-form" onSubmit={submit}>
      {error && <Notice tone="error">{error}</Notice>}
      <Field label={t('sport')}>
        <select required value={draft.categoryId} onChange={(e) => set({ categoryId: e.target.value, featureIds: [] })}>
          {!category && <option value={draft.categoryId}>{t('hiddenSport')}</option>}
          {categories.map((c) => (
            <option key={c.id} value={c.id}>
              {name(c)}
            </option>
          ))}
        </select>
      </Field>
      <div className="form-grid">
        <Field label={t('nameEn')}>
          <input required maxLength={60} value={draft.nameEn} onChange={(e) => set({ nameEn: e.target.value })} />
        </Field>
        <Field label={t('nameAr')}>
          <input required maxLength={60} dir="rtl" lang="ar" value={draft.nameAr} onChange={(e) => set({ nameAr: e.target.value })} />
        </Field>
      </div>
      <label className="check-row">
        <input type="checkbox" checked={draft.indoor} onChange={(e) => set({ indoor: e.target.checked })} />
        {t('indoor')}
      </label>
      {category && category.features.length > 0 && (
        <fieldset className="mg-fieldset">
          <legend>{t('courtFeatures')}</legend>
          <div className="chip-row">
            {category.features.map((feature) => (
              <label key={feature.id} className="check-chip">
                <input type="checkbox" checked={draft.featureIds.includes(feature.id)} onChange={() => toggleFeature(feature.id)} />
                {name(feature)}
              </label>
            ))}
          </div>
        </fieldset>
      )}
      <div className="form-grid">
        <Field label={t('sortOrder')}>
          <input type="number" min={0} value={draft.sortOrder} onChange={(e) => set({ sortOrder: Number(e.target.value) })} />
        </Field>
        <label className="check-row">
          <input type="checkbox" checked={draft.active} onChange={(e) => set({ active: e.target.checked })} />
          {t('courtOpenForBooking')}
        </label>
      </div>
      <div className="form-actions">
        <Button variant="secondary" onClick={onCancel}>
          {t('cancel')}
        </Button>
        <Button type="submit" disabled={busy}>
          {t('save')}
        </Button>
      </div>
    </form>
  );
}
```

- [ ] **Step 5: Create `admin/src/pages/venue/VenuePage.tsx`**

```tsx
import { useEffect, useState } from 'react';
import { Notice } from '../../components/Notice';
import { PageHeader } from '../../components/PageHeader';
import { api, ApiError, errorText } from '../../lib/api';
import { useI18n } from '../../lib/i18n';
import { MISSING_KEY, type Category, type CourtInput, type Hours, type MissingItem, type Venue } from '../../lib/types';
import { useLoad } from '../../lib/useLoad';
import type { MessageKey } from '../../locales/en';
import { CourtsSection } from './CourtsSection';
import { DetailsSection, type DetailsInput } from './DetailsSection';
import { HoursSection } from './HoursSection';
import { PublishPanel } from './PublishPanel';

type Toast = { tone: 'error' | 'success'; text: string };

export function VenuePage({ canEdit }: { canEdit: boolean }) {
  const { t, name } = useI18n();
  const venue = useLoad(() => api<Venue>('/venue'));
  const categories = useLoad(() => api<Category[]>('/categories'));
  const [toast, setToast] = useState<Toast>();

  // Success messages fade after a few seconds; errors stay until the next action.
  useEffect(() => {
    if (toast?.tone !== 'success') return;
    const timer = setTimeout(() => setToast(undefined), 5000);
    return () => clearTimeout(timer);
  }, [toast]);

  async function run(action: () => Promise<Venue>, success: MessageKey) {
    setToast(undefined);
    try {
      venue.setData(await action());
      setToast({ tone: 'success', text: t(success) });
    } catch (err) {
      const missing =
        err instanceof ApiError && err.missing
          ? ` ${err.missing.map((m) => t(MISSING_KEY[m as MissingItem])).join(', ')}`
          : '';
      setToast({ tone: 'error', text: errorText(err) + missing });
    }
  }

  async function saveCourt(courtId: string | null, input: CourtInput) {
    const updated = await api<Venue>(courtId ? `/venue/courts/${courtId}` : '/venue/courts', {
      method: courtId ? 'PUT' : 'POST',
      body: input,
    });
    venue.setData(updated);
    setToast({ tone: 'success', text: t('saved') });
  }

  if (venue.error) {
    return (
      <div className="page">
        <Notice tone="error">{venue.error}</Notice>
      </div>
    );
  }
  if (!venue.data || !categories.data) return <p className="page-loading">{t('loading')}</p>;
  const current = venue.data;

  return (
    <div className="page">
      <PageHeader title={name(current)} />
      {!canEdit && <Notice tone="info">{t('readOnlyVenue')}</Notice>}
      <PublishPanel
        venue={current}
        canEdit={canEdit}
        onPublish={() => void run(() => api<Venue>('/venue/publish', { method: 'POST' }), 'publishedNotice')}
        onUnpublish={() => void run(() => api<Venue>('/venue/unpublish', { method: 'POST' }), 'unpublishedNotice')}
      />
      <DetailsSection
        venue={current}
        canEdit={canEdit}
        onSave={(input: DetailsInput) => run(() => api<Venue>('/venue', { method: 'PUT', body: input }), 'saved')}
      />
      <HoursSection
        venue={current}
        canEdit={canEdit}
        onSave={(hours: Hours[]) => run(() => api<Venue>('/venue/hours', { method: 'PUT', body: hours }), 'saved')}
      />
      <CourtsSection venue={current} categories={categories.data} canEdit={canEdit} onSave={saveCourt} />
      <div className="toast-region" aria-live="polite">
        {toast && <Notice tone={toast.tone}>{toast.text}</Notice>}
      </div>
    </div>
  );
}
```

- [ ] **Step 6: Create `admin/src/pages/StaffPage.tsx`**

```tsx
import { useState } from 'react';
import { Button } from '../components/Button';
import { Dialog } from '../components/Dialog';
import { Notice } from '../components/Notice';
import { PageHeader } from '../components/PageHeader';
import { Pill } from '../components/Pill';
import { api } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { ROLE_KEY, type StaffSummary } from '../lib/types';
import { useLoad } from '../lib/useLoad';
import { StaffForm } from './StaffForm';

export function StaffPage() {
  const { t } = useI18n();
  const staff = useLoad(() => api<StaffSummary[]>('/venue/staff'));
  const [adding, setAdding] = useState(false);
  const [notice, setNotice] = useState<string>();
  const list = staff.data;

  return (
    <div className="page">
      <PageHeader title={t('navStaff')} action={<Button onClick={() => setAdding(true)}>{t('addDeskStaff')}</Button>} />
      {notice && <Notice tone="success">{notice}</Notice>}
      {staff.error && <Notice tone="error">{staff.error}</Notice>}
      {list?.length === 0 && <p className="empty">{t('noStaff')}</p>}
      {list && list.length > 0 && (
        <table className="mg-table">
          <thead>
            <tr>
              <th>{t('displayName')}</th>
              <th>{t('email')}</th>
              <th>{t('role')}</th>
              <th>{t('status')}</th>
            </tr>
          </thead>
          <tbody>
            {list.map((member) => (
              <tr key={member.id}>
                <td>{member.displayName}</td>
                <td dir="ltr">{member.email}</td>
                <td>{t(ROLE_KEY[member.role])}</td>
                <td>
                  <Pill tone={member.active ? 'success' : 'neutral'}>{member.active ? t('active') : t('inactive')}</Pill>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <Dialog open={adding} title={t('addDeskStaff')} onClose={() => setAdding(false)}>
        <StaffForm
          path="/venue/staff"
          onCancel={() => setAdding(false)}
          onSaved={() => {
            setAdding(false);
            setNotice(t('staffAdded'));
            staff.reload();
          }}
        />
      </Dialog>
    </div>
  );
}
```

- [ ] **Step 7: Build, lint, commit**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango\admin
npm run build
npm run lint
cd ..
git add admin
git commit -m "feat(admin): venue setup screens (publish checklist, details, hours, courts) and desk staff"
```

---

### Task 5: Sign-in, password change, shell and routes

**Files:**
- Create: `admin/src/lib/auth.tsx`, `admin/src/app/guards.tsx`, `admin/src/pages/SignInPage.tsx`, `admin/src/pages/ChangePasswordPage.tsx`, `admin/src/layout/Shell.tsx`
- Modify: `admin/src/App.tsx` (replace)

- [ ] **Step 1: Create `admin/src/lib/auth.tsx`**

```tsx
import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { api, onSessionEnded, refreshSession, setAccessToken } from './api';
import type { Staff } from './types';

type Status = 'loading' | 'signedOut' | 'signedIn';

type Auth = {
  status: Status;
  staff: Staff | null;
  signIn: (email: string, password: string) => Promise<void>;
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<Auth | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<Status>('loading');
  const [staff, setStaff] = useState<Staff | null>(null);

  const end = useCallback(() => {
    setAccessToken(null);
    setStaff(null);
    setStatus('signedOut');
  }, []);

  const loadMe = useCallback(async () => {
    setStaff(await api<Staff>('/staff/me'));
    setStatus('signedIn');
  }, []);

  // Restore the session from the HttpOnly cookie on every page load.
  useEffect(() => {
    onSessionEnded(end);
    refreshSession()
      .then((ok) => (ok ? loadMe() : end()))
      .catch(end);
  }, [end, loadMe]);

  const signIn = async (email: string, password: string) => {
    const result = await api<{ accessToken: string }>('/staff/auth/login?session=cookie', {
      method: 'POST',
      body: { email, password },
    });
    setAccessToken(result.accessToken);
    await loadMe();
  };

  const changePassword = async (currentPassword: string, newPassword: string) => {
    const result = await api<{ accessToken: string }>('/staff/me/password?session=cookie', {
      method: 'POST',
      body: { currentPassword, newPassword },
    });
    setAccessToken(result.accessToken);
    await loadMe();
  };

  const signOut = async () => {
    await api('/auth/logout', { method: 'POST', body: {} }).catch(() => undefined);
    end();
  };

  return (
    <AuthContext.Provider value={{ status, staff, signIn, changePassword, signOut }}>{children}</AuthContext.Provider>
  );
}

export function useAuth(): Auth {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
```

- [ ] **Step 2: Create `admin/src/pages/SignInPage.tsx`**

```tsx
import { useState, type FormEvent } from 'react';
import { Navigate, useNavigate } from 'react-router';
import { Button } from '../components/Button';
import { Field } from '../components/Field';
import { LanguageToggle } from '../components/LanguageToggle';
import { Notice } from '../components/Notice';
import { errorText } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';

export function SignInPage() {
  const { status, signIn } = useAuth();
  const { t } = useI18n();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  if (status === 'signedIn') return <Navigate to="/" replace />;

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await signIn(email, password);
      navigate('/', { replace: true });
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <div className="auth-page">
      <form className="auth-card" onSubmit={submit}>
        <img src="/midango-icon.png" alt="Midango" height={40} />
        <h1>{t('signInTitle')}</h1>
        {error && <Notice tone="error">{error}</Notice>}
        <Field label={t('email')}>
          <input type="email" required autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} />
        </Field>
        <Field label={t('password')}>
          <input
            type="password"
            required
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </Field>
        <Button type="submit" disabled={busy}>
          {t('signIn')}
        </Button>
        <LanguageToggle />
      </form>
    </div>
  );
}
```

- [ ] **Step 3: Create `admin/src/pages/ChangePasswordPage.tsx`**

```tsx
import { useState, type FormEvent } from 'react';
import { Button } from '../components/Button';
import { Field } from '../components/Field';
import { LanguageToggle } from '../components/LanguageToggle';
import { Notice } from '../components/Notice';
import { errorText } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';

const MIN_LENGTH = 10;

export function ChangePasswordPage() {
  const { changePassword, signOut } = useAuth();
  const { t } = useI18n();
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (next.length < MIN_LENGTH) return setError(t('passwordTooShort'));
    if (next !== confirm) return setError(t('passwordsDontMatch'));
    setBusy(true);
    setError(undefined);
    try {
      await changePassword(current, next);
    } catch (err) {
      setError(errorText(err));
      setBusy(false);
    }
  }

  return (
    <div className="auth-page">
      <form className="auth-card" onSubmit={submit}>
        <img src="/midango-icon.png" alt="Midango" height={40} />
        <h1>{t('changePasswordTitle')}</h1>
        <p>{t('changePasswordIntro')}</p>
        {error && <Notice tone="error">{error}</Notice>}
        <Field label={t('currentPassword')}>
          <input type="password" required autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} />
        </Field>
        <Field label={t('newPassword')} hint={t('passwordTooShort')}>
          <input type="password" required minLength={MIN_LENGTH} autoComplete="new-password" value={next} onChange={(e) => setNext(e.target.value)} />
        </Field>
        <Field label={t('confirmPassword')}>
          <input type="password" required autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
        </Field>
        <Button type="submit" disabled={busy}>
          {t('savePassword')}
        </Button>
        <Button variant="text" onClick={() => void signOut()}>
          {t('signOut')}
        </Button>
        <LanguageToggle />
      </form>
    </div>
  );
}
```

- [ ] **Step 4: Create `admin/src/layout/Shell.tsx`**

```tsx
import { Building2, MapPin, Shapes, Users, type LucideIcon } from 'lucide-react';
import { NavLink, Outlet } from 'react-router';
import { Button } from '../components/Button';
import { LanguageToggle } from '../components/LanguageToggle';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import { ROLE_KEY, type Role } from '../lib/types';
import type { MessageKey } from '../locales/en';

type NavItem = { to: string; label: MessageKey; icon: LucideIcon; roles: Role[] };

/** Only screens that work for the role are listed. */
const NAV: NavItem[] = [
  { to: '/categories', label: 'navCategories', icon: Shapes, roles: ['PlatformOperator'] },
  { to: '/venues', label: 'navVenues', icon: Building2, roles: ['PlatformOperator'] },
  { to: '/venue', label: 'navVenue', icon: MapPin, roles: ['VenueManager', 'DeskStaff'] },
  { to: '/staff', label: 'navStaff', icon: Users, roles: ['VenueManager'] },
];

export function Shell() {
  const { staff, signOut } = useAuth();
  const { t } = useI18n();
  if (!staff) return null;

  return (
    <div className="shell">
      <aside className="shell-nav">
        <div className="shell-brand">
          <img src="/midango-icon.png" alt="Midango" height={28} />
          <span>{t('appBusiness')}</span>
        </div>
        <nav aria-label={t('navLabel')}>
          {NAV.filter((item) => item.roles.includes(staff.role)).map((item) => (
            <NavLink key={item.to} to={item.to} className="shell-link">
              <item.icon size={18} aria-hidden="true" />
              {t(item.label)}
            </NavLink>
          ))}
        </nav>
        <div className="shell-user">
          <span className="shell-user-name">{staff.displayName}</span>
          <span className="shell-user-role">{t(ROLE_KEY[staff.role])}</span>
          <div className="shell-user-actions">
            <LanguageToggle />
            <Button variant="text" onClick={() => void signOut()}>
              {t('signOut')}
            </Button>
          </div>
        </div>
      </aside>
      <main className="shell-main">
        <Outlet />
      </main>
    </div>
  );
}
```

- [ ] **Step 5: Create `admin/src/app/guards.tsx`**

```tsx
import type { ReactNode } from 'react';
import { Navigate, Outlet } from 'react-router';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import type { Role } from '../lib/types';
import { ChangePasswordPage } from '../pages/ChangePasswordPage';

/** Signed-in staff only. A temporary password must be replaced before anything else. */
export function RequireStaff() {
  const { status, staff } = useAuth();
  const { t } = useI18n();
  if (status === 'loading') return <p className="page-loading">{t('loading')}</p>;
  if (status === 'signedOut' || !staff) return <Navigate to="/sign-in" replace />;
  if (staff.mustChangePassword) return <ChangePasswordPage />;
  return <Outlet />;
}

export function RequireRole({ roles, children }: { roles: Role[]; children: ReactNode }) {
  const { staff } = useAuth();
  return staff && roles.includes(staff.role) ? <>{children}</> : <Navigate to="/" replace />;
}

export function HomeRedirect() {
  const { staff } = useAuth();
  return <Navigate to={staff?.role === 'PlatformOperator' ? '/categories' : '/venue'} replace />;
}
```

- [ ] **Step 6: Replace `admin/src/App.tsx`**

```tsx
import { BrowserRouter, Navigate, Route, Routes } from 'react-router';
import { HomeRedirect, RequireRole, RequireStaff } from './app/guards';
import { Shell } from './layout/Shell';
import { AuthProvider, useAuth } from './lib/auth';
import { I18nProvider } from './lib/i18n';
import { CategoriesPage } from './pages/CategoriesPage';
import { SignInPage } from './pages/SignInPage';
import { StaffPage } from './pages/StaffPage';
import { VenuePage } from './pages/venue/VenuePage';
import { VenuesPage } from './pages/VenuesPage';
import { Styleguide } from './Styleguide';

function VenueRoute() {
  const { staff } = useAuth();
  return <VenuePage canEdit={staff?.role === 'VenueManager'} />;
}

export default function App() {
  return (
    <I18nProvider>
      <AuthProvider>
        <BrowserRouter>
          <Routes>
            <Route path="/styleguide" element={<Styleguide />} />
            <Route path="/sign-in" element={<SignInPage />} />
            <Route element={<RequireStaff />}>
              <Route element={<Shell />}>
                <Route index element={<HomeRedirect />} />
                <Route
                  path="categories"
                  element={
                    <RequireRole roles={['PlatformOperator']}>
                      <CategoriesPage />
                    </RequireRole>
                  }
                />
                <Route
                  path="venues"
                  element={
                    <RequireRole roles={['PlatformOperator']}>
                      <VenuesPage />
                    </RequireRole>
                  }
                />
                <Route
                  path="venue"
                  element={
                    <RequireRole roles={['VenueManager', 'DeskStaff']}>
                      <VenueRoute />
                    </RequireRole>
                  }
                />
                <Route
                  path="staff"
                  element={
                    <RequireRole roles={['VenueManager']}>
                      <StaffPage />
                    </RequireRole>
                  }
                />
              </Route>
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </BrowserRouter>
      </AuthProvider>
    </I18nProvider>
  );
}
```

- [ ] **Step 7: Build, lint, commit**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango\admin
npm run build
npm run lint
cd ..
git add admin
git commit -m "feat(admin): sign-in with cookie sessions, forced password change, role-based shell and routes"
```

Expected: build succeeds; lint reports no errors (warnings about fast-refresh exports in `lib/*.tsx` are acceptable; list them in the report).

---

### Task 6: Browser walkthrough (controller)

Run the API on 5300 and `npm run dev` in `admin/` (http://localhost:5173), then in a browser:

- [ ] **Operator:** sign in with `operator@midango.local` / `Operator!2026dev`. You land on Sport categories. Create "Padel / بادل" with the volleyball icon, 60/90/120, and features Panoramic / بانورامي. Edit it to add Floodlit / مضاء. Create a venue "Arena Padel Club / نادي أرينا بادل" and add a manager with a temporary password.
- [ ] **Session:** reload the page. You stay signed in, and no request or response body ever contains a refresh token (check the Network tab). Sign out, then reload: you get the sign-in page.
- [ ] **Manager:** sign in with the temporary password. The password-change screen appears first. Change the password. Venue shows "Not published" with four ○ items. Fill in details, then hours (one day ending at 02:00 shows "Closes the next day"), then add a court with Panoramic. Each step ticks off its item. Publish shows "Live". Closing the last court refuses with the server's message. Add desk staff.
- [ ] **Desk staff:** after the forced password change, sees only Venue, read-only (no Save buttons, the read-only notice is shown).
- [ ] **Arabic:** switch to العربية. The whole shell mirrors (sidebar on the right), every label is Arabic, and Arabic inputs type right-to-left. Reload: the choice is kept.
- [ ] **Narrow width (400 px):** the sidebar becomes a top bar, tables scroll, nothing overflows the page.
- [ ] Re-run the M2 and M3a API check scripts: `Failures: 0`.
- [ ] Tick M3b in the roadmap (`Business shell & catalog screens` → `… ✅ done`) and commit.

---

## What M3b deliberately leaves out

- **Map picker:** latitude and longitude are typed in. A picker is its own piece of work and produces the same two numbers.
- **Arabic error messages from the server:** the API answers in English. Localised problem titles (Accept-Language) are added to the roadmap.
- **Editing and deactivating staff, resetting passwords, operators editing venues:** come later, when needed.
- **Schedule, Bookings, Customers, Today:** M6, after the booking engine (M5).
