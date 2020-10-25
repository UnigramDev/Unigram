using Telegram.Td.Api;
using Windows.Networking.Connectivity;

namespace Unigram.Services
{
    public interface INetworkService
    {
        void Reconnect();

        NetworkType Type { get; }
    }

    public class NetworkService : INetworkService
    {
        private readonly IProtoService _protoService;

        public NetworkService(IProtoService protoService)
        {
            _protoService = protoService;

            NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;

            try
            {
                Update(NetworkInformation.GetInternetConnectionProfile());
            }
            catch { }
        }

        public void Reconnect()
        {
            _protoService.Send(new SetNetworkType(_type));
        }

        private void OnNetworkStatusChanged(object sender)
        {
            try
            {
                Update(NetworkInformation.GetInternetConnectionProfile());
            }
            catch { }
        }

        private void Update(ConnectionProfile profile)
        {
            _protoService.Send(new SetNetworkType(_type = GetNetworkType(profile)));
        }

        private NetworkType GetNetworkType(ConnectionProfile profile)
        {
            if (profile == null)
            {
                //return new NetworkTypeNone();
                return new NetworkTypeWiFi();
            }

            var level = profile.GetNetworkConnectivityLevel();
            if (level == NetworkConnectivityLevel.LocalAccess || level == NetworkConnectivityLevel.None)
            {
                //return new NetworkTypeNone();
                return new NetworkTypeWiFi();
            }

            var cost = profile.GetConnectionCost();
            if (cost != null && cost.Roaming)
            {
                return new NetworkTypeMobileRoaming();
            }
            else if (profile.IsWlanConnectionProfile)
            {
                return new NetworkTypeWiFi();
            }
            else if (profile.IsWwanConnectionProfile)
            {
                return new NetworkTypeMobile();
            }

            // This is most likely cable connection.
            //return new NetworkTypeOther();
            return new NetworkTypeWiFi();
        }

        private NetworkType _type = new NetworkTypeOther();
        public NetworkType Type
        {
            get { return _type; }
            private set { _type = value; }
        }
    }
}
