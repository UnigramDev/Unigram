using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Windows.Networking.Connectivity;

namespace Unigram.Services
{
    public interface INetworkService
    {
        NetworkType Type { get; }
    }

    public class NetworkService : INetworkService
    {
        private readonly IProtoService _protoService;

        public NetworkService(IProtoService protoService)
        {
            _protoService = protoService;

            NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
            Update(NetworkInformation.GetInternetConnectionProfile());
        }

        private void OnNetworkStatusChanged(object sender)
        {
            Update(NetworkInformation.GetInternetConnectionProfile());
        }

        private void Update(ConnectionProfile profile)
        {
            _protoService.Send(new SetNetworkType(_type = GetNetworkType(profile)));
        }

        private NetworkType GetNetworkType(ConnectionProfile profile)
        {
            if (profile == null)
            {
                return new NetworkTypeNone();
            }

            var level = profile.GetNetworkConnectivityLevel();
            if (level == NetworkConnectivityLevel.LocalAccess || level == NetworkConnectivityLevel.None)
            {
                return new NetworkTypeNone();
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

            return new NetworkTypeOther();
        }

        private NetworkType _type = new NetworkTypeOther();
        public NetworkType Type
        {
            get { return _type; }
            private set { _type = value; }
        }
    }
}
