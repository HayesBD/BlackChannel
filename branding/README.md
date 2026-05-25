# BlackChannel branding assets

Drop the BlackChannel icon/logo source files here. The deploy copies the web-facing ones
into `src/BlackChannel.Web/wwwroot/` (see the icon `<link>` tags in `wwwroot/index.html`).

## What to provide

| File | Size | Used for |
| --- | --- | --- |
| `favicon.ico`            | multi-res (16/32/48) | browser tab icon |
| `favicon-16.png`         | 16×16   | tab icon (modern) |
| `favicon-32.png`         | 32×32   | tab icon (modern) |
| `apple-touch-icon.png`   | 180×180 | iOS home-screen icon |
| `icon-512.png`           | 512×512 | PWA / large icon |
| `logo-wide.png`          | ~1200×400 | social / README header (optional) |

PNGs should have a transparent background. Keep the dark-theme look in mind (the site
background is near-black `#0d0f12`, accent signal-green `#2ecc71`).

## Wiring (once files are here)

1. Copy the web icons into `src/BlackChannel.Web/wwwroot/`:
   `favicon.ico`, `favicon-16.png`, `favicon-32.png`, `apple-touch-icon.png`, `icon-512.png`.
2. The `<link rel="icon" …>` tags in `wwwroot/index.html` already point at those names —
   nothing else to change.

Drop your icon files in this folder, copy the web-facing ones into `wwwroot/`, and the
existing `<link>` tags pick them up.
