# A4 Website Content (operator) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Midango operator edits the website's main texts (EN/AR), the FAQ, the contact details and (via A3) the store links from Business, and the website shows them without a redeploy.

**Defaults (stated to the owner, no extra questions):**
- Editable texts are a fixed set of keys (headlines, intros, the three player steps, the stores note, section titles, footer line), not every word on the page. The feature cards and the hero illustration stay as designed.
- The static HTML keeps today's wording as the fallback: the site fetches the content on load and swaps it in. If the API is down the page still reads well. ponytail: crawlers that don't run JS index the fallback text; render at build time if SEO needs the edited copy.
- FAQ: ordered list of question/answer pairs, EN + AR, max 20; seeded with today's six.
- Contact: email, phone, WhatsApp number, Instagram handle (all optional). The footer shows a Contact line with the ones that are set.
- Store links come from A3's App content (one source). When a link is set, the matching "Coming soon" badge becomes a real link ("Download on the App Store" / "Get it on Google Play"), and the "launches soon" note hides once both are set.
- Text is inserted with `textContent` only (no HTML from the API).

## Data
- `SiteText { Key (PK, ≤ 60), En (≤ 600), Ar (≤ 600), SortOrder, UpdatedAt }`, seeded by the migration with today's EN and AR wording from `website/index.html` and `website/ar/index.html` for these keys:
  `hero.title`, `hero.lede`, `players.kicker`, `players.title`, `players.lede`, `players.step1.title`, `players.step1.body`, `players.step2.title`, `players.step2.body`, `players.step3.title`, `players.step3.body`, `stores.note`, `venues.kicker`, `venues.title`, `venues.lede`, `request.kicker`, `request.title`, `request.lede`, `faq.title`, `faq.lede`, `footer.tagline`.
  (Use the exact strings in the HTML; if a key's element doesn't exist today, e.g. no players kicker/lede, drop that key.)
- `SiteFaq { Id, QuestionEn/Ar (≤ 200), AnswerEn/Ar (≤ 1000), SortOrder, CreatedAt }`, seeded with the six current pairs in both languages.
- `SiteSettings` singleton (Id = 1): `ContactEmail?` (≤ 120, valid email), `ContactPhone?` (≤ 30, digits/+/spaces), `WhatsApp?` (≤ 30, digits/+), `Instagram?` (≤ 60, handle without @), `UpdatedAt`.

## API
- **Operator** (`Policies.PlatformOperator`), `Platform/SiteContentEndpoints.cs`:
  - `GET /admin/site-content` → `{ texts: [{ key, en, ar }] (seed order), faqs: [{ id, questionEn, questionAr, answerEn, answerAr, sortOrder }], contact: { email, phone, whatsApp, instagram }, androidStoreUrl, iosStoreUrl }`.
  - `PUT /admin/site-content/texts` `[{ key, en, ar }]` (any subset; unknown key → 400 "Unknown text."; both languages 1–600 → else 400 "Write every text in English and Arabic, up to 600 characters.") → GET shape.
  - `POST /admin/site-content/faqs`, `PUT …/faqs/{id}` `{ questionEn, questionAr, answerEn, answerAr }`, `DELETE …/faqs/{id}`, `PUT …/faqs/order [id]` (exact set) → GET shape. Max 20.
  - `PUT /admin/site-content/contact` `{ email, phone, whatsApp, instagram }` → GET shape.
- **Public**, anonymous, CORS allowed for the website origin like `/leads` and `/legal`: `GET /site/content?lang=en|ar` → `{ texts: { "hero.title": "…", … }, faqs: [{ question, answer }], contact: { email, phone, whatsApp, instagram }, androidStoreUrl, iosStoreUrl }` in that language.

## Business (operator)
- Nav "Website" (lucide `Globe`) after App content, route `/website`, operator only. Cards:
  1. **Texts**: grouped by section (Hero, Players, Venues, Demo request, FAQ, Footer), each key a label + EN textarea + AR textarea (`dir="rtl"`), one Save for the card (sends the changed keys).
  2. **FAQ**: ordered list (question in UI language), up/down, Edit (dialog with the four fields), Delete (confirm), Add (max 20).
  3. **Contact**: four inputs (`dir="ltr"`), Save.
  4. **Store links**: read-only with a link "Edit in App content".
  Plus "View website" links (EN and AR) — base URL from `VITE_WEBSITE_URL`, default `http://localhost:5500/`.
- EN/AR, RTL.

## Website
- `website/content.js` (loaded by `index.html` and `ar/index.html`, `defer`): reads the API base from the same `data-api` the form uses (or move it to `<body data-api>` and keep the form working), fetches `/site/content?lang=<html lang>`, then:
  - for each `[data-text="key"]` element sets `textContent` when the key has a value;
  - rebuilds `.faq-list` with `<details><summary>` pairs (keeps the fallback if the list is empty);
  - fills a footer `.contact` line: email (`mailto:`), phone (`tel:`), WhatsApp (`https://wa.me/<digits>`), Instagram (`https://instagram.com/<handle>`); hidden when none are set;
  - store badges: when a URL is set, the badge becomes an `<a href>` (`target="_blank" rel="noopener"`) with the "download/get it" wording in the page language; hides the "launches soon" note when both are set.
  - any fetch error → leave the page as it is.
- Add `data-text` attributes to the matching elements in both HTML files and the empty footer contact element.

---

### Task 1: API
Entities, `MidangoDb` config, migration `SiteContent` (seed texts + FAQs + settings row), endpoints, public CORS like `/leads`, map in `Program.cs`. `dotnet build` clean. Commit `feat(api): website content — texts, FAQ, contact`.

### Task 2: Business
Page + nav + types + EN/AR. Build + lint clean. Don't commit.

### Task 3: Website
`content.js` + HTML attributes. Don't commit.

### Task 4: Verify (controller)
API: validation; text edit shows in `/site/content` both languages; FAQ add/order/delete/max; contact; manager 403; anonymous public read. Browser: Business page EN + AR; website EN + AR shows edited text, FAQ order, contact links, store badges as links; API down → fallback text.

---

## Verification (2026-11-18)

**API** (`a4-check.ps1`): **0 failures.** The script's last line, a CORS header check, raised a PowerShell type error because the header came back as an array. The header itself was present.
- **Access:** a venue manager gets 403, and a request without a token gets 401. The admin shape has 21 texts, 6 FAQs and the store link from A3.
- **Texts:** an unknown key, an empty Arabic text and a text of 601 characters each get 400. A headline edit is served in both languages, and the other texts are unchanged. An unknown `lang` falls back to English.
- **FAQ:** a missing Arabic question and a question over 200 characters each get 400. A partial order gets 400 "Send every question once". A new question moved to the top comes first on the Arabic site, and editing works. The 21st question gets 400, deleting works, and an unknown id gets 404.
- **Contact:** bad email, phone, WhatsApp and Instagram each get 400 with their sentence. Saving works and the leading @ is removed.

**Browser, website (Arabic, fresh context):**
- The edited headline, the reordered FAQ (7) and the footer contact line all show: email, phone, واتساب → `wa.me/966…`, and @midango.
- Both store badges became links reading "حمّله من App Store" and "احصل عليه من Google Play", and the "launches soon" note hid. The copyright line keeps "© 2026 MIDANGO." with the tagline once.
- The website agent tested the fallback against the 404 before the API existed: the page stayed as it was.
- It also tested injected `<b>` and `<img onerror>` text, which rendered as plain text, and a `javascript:` store URL, which was refused.

**Browser, Business:** the Website page shows the texts grouped by section with labels, the FAQ count (n of 20), contact and store links, in Arabic RTL. Changing the headline through the page and pressing Save reached `/site/content`.

**Gotcha:** the website's Vite dev server kept serving the old `styles.css`, even to a fresh browser context and after a `touch`. Restarting `vite website --port 5500` fixed it.

**Clean-up:** the test question, the contact details and the A3 placeholder store links were removed. The site is back to its original wording, with "Coming soon" badges.

**Known limits:** the edited text is applied by JS, so crawlers see the fallback (marked `ponytail:` in the plan). The feature cards and the hero art are not editable.
