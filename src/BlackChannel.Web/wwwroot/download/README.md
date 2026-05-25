# download/

Served at `/download/` — the native app packages the [`/download`](../) page links to:

- `blackchannel-android.apk` — Android sideload build
- `blackchannel-windows.zip` — unpackaged Windows build

**These binaries are gitignored** (they're large and rebuildable). The deploy
(`deploy.ps1` / the GitHub Action) builds them from `src/BlackChannel.App` and drops them
here before publishing the site, so the download links work in production.

Build them manually for local testing:

```powershell
# Android APK (signed with the auto debug keystore — fine for sideload)
dotnet publish ../../BlackChannel.App -f net10.0-android -c Release
# copy the produced *-Signed.apk here as blackchannel-android.apk

# Windows (unpackaged) — zip the publish folder as blackchannel-windows.zip
dotnet publish ../../BlackChannel.App -f net10.0-windows10.0.19041.0 -c Release
```
