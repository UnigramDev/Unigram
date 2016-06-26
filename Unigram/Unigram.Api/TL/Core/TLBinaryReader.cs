using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLBinaryReader : BinaryReader
    {
        public TLBinaryReader(byte[] payload)
            : base(new MemoryStream(payload))
        {

        }

        public TLBinaryReader(Stream input)
            : base(input)
        {

        }

        public override bool ReadBoolean()
        {
            var flag = ReadUInt32();
            if (flag == 0x997275B5)
            {
                return true;
            }
            else if (flag == 0xBC799737)
            {
                return false;
            }

            Debug.WriteLine("Invalid Boolean");

            return false;
        }

        public override string ReadString()
        {
            return Encoding.UTF8.GetString(ReadByteArray());
        }

        public byte[] ReadByteArray()
        {
            var sl = 1;
            var l = (int)ReadByte();

            if (l >= 254)
            {
                l = ReadByte() | (ReadByte() << 8) | (ReadByte() << 16);
                sl = 4;
            }

            var b = ReadBytes(l);

            var i = sl;
            while ((l + i) % 4 != 0)
            {
                ReadByte();
                i++;
            }

            return b;
        }
    }
}
