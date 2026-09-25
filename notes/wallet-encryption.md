# How the wallet's secret is kept

The design Fela settled on, 2026-09-25. `Telegram/Services/Wallet/WalletVault.cs` holds the key,
`WalletSecretStore` encrypts every secret with it, and `WalletVaultPrompt` asks the user.

## The contract this rests on

The wallet is **created automatically** and its recovery phrase is **backed up to the server
automatically**. The phrase is never shown unless the user asks for it, or disables the cloud
backup — and disabling it *forces* them to write the words down and proves it by asking for three
of them at random **before** the backup is actually turned off.

So the user cannot lose the phrase short of their own negligence, and **the local copy is never the
only copy**. Everything below follows from that: we can fail closed, because there is always a way
back — a silent re-bind from the cloud, or a re-import from paper.

## The shape

One **data encryption key**, 32 bytes, generated once and **never changed**. Everything the wallet
stores is encrypted with it. Changing how it is guarded rewraps 32 bytes; the data is not touched.
Re-encrypting the data on every passcode change would be slower and a chance to lose it.

Exactly one method guards it at a time — they are alternatives, not layers, because two would mean
the weaker one decides:

| method | KEK |
| --- | --- |
| `None` | nothing above DPAPI |
| `Passcode` | PBKDF2-SHA256 over the app passcode, 210k iterations, its own salt |
| `Hello` | SHA-256 of a TPM signature over the salt, via `KeyCredentialManager` |

**DPAPI (`LOCAL=user`) is underneath all three, always.** It costs nothing, asks nothing, and is
what stops the file being useful on another machine. It matters most under `Passcode`, where the
secret above it may be four digits and would otherwise fall to an offline guess instantly.

Two things worth keeping straight:

- **The app's passcode hash is not a key.** `PasscodeService.Set` stores SHA-1 of
  `salt‖passcode‖salt` — a verifier, and meant to be cheap. The vault runs the *same passcode*
  through PBKDF2 with an independent salt. Never derive a KEK from the login hash.
- **Hello here is a key, not a prompt.** `UserConsentVerifier`, which the old code used, only asks
  and then trusts the caller; anything running as the user can skip it. `KeyCredentialManager`
  gives a private key the TPM holds and only releases after Hello, so the signature - and the KEK
  from it - cannot be produced without the user.

The wrap is AES-GCM, so a wrong passcode fails the tag and is reported as `Incorrect` rather than
handing back 32 bytes of nonsense for the engine to choke on. The same primitive encrypts the
secrets themselves — `WalletVault.Encrypt`/`TryDecrypt`, shared rather than written twice, so the
two cannot drift into two formats.

## Who asks the user, and where

`WalletVault.LeaseAsync(navigation, reason)` is the one way in. The **operation** takes a lease
before it hands any work to the engine, and holds it for the duration; the secret store reads the
leased key and asks nothing. One lease per operation and a prompt per lease, nothing cached between
them — the engine reads a secret when it is about to sign, so an operation is a spend, and "confirm
all spending" only means something if each one asks.

**Enrolling is the one exception**, and it is a proof rather than a cache. Choosing how the wallet
is guarded and producing the credential there and then *is* a confirmation, and Windows charges two
prompts for it on Hello — one to create the credential, one to sign with it. The operation that
caused the enrollment would otherwise ask for the same credential a third time a second later. So a
key created during enrollment is kept for one minute and the next lease reuses it. An ordinary
unlock grants nothing: there the prompt is the confirmation of that particular spend. The window is
measured from the enrollment and never extends itself.

**The window is always a parameter.** `INavigationService` is threaded from the caller through
`IWalletService.SendAsync`/`BindAsync`/`ConnectAsync`/`DecryptCommentAsync`/`RevealRecoveryPhraseAsync`
into `LeaseAsync` and on into every `IWalletVaultPrompt` method. Nothing looks a window up. An
earlier draft resolved `WindowContext.Active` inside the prompt, which was wrong twice over: it
throws away the explicit passing the rest of the app is built on, and the wallet is reachable from
every window in the app, so "the current one" has no correct answer.

**Leases are serialized**, one at a time. A second spending operation waits for the first to finish
and then asks on its own account, rather than riding on a confirmation the user gave for something
else; waiting before asking rather than after is also what stops two prompts stacking up on them.
It follows that an operation must not take a lease while holding one — it would wait for itself.

That is a decision about confirmation, not a limit of the engine: wallet-engine is built for
concurrent calls (`RefreshNfts` is documented as independent of `refresh` precisely so that account,
activity and NFT data can load at once, and it answers `Skipped` rather than corrupting anything
where an operation cannot overlap itself). Everything that touches no secret — the fee estimate, DNS
resolution, refresh, activity paging — takes no lease and still runs concurrently.

That is also *why* the lease exists. The engine's host interface is generated —
`ReadProtectedSecret(ProtectedSecretRead)` carries nothing of ours and offers no way to tell which
call a callback belongs to — so a prompt raised from an engine thread could never know which window
it belonged in. Unlocking before the engine call removes the question instead of answering it
badly, and no prompt is ever raised off a UI thread.

The asking itself is `IWalletVaultPrompt`, and `WalletVaultPrompt` implements it — holding no window
of its own and marshalling onto the one it is given.

Two of the three questions are answered by screens that already exist.
`SettingsPasscodeConfirmPopup` counts the attempts and honours the lockout, so asking for the
passcode reuses it rather than adding a second way to guess; `SettingsPasscodeInputPopup` is what
sets one when the user picks the passcode and has none. `WalletEnrollPopup` is the new one, and it
only reports the *method* — the passcode is collected after it closes, because both screens that can
produce one are popups themselves and would otherwise have to open over it.

**Hello goes through a `ModalPopup` of ours** (`WalletHelloPopup`). Windows raises its PIN window
over whatever is on screen and says nothing about who asked or what for, so the popup sits behind it
and does, and gives a failure somewhere to be read and retried — on its own, a Hello window that
goes away has told the user nothing. The popup raises it on load rather than behind a button, closes
itself on success, and offers *Try Again* otherwise. Two failures do not get a retry: a cancel is
the user answering, and `Unenrolled` cannot be retried at all — both close and let the caller act.

`WalletService.EnsureStores` sets `Prompt`, so the enrollment screen is what the user meets the
first time anything is written. A secret read that arrives with no lease open is an operation that
forgot to take one: it fails as `PolicyViolation` rather than guessing a window to interrupt.

## What the engine asked for is not what is asked

`ProtectedSecretStore.RequireUserPresence` is dropped on the floor. It is the engine's default for
that secret, and the user has since been asked directly what they want to be asked for. Theirs wins.
It was also the live bug: the old store recorded the flag per entry and verified it with
`UserConsentVerifier`, which guards nothing a caller cannot skip, and left entries a device could no
longer satisfy.

## Nothing is tied to `is_backup_enabled`

An earlier draft made the protection depend on whether the cloud backup was on. **That was wrong**:
the flag is account state that any session can flip, so we would be silently at one tier while
believing another, and strengthening on demand would need the plaintext and therefore a prompt out
of nowhere.

The contract removes the need entirely — recoverability holds in *both* states — so the vault reads
no remote state at all. `is_backup_enabled` is a UI concern for the disable flow, never a crypto
input.

## What the user is asked

Modelled on tdesktop. The first time it is needed, three options, mutually exclusive:

- **Unlock with Windows Hello** — "Confirm all spending with your Windows PIN"
- **Ask for local passcode** — "Confirm all spending with your Telegram passcode"
- **Don't ask** — danger brush — "Anyone with access to your Telegram can spend your funds"

Choosing the passcode when none is set leads into the passcode enrollment. The choice is changeable
at any time from the wallet's `...` menu — *Confirm Spending*, with the current method beside it —
which calls `WalletVault.ReenrollAsync`.

**The credential is asked for after the choice, not before.** Looking at how the wallet is guarded
is not a privileged act; changing it is. So the screen opens for nothing, and the lease is taken
only once there is something to write — still before anything is written, so somebody who cannot
spend still cannot turn the asking off. Saving without changing the selection asks for nothing at
all, and neither does *keep asking for this passcode* on the way to disabling it, which is a refusal
to disable rather than a change.

On a vault with nothing enrolled yet there is nothing to prove, and the lease *is* the enrollment,
so it asks once rather than twice.

## When the app passcode moves

`WalletPasscodeGuard` opens every vault the passcode guards — one per account, since the passcode is
one app-wide setting and each account has a vault of its own — and holds them open across the whole
operation. A vault left wrapped under the old passcode cannot be opened by the new one, and that
wallet would have to be bound again.

- **Changed** (`SettingsPasscodeViewModel.Edit`). The guard opens before anything is asked, so the
  lease *is* the old passcode in hand; the new one is collected, the vaults are rewrapped, and only
  then is the app's own passcode set. That order is deliberate: a vault that cannot be rewrapped
  stops the change while everything still agrees, where the other way round leaves the wallet asking
  for a passcode that no longer exists.
- **Disabled** (`ToggleAsync`). Each guarded wallet gets the same three options, re-worded for the
  moment — Hello / *Keep asking for this passcode* / Don't ask. Coming back still on the passcode,
  whether by choosing to keep it or by backing out, cancels the disable: the passcode cannot be half
  turned off. It re-enrolls against the lease the guard already holds, so the passcode is asked for
  once, not twice.
- **Enabled from nothing.** No vault can be using a passcode that did not exist, so there is nothing
  to rewrap. The exception is a vault left on `Passcode` by an app version that disabled it without
  asking — it re-binds, which is the same recovery as any other lost credential.

## The trap in writing any of this

`Telegram.csproj` — the legacy UWP flavour — compiles these same sources, and has **none** of
`System.Security.Cryptography.AesGcm`, `RandomNumberGenerator.Fill`, `SHA256.HashData` or the
span-based `Rfc2898DeriveBytes`. Everything goes through WinRT crypto instead
(`SymmetricKeyAlgorithmProvider`, `KeyDerivationAlgorithmProvider`, `HashAlgorithmProvider`), which
is what `Common/WebAppStorage.cs` already does for AES-GCM.

## Asking before the screen, not on it

The disable-backup popup is priced before it opens: `WalletBackupViewModel` binds the device and
estimates the rotation fee, then hands the number to the popup. Doing it when the checkbox is
ticked - which is how Android reads, and how this was first built - meant the user opened a popup to
make one decision and was then asked for the account password and the wallet credential to fill in
a line of text on it. An update that cannot be priced is not offered at all: the checkbox is hidden
rather than shown without a cost.

`WalletUpdateSecretPhraseInfo`, the fee-less wording, covers a fee that comes back as zero - which
a key change should never do, being an external message the chain charges for, but `Network fee: 0`
would read stranger than saying nothing about the cost. It is presumably what Android shows while
the fee is still being worked out, which this flow never has to display.

## Replacing the phrase

`WalletService.UpdateRecoveryPhraseAsync` performs the rotation the disable-backup popup offers.
The address does not change; the signing half of the phrase does, and the new one has never been
anywhere but this device. **The ordering is the whole of the safety**, and every step of it is
forced by something that would otherwise lose the wallet:

1. **The backup is turned off first, then the phrase is replaced.** The other way round leaves the
   server holding a phrase that no longer signs — a backup that looks like one and restores a
   wallet nobody can spend from — for as long as the disable takes, or forever if it fails.
2. **The replacement phrase is stored before the message is submitted.** A key change that lands
   while the phrase behind it is nowhere leaves a wallet nobody can sign for.
3. **The descriptor is switched only once the chain shows the new key.** Until the contract accepts
   it, the old key is still the one that signs, so `_descriptor` and `_boundKey` stay where they
   are and `VerifySigningKeyAsync` is not allowed to mistake our own rotation for someone else's.
4. **The old phrase is forgotten last**, after the new descriptor is on disk, or a crash between
   the two leaves neither.
5. **Nothing else may send while one is out.** The contract takes one message per sequence number
   and the rotation is using it, so `SendAsync` throws `WalletRotationPendingException` rather than
   racing it.

The pending state is its own file, `rotation<suffix>.bin`, holding the replacement descriptor, the
key the contract should end up with, and the expiry the old key's signature covers. It is read back
on restore and settled against the chain — committed when the key appears, discarded once the
expiry passes, because a signature that can no longer be included can never set the key. The signed
message is deliberately *not* kept: resubmitting it is never the right answer, and discarding is.

Settling is polled, not awaited: nothing reports a rotation — TDLib keeps reporting the key the
wallet was created with, so the contract is the only authority — and the user should be writing the
new phrase down rather than watching a spinner. It is shown to them the moment the message is
accepted, because at that point it is the only copy in existence.

## How many times the user is asked

Counted end to end for disabling the backup *and* replacing the phrase, the longest thing the
wallet does:

| situation | prompts |
| --- | --- |
| vault enrolled, bound or not | 1 credential + 1 account password |
| vault not enrolled yet | 2 credential (Hello create + sign) + 1 account password |
| account has no 2-step verification | the credential only |

**One lease and one password cover the whole operation.** `WalletBackupViewModel.DisableBackup`
opens the vault first, then hands that lease to the bind, the estimate and the rotation in turn;
`WalletHelper.BindAsync` hands the account password back, because disabling the backup wants the
same one that bought the phrase from the cloud. Splitting one user action into separate leases, and
asking for the same password at each end of it, is what made this four prompts when unbound.

The vault is opened **before** binding rather than after, and that ordering is the point: binding
writes the phrase into the vault, so a lease taken afterwards is a second prompt rather than the
only one. It also means a declined prompt stops there — the bind is skipped rather than opening a
vault of its own and asking again — and the backup can still be turned off, which needs no key.

**The two prompts on enrolling Hello cannot be reduced.** `RequestCreateAsync` verifies the user to
create a Hello-bound key, and `RequestSignAsync` verifies them to use it; `KeyCredential` offers
signing and nothing else — no encrypt or decrypt — so the KEK can only be a signature, and wrapping
the key at enrollment needs exactly one signing operation. It happens once ever, per account.

The account password is asked twice only when the device is not bound, because binding fetches the
cloud phrase with it and disabling the backup needs it again. TDLib's
`disableTonWalletBackupWithProof` would remove that second ask, but it wants private-key bytes and
the engine states outright that none cross its API boundary.

**A lease held across a popup holds the vault's gate.** Anything else that needs the key - a TON
Connect request arriving, say - waits until the popup is closed. That is the serialization working
as intended, but the waits are now as long as a person takes to read a screen.

## Turning it back on

`WalletBackupViewModel.EnableBackup` uploads the phrase again, and needs the opposite of what
disabling needs: the phrase has to come **from this device**, because the server is being asked to
store one it does not have. So it binds, reads the phrase out of protected storage and sends it with
the account password - one lease over the bind and the read, and the password the bind already
collected, which is the same shape as the disable flow.

The section follows the account rather than a local flag: `IsBackupEnabled` picks the button and
`CanEnableBackup` decides whether the offer exists at all - a wallet still being created cannot be
backed up, and neither can one the server has no phrase for. With neither true the whole section
goes, rather than showing a button the server would refuse. The footer changes tense with it, which
is the only difference between `WalletBackupEnabledDescription` and its Disabled twin: the same
sentence told as a fact or as an offer.

## Proving ownership without the private key

The app cannot produce a private key and never will: wallet-engine exposes none -
`RotationMnemonicPublicKey` answers with the *public* anchor, and `SignTonConnectProof` states
outright that "no mnemonic or private-key bytes cross the API boundary". Deriving one here instead
would mean reimplementing TON's mnemonic-to-keypair derivation outside the component that owns it,
picking a scheme by hand where `DetectMnemonicSchemes` exists, and WinRT has no Ed25519 to do it
with.

**It does not need to.** The server is replacing the private-key methods with a challenge, and the
proof it wants is the one the engine already signs:

1. `wallet.getProofChallenge` answers `{payload, expires, domain}` - `telegram.org`, one attempt,
   300 seconds.
2. The client signs a TON Connect **ton-proof-item-v2** over the address derived from the public
   key being claimed (v6r2, workchain 0).
3. `wallet.replaceWallet{ inputWalletImported{ public_key, proof:{ timestamp, signature } } }`.

`IWalletLifecycle.SignTonConnectProof(descriptor, domain, timestamp, payload)` is exactly step 2 -
Rust builds the protocol digest itself - and it answers with the 64-byte signature and the 32-byte
public key the proof was made under, which is both halves of `inputWalletImported`. The same
challenge answers `wallet.disableBackup` without a password, with a proof under the current key.

**What is missing is the TDLib surface**, not the ability. It is already on layer 230 and does the
challenge internally, but `Libraries/tdjson/td_api.tl` still asks the caller for the key -
`replaceTonWallet password address private_key` and `disableTonWalletBackupWithProof address
private_key` - and nothing here can hand it one. The change to request (sent 2026-09-25):

```tl
//@description Contains a challenge to be used to prove ownership of a TON wallet
//@payload Payload that must be included in the proof
//@expiration_date Point in time (Unix timestamp) when the challenge expires
//@domain Domain that must be included in the proof
tonWalletProofChallenge payload:string expiration_date:int32 domain:string = TonWalletProofChallenge;

//@description Returns a challenge for proving ownership of a TON wallet.
//-The challenge can be used only once and expires in 300 seconds
getTonWalletProofChallenge = TonWalletProofChallenge;

//@description Contains a proof of ownership of a TON wallet
//@public_key Public key of the wallet, 32 bytes
//@timestamp Point in time (Unix timestamp) when the proof was created
//@signature Ed25519 signature of the TON Connect ton-proof-item-v2 message, 64 bytes
tonWalletOwnershipProof public_key:bytes timestamp:int32 signature:bytes = TonWalletOwnershipProof;

//@description Replaces the current TON wallet with another existing v6r2 wallet
//@password Password of the current user; pass an empty string if the user has no password
//@proof Proof of ownership of the new wallet
replaceTonWallet password:string proof:tonWalletOwnershipProof = Ok;

//@description Disables backup of the TON wallet using proof of wallet ownership instead of the password
//@proof Proof of ownership of the wallet
disableTonWalletBackupWithProof proof:tonWalletOwnershipProof = Ok;
```

Two points that decide whether it is usable:

- **The challenge has to be surfaced, not only consumed.** The signature covers `payload`, `domain`
  and `timestamp`, so the caller must see the first two and choose the third *before* signing.
  Fetching the challenge and signing inside one call only works for a caller holding the key, which
  is the thing being removed.
- **`address` and `private_key` both drop out.** The server derives the address from `public_key`
  (v6r2, workchain 0), which is what `inputWalletImported public_key proof` already does one layer
  down, so TDLib passes it through rather than re-deriving anything.

Worth checking against whatever lands: the MTProto `wallet.disableBackup` carries `new_public_key`
and `proof` as separate optional fields. If TDLib keeps them apart rather than folding the key into
the proof, the constructor above splits to match; the app can supply either shape.

## The account knows about rotations now

The server scans `change_wallet_key` on chain, re-reads the key when a transaction confirms,
rewrites `tonWalletState.public_key`, invalidates the backup if one was enabled, and pushes the
state on - whichever client performed the rotation. **So `public_key` is the source of truth**, and
three things here were built on it not being:

- `SettleRotationAsync` replaced a polling loop that asked TonCenter for `get_public_key` every few
  seconds. It now settles from the update that carries the key, and runs **before** the staleness
  check in `ApplyAsync` - our own rotation is otherwise indistinguishable from someone else taking
  the wallet over, and would archive the descriptor that holds the phrase the user has just written
  down. It reports whether it swapped the descriptor, because the engine client is built around the
  key it was given and has to be rebuilt when it changes.
- `BindAsync` recorded the chain's key rather than the account's, because the account used to report
  the key the wallet was created with forever. It records `wallet.PublicKey` now.
- `VerifySigningKeyAsync` is a backstop rather than the mechanism, and stands aside entirely while a
  rotation of ours is pending.

## Left to do

- A TON Connect request, or a send, attempted while a rotation is out reaches its popup as a
  generic failure. Safe - nothing is signed - but it says nothing useful, and the state lasts a
  block or two.
- *Import an Existing Wallet* on the delete flow, waiting on the TDLib surface above. *Create a
  New Wallet* is done.
- Nothing forces the user to write the new phrase down. The contract this rests on says disabling
  the backup should prove they have, by asking for three of the words; the popup shows them and
  trusts them.
- `WalletDisableBackupConfirmInfo` is now in `Resources.xml` but unused. Its wording - "Telegram
  will delete its encrypted backup, and your recovery key will be updated. This can't be undone." -
  says Android has a second confirmation after the checkbox, and that it treats the rotation as
  part of the same act.
- Whether `PolicyViolation` actually reaches a re-bind. The store now reports every non-cancel
  failure as one, which is the classification `EnsureBoundAsync` should act on — but the path from
  the engine's error back to it has not been walked.
- Whether opening the wallet should require the app passcode when one is set. Undecided.
