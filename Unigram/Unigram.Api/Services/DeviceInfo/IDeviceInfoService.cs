namespace Telegram.Api.Services.DeviceInfo
{
    public interface IDeviceInfoService
    {
        string DeviceModel { get; }
        string AppVersion { get; }
        string SystemVersion { get; }
        bool IsBackground { get; }
        string BackgroundTaskName { get; }
        int BackgroundTaskId { get; }
    }
}
