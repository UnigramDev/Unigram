using System.IO;
#if SILVERLIGHT
using SharpGIS;
#else
using System.IO.Compression;
#endif
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public class TLGzipPacked : TLObject
    {
        public const uint Signature = TLConstructors.TLGzipPacked;

        public TLString PackedData { get; set; }

        public TLObject Data { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            PackedData = GetObject<TLString>(bytes, ref position);

            var decopressedData = new byte[] {};
            var compressedData = PackedData.Data;
            var buffer = new byte[4096];
            
#if SILVERLIGHT
            var hgs = new GZipInflateStream(new MemoryStream(compressedData));
#else
            var hgs = new GZipStream(new MemoryStream(compressedData), CompressionMode.Decompress); 
#endif

            var bytesRead = hgs.Read(buffer, 0, buffer.Length);
            while (bytesRead > 0)
            {
                decopressedData = TLUtils.Combine(decopressedData, buffer.SubArray(0, bytesRead));
                bytesRead = hgs.Read(buffer, 0, buffer.Length);
            }

            bytesRead = 0;
            Data = GetObject<TLObject>(decopressedData, ref bytesRead);
            
            return this;
        }
    }
}
