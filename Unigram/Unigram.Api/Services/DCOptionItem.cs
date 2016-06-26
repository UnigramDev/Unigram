namespace Telegram.Api.Services
{
    public class ConnectionParams
    {
        public byte[] Salt { get; set; }

        public byte[] SessionId { get; set; }

        public byte[] AuthKey { get; set; }
    }

    public class DCOptionItem
    {
        public ConnectionParams Params { get; set; }

        public int Id { get; set; }

        public string Hostname { get; set; }

        public string IpAddress { get; set; }

        public int Port { get; set; }
    }
}
