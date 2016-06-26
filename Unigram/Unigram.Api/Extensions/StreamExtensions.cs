using System.IO;

namespace Telegram.Api.Extensions
{
    public static class StreamExtensions
    {
        public static void Write(this Stream output, byte[] buffer)
        {
            output.Write(buffer, 0, buffer.Length);
        }
    }
}
