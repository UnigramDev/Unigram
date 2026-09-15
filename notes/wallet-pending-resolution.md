# Resolving a pending transfer

A transfer leaves as a signed message and becomes a transaction some seconds later. Nothing is
pushed when that happens, so the row the wallet shows in between has to be settled by asking.

There are two ways to ask. This note records why the app uses the second one, and keeps the first
in case it becomes usable.

---

## What TDLib offers: `getTonWalletTransactionByMsgHash`

`sendTonWalletTransfer` answers with `tonWalletTransferResult.msg_hash`, and the schema offers

```
//@description Returns a TON wallet transaction by their msg_hash; return an error if the transaction isn't finalized yet
getTonWalletTransactionByMsgHash msg_hash:string = TonWalletTransaction;
```

**It does not resolve our transfers.** Every poll answers `404 Not Found`, and the server's own
answer underneath is an empty list:

```
Receive result for GetTonWalletTransactionQuery: wallet.transactions {
  balance = 199451020
  transactions = vector[0] { }
}
Sending result for request 152: error { code = 404, message = "Not Found" }
```

The balance in that same response had already moved, so the transfer had landed. Two things are
worth knowing before trying again:

1. **The message hash is not the transaction hash.** iOS's log shows both for one transfer:
   `msgHash=/y/Gu8DhtOIekfow4xM6T68hynft/ZOHlFNlx68s+vA=` and
   `transaction_hash=2YVYammFlCY7GAI2Wxexlzr6QVyw/whG/2063LZUt9s=`. For a gasless transfer the
   message that reaches the chain is the relayer's, and ours is not what the index is keyed by.
2. **`msg_hash` is `bytes` carrying text.** The field is declared `bytes` but contains the ASCII of
   the standard padded Base64 hash, while the method takes a `string`. Both `Convert.ToBase64String`
   (encodes it twice) and a raw byte copy are wrong; `Encoding.UTF8.GetString` is right. That cost a
   day of `Not Found` before the empty `wallet.transactions` above showed the format was never the
   problem.

iOS never calls this method at all.

### The polling resolver, as it was written

Kept verbatim. It was correct in shape - it treated "not final yet" and "never" as the same answer
and let the expiry decide between them - so if the method starts resolving handed-off transfers,
this is the code to put back, with the loop calling it once per pending row.

```csharp
private async Task<bool> ResolveAsync(TonWalletTransaction pending)
{
    if (pending.State is not TonWalletTransactionStatePending state)
    {
        return false;
    }

    var response = await _clientService.SendAsync(new GetTonWalletTransactionByMsgHash(state.MsgHash));
    var landed = response as TonWalletTransaction;

    if (landed == null && state.ExpirationDate > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
    {
        // Not final yet, which is what the error means while the message can still be
        // included. Asked about again next time round.
        return false;
    }

    await _mutex.WaitAsync();
    try
    {
        var index = _pending.FindIndex(x => x.Id == pending.Id);
        if (index < 0)
        {
            // Forgotten while the request was out - another wallet, or a refresh that
            // started the history over.
            return false;
        }

        var items = new List<TonWalletTransaction>(_pending);

        if (landed != null)
        {
            // The transaction it became goes where the account would have put it, which is
            // the top: nothing that lands after it has landed yet.
            items.RemoveAt(index);

            if (!_confirmed.Exists(x => x.Id == landed.Id))
            {
                var confirmed = new List<TonWalletTransaction>(_confirmed.Count + 1) { landed };
                confirmed.AddRange(_confirmed);

                _confirmed = confirmed;
            }
        }
        else
        {
            // Past its expiry with nothing to show for it: the message can no longer be
            // included, so the transfer did not happen. The row stays, saying so - it is
            // this device's record and the account has none.
            Logger.Error("wallet transfer expired: " + state.MsgHash);

            items[index] = new TonWalletTransaction(
                pending.Id,
                pending.PeerAddress,
                pending.PeerUserId,
                pending.PeerDomain,
                pending.Date,
                new TonWalletTransactionStateFailed(),
                pending.Type);
        }

        _pending = items;

        RebuildActivity();
        SetState(Project());
    }
    finally
    {
        _mutex.Release();
    }

    return true;
}
```

### What would have to be true to switch back

- A transfer this device handed off is findable by the `msg_hash` it was given, gasless included.
- Or the result carries something that is: the relayer's hash, or the transaction id directly.

Either would also remove the expiry clock's second job, which is guessing failure from silence.

---

## What the app uses: the engine's `ResolvePending`

`WalletClient.ResolvePending()` - "resolves the durable outgoing message from chain evidence
without signing", idempotent, reads no protected secret, commits terminal evidence to the journal.
It answers with `SendSnapshot { OperationId, Phase, ErrorMessage, Resolution }`, where
`ResolutionInfo` carries the confirmed `TransactionHash`, a `PendingReason` while there is none, and
a `RetryAfterHintMs` polling hint.

`SendPhase` has a **`HandedOff`** state, which is this app's shape exactly: the engine signs, and
the host submits the message somewhere else - here through `sendTonWalletTransfer`. The terminal
phases are `Confirmed`, `Replaced`, `SequenceNumberConsumed`, `Expired`, `Superseded`, `Failed` and
`Cancelled`.

This is what iOS does. Its log, for one transfer:

```
response … wallet.SentTransfer.sentTransfer(msgHash: /y/Gu8…, gaslessLeft: 0, gaslessResetAt: …)
event=wallet_pending_body_matched … finality=pending    transaction_hash=nil
event=wallet_pending_body_matched … finality=confirmed  transaction_hash=2YVYamm…
… then wallet.getTransactions, and toncenter.performApiRequest(/api/v2/getAddressInformation)
```

The journal is what makes it survive a restart, which the message-hash poll never could.
