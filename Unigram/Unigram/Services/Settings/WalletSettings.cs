using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Unigram.Services.Settings
{
    public class WalletSettings : SettingsServiceBase
    {
        public WalletSettings(ApplicationDataContainer container)
            : base(container)
        {

        }

        private string _config;
        public string Config
        {
            get
            {
                if (_config == null)
                    _config = GetValueOrDefault("WalletConfig", null as string);

                return _config;
            }
            set
            {
                _config = value;
                AddOrUpdateValue("WalletConfig", value);
            }
        }

        private string _name;
        public string Name
        {
            get
            {
                if (_name == null)
                    _name = GetValueOrDefault("WalletBlockchainName", null as string);

                return _name;
            }
            set
            {
                _name = value;
                AddOrUpdateValue("WalletBlockchainName", value);
            }
        }

    }
}
