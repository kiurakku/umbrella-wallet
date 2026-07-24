# 04 — Desktop Vault (Advanced: Electron / Tauri wrapper)

> **Status:** Not implemented in MVP. This document describes the architecture for a native desktop app (Windows/macOS/Linux) that would provide hardware-backed encryption and Tor integration.

---

## Why desktop over web?

| Security feature | Web (IndexedDB) | Desktop (native keychain) |
|------------------|-----------------|---------------------------|
| Seed encryption at rest | AES-256-GCM + Argon2id | Same, but key stored in OS Secure Enclave (Keychain / DPAPI / libsecret) |
| XSS protection | Limited (CSP helps) | Full (no DOM injection vector) |
| Screenshot protection | Not possible | OS-level flag (Windows: SetWindowDisplayAffinity) |
| Tor built-in | Manual proxy | Embedded Tor binary, managed lifecycle |
| Memory wiping | JS garbage collection | Explicit SecureZeroMemory / sodium_memzero |

---

## Tech stack (proposed)

| Layer | Technology | Why |
|-------|------------|-----|
| Shell | Tauri (Rust) | Smaller binary than Electron, Rust security |
| Alt: Shell | Electron (Chromium) | Mature, but large (~150MB) |
| Webview | System (WebView2 / WebKit) | No Chromium bundling in Tauri |
| Backend | Same NestJS (bundled) | Portable HTTP server, SQLite for local state |
| IPC | Tauri commands | Type-safe Rust ←→ JS bridge |
| Keychain | OS-native | Windows DPAPI, macOS Keychain, Linux libsecret |
| Tor | Embedded tor binary | Managed by app, SOCKS5 proxy on 127.0.0.1:9050 |

---

## Seed vault flow (desktop)

```
User creates wallet
  → Mnemonic generated in Tauri Rust backend (ephemeral memory)
  → User shown phrase for backup
  → User confirms backup
  → User sets encryption password
  → Tauri backend:
      • KDF: Argon2id → 32-byte key
      • AES-256-GCM encrypt mnemonic
      • Master password stored in OS keychain (encrypted by OS)
      • Ciphertext saved to SQLite (local DB, ~/.umbra/vault.db)
  → On unlock:
      • Tauri reads encrypted blob from SQLite
      • Retrieves master password from OS keychain
      • Decrypts seed in Rust memory (SecureString)
      • Derives addresses
      • Wipes memory after use (sodium_memzero)
```

---

## Monero support (desktop-only for now)

Monero is not BIP39-compatible. Desktop app would use:

| Library | Language | What |
|---------|----------|------|
| `monero-javascript` | JS | Wallet creation, viewkey / spendkey |
| `monerod` RPC | HTTP | Connect to local or remote daemon |
| Wallet RPC | HTTP | monero-wallet-rpc for tx creation |

**Storage:**
- Monero 25-word mnemonic encrypted same way as BIP39 seed (separate vault record).
- ViewKey stored for balance scanning (read-only).
- SpendKey encrypted at rest, only decrypted during send.

**Balance update:**
- Connect to Monero node (user-configurable: localhost:18081 or remote .onion)
- Scan blocks for incoming transactions using ViewKey (privacy-preserving)
- No address reuse — each receive generates a subaddress

---

## Tor integration

### Bundled Tor binary
- **Windows:** `tor.exe` + `libeay32.dll`, `ssleay32.dll`
- **macOS/Linux:** `tor` binary from Tor Browser bundle
- Managed by Tauri backend: start on app launch, stop on quit

### SOCKS5 proxy
- Tor listens on `127.0.0.1:9050` (local only)
- All HTTP requests routed through SOCKS5 proxy (backend → blockchain nodes, rate APIs)
- DNS leaks prevented (SOCKS5h)

### .onion access
If backend runs as hidden service:
- `torrc` config: `HiddenServiceDir`, `HiddenServicePort`
- Backend generates `.onion` address on first start
- User shares .onion URL (P2P discovery) — no Tor exit node needed

---

## Screenshot / screen-record protection

### Windows
```c++
SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
```
- Seed phrase screen: window is black in screenshots, OBS, Snipping Tool
- Tauri: call via FFI or WinAPI crate

### macOS
```swift
window.sharingType = .none  // NSWindow.SharingType
```
- Same effect: seed screen excluded from screenshots / screen recordings

### Linux
- No universal API (compositor-dependent)
- Watermark workaround: overlay semi-transparent "Recording detected" on seed screen

---

## Theming (dark / light / high contrast)

Desktop app supports:
- **Dark theme** (default) — optimized for OLED, low blue light
- **Light theme** — accessibility for daylight use
- **High contrast** — WCAG AAA compliance (for visually impaired)
- **Custom themes** — JSON config in `~/.umbra/themes/custom.json`

See `10-extending.md` for theme format.

---

## Build commands (future)

```bash
# Install Tauri CLI
cargo install tauri-cli

# Dev (hot reload)
cargo tauri dev

# Build installers
cargo tauri build
# → .exe (Windows), .dmg (macOS), .AppImage (Linux)

# Sign (Windows)
signtool sign /f cert.pfx /p password /t http://timestamp.digicert.com umbra-wallet.exe

# Notarize (macOS)
xcrun notarytool submit umbra-wallet.dmg --keychain-profile "AC_PASSWORD"
```

---

## No Electron in MVP — web-only for now

Desktop is **not** in scope for the first release. This doc is a roadmap for:
- Users who demand maximum security (OS keychain + no browser attack surface)
- Monero support (requires native wallet RPC)
- Tor hidden service self-hosting

For now, web app + privacy mode (manual Tor Browser) is sufficient for 95% of use cases.
