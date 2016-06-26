namespace Unigram.Core.Services
{
    using Telegram.Api.Services.DeviceInfo;

    public interface IExtendedDeviceInfoService : IDeviceInfoService
    {
        bool IsLowMemoryDevice { get; }

        bool IsWiFiEnabled { get; }

        bool IsCellularDataEnabled { get; }
    }
}
