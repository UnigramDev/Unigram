using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLBinaryWriter : BinaryWriter
    {
        public TLBinaryWriter(byte[] payload)
            : base(new MemoryStream(payload))
        {

        }

        public TLBinaryWriter(Stream output)
            : base(output)
        {

        }

        public override void Write(bool value)
        {
            if (value)
            {
                Write(0x997275B5);
            }
            else
            {
                Write(0xBC799737);
            }
        }

        public override void Write(string value)
        {
            WriteByteArray(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        public void WriteByteArray(byte[] b)
        {
            if (b.Length <= 253)
            {
                Write((byte)b.Length);
            }
            else
            {
                Write((byte)254);
                Write((byte)b.Length);
                Write((byte)(b.Length >> 8));
                Write((byte)(b.Length >> 16));
            }

            Write(b);

            int i = b.Length <= 253 ? 1 : 4;
            while ((b.Length + i) % 4 != 0)
            {
                Write((byte)0);
                i++;
            }
        }

        public void WriteObject(TLObject obj)
        {
            if (obj != null)
            {
                obj.Write(this);
            }
            else
            {
                // TLNull
                Write(0x56730BCC);
            }
        }
    }
}
