using System;
#if WINDOWS_PHONE
using System.Net.Sockets;
using SocketError = System.Net.Sockets.SocketError;
#endif
using System.Text;
using Telegram.Api.TL;

namespace Telegram.Api.Transport
{
    public class TcpTransportResult : TLRPCError
    {
#if WINDOWS_PHONE
        public SocketError Error { get; set; }

        public SocketAsyncOperation Operation { get; set; }
#endif

        public new Exception Exception { get; set; }

        public TcpTransportResult(Exception exception)
        {
            Exception = exception;
        }

#if WINDOWS_PHONE
        public TcpTransportResult(SocketAsyncOperation operation, SocketError error)
        {
            Operation = operation;
            Error = error;
        }

        public TcpTransportResult(SocketAsyncOperation operation, Exception exception)
        {
            Operation = operation;
            Exception = exception;
        }
#endif

        public override string ToString()
        {
            var sb = new StringBuilder();
#if WINDOWS_PHONE
            sb.AppendLine("Operation=" + Operation);
            sb.AppendLine("Error=" + Error);
#endif
            sb.AppendLine("Exception=" + Exception);

            return sb.ToString();
        }
    }
}
