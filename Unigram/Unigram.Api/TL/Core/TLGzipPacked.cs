using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLGzipPacked : TLObject
    {
        public const uint Signature = 812830625u;

        public TLObject Query { get; set; }

        public TLGzipPacked() { }
        public TLGzipPacked(TLBinaryReader from)
        {
            Read(from);
        }

        public override TLType TypeId { get { return TLType.GzipPacked; } }

        //public override TLObject FromBytes(byte[] bytes, ref int position)
        //{
        //    bytes.ThrowExceptionIfIncorrect(ref position, 812830625u);
        //    PackedData = TLObject.GetObject<string>(bytes, ref position);
        //    byte[] array = new byte[0];
        //    byte[] data = PackedData.Data;
        //    byte[] array2 = new byte[4096];
        //    GZipStream gZipStream = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        //    int i;
        //    for (i = gZipStream.Read(array2, 0, array2.Length); i > 0; i = gZipStream.Read(array2, 0, array2.Length))
        //    {
        //        array = TLUtils.Combine(new byte[][]
        //        {
        //            array,
        //            array2.SubArray(0, i)
        //        });
        //    }
        //    i = 0;
        //    Data = TLObject.GetObject<TLObject>(array, ref i);
        //    return this;
        //}

        public override void Read(TLBinaryReader from)
        {
            var data = from.ReadByteArray();
            var buffer = new byte[4096];

            using (var input = new MemoryStream())
            {
                using (var stream = new GZipStream(new MemoryStream(data), CompressionMode.Decompress))
                {
                    for (int i = stream.Read(buffer, 0, buffer.Length); i > 0; i = stream.Read(buffer, 0, buffer.Length))
                    {
                        input.Write(buffer, 0, buffer.Length);
                    }
                }

                input.Seek(0, SeekOrigin.Begin);
                using (var reader = new TLBinaryReader(input))
                {
                    Query = TLFactory.Read<TLObject>(reader);
                }
            }
        }

        public override void Write(TLBinaryWriter to)
        {
            // TODO: maybe
            throw new NotImplementedException();
        }
    }
}
