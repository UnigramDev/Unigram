//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Storage;

namespace Telegram.Services
{
    public interface IProxyService
    {
        void Migrate(int sessionId);

        Task<AddedProxies> GetProxiesAsync();

        void EnableProxy(int proxyId);
        void EnableSystemProxy();
        void DisableProxy();

        AddedProxy AddProxy(Proxy proxy, bool enabled);
        AddedProxy EditProxy(int proxyId, Proxy proxy);

        void RemoveProxy(int id);
    }

    public class ProxyService : IProxyService
    {
        private readonly LocalDatabase _database;
        private readonly ILifetimeService _lifetime;
        private readonly ISettingsService _settings;

        private readonly HttpProxyWatcher _watcher;
        private readonly EventDebouncer<bool> _debouncer;

        public ProxyService(ILifetimeService lifetime)
        {
            _settings = SettingsService.Current;
            _lifetime = lifetime;
            _database = new LocalDatabase();

            _database.Initialize(Path.Combine(ApplicationData.Current.LocalFolder.Path, "local.db"));
            _database.CreateTable("Proxy",
                new[] { "Id", "Server", "Port", "LastUsedDate", "Type", "Secret", "Username", "Password", "HttpOnly" },
                new[] { "INTEGER PRIMARY KEY AUTOINCREMENT", "TEXT NOT NULL", "INTEGER", "INTEGER", "INTEGER", "TEXT", "TEXT", "TEXT", "INTEGER" });

            _watcher = HttpProxyWatcher.Current;
            _debouncer = new EventDebouncer<bool>(Constants.HoldingThrottle,
                handler => _watcher.Changed += new TypedEventHandler<HttpProxyWatcher, bool>(handler),
                handler => _watcher.Changed -= new TypedEventHandler<HttpProxyWatcher, bool>(handler), true);

            _debouncer.Invoked += OnProxyChanged;
        }

        private void OnProxyChanged(object sender, bool e)
        {
            if (_settings.EnabledProxyId == -1)
            {
                EnableSystemProxy();
            }
        }

        public async void Migrate(int sessionId)
        {
            if (_settings.MigratedProxy)
            {
                if (_settings.EnabledProxyId == -1)
                {
                    EnableSystemProxy();
                }

                return;
            }

            _settings.MigratedProxy = true;

            var merged = new List<AddedProxy>();
            var enabled = default(AddedProxy);

            foreach (var client in _lifetime.ResolveAll<IClientService>())
            {
                var systemProxyId = await client.SendAsync(new GetOption(OptionsService.R.SystemProxy)) as OptionValueInteger;

                var response = await client.SendAsync(new GetProxies());
                if (response is AddedProxies proxies)
                {
                    foreach (var proxy in proxies.Proxies)
                    {
                        if (proxy.Id != systemProxyId?.Value && !merged.Any(x => AreTheSame(x.Proxy, proxy.Proxy)))
                        {
                            merged.Add(proxy);

                            if (client.SessionId == sessionId && proxy.IsEnabled)
                            {
                                enabled = proxy;
                            }
                        }
                    }
                }
            }

            var rows = new List<object[]>();

            foreach (var item in merged)
            {
                rows.Add(ProxyToRow(item));
            }

            if (rows.Count > 0)
            {
                _database.Insert("Proxy",
                    new[] { "Server", "Port", "LastUsedDate", "Type", "Secret", "Username", "Password", "HttpOnly" },
                    rows);
            }

            if (_lifetime.TryResolve(sessionId, out ISettingsService settings) && settings.UseSystemProxy)
            {
                settings.UseSystemProxy = false;
                EnableSystemProxy();
            }
            else if (enabled != null)
            {
                EnableProxy(enabled);
            }
        }

        public AddedProxy AddProxy(Proxy proxy, bool enabled)
        {
            if (ProxyExistsInDatabase(proxy))
            {
                return null;
            }

            var addedProxy = new AddedProxy(0, 0, false, proxy);
            _database.Insert("Proxy",
                new[] { "Server", "Port", "LastUsedDate", "Type", "Secret", "Username", "Password", "HttpOnly" },
                new[] { ProxyToRow(addedProxy) });

            addedProxy.Id = _database.GetLastInsertRowId();

            if (enabled)
            {
                EnableProxy(addedProxy);
            }

            return addedProxy;
        }

        private bool ProxyExistsInDatabase(Proxy proxy)
        {
            if (proxy == null)
            {
                return false;
            }

            // Build the WHERE clause based on proxy type
            string typeFilter;
            List<object> parameters = new List<object>
            {
                proxy.Server,
                proxy.Port
            };

            switch (proxy.Type)
            {
                case ProxyTypeMtproto mtproto:
                    typeFilter = "Type = ? AND Secret = ?";
                    parameters.Add(0); // Type code for MTProto
                    parameters.Add(mtproto.Secret);
                    break;

                case ProxyTypeSocks5 socks5:
                    typeFilter = "Type = ? AND Username = ? AND Password = ?";
                    parameters.Add(1); // Type code for SOCKS5
                    parameters.Add(socks5.Username ?? string.Empty);
                    parameters.Add(socks5.Password ?? string.Empty);
                    break;

                case ProxyTypeHttp http:
                    typeFilter = "Type = ? AND Username = ? AND Password = ? AND HttpOnly = ?";
                    parameters.Add(2); // Type code for HTTP
                    parameters.Add(http.Username ?? string.Empty);
                    parameters.Add(http.Password ?? string.Empty);
                    parameters.Add(http.HttpOnly ? 1 : 0);
                    break;

                default:
                    return false;
            }

            string query = $@"
                SELECT COUNT(*) 
                FROM Proxy 
                WHERE Server = ? AND Port = ? AND {typeFilter};
            ";

            var result = _database.ExecuteScalarQuery(query, parameters);
            return result > 0;
        }

        public AddedProxy EditProxy(int proxyId, Proxy proxy)
        {
            var addedProxy = GetProxyById(proxyId);
            if (addedProxy == null)
            {
                return null;
            }

            addedProxy.Proxy = proxy;

            _database.Update("Proxy",
                new[] { "Server", "Port", "LastUsedDate", "Type", "Secret", "Username", "Password", "HttpOnly" },
                ProxyToRow(addedProxy),
                "Id",
                proxyId);

            if (addedProxy.Id == _settings.EnabledProxyId)
            {
                EnableProxy(addedProxy);
            }

            return addedProxy;
        }

        public void RemoveProxy(int id)
        {
            // If deleting the currently enabled proxy, clear the setting
            if (_settings.EnabledProxyId == id)
            {
                DisableProxy();
            }

            _database.Delete("Proxy", "Id", id);
        }

        public AddedProxy GetProxyById(int id)
        {
            var rows = _database.Select("Proxy", "Id", id, null, null, 1);
            if (rows.Count > 0)
            {
                var proxy = RowToProxy(rows[0]);
                // Set IsEnabled based on settings
                proxy.IsEnabled = _settings.EnabledProxyId == proxy.Id;
                return proxy;
            }
            return null;
        }

        public AddedProxy GetEnabledProxy()
        {
            if (_settings.EnabledProxyId != 0)
            {
                return GetProxyById(_settings.EnabledProxyId);
            }
            return null;
        }

        public int GetProxyCount()
        {
            var rows = _database.Select("Proxy");
            return rows.Count;
        }

        public async void EnableProxy(AddedProxy proxy)
        {
            int currentTimestamp = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            proxy.LastUsedDate = currentTimestamp;

            if (proxy.Id > 0)
            {
                _database.Update("Proxy",
                    new[] { "LastUsedDate" },
                    new object[] { currentTimestamp },
                    "Id",
                    proxy.Id);
            }

            _settings.EnabledProxyId = proxy.Id;
            proxy.IsEnabled = true;

            foreach (var client in _lifetime.ResolveAll<IClientService>())
            {
                var proxyId = await client.SendAsync(new GetOption(OptionsService.R.Proxy)) as OptionValueInteger;
                if (proxyId != null)
                {
                    await client.SendAsync(new EditProxy((int)proxyId.Value, proxy.Proxy, true));
                }
                else
                {
                    var added = await client.SendAsync(new AddProxy(proxy.Proxy, true)) as AddedProxy;
                    if (added != null)
                    {
                        client.Options.Proxy = added.Id;
                    }
                }
            }
        }

        public void EnableProxy(int proxyId)
        {
            var proxy = GetProxyById(proxyId);
            if (proxy != null)
            {
                EnableProxy(proxy);
            }
        }

        public void EnableSystemProxy()
        {
            if (_watcher.IsEnabled)
            {
                string host;
                int port;
                if (TryCreateUri(_watcher.Server, out Uri result))
                {
                    host = result.Host;
                    port = result.Port;
                }
                else
                {
                    host = "localhost";
                    port = 80;
                }

                EnableProxy(new AddedProxy(-1, 0, true, new Proxy(host, port, new ProxyTypeHttp())));
            }
            else
            {
                _settings.EnabledProxyId = -1;

                foreach (var client in _lifetime.ResolveAll<IClientService>())
                {
                    client.Send(new DisableProxy());
                }
            }
        }

        private bool TryCreateUri(string server, out Uri result)
        {
            var query = server.Split(';');

            foreach (var part in query)
            {
                var split = part.Split('=');
                if (split.Length == 2 && string.Equals(split[0], "http"))
                {
                    return MessageHelper.TryCreateUri(split[1], out result);
                }
                else if (split.Length == 1)
                {
                    return MessageHelper.TryCreateUri(split[0], out result);
                }
            }

            result = null;
            return false;
        }

        public void DisableProxy()
        {
            // If enabled proxy is -1 (system) we don't need to update settings
            if (_settings.EnabledProxyId > 0)
            {
                // Update LastUsedDate to current timestamp
                int currentTimestamp = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                _database.Update("Proxy",
                    new[] { "LastUsedDate" },
                    new object[] { currentTimestamp },
                    "Id",
                    _settings.EnabledProxyId);
            }

            // Clear the enabled proxy ID from settings
            _settings.EnabledProxyId = 0;

            foreach (var client in _lifetime.ResolveAll<IClientService>())
            {
                client.Send(new DisableProxy());
            }
        }

        private bool AreTheSame(Proxy x, Proxy y)
        {
            if (x == null || y == null)
            {
                return x == y;
            }

            if (x.Server == y.Server && x.Port == y.Port)
            {
                if (x.Type is ProxyTypeMtproto xMtproto && y.Type is ProxyTypeMtproto yMtproto)
                {
                    return xMtproto.Secret == yMtproto.Secret;
                }
                else if (x.Type is ProxyTypeSocks5 xSocks5 && y.Type is ProxyTypeSocks5 ySocks5)
                {
                    return xSocks5.Username == ySocks5.Username
                        && xSocks5.Password == ySocks5.Password;
                }
                else if (x.Type is ProxyTypeHttp xHttp && y.Type is ProxyTypeHttp yHttp)
                {
                    return xHttp.Username == yHttp.Username
                        && xHttp.Password == yHttp.Password
                        && xHttp.HttpOnly == yHttp.HttpOnly;
                }
            }

            return false;
        }

        private object[] ProxyToRow(AddedProxy proxy)
        {
            return proxy.Proxy.Type switch
            {
                ProxyTypeMtproto mtproto => new object[]
                {
                    proxy.Proxy.Server,
                    proxy.Proxy.Port,
                    proxy.LastUsedDate,
                    0, // Type: MTProto
                    mtproto.Secret,
                    null,
                    null,
                    0
                },
                ProxyTypeSocks5 socks5 => new object[]
                {
                    proxy.Proxy.Server,
                    proxy.Proxy.Port,
                    proxy.LastUsedDate,
                    1, // Type: SOCKS5
                    null,
                    socks5.Username ?? string.Empty,
                    socks5.Password ?? string.Empty,
                    0
                },
                ProxyTypeHttp http => new object[]
                {
                    proxy.Proxy.Server,
                    proxy.Proxy.Port,
                    proxy.LastUsedDate,
                    2, // Type: HTTP
                    null,
                    http.Username ?? string.Empty,
                    http.Password ?? string.Empty,
                    http.HttpOnly ? 1 : 0
                },
                _ => throw new NotSupportedException($"Proxy type {proxy.Proxy.Type?.GetType().Name} is not supported")
            };
        }

        private AddedProxy RowToProxy(object[] row)
        {
            // Row structure: Id, Server, Port, LastUsedDate, Type, Secret, Username, Password, HttpOnly
            var id = Convert.ToInt32(row[0]);
            var server = (string)row[1];
            var port = Convert.ToInt32(row[2]);
            var lastUsedDate = Convert.ToInt32(row[3]);
            var typeCode = Convert.ToInt32(row[4]);

            ProxyType type = typeCode switch
            {
                0 => new ProxyTypeMtproto((string)row[5] ?? string.Empty),
                1 => new ProxyTypeSocks5((string)row[6] ?? string.Empty, (string)row[7] ?? string.Empty),
                2 => new ProxyTypeHttp((string)row[6] ?? string.Empty, (string)row[7] ?? string.Empty, Convert.ToBoolean(row[8])),
                _ => throw new NotSupportedException($"Unknown proxy type code: {typeCode}")
            };

            // IsEnabled is determined by settings, not stored in DB
            bool isEnabled = _settings.EnabledProxyId == id;

            return new AddedProxy(id, lastUsedDate, isEnabled, new Proxy(server, port, type));
        }

        private AddedProxies GetProxiesImpl()
        {
            var rows = _database.Select("Proxy", null, new[] { "LastUsedDate" });
            var items = rows.Select(RowToProxy).ToList();
            return new AddedProxies(items);
        }

        public Task<AddedProxies> GetProxiesAsync()
        {
            return Task.Run(GetProxiesImpl);
        }
    }
}
