using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HapticsDevice = Windows.Devices.Haptics.VibrationDevice;
using PhoneDevice = Windows.Phone.Devices.Notification.VibrationDevice;

namespace Unigram.Core.Services
{
    public interface IVibrationService
    {
        Task<bool> GetAvailabilityAsync();
        Task VibrateAsync();
    }

    public class VibrationService : IVibrationService
    {
        public async Task<bool> GetAvailabilityAsync()
        {
            var access = await HapticsDevice.RequestAccessAsync();
            if (access == Windows.Devices.Haptics.VibrationAccessStatus.Allowed)
            {
                var device = await HapticsDevice.GetDefaultAsync();
                return device != null;
            }

            return true;
        }

        public async Task VibrateAsync()
        {
            var access = await HapticsDevice.RequestAccessAsync();
            if (access == Windows.Devices.Haptics.VibrationAccessStatus.Allowed)
            {
                var device = await HapticsDevice.GetDefaultAsync();
                if (device != null)
                {
                    device.SimpleHapticsController.SendHapticFeedback(device.SimpleHapticsController.SupportedFeedback[0]);
                }
            }
        }
    }

    public class FakeVibrationService : IVibrationService
    {
        public Task<bool> GetAvailabilityAsync()
        {
            return Task.FromResult(false);
        }

        public Task VibrateAsync()
        {
            return Task.CompletedTask;
        }
    }

    public class WindowsPhoneVibrationService : IVibrationService
    {
        public Task<bool> GetAvailabilityAsync()
        {
            return Task.Run(() =>
            {
                var device = PhoneDevice.GetDefault();
                return device != null;
            });
        }

        public Task VibrateAsync()
        {
            return Task.Run(() =>
            {
                var device = PhoneDevice.GetDefault();
                if (device != null)
                {
                    device.Vibrate(TimeSpan.FromMilliseconds(200));
                }
            });
        }
    }
}
