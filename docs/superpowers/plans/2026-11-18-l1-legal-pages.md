# L1 Legal Pages Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A privacy policy and terms & conditions in English and Arabic, edited by the operator in the admin, shown on the website and in the app.

**Design (approved 2026-11-18, "Editable in admin"):**
- **Storage:** table `legal_documents` — `Kind` ("privacy" | "terms", key), `BodyEn`, `BodyAr` (text, ≤ 60 000 chars each), `UpdatedAt`. Seeded once at startup (when a kind is missing) from draft files `api/src/Midango.Api/Platform/Legal/{privacy,terms}.{en,ar}.md` copied to the output.
- **Format:** a small Markdown subset only — `# ` / `## ` headings, paragraphs (blank line), `- ` bullet lists, `**bold**`. Every client renders it with a tiny renderer that escapes text first (never `innerHTML` of raw input).
- **API:** public `GET /legal/{kind}` → `{ kind, bodyEn, bodyAr, updatedAt }` (`Cache-Control: public, max-age=300`); 404 for unknown kinds. Operator `GET /admin/legal/{kind}`, `PUT /admin/legal/{kind}` `{ bodyEn, bodyAr }` → both non-empty, ≤ 60 000 chars; sets `UpdatedAt`.
- **Drafts:** written for a court-booking marketplace in Saudi Arabia/GCC (mentions the Saudi Personal Data Protection Law), describing what Midango actually collects: phone number, name, bookings, no-show history, push token, device language; venues see a masked phone and the name the customer gave; cash at the venue, no card data. Each draft starts with a visible line: "Draft — to be reviewed by a lawyer before launch." Arabic is a proper translation, not machine-literal.
- **Admin:** operator page "Legal pages" with two tabs (Privacy / Terms), side-by-side EN and AR textareas (AR `dir="rtl"`), a live preview below each, "Last updated {date}", Save.
- **Website:** `privacy/index.html`, `terms/index.html`, `ar/privacy/index.html`, `ar/terms/index.html` sharing `legal.js` that fetches `${data-api}/legal/{kind}` and renders the language of the page; loading and error states; footer links on every page (both languages).
- **App:** Profile tab gets "Privacy policy" and "Terms & conditions" rows opening a `LegalPage` that fetches `/legal/{kind}` and renders it (headings, paragraphs, bullets, bold) in the app language; error with retry.

Project rules: **no automated tests** — verify by building and running. Bilingual EN + AR with RTL.

---

### Task 1: API + drafts
- `Platform/LegalDocument.cs` (entity), `Platform/LegalEndpoints.cs` (`MapLegalEndpoints`, `SeedLegalAsync` called in Program after migrations — **also outside Development**, since the table must have content in production), DbSet + config (`Kind` max 16 key), migration `LegalDocuments`, the four `.md` drafts with `<None Update="Platform/Legal/*.md" CopyToOutputDirectory="PreserveNewest" />` in the csproj.
- Commit `feat(api): editable privacy policy and terms with drafts`.

### Task 2: Admin
- `pages/LegalPage.tsx`, route `legal` (operator), nav item after Venue requests (lucide `Scale`), `lib/markdown.tsx` (renders the subset to React elements — no `dangerouslySetInnerHTML`), locales EN/AR. Build + lint 0 errors. Don't commit.

### Task 3: Website
- Four pages reusing the site's header/footer/styles, `legal.js` (same subset renderer, builds DOM nodes with `textContent`), footer links added to `index.html` and `ar/index.html` and the four new pages, hreflang pairs. Don't commit.

### Task 4: App
- `app/lib/profile/legal_page.dart` + rows in `profile_tab.dart`, strings EN/AR, a small renderer for the subset. `flutter analyze` clean. Don't commit.

### Task 5: Verify (controller)
- API: public GET both kinds (EN+AR non-empty, draft banner line present), unknown kind 404, PUT as operator updates `updatedAt`, empty/too long → 400, manager → 403; HTML in the body (`<script>`) shows as text on every client.
- Browser: edit in admin (EN + AR), see it on the website pages (both languages, 390 px) and the Flutter web app's Profile → Privacy policy.

---

## Verification (2026-11-18)

**API:**
- Both kinds are seeded at startup: privacy EN 6,349 / AR 5,614 characters; terms EN 5,855 / AR 5,047. Each has the lawyer-review draft line.
- `Cache-Control: max-age=300`. An unknown kind gets 404. A blank English body or one over 60,000 characters gets 400. Anonymous admin GET gets 401. Saving changes `updatedAt`.

**Browser:**
- **Website, Arabic privacy page at 390 px:** loads from the API, right to left, "Last updated" with Western digits, the draft line in bold, 31 list items, page title from the heading, no horizontal scroll.
- **Admin "Legal pages", in Arabic:** English and Arabic side by side with live previews. Typed `<script>alert(1)</script>` showed as text and no script element was created. The "unsaved changes" note appeared on the tab. The test edit was not saved.
- **Flutter web app:** Profile shows "Privacy policy" / "Terms & conditions", also when signed out. In Arabic, the terms page renders headings, bullets and "Last updated" right to left. (The running `flutter run` web server had to be restarted to pick up the new code; reloading the page doesn't rebuild it.)

**Placeholders for the owner to fill before launch:** company legal name, CR number and address; hosting provider; SMS provider; retention months, reply days and minimum age; liability cap; city for court disputes; `privacy@` / `support@` addresses. **The drafts must be reviewed by a lawyer.**
