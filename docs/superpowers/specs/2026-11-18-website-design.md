# MIDANGO website — design

Approved 2026-11-18.

## Goal
One informative site for two audiences: players (what Midango is, get the app) and venue owners (run your venue on Midango, request a demo). Bilingual EN at `/`, AR at `/ar/` (RTL). No live venue data.

## Decisions
- **Plain static HTML + CSS + one small JS file**, no build step, no new dependencies. Folder `website/`. Best for SEO and speed; the cost is the header/footer written once per language.
- **Brand** = the admin's tokens: navy `#071329`, slate `#202b43`, cyan `#00cdeb` / ink `#00788a`, lime `#a8ff00`, cloud `#f5f7fa`; Space Grotesk headings, Inter body, IBM Plex Sans Arabic for AR. Logo/icon from `assets/brand/`.
- **Sections:** hero (two paths: "Book a court" / "For venues") → players: 3 steps (pick sport → pick time → show QR at the desk), store badges marked "coming soon" → venues: schedule, desk & cash check-in, no-show rules, reports → demo form → FAQ → footer with language switch.
- **Demo form → API.** Public `POST /leads` (rate-limited with the auth limiter, honeypot field `website`, field validation) saves to `venue_leads`. Operator-only `GET /admin/leads` and `PUT /admin/leads/{id}/contacted`. New admin page "Venue requests" (operator) lists newest first, mark/unmark contacted.
- The form reads the API base from `data-api` on the `<form>` (localhost:5300 in dev; changed at deploy). Failure shows an inline error and keeps the typed values.
- Dev: served on :5500 with the admin's installed Vite; `http://localhost:5500` added to dev CORS origins.

## Out of scope
Blog, analytics, cookie banner (no tracking), live venues, real store links (app not published), email notification of new leads.

## Verification
Build API + admin; script: valid lead 202, invalid 400, honeypot 202-but-not-saved, non-operator 403, list + contacted toggle. Browser: both languages at 390px and 1280px, submit the form, see it in the admin, toggle contacted, Arabic admin page.
