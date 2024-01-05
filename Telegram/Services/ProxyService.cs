using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Td.Api;
using Windows.Storage;

namespace Telegram.Services
{
    public class ProxyService
    {
        private readonly LocalDatabase _database;

        public ProxyService()
        {
            _database = new LocalDatabase();

            _database.Initialize(Path.Combine(ApplicationData.Current.LocalFolder.Path, "local.db"));
            _database.CreateTable("Proxy",
                new[] { "Id", "Server", "Port", "LastUsedDate", "Type", "Secret", "Username", "Password", "HttpOnly" },
                new[] { "INTEGER PRIMARY KEY AUTOINCREMENT", "TEXT NOT NULL", "INTEGER", "INTEGER", "INTEGER", "TEXT", "TEXT", "TEXT", "INTEGER" });
        }

        public Task<Proxies> GetProxiesAsync(IClientService merge, ISettingsService settings)
        {
            if (settings.AreMaterialsEnabled)
            {
                return GetProxiesAsyncImpl();
            }

            var tsc = new TaskCompletionSource<Proxies>();
            merge.Send(new GetProxies(), result =>
            {
                var impl = GetProxiesImpl();

                if (result is Proxies merge)
                {
                    var rows = new List<object[]>();

                    foreach (var item in merge.ProxiesValue)
                    {
                        if (impl.ProxiesValue.Any(x => AreTheSame(x, item)))
                        {
                            continue;
                        }

                        impl.ProxiesValue.Add(item);

                        if (item.Type is ProxyTypeMtproto mtproto)
                        {
                            rows.Add(new object[]
                            {
                                item.Server, item.Port, item.LastUsedDate, 0, mtproto.Secret, null, null, 0
                            });
                        }
                        else if (item.Type is ProxyTypeSocks5 socks5)
                        {
                            rows.Add(new object[]
                            {
                                item.Server, item.Port, item.LastUsedDate, 1, null, socks5.Username, socks5.Password, 0
                            });
                        }
                        else if (item.Type is ProxyTypeHttp http)
                        {
                            rows.Add(new object[]
                            {
                                item.Server, item.Port, item.LastUsedDate, 2, null, http.Username, http.Password, http.HttpOnly ? 1 : 0
                            });
                        }
                    }

                    _database.Insert("Proxy",
                        new[] { "Id", "Server", "Port", "LastUsedDate", "Type", "Secret", "Username", "Password", "HttpOnly" },
                        rows);
                }

                tsc.SetResult(impl);
            });

            return tsc.Task;
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

        private Proxies GetProxiesImpl()
        {
            var rows = _database.Select("Proxy", null, new[] { "LastUsedDate" });
            var items = new List<Proxy>();

            foreach (var row in rows)
            {
                ProxyType type = row[4] switch
                {
                    0 => new ProxyTypeMtproto((string)row[5]),
                    1 => new ProxyTypeSocks5((string)row[6], (string)row[7]),
                    2 => new ProxyTypeHttp((string)row[6], (string)row[7], Convert.ToBoolean(row[8])),
                    _ => null
                };

                items.Add(new Proxy((int)row[0], (string)row[1], (int)row[2], (int)row[3], false, type));
            }

            return new Proxies(items);
        }

        private Task<Proxies> GetProxiesAsyncImpl()
        {
            return Task.Run(GetProxiesImpl);
        }
    }
}
