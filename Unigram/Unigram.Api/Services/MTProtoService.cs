//#define DEBUG_UPDATEDCOPTIONS
using System;
using Telegram.Api.Helpers;
using Windows.UI.Xaml;

namespace Telegram.Api.Services
{
    public class CountryEventArgs : EventArgs
    {
        public string Country { get; set; }
    }

    public partial class MTProtoService : ServiceBase, IMTProtoService
    {
        public event EventHandler<CountryEventArgs> GotUserCountry;

        protected void RaiseGotUserCountry(string country)
        {
            _country = country;
            GotUserCountry?.Invoke(this, new CountryEventArgs { Country = country });
        }

        private string _country;
        public string Country => _country;

        public MTProtoService(int account)
        {
        }
    }
}
