using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Haptics;

namespace Unigram.Services
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
            var access = await VibrationDevice.RequestAccessAsync();
            if (access == VibrationAccessStatus.Allowed)
            {
                var device = await VibrationDevice.GetDefaultAsync();
                return device != null;
            }

            return true;
        }

        public async Task VibrateAsync()
        {
            var access = await VibrationDevice.RequestAccessAsync();
            if (access == VibrationAccessStatus.Allowed)
            {
                var device = await VibrationDevice.GetDefaultAsync();
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
}
