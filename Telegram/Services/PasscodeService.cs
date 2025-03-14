//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services.Settings;
using Telegram.Services.Updates;
using Telegram.Views;
using Windows.Security.Cryptography;

namespace Telegram.Services
{
    public interface IPasscodeService
    {
        int RetryIn { get; }

        bool IsEnabled { get; }
        bool IsSimple { get; }
        bool IsLocked { get; }
        bool IsBiometricsEnabled { get; set; }

        DateTime CloseTime { get; set; }
        int AutolockTimeout { get; set; }

        bool IsLockscreenRequired { get; }

        void Lock(bool biometrics);
        void Unlock();

        bool TryUnlock(string passcode);

        void Set(string passcode, bool simple, int timeout);
        void Reset();
    }

    public partial class PasscodeService : IPasscodeService
    {
        private readonly PasscodeLockSettings _settingsService;

        private double _retryIn;

        public PasscodeService(PasscodeLockSettings settingsService)
        {
            _settingsService = settingsService;

            if (_settingsService.RetryCount >= 3)
            {
                _retryIn = 5000 + (5000 * (Math.Min(_settingsService.RetryCount, 8) - 3));
            }

            InactivityHelper.Detected += OnInactivityDetected;
        }

        private void OnInactivityDetected(object sender, EventArgs e)
        {
            Lock(false);
        }

        public int RetryIn => (int)Math.Ceiling(UpdateRetryIn() / 1000);

        public bool IsEnabled => _settingsService.Hash.Length > 0;

        public bool IsSimple => _settingsService.IsSimple;

        public bool IsLocked => _settingsService.IsLocked;

        public bool IsBiometricsEnabled
        {
            get => _settingsService.IsHelloEnabled;
            set => _settingsService.IsHelloEnabled = value;
        }

        public DateTime CloseTime
        {
            get => _settingsService.CloseTime;
            set => _settingsService.CloseTime = value;
        }

        public int AutolockTimeout
        {
            get => _settingsService.AutolockTimeout;
            set => _settingsService.AutolockTimeout = value;
        }

        public bool IsLockscreenRequired =>
            IsEnabled && ((AutolockTimeout > 0 && CloseTime < DateTime.MaxValue && CloseTime > DateTime.UtcNow.AddSeconds(-AutolockTimeout)) || IsLocked);

        public void Lock(bool biometrics)
        {
            if (IsEnabled)
            {
                _settingsService.IsLocked = true;
                _settingsService.CloseTime = DateTime.MaxValue;
                Publish(true);

                WindowContext.ForEach(window => window.Lock(biometrics));
            }
        }

        public void Unlock()
        {
            _settingsService.IsLocked = false;
            _settingsService.CloseTime = DateTime.MaxValue;
            _settingsService.RetryCount = 0;
            _settingsService.RetryTime = DateTime.MaxValue;
            _retryIn = 0;
            Publish(true);

            WindowContext.ForEach(window => window.Unlock());
        }

        public void Set(string passcode, bool simple, int timeout)
        {
            var salt = CryptographicBuffer.GenerateRandom(256).ToArray();
            var data = Utils.ComputeSHA1(Utils.Combine(salt, Encoding.UTF8.GetBytes(passcode), salt));

            _settingsService.Hash = data;
            _settingsService.Salt = salt;
            _settingsService.IsSimple = simple;
            _settingsService.AutolockTimeout = timeout;
            _settingsService.CloseTime = DateTime.MaxValue;
            _settingsService.RetryCount = 0;
            _settingsService.RetryTime = DateTime.MaxValue;
            _settingsService.IsLocked = false;
            Publish(true);
        }

        public void Reset()
        {
            _retryIn = 0;
            _settingsService.Clear();
            Publish(false);
        }

        private void Publish(bool enabled)
        {
            var update = new UpdatePasscodeLock(enabled);

            foreach (var aggregator in TypeResolver.Current.ResolveAll<IEventAggregator>())
            {
                aggregator.Publish(update);
            }
        }

        public bool CheckSimple(string passcode)
        {
            if (passcode != null && passcode.Length == 4)
            {
                return passcode.All(x => x is >= '0' and <= '9');
            }

            return false;
        }

        public bool TryUnlock(string passcode)
        {
            if (_retryIn > 0)
            {
                return false;
            }

            var unlock = Utils.ByteArraysEqual(Utils.ComputeHash(_settingsService.Salt, Encoding.UTF8.GetBytes(passcode)), _settingsService.Hash);
            if (unlock)
            {
                Unlock();
            }
            else
            {
                _settingsService.RetryCount++;

                if (_settingsService.RetryCount >= 3)
                {
                    _settingsService.RetryTime = DateTime.UtcNow;
                    _retryIn = 5000 + (5000 * (Math.Min(_settingsService.RetryCount, 8) - 3));
                }
            }

            return unlock;
        }

        public double UpdateRetryIn()
        {
            var time = DateTime.UtcNow;
            if (time > _settingsService.RetryTime)
            {
                _retryIn -= (time - _settingsService.RetryTime).TotalMilliseconds;

                if (_retryIn < 0)
                {
                    _retryIn = 0;
                }
            }

            _settingsService.RetryTime = time;
            return _retryIn;
        }
    }
}
