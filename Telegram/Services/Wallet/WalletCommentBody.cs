//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// Puts the encrypted comment TDLib reports back into the message body the engine decrypts.
    /// </summary>
    /// <remarks>
    /// TDLib hands over the payload alone, Base64 encoded: the sender's public key xored with the
    /// recipient's, the message key, and the ciphertext. The engine's decrypt_comment takes the
    /// complete message-body cell as a BOC, so the opcode and the cells around it are put back
    /// here.
    ///
    /// The shape mirrors the engine's own builder (wallet/encrypted_comment.rs): 35 bytes of
    /// payload in the root cell after the opcode, 127 in each cell of the chain that follows. Any
    /// chunking reads back the same, but with this one a body we build is the body it would have
    /// built.
    /// </remarks>
    public static class WalletCommentBody
    {
        // TON's encrypted-comment message-body opcode.
        private const uint EncryptedCommentOpcode = 0x2167DA4B;

        private const int RootPayloadBytes = 35;
        private const int RefPayloadBytes = 127;

        // The public key, the message key, and at least one AES block, up to what the engine
        // accepts. Anything outside this cannot be an encrypted comment.
        private const int MinimumPayloadBytes = 32 + 16 + 16;
        private const int MaximumPayloadBytes = 1024;

        /// <summary>
        /// The Base64 BOC for a payload as TDLib reports it, or null if it is not one.
        /// </summary>
        public static string FromPayload(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return null;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(payload);
            }
            catch (FormatException)
            {
                return null;
            }

            // Should TDLib ever report the body itself, it already is what the engine wants: a BOC
            // begins with this magic, and a payload cannot, being the low half of a public key.
            if (bytes.Length > 4 && bytes[0] == 0xB5 && bytes[1] == 0xEE && bytes[2] == 0x9C && bytes[3] == 0x72)
            {
                return payload;
            }

            if (bytes.Length < MinimumPayloadBytes || bytes.Length > MaximumPayloadBytes)
            {
                return null;
            }

            return Serialize(bytes);
        }

        private static string Serialize(byte[] payload)
        {
            var cells = new List<byte[]>();

            var root = new byte[4 + Math.Min(RootPayloadBytes, payload.Length)];
            root[0] = (byte)(EncryptedCommentOpcode >> 24);
            root[1] = (byte)((EncryptedCommentOpcode >> 16) & 0xFF);
            root[2] = (byte)((EncryptedCommentOpcode >> 8) & 0xFF);
            root[3] = (byte)(EncryptedCommentOpcode & 0xFF);

            Buffer.BlockCopy(payload, 0, root, 4, root.Length - 4);
            cells.Add(root);

            for (int i = RootPayloadBytes; i < payload.Length; i += RefPayloadBytes)
            {
                var length = Math.Min(RefPayloadBytes, payload.Length - i);
                var cell = new byte[length];

                Buffer.BlockCopy(payload, i, cell, 0, length);
                cells.Add(cell);
            }

            var total = 0;

            for (int i = 0; i < cells.Count; i++)
            {
                // Two descriptor bytes, the data, and one byte of reference for every cell but the
                // last, each referencing the one after it.
                total += 2 + cells[i].Length + (i < cells.Count - 1 ? 1 : 0);
            }

            var offsetBytes = total > byte.MaxValue ? 2 : 1;
            var boc = new byte[10 + offsetBytes + total];
            var index = 0;

            boc[index++] = 0xB5;
            boc[index++] = 0xEE;
            boc[index++] = 0x9C;
            boc[index++] = 0x72;

            // One byte per cell index, with neither the index table nor the checksum: both are
            // optional, and the flags say which are there.
            boc[index++] = 0x01;
            boc[index++] = (byte)offsetBytes;
            boc[index++] = (byte)cells.Count;
            boc[index++] = 0x01;
            boc[index++] = 0x00;

            if (offsetBytes == 2)
            {
                boc[index++] = (byte)(total >> 8);
            }

            boc[index++] = (byte)total;
            boc[index++] = 0x00;

            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var references = i < cells.Count - 1 ? 1 : 0;

                boc[index++] = (byte)references;

                // Both halves of the length descriptor, which is whole bytes plus started ones. The
                // payload is whole bytes throughout, so the two agree and no completion tag follows.
                boc[index++] = (byte)(cell.Length * 2);

                Buffer.BlockCopy(cell, 0, boc, index, cell.Length);
                index += cell.Length;

                if (references > 0)
                {
                    // The chain runs forward, which is what allows the cells to be written in this
                    // order: a reference must point at a cell that comes later.
                    boc[index++] = (byte)(i + 1);
                }
            }

            return Convert.ToBase64String(boc);
        }
    }
}
