//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Numerics;
using Telegram.Converters;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// Which way a wallet moved in one step of an emulated request.
    /// </summary>
    public enum WalletRequestDirection
    {
        /// <summary>Grams arrive at the address the row names.</summary>
        Deposit,

        /// <summary>Grams leave it.</summary>
        Withdraw
    }

    /// <summary>
    /// One row of the emulation: what an account ends up doing if the request is confirmed.
    /// </summary>
    /// <remarks>
    /// Drawn from the engine's <c>SendEmulationAction</c>, one per action, in the order the
    /// emulator produced them. The point of showing them is that the dApp's own description of a
    /// request is a claim, and this is what the chain would actually do with it.
    /// </remarks>
    public sealed record WalletRequestAction(
        string Address,
        WalletRequestDirection Direction,
        BigInteger Nanograms,
        string Comment,
        string Label)
    {
        public bool IsDeposit => Direction == WalletRequestDirection.Deposit;

        public bool IsWithdraw => Direction == WalletRequestDirection.Withdraw;

        // A contract deploy moves nothing of its own, and a "-0" would read as a transfer.
        public bool IsDepositAmount => IsDeposit && !Nanograms.IsZero;

        public bool IsWithdrawAmount => IsWithdraw && !Nanograms.IsZero;

        public bool HasComment => !string.IsNullOrEmpty(Comment);

        /// <summary>The address short enough to read, with both ends kept to recognise it by.</summary>
        public string ShortAddress => WalletRequest.Shorten(Address);

        /// <summary>Signed, because which way it went is the point of the row.</summary>
        public string AmountText => (IsDeposit ? "+" : "\u2212") + Formatter.GramExact(Nanograms).Join();
    }

    /// <summary>
    /// One message of the request, as the dApp wrote it.
    /// </summary>
    /// <remarks>
    /// Every one is shown: the request is signed whole, so a message left off the sheet is one the
    /// user agrees to without seeing.
    /// </remarks>
    public sealed record WalletRequestMessage(
        string Address,
        BigInteger Nanograms,
        string Comment,
        string Payload,
        bool Deploys)
    {
        // A payload is a BOC, so the head is what can be compared.
        private const int PayloadShown = 1024;

        public string AmountText => Formatter.Grams(Nanograms);

        public string RecipientText => string.Format(Strings.WalletTransferTo, WalletRequest.Shorten(Address));

        public bool HasComment => !string.IsNullOrEmpty(Comment);

        public bool HasPayload => !string.IsNullOrEmpty(Payload);

        public string PayloadText => Payload == null || Payload.Length <= PayloadShown
            ? Payload
            : Payload.Substring(0, PayloadShown) + "\u2026";
    }

    /// <summary>
    /// A <c>sendTransaction</c> a dApp has asked for, as much as can be known before the user
    /// agrees to it.
    /// </summary>
    /// <remarks>
    /// Only <c>sendTransaction</c>. The protocol also carries <c>signData</c> and
    /// <c>signMessage</c>; neither is supported, and a request for one is answered with an error
    /// rather than shown.
    ///
    /// Everything here is already validated: the request decrypted, its validity window is still
    /// open, its network and sender are ours, and the emulation came back. What the user is being
    /// asked is only whether they want it.
    /// </remarks>
    public sealed class WalletRequest
    {
        public WalletRequest(long sessionId, long messageId, string dappRequestId, string traceId)
        {
            SessionId = sessionId;
            MessageId = messageId;
            DappRequestId = dappRequestId;
            TraceId = traceId;
        }

        public long SessionId { get; }

        /// <summary>The service message the request arrived as, which is what claims address.</summary>
        public long MessageId { get; }

        /// <summary>The dApp's own identifier for it, echoed back in the answer.</summary>
        public string DappRequestId { get; }

        public string TraceId { get; }

        /// <summary>The dApp as it asked to be known, and the domain that is the real identity.</summary>
        public string Name { get; init; }

        public string Domain { get; init; }

        /// <summary>Where the grams go. The one address the card engraves.</summary>
        public string Recipient { get; init; }

        /// <summary>The total of every message, not only the one whose recipient is shown.</summary>
        public BigInteger Nanograms { get; init; }

        public IReadOnlyList<WalletRequestMessage> Messages { get; init; } = new List<WalletRequestMessage>();

        /// <summary>What the network charges, which the dApp does not pay and does not decide.</summary>
        public BigInteger FeeNanograms { get; init; }

        /// <summary>
        /// The emulation, or empty when it could not be run.
        /// </summary>
        /// <remarks>
        /// Empty is not a reason to refuse: the transfer is still exactly what the request said,
        /// and the preview is a second opinion rather than the authority. The sheet hides the
        /// section instead.
        /// </remarks>
        public IReadOnlyList<WalletRequestAction> Actions { get; init; } = new List<WalletRequestAction>();

        /// <summary>
        /// When the request stops being answerable. The sheet closes itself at this point, and
        /// nothing announces it - the server sends no signal and the dApp is already counting.
        /// </summary>
        public int ExpirationDate { get; init; }

        public string ShortRecipient => Shorten(Recipient);

        /// <summary>
        /// An address with its middle taken out, which is how one is read at a glance.
        /// </summary>
        public static string Shorten(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length <= 12)
            {
                return address ?? string.Empty;
            }

            return address.Substring(0, 4) + "\u2026" + address.Substring(address.Length - 4);
        }
    }
}
