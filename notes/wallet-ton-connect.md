# TON Connect, as Telegram does it

Source: `wallet.md` and `client-flows.html` (September 2026, layer 230), from the TON team. This
note records what they mean for *our* code — the mapping onto TDLib, what we have to build, and the
one thing that currently blocks it. The documents themselves are the specification; do not restate
them here.

## Which layer

**All of this is layer 230.** The wallet was briefly split across 230 and 231; on 2026-09-25 it was
consolidated back into 230 and 231 is empty again, because 230 is not being released on its own.
Anything below that says 231 means 230 - the constructors did not change, only the layer they
landed in.

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

Layer 230 closed the two gaps this note used to record: `wallet.tonConnectNextEventId` is
`getTonConnectSessionNextEventId`, `wallet.tonConnectCloseSession` is `disconnectTonConnectSession`,
and `updateWalletTonConnectPendingDisconnect` is `updateTonWalletTonConnectSessionDisconnectRequired`.
Nothing in the protocol is unreachable from TDLib any more.

## The session key — solved 2026-09-25

`desktop-app/patches/wallet-engine.patch` adds the whole MTProto-relayed flow to the engine, and
layer 230 adds the TDLib half. **Neither a NaCl nor the seed is needed on our side**: the key is
derived inside Rust and never leaves it. What the earlier version of this note called a blocker was
right only about the bridge session being the wrong tool.

Everything below is reached through `WalletLifecycle` or the `TonConnectDerivedSession` it returns:

| | |
| --- | --- |
| `DeriveTonConnectSession(descriptor, dappClientId, nonce)` | the `tonconnect/session/v1` derivation |
| `PublicKeyHex()` | `W`, the key to register |
| `OpenChallenge(challenge)` | the 32-byte answer |
| `EncryptConnectEvent` / `EncryptConnectError` | the sealed connect reply |
| `DecryptRequest(body, now)` | an incoming request |
| `EncryptSendSuccess` / `EncryptSignDataSuccess` / `EncryptError` | the sealed answers |
| `EncryptDisconnectEvent` / `EncryptDisconnectSuccess` | both directions of disconnect |
| `DecodeSignDataCell(schema, cell)` | a `signData` payload, by TL-B schema |
| `TonConnectAccount(descriptor)` | address, StateInit and key, reading no secret |
| `SignTonConnectProof(request)` | `ton_proof` |

**The patch is not on any public branch of `i582/wallet-engine`.** Our clone is patched and the
patch is not committed there, so a fresh clone has to reapply it - and a rebuilt `wallet_engine.dll`
must be committed together with a regenerated `wallet_engine.cs`, because the bindings assert a
checksum per function against the library they load.

## Where the app is now

Done, and reached from a link end to end:

- `internalLinkTypeTonConnect` in `MessageHelper` - the temporary `startapp` decoder is deleted.
- `WalletConnectPopup`: creates the session, follows the manifest, shows the dApp and the wallet
  card, derives the key, registers it, opens the challenge, and sends the sealed `ConnectEvent`.
- `ton_proof`, including the detail that decides whether a dApp accepts it: after a rotation the
  proof's key is not the anchor key, and the `ton_addr` reply beside it must carry the proof's.

## What is left

In the order the protocol needs them, not the order they matter to a user.

### 0. What the sheet looks like, and what opens it

**Only `sendTransaction`.** `signData` and `signMessage` are not supported and are answered with an
error rather than shown - decided 2026-09-25.

`WalletRequestPopup` is built, to the design Fela supplied: two states of one sheet rather than two
popups, because the header morphs between them - the avatar shrinks 96 to 36, the title and domain
stay, and the left button turns from dismiss into back. Page one is `WalletCard` in its new
`SetTransfer` mode, carrying the signed amount with the *recipient* engraved on it and an info
button over the corner. Page two is the TRANSFER row and the PREVIEW list. The fee line sits under
both, because it belongs to neither. It is fed by `WalletRequest` (`Services/Wallet/WalletRequest.cs`),
which is the contract the protocol half has to fill.

**Nothing opens it yet, and that is the next thing to build.** The entry point is a service message
content, and the chain to copy is `MessageTonWalletTransfer`, which already exists end to end:

| step | file |
| --- | --- |
| item type | `Controls/Chats/ChatHistoryViewItem.cs` - add to the enum |
| content to item type | `Views/ChatView.Bubbles.xaml.cs` (~1701) |
| item type to template | `Views/ChatView.xaml.cs` (~157), `AddStrategy` |
| the template | `Views/ChatView.xaml` |
| the control | `Controls/Messages/Service/MessageTonConnectRequestContent.xaml(.cs)` |
| the text | `Controls/Messages/MessageServiceText.cs` (~86 and ~2027) |

The control's tap is what opens the sheet, and what it needs first is the request itself:
`getTonConnectSessionPendingRequests(session_id)` matched on `message_id`, the engine's
`DecryptRequest(body, now)`, the validation below, then `PreviewTonConnect` for the emulation that
fills `WalletRequest.Actions`. The one piece with real ambiguity is
`SendEmulationAction.DetailsJson` - amount, comment and which side our address is on all come out
of that JSON, and the direction is not in the data.

`messageTonConnectRequest` also carries `state:TonConnectRequestState`, so a message that was
already answered - by this device or another - must draw as accepted or declined rather than
offering a sheet.

### 1. The request sheet — the biggest piece, and nothing else works without it

A dApp that is connected can ask for `sendTransaction`, `signData` and `signMessage`. The request
arrives on every device as a service message from 777000 (`messageTonConnectRequest`), which carries
no ciphertext - it is a pointer. None of this is written yet:

- handle `MessageTonConnectRequest` as a message content, and open a sheet from it
- `getTonConnectSessionPendingRequests(session_id)`, matching on `message_id`
- `DecryptRequest(body, now)`, then the validation the spec requires: `valid_until` in the future,
  `network` ours, `from` our address, message count within `maxMessages` - any failure is answered
  with error code 1 rather than shown
- claim **at the tap**, not at the sheet: `claimTonConnectRequest(session_id, message_id,
  dapp_request_id, is_rejected)`. It is atomic across devices; the loser gets
  `TONCONNECT_REQUEST_ALREADY_CLAIMED`, and the server edits the service message so every other
  device sees `accepted`/`declined` through an ordinary message update
- sign, then `answerTonConnectRequest(session_id, message_id, trace_id, body)`
- the expiry: the sheet closes itself when `expiration_date` passes, with no signal from the server
- a device that has not yet answered a challenge in this session must `setTonConnectSessionWalletClientId`
  first and pass the answer with its first claim

`dapp_request_id` is a **string**; it was `int64` before layer 230.

### 2. Disconnect, in all three directions

All three methods exist now - they were the gap this note used to record.

- **dApp asks**: arrives as an ordinary request with `topic=disconnect`; claim, answer `{}`, and the
  session closes. No disconnect event is sent back - the spec forbids it.
- **We ask**: `getTonConnectSessionNextEventId(session_id)`, then `EncryptDisconnectEvent(eventId)`,
  then `disconnectTonConnectSession(session_id, body)`. Idempotent.
- **The server asks**: `updateTonWalletTonConnectSessionDisconnectRequired` carries session ids and
  the state becomes `Closing`. The first device that can derive the session sends the disconnect.
  This fires on wallet key rotation, so it is not an edge case - and less of one than it looks,
  because the server now watches `change_wallet_key` on chain itself and reacts to a rotation
  performed by *any* client, ours included. Replacing the phrase from the disable-backup flow will
  close every connected dApp session, which is correct and worth saying on screen somewhere.

### 3. Connected apps

`getTonConnectSessions` for a screen listing them, with disconnect per row. Nothing exists yet;
`WalletBackupPopup` is the shape to copy.

### 4. The embedded request

`internalLinkTypeTonConnect.rpc_request` is a base64url JSON request meant to be answered **inside
the same `ConnectEvent`**, so the dApp gets connection and answer in one event. Currently ignored.

### 5. Late requests to a closed session

Delivered for at least 7 days after closing. No sheet: decrypt, claim, and answer
`error 100 UNKNOWN_APP` with the same id, so the dApp drops its stored state instead of waiting for
a timeout.

## Wallet, outside TON Connect

- **~98 bracketed placeholder strings** across the wallet views. They are the last thing between
  this and shippable, and they need real Android strings rather than invented ones.
- `BadgeControlButtonStyle` is still uncommitted in `Themes/CommonStyles.xaml`, so two popups
  resolve it to nothing at runtime.
- `WalletCardSheen.Accent` is unconsumed: `#6DDCFF` is still a literal in `WalletCard.xaml` and
  `MessageTonWalletTransferContent.xaml`.
- The deferred set from `notes/wallet-engine-csharp-todo.md`: Hello/TPM tiering for
  `WalletSecretStore` and its no-Hello fail-closed bug, rotate-on-disable-backup, durable pending
  rows from the journal, and the dead markup in `WalletSharePopup.xaml`.
- New in layer 230 and unhandled: `tonWalletTransactionTypeOnRampDeposit` - the receipt and the
  history cell will fall through to nothing for it.
- `disableTonWalletBackupWithProof` and `replaceTonWallet` are unreachable as TDLib declares them:
  both want `private_key:bytes` and wallet-engine emits no private key. See
  `notes/wallet-encryption.md` for the TL change requested to take a signature instead.

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
