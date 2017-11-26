using System;
using Telegram.Api.Services;

namespace Telegram.Api.Transport
{
    public interface ITransportService
    {
        ITransport GetTransport(string host, int port, TransportType type, out bool isCreated);
        ITransport GetFileTransport(string host, int port, TransportType type, out bool isCreated);
        ITransport GetFileTransport2(string host, int port, TransportType type, out bool isCreated);
        ITransport GetSpecialTransport(string host, int port, TransportType type, out bool isCreated);

        void Close();
        void CloseTransport(ITransport transport);

        event EventHandler CheckConfig;

        event EventHandler<TransportEventArgs> TransportConnecting;
        event EventHandler<TransportEventArgs> TransportConnected;

        event EventHandler<TransportEventArgs> ConnectionLost;
        event EventHandler<TransportEventArgs> FileConnectionLost;
    }
}
