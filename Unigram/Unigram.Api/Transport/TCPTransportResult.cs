using System;
using System.Net.Sockets;
using System.Text;

namespace Telegram.Api.Transport
{
    public class TcpTransportResult
    {
        public SocketError Error { get; set; }

        public SocketAsyncOperation Operation { get; set; }

        public Exception Exception { get; set; }

        public TcpTransportResult(Exception exception)
        {
            Exception = exception;
        }

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

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Operation=" + Operation);
            sb.AppendLine("Error=" + Error);
            sb.AppendLine("Exception=" + Exception);

            return sb.ToString();
        }
    }
}
