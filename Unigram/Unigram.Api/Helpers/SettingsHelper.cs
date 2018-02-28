using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using Windows.Storage;

namespace Telegram.Api.Helpers
{
    public static class SettingsHelper
    {
        private static readonly ApplicationDataContainer isolatedStore;

        static SettingsHelper()
        {
            try
            {
                isolatedStore = ApplicationData.Current.LocalSettings;
            }
            catch { }
        }

        private static bool AddOrUpdateValue(string key, Object value)
        {
            bool valueChanged = false;

            if (isolatedStore.Values.ContainsKey(key))
            {
                if (isolatedStore.Values[key] != value)
                {
                    isolatedStore.Values[key] = value;
                    valueChanged = true;
                }
            }
            else
            {
                isolatedStore.Values.Add(key, value);
                valueChanged = true;
            }

            return valueChanged;
        }

        private static valueType GetValueOrDefault<valueType>(string key, valueType defaultValue)
        {
            valueType value;

            if (isolatedStore.Values.ContainsKey(key))
            {
                value = (valueType)isolatedStore.Values[key];
            }
            else
            {
                value = defaultValue;
            }

            return value;
        }

        private static int? _selectedAccount;
        public static int SelectedAccount
        {
            get
            {
                if (_selectedAccount == null)
                    _selectedAccount = GetValueOrDefault("SelectedAccount", 0);

                return _selectedAccount ?? 0;
            }
            set
            {
                _selectedAccount = value;
                AddOrUpdateValue("SelectedAccount", value);
            }
        }

        public static string ChannelUri
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("ChannelUri"))
                {
                    return (string)ApplicationData.Current.LocalSettings.Values["ChannelUri"];
                }

                return null;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["ChannelUri"] = value;
            }
        }

        #region Proxy

        public static void CleanUp()
        {
            _isProxyEnabled = null;
            _isCallsProxyEnabled = null;
            _proxyServer = null;
            _proxyPort = null;
            _proxyUsername = null;
            _proxyPassword = null;
        }

        private static bool? _isProxyEnabled;
        public static bool IsProxyEnabled
        {
            get
            {
                if (_isProxyEnabled == null)
                    _isProxyEnabled = GetValueOrDefault("ProxyEnabled", false);

                return _isProxyEnabled ?? false;
            }
            set
            {
                _isProxyEnabled = value;
                AddOrUpdateValue("ProxyEnabled", value);
            }
        }

        private static bool? _isCallsProxyEnabled;
        public static bool IsCallsProxyEnabled
        {
            get
            {
                if (_isCallsProxyEnabled == null)
                    _isCallsProxyEnabled = GetValueOrDefault("CallsProxyEnabled", false);

                return _isCallsProxyEnabled ?? false;
            }
            set
            {
                _isCallsProxyEnabled = value;
                AddOrUpdateValue("CallsProxyEnabled", value);
            }
        }

        private static string _proxyServer;
        public static string ProxyServer
        {
            get
            {
                if (_proxyServer == null)
                    _proxyServer = GetValueOrDefault<string>("ProxyServer", null);

                return _proxyServer;
            }
            set
            {
                _proxyServer = value;
                AddOrUpdateValue("ProxyServer", value);
            }
        }

        private static int? _proxyPort;
        public static int ProxyPort
        {
            get
            {
                if (_proxyPort == null)
                    _proxyPort = GetValueOrDefault("ProxyPort", 1080);

                return _proxyPort ?? 1080;
            }
            set
            {
                _proxyPort = value;
                AddOrUpdateValue("ProxyPort", value);
            }
        }

        private static string _proxyUsername;
        public static string ProxyUsername
        {
            get
            {
                if (_proxyUsername == null)
                    _proxyUsername = GetValueOrDefault<string>("ProxyUsername", null);

                return _proxyUsername;
            }
            set
            {
                _proxyUsername = value;
                AddOrUpdateValue("ProxyUsername", value);
            }
        }

        private static string _proxyPassword;
        public static string ProxyPassword
        {
            get
            {
                if (_proxyPassword == null)
                    _proxyPassword = GetValueOrDefault<string>("ProxyPassword", null);

                return _proxyPassword;
            }
            set
            {
                _proxyPassword = value;
                AddOrUpdateValue("ProxyPassword", value);
            }
        }

        #endregion
    }
}