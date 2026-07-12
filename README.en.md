# IP Widget

[Русский](README.md) · **English**

A lightweight cross-platform widget for checking your public IP, built with **Avalonia / .NET 9**.

<p align="center">
  <em>Glassy frameless widget · 6-source check · geolocation · VPN/hosting detection · transparent HUD · tray</em>
</p>

## Features

- **6 sources in parallel** — ipify, ifconfig.me, icanhazip, ipinfo.io, seeip, checkip.amazonaws. The final IP is chosen by majority vote, so a single failing endpoint won't mislead you.
- Every source shows its own status (pending → ok/failed) and **latency in ms**.
- **Geolocation + ISP/ASN** via [ipwho.is](https://ipwho.is) — country, city, ISP, autonomous system number.
- **Country flags** — fetched from [flagcdn.com](https://flagcdn.com) and cached.
- **VPN / hosting heuristic** — flags datacenter addresses by fingerprinting the ISP/org/domain fields.
- **Transparent HUD mode** — hides everything except the flag, IP and country; the window turns see-through.
- **Settings** — close behavior (to tray / full quit), always-on-top, show flag, compact mode. Stored in `%AppData%/IpWidget/settings.json`; window position is remembered.
- **Tray** — minimize to tray, menu *Show / Refresh / Quit*.
- Vibey look: AcrylicBlur, gradient, neon glow, vector icons (MDI).

## Run

```bash
dotnet run -c Release
```

## Single-file build

```bash
# win-x64 / linux-x64 / osx-arm64 — only -r changes
dotnet publish -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

## Stack

- [Avalonia UI](https://avaloniaui.net) 11.2 · .NET 9
- No external assets — the app/tray icon is drawn at runtime.

## Releases

Pushing a `v*` tag builds single-file binaries for
`win-x64`, `linux-x64`, `osx-x64`, `osx-arm64` via GitHub Actions and attaches them to the release.

```bash
git tag v0.1.1 && git push origin v0.1.1
```

## License

[MIT](LICENSE) © keyldev
