using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Unigram.Common;
using Unigram.Services.Settings;
using Unigram.Services.Updates;
using Unigram.Views;
using Windows.Security.Cryptography;

namespace Unigram.Services
{
    public interface IPasscodeService
    {
        bool IsEnabled { get; }
        bool IsSimple { get; }
        bool IsLocked { get; }
        bool IsBiometricsEnabled { get; set; }

        DateTime CloseTime { get; set; }
        int AutolockTimeout { get; set; }

        bool IsLockscreenRequired { get; }

        void Lock();
        void Unlock();

        bool Check(string passcode);
        void Set(string passcode, bool simple, int timeout);
        void Reset();
    }

    public class PasscodeService : IPasscodeService
    {
        private readonly PasscodeLockSettings _settingsService;

        public PasscodeService(PasscodeLockSettings settingsService)
        {
            _settingsService = settingsService;
        }

        public bool IsEnabled
        {
            get => _settingsService.Hash.Length > 0;
        }

        public bool IsSimple
        {
            get => _settingsService.IsSimple;
        }

        public bool IsLocked
        {
            get => _settingsService.IsLocked;
        }

        public bool IsBiometricsEnabled
        {
            get => _settingsService.IsHelloEnabled;
            set =>_settingsService.IsHelloEnabled = value;
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

        public bool IsLockscreenRequired
        {
            get => IsEnabled && ((AutolockTimeout > 0 && CloseTime < DateTime.MaxValue && DateTime.Now > CloseTime.AddSeconds(AutolockTimeout)) || IsLocked);
        }

        public void Lock()
        {
            _settingsService.IsLocked = true;
            _settingsService.CloseTime = DateTime.MaxValue;
            Publish(true);
        }

        public void Unlock()
        {
            _settingsService.IsLocked = false;
            _settingsService.CloseTime = DateTime.MaxValue;
            Publish(true);
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
            _settingsService.IsLocked = false;
            Publish(true);
        }

        public void Reset()
        {
            _settingsService.Clear();
            Publish(false);
        }

        private void Publish(bool enabled)
        {
            var update = new UpdatePasscodeLock(enabled);

            foreach (var aggregator in TLContainer.Current.ResolveAll<IEventAggregator>())
            {
                aggregator.Publish(update);
            }
        }

        public bool CheckSimple(string passcode)
        {
            if (passcode != null && passcode.Length == 4)
            {
                return passcode.All(x => x >= '0' && x <= '9');
            }

            return false;
        }

        public bool Check(string passcode)
        {
            return Utils.ByteArraysEqual(Utils.ComputeHash(_settingsService.Salt, Encoding.UTF8.GetBytes(passcode)), _settingsService.Hash);
        }
    }
}
