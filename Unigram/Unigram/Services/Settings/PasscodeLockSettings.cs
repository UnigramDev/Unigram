using System;

namespace Unigram.Services.Settings
{
    public class PasscodeLockSettings : SettingsServiceBase
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
        }

        private byte[] _hash;
        public byte[] Hash
        {
            get
            {
                if (_hash == null)
                    _hash = Convert.FromBase64String(GetValueOrDefault("Hash", string.Empty));

                return _hash ?? new byte[0];
            }
            set
            {
                _hash = value;
                AddOrUpdateValue("Hash", Convert.ToBase64String(value));
            }
        }

        private byte[] _salt;
        public byte[] Salt
        {
            get
            {
                if (_salt == null)
                    _salt = Convert.FromBase64String(GetValueOrDefault("Salt", string.Empty));

                return _salt ?? new byte[0];
            }
            set
            {
                _salt = value;
                AddOrUpdateValue("Salt", Convert.ToBase64String(value));
            }
        }

        private bool? _isSimple;
        public bool IsSimple
        {
            get
            {
                if (_isSimple == null)
                    _isSimple = GetValueOrDefault("IsSimple", true);

                return _isSimple ?? true;
            }
            set
            {
                _isSimple = value;
                AddOrUpdateValue("IsSimple", value);
            }
        }

        private DateTime? _closeTime;
        public DateTime CloseTime
        {
            get
            {
                if (_closeTime == null)
                    _closeTime = DateTime.FromFileTimeUtc(GetValueOrDefault("CloseTime", 2650467743999999999 /* DateTime.MaxValue */));

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
            get
            {
                if (_autolockTimeout == null)
                    _autolockTimeout = GetValueOrDefault("AutolockTimeout", 0);

                return _autolockTimeout ?? 0;
            }
            set
            {
                _autolockTimeout = value;
                AddOrUpdateValue("AutolockTimeout", value);
            }
        }

        private bool? _isLocked;
        public bool IsLocked
        {
            get
            {
                if (_isLocked == null)
                    _isLocked = GetValueOrDefault("IsLocked", false);

                return _isLocked ?? false;
            }
            set
            {
                _isLocked = value;
                AddOrUpdateValue("IsLocked", value);
            }
        }

        private bool? _isHelloEnabled;
        public bool IsHelloEnabled
        {
            get
            {
                if (_isHelloEnabled == null)
                    _isHelloEnabled = GetValueOrDefault("IsHelloEnabled", false);

                return _isHelloEnabled ?? false;
            }
            set
            {
                _isHelloEnabled = value;
                AddOrUpdateValue("IsHelloEnabled", value);
            }
        }

        private bool? _isScreenshotEnabled;
        public bool IsScreenshotEnabled
        {
            get
            {
                if (_isScreenshotEnabled == null)
                    _isScreenshotEnabled = GetValueOrDefault("IsScreenshotEnabled", true);

                return _isScreenshotEnabled ?? true;
            }
            set
            {
                _isScreenshotEnabled = value;
                AddOrUpdateValue("IsScreenshotEnabled", value);
            }
        }
    }
}
