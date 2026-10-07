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

        /// <summary>
        /// The payload TDLib wants for an encrypted comment, from the message body the engine
        /// encrypted it into; null if the body is not one.
        /// </summary>
        /// <remarks>
        /// The inverse of <see cref="FromPayload"/>: the payload is the root cell's data after the
        /// opcode, followed by the data of each cell in the chain hanging off it. Read with any
        /// chunking, not only the engine's, and with or without the BOC's optional index and
        /// checksum.
        /// </remarks>
        public static string ToPayload(string boc)
        {
            if (string.IsNullOrEmpty(boc))
            {
                return null;
            }

            try
            {
                var bytes = Convert.FromBase64String(boc);
                var cells = Deserialize(bytes, out int root);

                if (cells == null || root < 0 || root >= cells.Count)
                {
                    return null;
                }

                var first = cells[root].Data;
                if (first.Length < 4
                    || first[0] != (byte)(EncryptedCommentOpcode >> 24)
                    || first[1] != (byte)((EncryptedCommentOpcode >> 16) & 0xFF)
                    || first[2] != (byte)((EncryptedCommentOpcode >> 8) & 0xFF)
                    || first[3] != (byte)(EncryptedCommentOpcode & 0xFF))
                {
                    return null;
                }

                var payload = new List<byte>(MaximumPayloadBytes);
                payload.AddRange(new ArraySegment<byte>(first, 4, first.Length - 4));

                // Down the chain, each cell its one reference's parent. Bounded by the cell count,
                // so a malformed cycle ends rather than looping.
                var cell = cells[root];
                for (int i = 0; i < cells.Count && cell.References.Length > 0; i++)
                {
                    var next = cell.References[0];
                    if (next < 0 || next >= cells.Count)
                    {
                        return null;
                    }

                    cell = cells[next];
                    payload.AddRange(cell.Data);
                }

                if (payload.Count < MinimumPayloadBytes || payload.Count > MaximumPayloadBytes)
                {
                    return null;
                }

                return Convert.ToBase64String(payload.ToArray());
            }
            catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or ArgumentException)
            {
                return null;
            }
        }

        private readonly struct Cell
        {
            public Cell(byte[] data, int[] references)
            {
                Data = data;
                References = references;
            }

            public byte[] Data { get; }

            public int[] References { get; }
        }

        // A bag of cells, as far as an encrypted comment needs it: ordinary cells of whole bytes.
        private static List<Cell> Deserialize(byte[] bytes, out int root)
        {
            root = -1;

            if (bytes.Length < 6 || bytes[0] != 0xB5 || bytes[1] != 0xEE || bytes[2] != 0x9C || bytes[3] != 0x72)
            {
                return null;
            }

            var flags = bytes[4];
            var hasIndex = (flags & 0x80) != 0;
            var size = flags & 0x07;
            var offsetBytes = bytes[5];

            if (size < 1 || size > 4 || offsetBytes < 1 || offsetBytes > 8)
            {
                return null;
            }

            var index = 6;

            long Read(int width)
            {
                long value = 0;
                for (int i = 0; i < width; i++)
                {
                    value = (value << 8) | bytes[index++];
                }

                return value;
            }

            var count = (int)Read(size);
            var roots = (int)Read(size);
            Read(size);
            Read(offsetBytes);

            if (count < 1 || count > 1024 || roots < 1)
            {
                return null;
            }

            root = (int)Read(size);
            index += (roots - 1) * size;

            if (hasIndex)
            {
                index += count * offsetBytes;
            }

            var cells = new List<Cell>(count);

            for (int i = 0; i < count; i++)
            {
                var d1 = bytes[index++];
                var d2 = bytes[index++];

                // Exotic cells, or data that ends part way through a byte, are no comment.
                if ((d1 & 0x08) != 0 || (d2 & 1) != 0)
                {
                    return null;
                }

                var data = new byte[d2 / 2];
                Buffer.BlockCopy(bytes, index, data, 0, data.Length);
                index += data.Length;

                var references = new int[d1 & 0x07];
                for (int j = 0; j < references.Length; j++)
                {
                    references[j] = (int)Read(size);
                }

                cells.Add(new Cell(data, references));
            }

            return cells;
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
