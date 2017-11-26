using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Windows.Security.Cryptography;
using TLPasscodeTuple = Telegram.Api.TL.TLTuple<byte[], byte[], bool, int, int, bool, bool, bool>;

namespace Unigram.Services
{
    public interface IPasscodeService : INotifyPropertyChanged
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

    public class PasscodeService : ServiceBase, IPasscodeService
    {
        private readonly object _passcodeParamsFileSyncRoot = new object();
        private TLPasscodeParams _cachedParams;
        private bool _readOnce;

        private TLPasscodeParams GetParams()
        {
            if (_cachedParams != null)
            {
                return _cachedParams;
            }

            if (!_readOnce)
            {
                _readOnce = true;
                _cachedParams = new TLPasscodeParams(TLUtils.OpenObjectFromMTProtoFile<TLPasscodeTuple>(_passcodeParamsFileSyncRoot, "passcode_params.dat"));
            }

            return _cachedParams;
        }

        public bool IsEnabled
        {
            get
            {
                var data = GetParams();
                if (data != null)
                {
                    return data.Hash != null && data.Hash.Length > 0;
                }

                return false;
            }
        }

        public bool IsSimple
        {
            get
            {
                var data = GetParams();
                if (data != null)
                {
                    return data.IsSimple;
                }

                return true;
            }
        }

        public bool IsLocked
        {
            get
            {
                var data = GetParams();
                if (data != null)
                {
                    return data.IsLocked;
                }

                return false;
            }
        }

        public bool IsBiometricsEnabled
        {
            get
            {
                var data = GetParams();
                if (data != null)
                {
                    return data.IsHelloEnabled;
                }

                return false;
            }
            set
            {
                var data = GetParams();
                if (data != null)
                {
                    data.IsHelloEnabled = value;
                    Save();
                }
            }
        }

        public DateTime CloseTime
        {
            get
            {
                var data = GetParams();
                if (data != null)
                {
                    return TLUtils.ToDateTime(data.CloseTime);
                }

                return DateTime.Now.AddYears(1);
            }
            set
            {
                var data = GetParams();
                if (data != null)
                {
                    data.CloseTime = TLUtils.ToTLInt(value);
                    Save();
                }
            }
        }

        public int AutolockTimeout
        {
            get
            {
                var data = GetParams();
                if (data != null)
                {
                    return data.AutolockTimeout;
                }

                return 0;
            }
            set
            {
                var data = GetParams();
                if (data != null)
                {
                    data.AutolockTimeout = value;
                    Save();
                }
            }
        }

        public bool IsLockscreenRequired
        {
            get
            {
                return IsEnabled && ((AutolockTimeout > 0 && DateTime.Now > CloseTime.AddSeconds(AutolockTimeout)) || IsLocked);
            }
        }

        public void Lock()
        {
            var data = GetParams();
            if (data != null)
            {
                data.IsLocked = true;
                Save();
            }

            RaisePropertyChanged(() => IsLocked);
        }

        public void Unlock()
        {
            var data = GetParams();
            if (data != null)
            {
                data.IsLocked = false;
                Save();
            }

            RaisePropertyChanged(() => IsLocked);
        }

        public void ChangeLocked()
        {
            var data = GetParams();
            if (data != null)
            {
                if (data.IsLocked)
                {
                    Unlock();
                }
                else
                {
                    Lock();
                }
            }
        }

        private void Save()
        {
            if (_cachedParams != null && _cachedParams.Hash != null && _cachedParams.Salt != null)
            {
                TLUtils.SaveObjectToMTProtoFile(_passcodeParamsFileSyncRoot, "passcode_params.dat", _cachedParams.ToTuple());
            }
        }

        public void Set(string passcode, bool simple, int timeout)
        {
            var salt = CryptographicBuffer.GenerateRandom(256).ToArray();
            var data = Utils.ComputeSHA1(TLUtils.Combine(salt, Encoding.UTF8.GetBytes(passcode), salt));

            var cachedParams = new TLPasscodeParams
            {
                Hash = data,
                Salt = salt,
                IsSimple = simple,
                AutolockTimeout = timeout,
                CloseTime = 0,
                IsLocked = false
            };

            _cachedParams = cachedParams;
            Save();

            RaisePropertyChanged(() => IsEnabled);
            RaisePropertyChanged(() => IsLocked);
        }

        public void Reset()
        {
            _cachedParams = null;
            FileUtils.Delete(_passcodeParamsFileSyncRoot, "passcode_params.dat");

            RaisePropertyChanged(() => IsEnabled);
            RaisePropertyChanged(() => IsLocked);
        }

        public bool CheckSimple(string passcode)
        {
            if (passcode != null && passcode.Length == 4)
            {
                return passcode.All(x => x >= '0' && x <= '9');
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public byte[] ComputeHash(byte[] salt, byte[] passcode)
        {
            var array = TLUtils.Combine(salt, passcode, salt);
            for (int i = 0; i < 1000; i++)
            {
                var data = TLUtils.Combine(BitConverter.GetBytes(i), array);
                Utils.ComputeSHA1(data);
            }
            return Utils.ComputeSHA1(array);
        }

        public bool Check(string passcode)
        {
            var cached = GetParams();
            if (cached != null)
            {
                return TLUtils.ByteArraysEqual(ComputeHash(cached.Salt, Encoding.UTF8.GetBytes(passcode)), cached.Hash);
            }

            return true;
        }

        public class TLPasscodeParams
        {
            public byte[] Hash { get; set; }
            public byte[] Salt { get; set; }
            public bool IsSimple { get; set; }
            public int CloseTime { get; set; }
            public int AutolockTimeout { get; set; }
            public bool IsLocked { get; set; }
            public bool IsHelloEnabled { get; set; }
            public bool IsScreenshotEnabled { get; set; }

            public TLPasscodeParams()
            {

            }

            public TLPasscodeParams(TLPasscodeTuple tuple)
            {
                if (tuple == null)
                {
                    return;
                }

                Hash = tuple.Item1;
                Salt = tuple.Item2;
                IsSimple = tuple.Item3;
                CloseTime = tuple.Item4;
                AutolockTimeout = tuple.Item5;
                IsLocked = tuple.Item6;
                IsHelloEnabled = tuple.Item7;
                IsScreenshotEnabled = tuple.Item8;
            }

            public TLPasscodeTuple ToTuple()
            {
                return new TLPasscodeTuple(Hash, Salt, IsSimple, CloseTime, AutolockTimeout, IsLocked, IsHelloEnabled, IsScreenshotEnabled);
            }
        }
    }
}
