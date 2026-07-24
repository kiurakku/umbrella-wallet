# 05 — Web Routes & UI Flows

## Route map

| Path | Component | Description | Auth required |
|------|-----------|-------------|---------------|
| `/` | `index.tsx` | Wallet dashboard: total balance, assets, recent P2P deals | ✓ |
| `/p2p` | `p2p.tsx` | P2P market: offers, my deals, my offers | ✓ |
| `/exchange` | `exchange.tsx` | Swap preview, rates, create offer shortcut | ✓ |
| `/stats` | `stats.tsx` | Portfolio analytics, sparklines, distribution chart | ✓ |
| `/nft` | `nft.tsx` | NFT gallery (read-only, demo data for now) | ✓ |
| `/settings` | `settings.tsx` | Profile, wallets, seed, 2FA, KYC, privacy, language | ✓ |
| `/help` | `help.tsx` | FAQ, support, documentation links | (public) |
| `/legal/terms` | `legal/terms.tsx` | Terms of Service | (public) |
| `/legal/privacy` | `legal/privacy.tsx` | Privacy Policy | (public) |
| `/legal/agreement` | `legal/agreement.tsx` | User Agreement | (public) |

---

## Onboarding flow

### First visit (no session)
```
1. Cover screen
   • Umbrella logo + wordmark
   • "Anonymous wallet, exchange, P2P"
   • Button: "Get started" → step 2
   • Link: "I already have an account" → step 4 (skip to auth)

2. Slide 1: "Anonymous by default"
   • Icon: EyeOff
   • Text: No email, phone, or documents. Nickname + password only.

3. Slide 2: "Keys stay with you"
   • Icon: KeyRound
   • Text: Seed encrypted on your device. We never see your keys.

4. Slide 3: "P2P without middlemen"
   • Icon: Users
   • Text: Trade directly at live rates. Telegram notifications are optional.
   • Button: "Create wallet" → step 5

5. Auth form
   • Toggle: Sign up | Log in
   • Fields: Nickname (3–32 chars), Password (≥8 chars)
   • Button: "Create account" / "Log in"
   • Optional: "Continue with Telegram" (if not privacy mode)
   • Footer: links to Terms, Privacy, User Agreement
```

### After successful auth
```
6. Seed onboarding (if no seed exists)
   • Detect: check IndexedDB for seed:{userId}
   • If missing:
      a. Generate 12-word mnemonic (BIP39)
      b. Show phrase with "Write this down" warning
      c. Quiz: ask user to enter 3 random words to confirm backup
      d. Prompt: "Set encryption password" (≥8 chars, different from account password recommended)
      e. Encrypt seed with Argon2id + AES-GCM
      f. Save to IndexedDB
      g. Derive first addresses (ETH, BTC, SOL, TRX)
      h. POST /wallets for each derived address → link to account
   • User can skip seed setup (flag: skipSeedSetup:{userId} in localStorage)
      → can import later in /settings

7. Dashboard (index.tsx)
   • Show "Link accounts" prompt if no wallets/banks linked
   • Otherwise: portfolio overview
```

---

## Browser vault (IndexedDB) structure

```javascript
// Database: "umbra-vault"
// Object store: "vault"

// Key format: "seed:{userId}"
// Value:
{
  ciphertext: "base64...",  // AES-256-GCM output
  iv: "base64...",          // 12 bytes
  salt: "base64...",        // 16 bytes for Argon2id
  version: 1
}
```

**Encryption password** is never stored — only exists in memory during unlock. User must re-enter on each session (or use browser auto-fill, at their own risk).

---

## Demo mode

When `VITE_DEMO=true` (dev only):
- Login bypass: username `demo`, password `password123`
- Mock data shown instead of real API calls
- "Demo mode" banner at top
- No real tx sending (simulated success toasts)

**Demo users:**
```javascript
// src/lib/api/demo.ts
export const DEMO_TEST_USERNAME = "demo";
export const DEMO_TEST_PASSWORD = "password123";
```

---

## Aggregator model (linking external wallets)

### WalletConnect flow
```
Settings → Wallets → "WalletConnect (QR)"
  → walletConnect.ts: initWalletConnect()
  → QR modal appears (user scans with MetaMask / Trust Wallet)
  → User approves connection
  → walletConnect.connect() resolves: { address, chain, label }
  → GET /wallets/challenge → { message: "nonce-timestamp" }
  → signLinkProof(message, address) → signature
  → POST /wallets { chain, address, label, message, signature }
  → Backend verifies signature (ecrecover / ed25519 verify)
  → Saves to linked_wallets table
  → Frontend refetches GET /wallets → balance updates
```

### Injected wallet (MetaMask / Rainbow in browser)
```
Settings → Wallets → "Browser wallet"
  → Detect: window.ethereum
  → connectInjectedWallet() → eth_requestAccounts
  → Same signature flow as WalletConnect
  → POST /wallets
```

### Monobank link
```
Settings → Payment methods → "Connect Monobank"
  → User pastes Monobank personal token (X-Token from https://api.monobank.ua)
  → POST /bank-accounts/monobank/link { token }
  → Backend calls GET https://api.monobank.ua/personal/client-info
  → Saves: provider='monobank', providerAccountId, maskedIban, bankName
  → Token encrypted with AES-256 (key from env BANK_TOKEN_ENCRYPTION_KEY)
  → Stored in linked_bank_accounts.encryptedProviderToken
```

---

## Privacy mode toggle

```
Settings → Privacy (Tor) mode → Switch
  → isPrivacyMode() checks localStorage flag "umbra:privacyMode"
  → When ON:
      • Telegram SDK: NOT loaded (no WebApp.initData)
      • Google OAuth: disabled (button hidden)
      • Apple OAuth: disabled
      • WalletConnect: modal warns "Tor connection recommended"
      • All API calls: user should proxy through Tor Browser (SOCKS5)
  → When .onion domain detected:
      • Privacy mode forced ON (cannot be toggled OFF)
      • Toast: "Always active on .onion — no third-party connections"
```

---

## Theming (light / dark)

- CSS variables in `src/styles.css`
- Toggle in top-right corner (moon / sun icon) or Settings → Appearance (future)
- Persisted in localStorage: `theme: "light" | "dark"`
- Default: `prefers-color-scheme` media query

---

## Honest boundaries (what the web app CANNOT do)

1. **Screenshot protection** — Impossible in browsers. Seed phrase screen has warning: "Do not screenshot."
2. **Hardware Secure Enclave** — IndexedDB is software-encrypted. For ultimate security, desktop app (Tauri) or hardware wallet (Ledger) required.
3. **Tor built-in** — User must manually use Tor Browser or configure SOCKS5 proxy. No embedded Tor in web.
4. **XSS resistance** — CSP mitigates, but web is always attack surface. We scrub secrets from logs, use strict CSP, and recommend desktop app for large holdings.

---

## Future: Progressive Web App (PWA)

Not in MVP, but possible:
- `manifest.json` — install to home screen
- Service worker — offline asset caching (but NOT seed vault — IndexedDB already persists)
- Push notifications — via backend WebPush (subscribe on settings toggle)

Pros: native-like UX, no app store approval.
Cons: still subject to web security model (no screenshot protection, no hardware keychain).
