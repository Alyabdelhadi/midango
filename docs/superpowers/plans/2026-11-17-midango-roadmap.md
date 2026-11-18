# Midango Roadmap

**Date:** 2026-11-17
**Source of truth for design:** `docs/research/midango-design-research.html` (brand decisions D1–D10 approved, Q1–Q6 answered)

## Fixed decisions this roadmap builds on

| Topic | Decision |
|---|---|
| Codebase | **Start fresh.** New repo `C:\Users\Lenovo\Desktop\Ali\midango`. Velocity Play (`yallaplay`) is reference only; nothing is imported from it. |
| Stack | .NET 10 API · PostgreSQL 18 · React (Vite + TypeScript) for MIDANGO Business and Desk · Flutter for the MIDANGO App |
| Scope v1 | Courts only. Classes and events come later on the same slot model. |
| Languages | English + Arabic, full RTL in both the admin and the app |
| Payment | **No online payment.** Customers pay cash at the venue. |
| Privacy | Venue staff never see a customer's full phone number (masked, write-only on staff-created bookings) |
| Sports | Sport categories are created by the Midango platform admin, not hardcoded |
| Logo | *Motion Play* is canonical. Vector redraw is still outstanding. |
| Testing | No automated tests (project rule). Each task is verified by building and running it. |

## Plans, in order

Each plan ships something that runs on its own. A plan is written in detail only when the previous one is done.

| # | Plan | Delivers | Depends on |
|---|---|---|---|
| **M1** | **Foundations** ✅ done | Repo; brand assets; API host with a database health check; React admin and Flutter app projects carrying the approved tokens, fonts, buttons, status tags and slot chip, each with a styleguide screen working in English and Arabic | — |
| M2 | Identity & access ✅ done | API only: customer sign-in by phone code (dev mode logs the code until an SMS provider is chosen); staff sign-in with three roles: *platform operator*, *venue manager*, *desk staff*; venue-scoped staff management; masked phone value type | M1 |
| M3a | Catalog API ✅ done | Sport categories (platform operator: EN/AR names, icon key, court features, durations, order, active; public list for the app); venue details, opening hours (past-midnight closing), courts with features; publish checklist | M2 |
| M3b | Business shell & catalog screens ✅ done | Business sign-in + forced password change + CORS; i18n EN/AR in React; sidebar (only items that work); sport categories screen; first-run venue setup wizard; courts & hours screens | M3a |
| M4 | Prices ✅ done | Weekly price board per court (days × time bands), price preview line ("Fri 20:00, 90 min = SAR 270"), VAT handling | M3 |
| M5 | Availability & booking engine ✅ done | Slot calculation across courts and durations; database-enforced no-overlap (exclusion constraint); short hold while the confirm sheet is open; create / cancel booking with *cash at venue*; cancellation window | M4 |
| M6a | Venue operations API ✅ done | Schedule, staff and walk-in bookings, blocked time, move, cancel, bookings search, customers (masked phone), Today numbers, free-cancellation setting | M5 |
| M6b | Business: Schedule, Bookings, Customers, Today ✅ done | The court × time grid (click to book, drag to move, block time), booking side panel with state-driven primary action, Bookings list with quick filters, Customers (name + masked phone), Today numbers | M6a |
| M7 | MIDANGO Desk ✅ done | Counter screen: Now & next list, *Take cash & check in*, walk-ins, QR lookup, undo, mark no-show | M6 |
| M8 | MIDANGO App: booking ✅ done | Play tab (sport chips from categories, venue cards with open times), filters, venue page, confirm sheet, confirmation with QR, Bookings tab, cancel, Profile, guest browsing with sign-in at confirm, Arabic | M5 (M2 for sign-in) |
| M9a | No-show limit & reminders (server) ✅ done | Rules page (operator), no-show limit, push devices, reminder job (FCM when keyed, log otherwise) | M7, M8 |
| M9b | Push in the app 🟡 built (phone test pending) | firebase_messaging (Android), device registration, open booking from a reminder | M9a + Firebase (done) |
| MM | Matchmaking — MMa ✅ done (server + app), MMb desk next | Open matches: join a booking, split at the desk, levels | M9b |
| LY | Loyalty | Points for checked-in games, tiers, redeem at the desk | MM |
| T | Tournaments | Venue events, team sign-up, draw and results | MM, LY |
| M10 | Reports ✅ done | Occupancy heatmap, cash collected by court and week, no-show rate | M6 |
| W1 | Website ✅ done | Static EN/AR informative site (`website/`), demo form → `POST /leads`, operator "Venue requests" page | M2 |
| A1 | Venue control ✅ done | Operator opens any venue (1-h acting token, change log), suspend/unsuspend, staff deactivate + reset password | M6 |
| A1b | Photos & icons ✅ done | Venue logo + up to 10 ordered photos, uploaded sport / feature icons; shown in the app | A1 |
| L1 | Legal pages ✅ done | Privacy policy + terms (EN/AR drafts for lawyer review), operator editor, website /privacy /terms, app Profile links | M2 |

## Later (not planned)

Online payment and split payment · dark theme · reviews · favourites · classes and events · loyalty · multi-venue switcher for owners · production app icons and favicon from the vector logo.

## Open items that block specific plans

- **M2:** SMS/OTP provider for Saudi phone numbers.
- **M9:** confirm the no-show numbers (3 h reminder; 2 no-shows in 30 days → 1 upcoming booking).
- **Before store release:** vector logo (the guide requires a redraw before production).
