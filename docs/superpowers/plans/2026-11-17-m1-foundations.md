# M1 Foundations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the Midango repo with a running .NET API (database health check), a React admin and a Flutter app that both render the approved brand (colors, fonts, buttons, status tags, slot chip) on a styleguide screen in English and Arabic.

**Architecture:** One repo, three top-level projects: `api/` (.NET 10 minimal API), `admin/` (Vite + React + TypeScript, used later for both MIDANGO Business and Desk), `app/` (Flutter, the MIDANGO App). Brand tokens are written once per platform (CSS custom properties for React, a Dart constants class for Flutter) from the approved values in `docs/research/midango-design-research.html`. No feature screens yet: the only screens are styleguides, so no control exists that does not work.

**Tech Stack:** .NET SDK 10.0.100 · Npgsql · PostgreSQL 18 (local service `postgresql-x64-18`, port 5432) · Node 24 + Vite + React + TypeScript · lucide-react · Flutter (`C:\Users\Lenovo\flutter`) · google_fonts · lucide_icons_flutter · flutter_localizations

**Project rules that override the skill defaults:**
- **No automated tests.** Every task ends with a build and a manual run check with an exact expected result.
- Run every `flutter` command from **PowerShell**, using `C:\Users\Lenovo\flutter\bin\flutter.bat`. Never from Git Bash: it hangs.
- `psql.exe` is not on PATH. It is at `C:\Program Files\PostgreSQL\18\bin\psql.exe`.

**Approved values used in this plan (from the research document):**

| Token | Value | Rule |
|---|---|---|
| lime | `#A8FF00` | Fill only, always with navy content on top (D1) |
| navy | `#071329` | Headings, text on lime |
| slate | `#202B43` | Body text |
| muted | `#5B6478` | Secondary text (D3) |
| cyan | `#00CDEB` | Gradients, links on navy only |
| cyanInk | `#00788A` | Links and focus on light surfaces (D2) |
| cloud | `#F5F7FA` | Page background |
| white | `#FFFFFF` | Cards, sheets |
| line | `#E3E7EE` | Borders |
| disabledBg | `#E7EAF2` | Disabled fill, unavailable slot |
| error / errorBg | `#D92D20` / `#FEF0EF` | (D4) |
| warning / warningBg | `#B54708` / `#FFF4E5` | (D4), used for "Pay at venue" |
| Fonts | Space Grotesk (titles, prices, times) · Inter (UI) · IBM Plex Sans Arabic (Arabic) | (D5) |
| Radii | app 12 · web 8 | |

---

## File structure

```
midango/
├── .gitignore
├── README.md
├── global.json                          .NET SDK pin
├── assets/brand/
│   ├── midango-logo.png                 Motion Play logo (canonical, raster)
│   └── midango-icon.png                 M symbol cropped from it (interim until vector)
├── docs/
│   ├── brand/MIDANGO_Brand_Guidelines.pdf
│   ├── research/midango-design-research.html
│   └── superpowers/plans/               this plan + roadmap
├── api/
│   ├── Midango.sln
│   └── src/Midango.Api/
│       ├── Midango.Api.csproj
│       ├── Program.cs                   host + /health
│       └── appsettings.Development.json connection string
├── admin/                               Vite React TS
│   ├── index.html                       fonts, title, favicon
│   ├── public/midango-icon.png
│   └── src/
│       ├── main.tsx
│       ├── index.css                    base styles, imports tokens
│       ├── theme/tokens.css             all CSS custom properties
│       ├── components/Button.tsx + button.css
│       ├── components/StatusTag.tsx + status-tag.css
│       ├── Styleguide.tsx + styleguide.css
│       └── App.tsx                      renders Styleguide
└── app/                                 Flutter
    ├── pubspec.yaml
    ├── assets/brand/midango-icon.png
    └── lib/
        ├── main.dart                    MaterialApp, EN/AR locale switch
        ├── theme/midango_colors.dart
        ├── theme/midango_theme.dart
        ├── widgets/slot_chip.dart
        └── styleguide/styleguide_page.dart
```

---

### Task 1: Create the repo and bring in the brand assets

**Files:**
- Create: `.gitignore`, `README.md`, `assets/brand/midango-logo.png`, `docs/brand/MIDANGO_Brand_Guidelines.pdf`, `docs/research/midango-design-research.html`

- [ ] **Step 1: Copy the assets** (PowerShell)

```powershell
$root = "C:\Users\Lenovo\Desktop\Ali\midango"
New-Item -ItemType Directory -Force "$root\assets\brand", "$root\docs\brand", "$root\docs\research" | Out-Null
Copy-Item "C:\Users\Lenovo\Downloads\Midango Motion Play Logo.png" "$root\assets\brand\midango-logo.png"
Copy-Item "C:\Users\Lenovo\Downloads\MIDANGO_Brand_Guidelines.pdf" "$root\docs\brand\MIDANGO_Brand_Guidelines.pdf"
Copy-Item "C:\Users\Lenovo\Desktop\Ali\yallaplay\docs\midango\midango-design-research.html" "$root\docs\research\midango-design-research.html"
Get-ChildItem -Recurse "$root\assets", "$root\docs" | Select-Object FullName
```

Expected: the three files listed, plus the two plan files already in `docs\superpowers\plans`.

- [ ] **Step 2: Write `.gitignore`**

```gitignore
# .NET
bin/
obj/
*.user
.vs/

# Node
node_modules/
dist/

# Flutter
.dart_tool/
build/
.flutter-plugins
.flutter-plugins-dependencies
*.iml
.idea/

# Local secrets
.env
appsettings.Local.json
```

- [ ] **Step 3: Write `README.md`**

```markdown
# Midango

Court booking for venues and players. Press play. Start moving.

| Folder | What | Run |
|---|---|---|
| `api/` | .NET 10 API | `dotnet run --project api/src/Midango.Api --urls http://localhost:5300` |
| `admin/` | MIDANGO Business & Desk (React) | `cd admin; npm run dev` → http://localhost:5173 |
| `app/` | MIDANGO App (Flutter) | `cd app; C:\Users\Lenovo\flutter\bin\flutter.bat run -d chrome` |

Design source of truth: `docs/research/midango-design-research.html`.
Plans: `docs/superpowers/plans/`.
```

- [ ] **Step 4: Initialise git and commit**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango
git init -b main
git add .
git commit -m "chore: start Midango repo with brand assets, research and plans"
```

Expected: `git log --oneline` shows one commit.

---

### Task 2: Crop an interim M icon from the logo

The brand package has only the horizontal raster logo. The apps and favicon need the M symbol alone. This crop is temporary until the vector redraw exists.

**Files:**
- Create: `assets/brand/midango-icon.png`

- [ ] **Step 1: Print the logo size and crop the symbol** (PowerShell)

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Bitmap]::new("$PWD\assets\brand\midango-logo.png")
"logo size: $($src.Width) x $($src.Height)"
$rect = [System.Drawing.Rectangle]::new(70, 185, 560, 295)
$icon = $src.Clone($rect, $src.PixelFormat)
$icon.Save("$PWD\assets\brand\midango-icon.png", [System.Drawing.Imaging.ImageFormat]::Png)
$icon.Dispose(); $src.Dispose()
```

Expected: `logo size: 2172 x <height>` and a new `midango-icon.png`.

- [ ] **Step 2: Check the crop by eye**

Open `assets\brand\midango-icon.png`. Expected: the whole M symbol (lime/navy/cyan) with its play triangle, a little white margin on every side, no part of the vertical divider line or the word MIDANGO. If the symbol is clipped, widen the rectangle by 20 px on the clipped side and re-run Step 1. If the divider shows, reduce the width by 20 px.

- [ ] **Step 3: Commit**

```powershell
git add assets/brand/midango-icon.png
git commit -m "chore: add interim M icon cropped from the canonical logo"
```

---

### Task 3: Create the Midango database

- [ ] **Step 1: Create the role and database**

This prompts for the `postgres` superuser password, so the user runs it (in Claude Code: type it with a leading `!`):

```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost -c "CREATE ROLE midango LOGIN PASSWORD 'midango_dev';" -c "CREATE DATABASE midango OWNER midango;"
```

Expected:
```
CREATE ROLE
CREATE DATABASE
```

- [ ] **Step 2: Check the new login works**

```powershell
$env:PGPASSWORD = "midango_dev"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U midango -h localhost -d midango -c "SELECT current_user, current_database();"
```

Expected: one row, `midango | midango`.

---

### Task 4: API host with a database health check

**Files:**
- Create: `global.json`, `api/Midango.sln`, `api/src/Midango.Api/*` (generated), then replace `Program.cs` and `appsettings.Development.json`

- [ ] **Step 1: Pin the SDK and generate the project**

`global.json` at the repo root:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango\api
dotnet new sln -n Midango
dotnet new web -n Midango.Api -o src/Midango.Api -f net10.0
dotnet sln Midango.sln add src/Midango.Api/Midango.Api.csproj
dotnet add src/Midango.Api package Npgsql
```

Expected: each command ends without errors. `src/Midango.Api/Midango.Api.csproj` contains `<TargetFramework>net10.0</TargetFramework>` and a `PackageReference` to `Npgsql`.

- [ ] **Step 2: Replace `api/src/Midango.Api/Program.cs`**

```csharp
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
```

- [ ] **Step 3: Replace `api/src/Midango.Api/appsettings.Development.json`**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "Midango": "Host=localhost;Port=5432;Database=midango;Username=midango;Password=midango_dev"
  }
}
```

- [ ] **Step 4: Build**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango
dotnet build api/Midango.sln
```

Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 5: Run and call the health check**

Terminal 1:
```powershell
dotnet run --project api/src/Midango.Api --urls http://localhost:5300
```
Terminal 2:
```powershell
curl.exe -s -w "`nHTTP %{http_code}`n" http://localhost:5300/health
```

Expected:
```
{"status":"ok","database":"ok"}
HTTP 200
```

- [ ] **Step 6: Check the failure path**

Stop the API. Start it again with a wrong password:
```powershell
$env:ConnectionStrings__Midango = "Host=localhost;Port=5432;Database=midango;Username=midango;Password=wrong"
dotnet run --project api/src/Midango.Api --urls http://localhost:5300
```
Call `/health` again. Expected: `{"status":"degraded","database":"unreachable"}` and `HTTP 503`; the API console logs the real error. Then stop the API and run `Remove-Item Env:ConnectionStrings__Midango`.

- [ ] **Step 7: Commit**

```powershell
git add global.json api
git commit -m "feat(api): .NET 10 host with a database health check"
```

---

### Task 5: React admin project with the brand tokens

**Files:**
- Create: `admin/` (generated by Vite), then `admin/index.html`, `admin/public/midango-icon.png`, `admin/src/theme/tokens.css`, `admin/src/index.css`
- Delete: `admin/src/App.css`, `admin/src/assets/react.svg`, `admin/public/vite.svg`

- [ ] **Step 1: Generate the project**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango
npm create vite@latest admin -- --template react-ts
cd admin
npm install
npm install lucide-react
```

If `create-vite` asks whether to install and start now, answer **No**. The next commands do the install.

Expected: `admin/package.json` lists `react`, `react-dom`, `lucide-react`, and `vite` in devDependencies.

- [ ] **Step 2: Remove the Vite sample files and add the icon**

```powershell
Remove-Item src/App.css, src/assets/react.svg, public/vite.svg -ErrorAction SilentlyContinue
Copy-Item ..\assets\brand\midango-icon.png public\midango-icon.png
```

- [ ] **Step 3: Replace `admin/index.html`**

```html
<!doctype html>
<html lang="en" dir="ltr">
  <head>
    <meta charset="UTF-8" />
    <link rel="icon" type="image/png" href="/midango-icon.png" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Midango Business</title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link
      href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans+Arabic:wght@400;500;600;700&family=Inter:wght@400;500;600;700&family=Space+Grotesk:wght@500;700&display=swap"
      rel="stylesheet"
    />
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

- [ ] **Step 4: Create `admin/src/theme/tokens.css`**

```css
/* Midango brand tokens (web). Source: docs/research/midango-design-research.html, decisions D1–D10. */
:root {
  /* Color: lime is a fill only, always with navy content on top (D1). */
  --mg-lime: #a8ff00;
  --mg-navy: #071329;
  --mg-slate: #202b43;
  --mg-muted: #5b6478;
  --mg-cyan: #00cdeb;      /* gradients and links on navy only */
  --mg-cyan-ink: #00788a;  /* links and focus on light surfaces (D2) */
  --mg-cloud: #f5f7fa;
  --mg-white: #ffffff;
  --mg-line: #e3e7ee;
  --mg-disabled-bg: #e7eaf2;
  --mg-error: #d92d20;
  --mg-error-bg: #fef0ef;
  --mg-warning: #b54708;
  --mg-warning-bg: #fff4e5;
  --mg-success-bg: #f3ffe0; /* lime tint for "Checked in" */

  /* Type (D5). Arabic falls back to IBM Plex Sans Arabic in every stack. */
  --mg-font-display: "Space Grotesk", "IBM Plex Sans Arabic", system-ui, sans-serif;
  --mg-font-ui: Inter, "IBM Plex Sans Arabic", system-ui, sans-serif;

  /* Web type scale: size / line-height */
  --mg-text-display: 700 32px/40px var(--mg-font-display);
  --mg-text-title-l: 700 24px/32px var(--mg-font-display);
  --mg-text-title-m: 500 18px/26px var(--mg-font-display);
  --mg-text-numeric: 500 14px/20px var(--mg-font-display);
  --mg-text-body: 400 14px/22px var(--mg-font-ui);
  --mg-text-label: 600 13px/18px var(--mg-font-ui);
  --mg-text-caption: 500 12px/16px var(--mg-font-ui);

  /* Shape */
  --mg-radius: 8px;
  --mg-control-height: 40px;
}
```

- [ ] **Step 5: Replace `admin/src/index.css`**

```css
@import "./theme/tokens.css";

*,
*::before,
*::after {
  box-sizing: border-box;
}

body {
  margin: 0;
  background: var(--mg-cloud);
  color: var(--mg-slate);
  font: var(--mg-text-body);
}

h1,
h2,
h3 {
  color: var(--mg-navy);
  margin: 0;
}

a {
  color: var(--mg-cyan-ink);
}

:focus-visible {
  outline: 3px solid var(--mg-cyan-ink);
  outline-offset: 2px;
}
```

- [ ] **Step 6: Commit** (the app does not build yet: `App.tsx` still imports the deleted `App.css`. Task 6 fixes it.)

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango
git add admin
git commit -m "feat(admin): Vite React project with Midango tokens and fonts"
```

---

### Task 6: React brand components and styleguide

**Files:**
- Create: `admin/src/components/Button.tsx`, `admin/src/components/button.css`, `admin/src/components/StatusTag.tsx`, `admin/src/components/status-tag.css`, `admin/src/Styleguide.tsx`, `admin/src/styleguide.css`
- Modify: `admin/src/App.tsx` (replace)

- [ ] **Step 1: Create `admin/src/components/Button.tsx`**

```tsx
import { useId, type ButtonHTMLAttributes } from 'react';
import './button.css';

type Variant = 'primary' | 'secondary' | 'text' | 'destructive';

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
  /** Shown under a disabled button. A button is never disabled without a reason. */
  disabledReason?: string;
};

export function Button({ variant = 'primary', disabledReason, className, ...rest }: Props) {
  const reasonId = useId();
  const disabled = rest.disabled || disabledReason !== undefined;

  return (
    <span className="mg-button-wrap">
      <button
        type="button"
        {...rest}
        disabled={disabled}
        aria-describedby={disabledReason ? reasonId : undefined}
        className={['mg-button', `mg-button--${variant}`, className].filter(Boolean).join(' ')}
      />
      {disabledReason && (
        <span id={reasonId} className="mg-button-reason">
          {disabledReason}
        </span>
      )}
    </span>
  );
}
```

- [ ] **Step 2: Create `admin/src/components/button.css`**

```css
.mg-button-wrap {
  display: inline-flex;
  flex-direction: column;
  gap: 4px;
}

.mg-button {
  height: var(--mg-control-height);
  padding-inline: 16px;
  border-radius: var(--mg-radius);
  border: 0;
  font: var(--mg-text-label);
  cursor: pointer;
}

.mg-button--primary {
  background: var(--mg-lime);
  color: var(--mg-navy);
}

.mg-button--secondary {
  background: var(--mg-white);
  color: var(--mg-navy);
  border: 1.5px solid var(--mg-navy);
}

.mg-button--text {
  background: transparent;
  color: var(--mg-cyan-ink);
  text-decoration: underline;
  padding-inline: 4px;
}

.mg-button--destructive {
  background: var(--mg-error);
  color: var(--mg-white);
}

.mg-button:disabled {
  background: var(--mg-disabled-bg);
  color: var(--mg-muted);
  border-color: transparent;
  cursor: not-allowed;
}

.mg-button-reason {
  font: var(--mg-text-caption);
  color: var(--mg-muted);
}
```

- [ ] **Step 3: Create `admin/src/components/StatusTag.tsx`**

```tsx
import './status-tag.css';

export type BookingState = 'free' | 'pay-at-venue' | 'paid' | 'checked-in' | 'blocked';

// Every state carries a word, never colour alone.
const LABEL: Record<BookingState, string> = {
  free: 'Free',
  'pay-at-venue': 'Pay at venue',
  paid: 'Paid',
  'checked-in': '✓ Checked in',
  blocked: 'Blocked',
};

export function StatusTag({ state }: { state: BookingState }) {
  return <span className={`mg-tag mg-tag--${state}`}>{LABEL[state]}</span>;
}
```

- [ ] **Step 4: Create `admin/src/components/status-tag.css`**

```css
.mg-tag {
  display: inline-block;
  padding: 3px 8px;
  border-radius: 999px;
  font: var(--mg-text-caption);
  white-space: nowrap;
}

.mg-tag--free {
  background: var(--mg-white);
  color: var(--mg-slate);
  border: 1px solid var(--mg-line);
}

.mg-tag--pay-at-venue {
  background: var(--mg-warning-bg);
  color: var(--mg-warning);
}

.mg-tag--paid {
  background: var(--mg-disabled-bg);
  color: var(--mg-navy);
}

.mg-tag--checked-in {
  background: var(--mg-success-bg);
  color: var(--mg-navy);
}

.mg-tag--blocked {
  background: repeating-linear-gradient(135deg, var(--mg-disabled-bg) 0 6px, var(--mg-white) 6px 12px);
  color: var(--mg-muted);
}
```

- [ ] **Step 5: Create `admin/src/Styleguide.tsx`**

```tsx
import { useEffect, useState } from 'react';
import { Languages } from 'lucide-react';
import { Button } from './components/Button';
import { StatusTag, type BookingState } from './components/StatusTag';
import './styleguide.css';

const SWATCHES: Array<[name: string, cssVar: string]> = [
  ['Lime', '--mg-lime'],
  ['Navy', '--mg-navy'],
  ['Slate', '--mg-slate'],
  ['Muted', '--mg-muted'],
  ['Cyan', '--mg-cyan'],
  ['Cyan Ink', '--mg-cyan-ink'],
  ['Cloud', '--mg-cloud'],
  ['White', '--mg-white'],
  ['Error', '--mg-error'],
  ['Warning', '--mg-warning'],
];

const TYPE: Array<[name: string, cssVar: string]> = [
  ['display', '--mg-text-display'],
  ['title-l', '--mg-text-title-l'],
  ['title-m', '--mg-text-title-m'],
  ['numeric', '--mg-text-numeric'],
  ['body', '--mg-text-body'],
  ['label', '--mg-text-label'],
  ['caption', '--mg-text-caption'],
];

const STATES: BookingState[] = ['free', 'pay-at-venue', 'paid', 'checked-in', 'blocked'];

export function Styleguide() {
  const [dir, setDir] = useState<'ltr' | 'rtl'>('ltr');

  useEffect(() => {
    document.documentElement.dir = dir;
    document.documentElement.lang = dir === 'rtl' ? 'ar' : 'en';
  }, [dir]);

  return (
    <main className="sg">
      <header className="sg-header">
        <img src="/midango-icon.png" alt="Midango" height={32} />
        <h1>Styleguide</h1>
        <Button variant="secondary" onClick={() => setDir(dir === 'ltr' ? 'rtl' : 'ltr')}>
          <Languages size={16} aria-hidden="true" /> {dir === 'ltr' ? 'العربية' : 'English'}
        </Button>
      </header>

      <section>
        <h2>Colors</h2>
        <div className="sg-swatches">
          {SWATCHES.map(([name, cssVar]) => (
            <div key={name} className="sg-swatch">
              <div style={{ background: `var(${cssVar})` }} />
              <span>{name}</span>
            </div>
          ))}
        </div>
      </section>

      <section>
        <h2>Type</h2>
        {TYPE.map(([name, cssVar]) => (
          <p key={name} style={{ font: `var(${cssVar})` }}>
            {name} · Book your next move · احجز ملعبك القادم · 20:00
          </p>
        ))}
      </section>

      <section>
        <h2>Buttons</h2>
        <div className="sg-row">
          <Button>Save booking</Button>
          <Button variant="secondary">Block time</Button>
          <Button variant="text">View policy</Button>
          <Button variant="destructive">Cancel booking</Button>
          <Button disabledReason="Court 3 is closed for maintenance">Save booking</Button>
        </div>
      </section>

      <section>
        <h2>Booking states</h2>
        <div className="sg-row">
          {STATES.map((s) => (
            <StatusTag key={s} state={s} />
          ))}
        </div>
      </section>
    </main>
  );
}
```

- [ ] **Step 6: Create `admin/src/styleguide.css`**

```css
.sg {
  max-width: 960px;
  margin: 0 auto;
  padding: 24px 16px 64px;
  display: grid;
  gap: 32px;
}

.sg-header {
  display: flex;
  align-items: center;
  gap: 12px;
}

.sg-header h1 {
  font: var(--mg-text-title-l);
  flex: 1;
}

.sg h2 {
  font: var(--mg-text-title-m);
  margin-block-end: 12px;
}

.sg p {
  margin: 0 0 8px;
}

.sg-swatches {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 12px;
}

.sg-swatch {
  background: var(--mg-white);
  border: 1px solid var(--mg-line);
  border-radius: var(--mg-radius);
  overflow: hidden;
  font: var(--mg-text-caption);
}

.sg-swatch div {
  height: 48px;
}

.sg-swatch span {
  display: block;
  padding: 6px 8px;
}

.sg-row {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: flex-start;
}

.sg-row .mg-button {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
```

- [ ] **Step 7: Replace `admin/src/App.tsx`**

```tsx
import { Styleguide } from './Styleguide';

export default function App() {
  return <Styleguide />;
}
```

`admin/src/main.tsx` stays as generated (it imports `./index.css` and renders `<App />`). If the generated file imports anything else that was deleted, remove that import.

- [ ] **Step 8: Build**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango\admin
npm run build
```

Expected: TypeScript passes and Vite prints `✓ built in …` with files in `dist/`. No errors.

- [ ] **Step 9: Run and check by eye**

```powershell
npm run dev
```

Open http://localhost:5173. Expected:
- Cloud White page background, the M icon and "Styleguide" in Space Grotesk.
- Ten swatches. Lime appears only as a fill.
- Seven type rows. The Arabic text renders in IBM Plex Sans Arabic, not a system fallback.
- The primary button is lime with navy text. The disabled button is grey, with "Court 3 is closed for maintenance" under it.
- Five tags, each with a word.
- Click **العربية**: the whole page mirrors (header order reverses, text aligns right) and the button now reads **English**. Click it again to return.
- Press Tab: every button shows a cyan-ink focus ring.

- [ ] **Step 10: Commit**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango
git add admin
git commit -m "feat(admin): brand buttons, status tags and an EN/AR styleguide"
```

---

### Task 7: Flutter app project with the brand theme

**Files:**
- Create: `app/` (generated by Flutter), `app/assets/brand/midango-icon.png`, `app/lib/theme/midango_colors.dart`, `app/lib/theme/midango_theme.dart`
- Modify: `app/pubspec.yaml`

- [ ] **Step 1: Generate the project** (PowerShell only)

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango
& C:\Users\Lenovo\flutter\bin\flutter.bat create --org com.midango --project-name midango --platforms android,ios,web app
cd app
& C:\Users\Lenovo\flutter\bin\flutter.bat pub add google_fonts lucide_icons_flutter
New-Item -ItemType Directory -Force assets\brand | Out-Null
Copy-Item ..\assets\brand\midango-icon.png assets\brand\midango-icon.png
Remove-Item test -Recurse -Force
```

`web` is included only so the styleguide can be checked in Chrome. The generated `test/` folder is removed because the project rule is no automated tests, and the sample test would break once `main.dart` changes.

- [ ] **Step 2: Edit `app/pubspec.yaml`**

Under `dependencies:` add `flutter_localizations` directly below `flutter:`:

```yaml
dependencies:
  flutter:
    sdk: flutter
  flutter_localizations:
    sdk: flutter
```

In the `flutter:` section at the bottom, make it:

```yaml
flutter:
  uses-material-design: true
  assets:
    - assets/brand/
```

Then:
```powershell
& C:\Users\Lenovo\flutter\bin\flutter.bat pub get
```

Expected: `Got dependencies!`

- [ ] **Step 3: Create `app/lib/theme/midango_colors.dart`**

```dart
import 'package:flutter/painting.dart';

/// Midango brand colors. Source: docs/research/midango-design-research.html (D1–D4).
/// Lime is a fill only: never use it as text or icon color on light surfaces.
abstract final class MidangoColors {
  static const lime = Color(0xFFA8FF00);
  static const navy = Color(0xFF071329);
  static const slate = Color(0xFF202B43);
  static const muted = Color(0xFF5B6478);
  static const cyan = Color(0xFF00CDEB); // gradients and links on navy only
  static const cyanInk = Color(0xFF00788A); // links and focus on light surfaces
  static const cloud = Color(0xFFF5F7FA);
  static const white = Color(0xFFFFFFFF);
  static const line = Color(0xFFE3E7EE);
  static const disabledBg = Color(0xFFE7EAF2);
  static const error = Color(0xFFD92D20);
  static const errorBg = Color(0xFFFEF0EF);
  static const warning = Color(0xFFB54708);
  static const warningBg = Color(0xFFFFF4E5);
}
```

- [ ] **Step 4: Create `app/lib/theme/midango_theme.dart`**

```dart
import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'midango_colors.dart';

/// App type scale (D5), logical px. Arabic glyphs fall back to IBM Plex Sans Arabic.
TextTheme _textTheme() {
  final arabic = GoogleFonts.ibmPlexSansArabic().fontFamily!;
  TextStyle display(double size, double height, FontWeight weight) =>
      GoogleFonts.spaceGrotesk(fontSize: size, height: height / size, fontWeight: weight, color: MidangoColors.navy)
          .copyWith(fontFamilyFallback: [arabic]);
  TextStyle ui(double size, double height, FontWeight weight, Color color) =>
      GoogleFonts.inter(fontSize: size, height: height / size, fontWeight: weight, color: color)
          .copyWith(fontFamilyFallback: [arabic]);

  return TextTheme(
    headlineLarge: display(32, 38, FontWeight.w700), // display: "You're booked"
    titleLarge: display(24, 30, FontWeight.w700), // screen titles, venue name
    titleMedium: display(18, 24, FontWeight.w500), // section heads, price
    bodyLarge: ui(16, 24, FontWeight.w400, MidangoColors.slate), // body
    labelLarge: ui(14, 20, FontWeight.w600, MidangoColors.navy), // buttons, chips
    bodySmall: ui(12, 16, FontWeight.w500, MidangoColors.muted), // caption, metadata
  );
}

ThemeData buildMidangoTheme() {
  const scheme = ColorScheme.light(
    primary: MidangoColors.lime,
    onPrimary: MidangoColors.navy,
    secondary: MidangoColors.cyanInk,
    onSecondary: MidangoColors.white,
    surface: MidangoColors.white,
    onSurface: MidangoColors.navy,
    error: MidangoColors.error,
    onError: MidangoColors.white,
    outline: MidangoColors.line,
  );
  final text = _textTheme();
  final shape = RoundedRectangleBorder(borderRadius: BorderRadius.circular(12));
  const minSize = Size(64, 48);

  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: MidangoColors.cloud,
    textTheme: text,
    appBarTheme: AppBarTheme(
      backgroundColor: MidangoColors.white,
      foregroundColor: MidangoColors.navy,
      elevation: 0,
      titleTextStyle: text.titleMedium,
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: MidangoColors.lime,
        foregroundColor: MidangoColors.navy,
        disabledBackgroundColor: MidangoColors.disabledBg,
        disabledForegroundColor: MidangoColors.muted,
        minimumSize: minSize,
        shape: shape,
        textStyle: text.labelLarge,
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: MidangoColors.navy,
        side: const BorderSide(color: MidangoColors.navy, width: 1.5),
        minimumSize: minSize,
        shape: shape,
        textStyle: text.labelLarge,
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: MidangoColors.cyanInk,
        textStyle: text.labelLarge?.copyWith(decoration: TextDecoration.underline),
      ),
    ),
    cardTheme: CardThemeData(
      color: MidangoColors.white,
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: MidangoColors.line),
      ),
    ),
  );
}
```

- [ ] **Step 5: Commit** (`main.dart` still holds the counter sample; Task 8 replaces it)

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango
git add app
git commit -m "feat(app): Flutter project with Midango colors, fonts and theme"
```

---

### Task 8: Flutter slot chip, styleguide and EN/AR switch

**Files:**
- Create: `app/lib/widgets/slot_chip.dart`, `app/lib/styleguide/styleguide_page.dart`
- Modify: `app/lib/main.dart` (replace)

- [ ] **Step 1: Create `app/lib/widgets/slot_chip.dart`**

```dart
import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import '../theme/midango_colors.dart';

/// A start time the customer can tap. Lime fill = open; grey + struck through = full.
/// Full times stay visible so the customer knows why a time is missing.
class SlotChip extends StatelessWidget {
  const SlotChip({
    super.key,
    required this.time,
    required this.available,
    required this.semanticLabel,
    this.onTap,
  });

  final String time;
  final bool available;

  /// Read by screen readers, e.g. "20:00, available, 180 riyals".
  final String semanticLabel;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      enabled: available,
      label: semanticLabel,
      excludeSemantics: true,
      child: Material(
        color: available ? MidangoColors.lime : MidangoColors.disabledBg,
        borderRadius: BorderRadius.circular(8),
        child: InkWell(
          borderRadius: BorderRadius.circular(8),
          onTap: available ? onTap : null,
          child: ConstrainedBox(
            constraints: const BoxConstraints(minWidth: 64, minHeight: 44),
            child: Center(
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 12),
                child: Text(
                  time,
                  style: GoogleFonts.spaceGrotesk(
                    fontSize: 16,
                    fontWeight: FontWeight.w500,
                    color: available ? MidangoColors.navy : MidangoColors.muted,
                    decoration: available ? null : TextDecoration.lineThrough,
                    fontFeatures: const [FontFeature.tabularFigures()],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
```

- [ ] **Step 2: Create `app/lib/styleguide/styleguide_page.dart`**

```dart
import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../theme/midango_colors.dart';
import '../widgets/slot_chip.dart';

class StyleguidePage extends StatefulWidget {
  const StyleguidePage({super.key, required this.isArabic, required this.onToggleLanguage});

  final bool isArabic;
  final VoidCallback onToggleLanguage;

  @override
  State<StyleguidePage> createState() => _StyleguidePageState();
}

class _StyleguidePageState extends State<StyleguidePage> {
  String? _picked;

  static const _swatches = <(String, Color)>[
    ('Lime', MidangoColors.lime),
    ('Navy', MidangoColors.navy),
    ('Slate', MidangoColors.slate),
    ('Muted', MidangoColors.muted),
    ('Cyan', MidangoColors.cyan),
    ('Cyan Ink', MidangoColors.cyanInk),
    ('Cloud', MidangoColors.cloud),
    ('Error', MidangoColors.error),
    ('Warning', MidangoColors.warning),
  ];

  static const _slots = <(String, bool)>[
    ('18:00', true),
    ('19:00', true),
    ('20:00', true),
    ('20:30', false),
    ('21:30', true),
  ];

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final sample = widget.isArabic ? 'احجز ملعبك القادم' : 'Book your next move';

    return Scaffold(
      appBar: AppBar(
        leading: Padding(
          padding: const EdgeInsetsDirectional.only(start: 16),
          child: Image.asset('assets/brand/midango-icon.png', semanticLabel: 'Midango'),
        ),
        title: const Text('Styleguide'),
        actions: [
          TextButton.icon(
            onPressed: widget.onToggleLanguage,
            icon: const Icon(LucideIcons.languages, size: 18),
            label: Text(widget.isArabic ? 'English' : 'العربية'),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('Colors', style: text.titleMedium),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final (name, color) in _swatches)
                Column(
                  children: [
                    Container(
                      width: 56,
                      height: 40,
                      decoration: BoxDecoration(
                        color: color,
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(color: MidangoColors.line),
                      ),
                    ),
                    Text(name, style: text.bodySmall),
                  ],
                ),
            ],
          ),
          const SizedBox(height: 24),
          Text('Type', style: text.titleMedium),
          Text(sample, style: text.headlineLarge),
          Text(sample, style: text.titleLarge),
          Text(sample, style: text.titleMedium),
          Text(sample, style: text.bodyLarge),
          Text(sample, style: text.labelLarge),
          Text(sample, style: text.bodySmall),
          const SizedBox(height: 24),
          Text('Slots', style: text.titleMedium),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final (time, open) in _slots)
                SlotChip(
                  time: time,
                  available: open,
                  semanticLabel: open ? '$time, available' : '$time, full',
                  onTap: () => setState(() => _picked = time),
                ),
            ],
          ),
          const SizedBox(height: 8),
          Text(_picked == null ? 'Tap an open time' : 'Picked $_picked', style: text.bodySmall),
          const SizedBox(height: 24),
          Text('Buttons', style: text.titleMedium),
          const SizedBox(height: 8),
          FilledButton(onPressed: () {}, child: const Text('Confirm booking')),
          const SizedBox(height: 8),
          OutlinedButton(onPressed: () {}, child: const Text('Change time')),
          const SizedBox(height: 8),
          const FilledButton(onPressed: null, child: Text('Confirm booking')),
          Text('Disabled example: no open time selected', style: text.bodySmall),
        ],
      ),
    );
  }
}
```

The "Confirm booking" and "Change time" buttons here only demonstrate styles inside the styleguide, which is a developer screen. No customer-facing screen ships in M1.

- [ ] **Step 3: Replace `app/lib/main.dart`**

```dart
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'styleguide/styleguide_page.dart';
import 'theme/midango_theme.dart';

void main() => runApp(const MidangoApp());

class MidangoApp extends StatefulWidget {
  const MidangoApp({super.key});

  @override
  State<MidangoApp> createState() => _MidangoAppState();
}

class _MidangoAppState extends State<MidangoApp> {
  Locale _locale = const Locale('en');

  @override
  Widget build(BuildContext context) {
    final isArabic = _locale.languageCode == 'ar';
    return MaterialApp(
      title: 'Midango',
      debugShowCheckedModeBanner: false,
      theme: buildMidangoTheme(),
      locale: _locale,
      supportedLocales: const [Locale('en'), Locale('ar')],
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      home: StyleguidePage(
        isArabic: isArabic,
        onToggleLanguage: () => setState(
          () => _locale = isArabic ? const Locale('en') : const Locale('ar'),
        ),
      ),
    );
  }
}
```

- [ ] **Step 4: Analyze**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango\app
& C:\Users\Lenovo\flutter\bin\flutter.bat analyze
```

Expected: `No issues found!`. If `LucideIcons.languages` is reported as undefined, open `%LOCALAPPDATA%\Pub\Cache\hosted\pub.dev\lucide_icons_flutter-*\lib\` and use the import path and class name that file exports. Do not switch to Material icons, because D9 requires one icon set.

- [ ] **Step 5: Run and check by eye**

```powershell
& C:\Users\Lenovo\flutter\bin\flutter.bat run -d chrome
```

Expected:
- A white app bar with the M icon, "Styleguide" in Space Grotesk, and **العربية** in cyan-ink on the right.
- Nine swatches and six type rows ("Book your next move").
- Five slot chips: four lime with navy text, 20:30 grey and struck through. Tapping 19:00 shows "Picked 19:00". Tapping 20:30 does nothing.
- A lime "Confirm booking" button, an outlined "Change time" button, and a grey disabled button with its reason underneath.
- Tap **العربية**: the layout mirrors (the icon moves right, the language button moves left), the samples switch to "احجز ملعبك القادم" in IBM Plex Sans Arabic, and the button reads **English**.

Press `q` in the terminal to stop.

- [ ] **Step 6: Commit**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango
git add app
git commit -m "feat(app): slot chip and an EN/AR styleguide on the Midango theme"
```

---

### Task 9: Close out M1

- [ ] **Step 1: Full check from a clean state**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango
git status --short
dotnet build api/Midango.sln
cd admin; npm run build; cd ..
cd app; & C:\Users\Lenovo\flutter\bin\flutter.bat analyze; cd ..
```

Expected: `git status` prints nothing, the .NET build succeeds, the Vite build succeeds, and Flutter reports `No issues found!`.

- [ ] **Step 2: Tick M1 in the roadmap**

In `docs/superpowers/plans/2026-11-17-midango-roadmap.md`, change the M1 row's plan cell from `**Foundations**` to `**Foundations** ✅ done`.

```powershell
git add docs/superpowers/plans/2026-11-17-midango-roadmap.md
git commit -m "docs: mark M1 Foundations done"
```

---

## What M1 deliberately leaves out

- **App icon, splash screen and favicon at all sizes:** these wait for the vector logo. The interim crop is used only in the styleguides.
- **Router, Business sidebar, i18n library:** these arrive in M3 with the first real screen. A sidebar now would be full of links that go nowhere.
- **CORS, auth, EF Core, migrations:** these arrive in M2, when the first endpoint that needs them exists.
- **Bundled font files:** both clients load the fonts from Google Fonts. Bundle them before store release, so the app works offline on first launch.
