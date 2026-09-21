# TON Connect, as Telegram does it

Source: `wallet.md` and `client-flows.html` (September 2026, layer 230), from the TON team. This
note records what they mean for *our* code — the mapping onto TDLib, what we have to build, and the
one thing that currently blocks it. The documents themselves are the specification; do not restate
them here.

## The one fact that changes everything

**Our devices never talk to the TON Connect bridge.** The dApp does — to `ton.stel.com/bridge2`,
speaking ordinary TON Connect, and as far as it is concerned we are an ordinary wallet. Everything
that reaches us arrives instead as **service messages from 777000**, as ordinary MTProto updates,
and replies go out through server methods.

That retires an assumption this repo was carrying. wallet-engine's `ITonConnectSession` — with
`IngestSseChunk`, `PendingPost`, `PrepareDisconnectSuccess` — is written for a wallet that owns an
HTTP bridge connection. **It is the wrong tool and should not be used for this.** The engine's part
in TON Connect here is much smaller: signing a `ton_proof`, and whatever the wallet key is needed
for.

The server never sees plaintext. It holds sessions, hands out event numbers, and checks that a
device owns the key it claims - nothing more.

## The TL and what TDLib calls it

`wallet.md` gives the MTProto layer. TDLib wraps it, and the names differ enough to be worth a
table:

| MTProto (`wallet.md`) | TDLib (`td_api.tl`) |
| --- | --- |
| `wallet.tonConnectCreateSession` | `createTonConnectSession` |
| `wallet.tonConnectRegisterKey` | `setTonConnectSessionWalletClientId` |
| `wallet.tonConnectSubmitConnectResult` | `sendTonConnectSessionConnectResult` |
| `wallet.tonConnectGetPending` | `getTonConnectSessionPendingRequests` / `getTonConnectDAppPendingRequests` |
| `wallet.tonConnectClaimRequest` | `claimTonConnectRequest` |
| `wallet.tonConnectSubmitResponse` | `answerTonConnectRequest` |
| `wallet.tonConnectGetSessions` | `getTonConnectSessions` |
| `updateWalletTonConnectSession` | `updateTonWalletTonConnectSession` |
| `messageActionWalletTonConnectRequest` | `messageTonConnectRequest` |

So TDLib's `wallet_client_id` is the document's **W**, and its `tonConnectChallenge` is the
document's challenge verbatim.

`wallet.tonConnectNextEventId` and `updateWalletTonConnectPendingDisconnect` have **no TDLib
equivalent in the schema we have**, which means wallet-initiated disconnect and server-initiated
disconnect cannot be implemented yet. Worth raising.

## What blocks us: the session key

Every device derives the same X25519 pair from the wallet key, and the scheme is fully specified
(v1, identical on every platform):

```
seed = 32 bytes of the wallet's Ed25519 private key
prk  = HMAC-SHA512(seed, "tonconnect/session/v1")
okm  = HMAC-SHA512(prk, hex_decode(A) || nonce)
sk   = clamp(okm[0..31])          // sk[0] &= 248; sk[31] &= 127; sk[31] |= 64
W    = X25519_publickey(sk)
```

and the challenge is `crypto_box_open(box, nonce, ephemeral_pk, sk)` over the 104 bytes
`ephemeral_pk(32) ‖ nonce(24) ‖ box(48)`, giving the 32-byte answer.

**We can do none of this today**, for two reasons, and neither is a small job:

1. **No NaCl.** The app has no libsodium, no tweetnacl, nothing. `crypto_box_open` is
   XSalsa20-Poly1305 over an X25519 shared secret; WinRT's `EccCurveNames.Curve25519` is an ECDH
   primitive and does not get us there. HMAC-SHA512 we have.
2. **No seed.** The 32-byte Ed25519 seed lives in the engine's protected storage and is deliberately
   never handed out - `RevealRecoveryPhrase` returns the mnemonic, and the mnemonic-to-seed mapping
   is the engine's, with several `MnemonicScheme`s to choose between.

Both point the same way: **this belongs in wallet-engine**, which already has the seed, the
crypto, and a reason to be the only thing that touches either. What it would need to expose is
small and shaped by the document rather than by the bridge - roughly `derive_session_key(a, nonce)`
returning W, `open_challenge(challenge)` returning the answer, and `seal(a, plaintext)` returning
`nonce(24) ‖ box`. That is a conversation with TON Org, not something to work around here.

The alternative - a NaCl in C# or in `Telegram.Native`, plus prising the seed out of the engine -
would put the wallet's private key through our own crypto. It should not be chosen by default.

## Where the app is now

- `TonConnectLink` parses the `startapp` payload. **Temporary**: TDLib will report these as a link
  type of its own.
- `MessageHelper` recognises both link shapes and opens `WalletConnectPopup`.
- `WalletConnectPopup` calls `createTonConnectSession`, follows `updateTonWalletTonConnectSession`
  for the manifest, and shows the dApp with the wallet card. Its Connect button is where the
  derivation would go, and is the `TODO`.

Not started, in the order the document puts them: the request sheet
(`messageTonConnectRequest` -> `getTonConnectSessionPendingRequests` -> claim -> answer), the
connected-apps screen (`getTonConnectSessions`), and disconnect in all three directions.

## Details that will bite

- **`body` is raw bytes, never base64.** The document is explicit: the backend does the base64, the
  client never sees it. Our `Boc` handling has the opposite convention, so this is a trap.
- **Claim at the tap, not at the sheet.** `claimTonConnectRequest` runs when the user presses
  Confirm or Decline, and it is atomic - whichever device gets there first wins, and the others see
  the service message edited with `accepted`/`declined` through an ordinary message update.
- **A device that has not answered a challenge in this session must `registerKey` first** and pass
  the answer with its first claim. That is how a second device joins.
- **A late request to a closed session is still delivered**, for at least 7 days, and must be
  answered `error 100 UNKNOWN_APP` without showing a sheet - it is how the dApp learns to forget.
- `expires` is at most 300 seconds; the sheet closes itself when it passes, with no signal.
- At most 10 pending requests per session; the server drops the rest silently.
