//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.Td.Api
{
    /// <summary>
    /// A transfer this device signed and sent, which the chain has not settled yet.
    /// </summary>
    /// <remarks>
    /// TDLib knows only of transactions that happened: its states are succeeded and failed, and a
    /// sent message has no transaction at all until it lands - getTonWalletTransactionByMsgHash
    /// answers with an error until then. The row in between is this one, and it is the app's,
    /// which is why the state carries the message rather than the transaction.
    /// </remarks>
    public partial class TonWalletTransactionStatePending : TonWalletTransactionState
    {
        public TonWalletTransactionStatePending(string operationId, string msgHash, int expirationDate)
        {
            OperationId = operationId;
            MsgHash = msgHash;
            ExpirationDate = expirationDate;
        }

        /// <summary>
        /// The engine's name for this send. What settles the row is the engine resolving that
        /// operation against the chain, so this is how a resolution is matched to a row.
        /// </summary>
        public string OperationId { get; }

        /// <summary>
        /// What the account answered the transfer with, and how the transaction is asked for once
        /// it lands: <c>getTonWalletTransactionByMsgHash</c> answers with an error until the
        /// transfer is final and with the transaction itself afterwards.
        /// </summary>
        public string MsgHash { get; }

        /// <summary>
        /// When the signed message stops being valid. Past it the transfer can no longer land, and
        /// a row still pending is a row that failed.
        /// </summary>
        public int ExpirationDate { get; }

        public override string ToString()
        {
            return nameof(TonWalletTransactionStatePending);
        }
    }
}
