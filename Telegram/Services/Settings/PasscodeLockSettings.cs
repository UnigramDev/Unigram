//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;

namespace Telegram.Services.Settings
{
    public partial class PasscodeLockSettings : SettingsServiceBase
    {
        public PasscodeLockSettings()
            : base("PasscodeLock")
        {
        }

        public override void Clear()
        {
            base.Clear();

            _hash = null;
            _salt = null;
            _isSimple = null;
            _closeTime = null;
            _autolockTimeout = null;
            _isLocked = null;
            _isHelloEnabled = null;
            _isScreenshotEnabled = null;
            _retryCount = null;
            _retryTime = null;
        }

        private byte[] _hash;
        public byte[] Hash
        {
            get => _hash ??= Convert.FromBase64String(GetValueOrDefault("Hash", string.Empty));
            set => AddOrUpdateValue("Hash", Convert.ToBase64String(_hash = value));
        }

        private byte[] _salt;
        public byte[] Salt
        {
            get => _salt ??= Convert.FromBase64String(GetValueOrDefault("Salt", string.Empty));
            set => AddOrUpdateValue("Salt", Convert.ToBase64String(_salt = value));
        }

        private bool? _isSimple;
        public bool IsSimple
        {
            get => _isSimple ??= GetValueOrDefault("IsSimple", true);
            set => AddOrUpdateValue(ref _isSimple, "IsSimple", value);
        }

        private DateTime? _closeTime;
        public DateTime CloseTime
        {
            get
            {
                if (_closeTime == null)
                {
                    _closeTime = DateTime.FromFileTimeUtc(GetValueOrDefault("CloseTime", 2650467743999999999 /* DateTime.MaxValue */));
                }

                return _closeTime ?? DateTime.MaxValue;
            }
            set
            {
                _closeTime = value;
                AddOrUpdateValue("CloseTime", value.ToFileTimeUtc());
            }
        }

        private int? _autolockTimeout;
        public int AutolockTimeout
        {
            get => _autolockTimeout ??= GetValueOrDefault("AutolockTimeout", 0);
            set => AddOrUpdateValue(ref _autolockTimeout, "AutolockTimeout", value);
        }

        private bool? _isLocked;
        public bool IsLocked
        {
            get => _isLocked ??= GetValueOrDefault("IsLocked", false);
            set => AddOrUpdateValue(ref _isLocked, "IsLocked", value);
        }

        private bool? _isHelloEnabled;
        public bool IsHelloEnabled
        {
            get => _isHelloEnabled ??= GetValueOrDefault("IsHelloEnabled", false);
            set => AddOrUpdateValue(ref _isHelloEnabled, "IsHelloEnabled", value);
        }

        private bool? _isScreenshotEnabled;
        public bool IsScreenshotEnabled
        {
            get => _isScreenshotEnabled ??= GetValueOrDefault("IsScreenshotEnabled", true);
            set => AddOrUpdateValue(ref _isScreenshotEnabled, "IsScreenshotEnabled", value);
        }

        private int? _retryCount;
        public int RetryCount
        {
            get => _retryCount ??= GetValueOrDefault("RetryCount", 0);
            set => AddOrUpdateValue(ref _retryCount, "RetryCount", value);
        }


        private DateTime? _retryTime;
        public DateTime RetryTime
        {
            get
            {
                if (_retryTime == null)
                {
                    _retryTime = DateTime.FromFileTimeUtc(GetValueOrDefault("RetryTime", 2650467743999999999 /* DateTime.MaxValue */));
                }

                return _retryTime ?? DateTime.MaxValue;
            }
            set
            {
                _retryTime = value;
                AddOrUpdateValue("RetryTime", value.ToFileTimeUtc());
            }
        }
    }
}
