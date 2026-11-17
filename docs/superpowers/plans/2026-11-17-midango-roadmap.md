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
| M3 | Catalog & Business shell | Business sign-in + forced password change + CORS; sport categories screen (platform operator: EN/AR names, icon key, court features, default durations, order, active); venues, courts, opening hours; Business sidebar (only items that work); first-run venue setup wizard; i18n in React | M2 |
| M4 | Prices | Weekly price board per court (days × time bands), price preview line ("Fri 20:00, 90 min = SAR 270"), VAT handling | M3 |
| M5 | Availability & booking engine | Slot calculation across courts and durations; database-enforced no-overlap (exclusion constraint); short hold while the confirm sheet is open; create / cancel booking with *cash at venue*; cancellation window | M4 |
| M6 | Business: Schedule, Bookings, Customers, Today | The court × time grid (click to book, drag to move, block time), booking side panel with state-driven primary action, Bookings list with quick filters, Customers (name + masked phone), Today numbers | M5 |
| M7 | MIDANGO Desk | Counter screen: Now & next list, *Take cash & check in*, walk-ins, QR lookup, undo, mark no-show | M6 |
| M8 | MIDANGO App: booking | Play tab (sport chips from categories, venue cards with open times), filters, venue page, confirm sheet, confirmation with QR, Bookings tab, cancel, Profile, guest browsing with sign-in at confirm, Arabic | M5 (M2 for sign-in) |
| M9 | No-shows & reminders | Push notifications (FCM), "Still coming?" reminder 3 h before, no-show limit (2 in 30 days → 1 upcoming booking), settings for those numbers | M7, M8 |
| M10 | Reports | Occupancy heatmap, cash collected by court and week, no-show rate | M6 |

## Later (not planned)

Online payment and split payment · dark theme · reviews · favourites · classes and events · loyalty · multi-venue switcher for owners · production app icons and favicon from the vector logo.

## Open items that block specific plans

- **M2:** SMS/OTP provider for Saudi phone numbers.
- **M9:** confirm the no-show numbers (3 h reminder; 2 no-shows in 30 days → 1 upcoming booking).
- **Before store release:** vector logo (the guide requires a redraw before production).
