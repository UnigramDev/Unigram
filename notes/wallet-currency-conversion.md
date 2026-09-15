# Converting grams to money

Every amount the wallet shows in a currency goes through two steps, and both of them belong to the
account rather than to the engine:

1. **grams to dollars** — `nanograms * Options.MillionGramToUsdRate / 1e15`
2. **dollars to the chosen currency** — `dollars * WalletState.CurrencyRate`

`WalletHelper.ToCurrency` does both, and `WalletHelper.ToNanograms` is its inverse, for an amount
typed in money. They are the only implementation; anything else doing this arithmetic is a copy that
will drift.

## The rate

`currencyExchangeRate.rate` is **how many units of the currency one dollar buys** - 3.672947 for AED,
0.881710 for EUR, 1 for USD, measured from a real `getCurrencyExchangeRates` response. So dollars are
**multiplied** by it.

`WalletState.CurrencyRate` carries that, with one addition: it is **zero while the rates have not
arrived**. That is not a rate of one. A rate of one means "these are dollars", and showing dollars
under another currency's name is a wrong number rather than a missing one.

Two bugs came out of getting this wrong, and both are easy to reintroduce:

- **Dividing instead of multiplying.** The answer is wrong by the *square* of the rate, which looks
  plausible: 1.19 grams showed as 0.44 AED where it is 5.94. If a converted amount is out by a factor
  and the factor is suspiciously square, this is why.
- **Loading the rates without telling anyone.** `LoadRatesAsync` filled its cache and stopped, so the
  state kept the stand-in rate from before they arrived and the view kept showing dollars. It now
  re-projects and raises.

## Where a conversion is shown

| Where | Path | Waits for the rate? |
| --- | --- | --- |
| The card's second line (`WalletWindow.UpdateBalance`) | its own two steps, not the helper | **yes** - skeleton until `IsSynchronized && CurrencyRate > 0` |
| Send popup, the converted pill | `WalletHelper.ToCurrency` | no |
| Send popup, typing in currency (the swap) | `WalletHelper.ToNanograms` | no |
| Transaction popup, the line under the amount | `WalletHelper.ToCurrency` | no |
| Transaction popup, the fee's `~` half | `WalletHelper.ToCurrency` | no |

**The ones that do not wait are the open item.** They are reached from the wallet window, which
fetches the rates when it opens, so the window in which they would be wrong is small - but it is the
same wrongness the card now avoids, and the fix is the same shape: show nothing where the number
would be, rather than a number.

`WalletWindow.UpdateBalance` keeping its own arithmetic instead of calling the helper is the other
loose end. It predates the helper; folding it in would leave one implementation and one place to get
the direction wrong.

## Formatting, once converted

`Formatter.FormatAmountExact` prints it: the currency's own precision where the amount reaches it,
and as deep as the first non-zero digit where it does not, because a fee is a fraction of a cent more
often than not. It writes the number in the app's language (`SplitAmount`) and asks
`CurrencyNumberFormatter` only for the decoration, so a converted amount and the grams beside it are
not written in two different languages.
