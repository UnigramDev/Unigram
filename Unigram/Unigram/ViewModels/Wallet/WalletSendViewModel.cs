using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ton.Tonlib.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Wallet
{
    public class WalletSendViewModel : TonViewModelBase
    {
        public WalletSendViewModel(ITonlibService tonlibService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonlibService, protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
        }

        private string _address;
        public string Address
        {
            get => _address;
            set => Set(ref _address, value);
        }

        private long _amount;
        public long Amount
        {
            get => _amount;
            set => Set(ref _amount, value);
        }

        private string _comment;
        public string Comment
        {
            get => _comment;
            set => Set(ref _comment, value);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is string url && TryParseUrl(url))
            {
                return base.OnNavigatedToAsync(parameter, mode, state);
            }

            if (state.TryGet("address", out string address))
            {
                Address = address;
            }
            if (state.TryGet("amount", out string amountValue) && long.TryParse(amountValue, out long amount))
            {
                Amount = amount;
            }
            if (state.TryGet("text", out string comment))
            {
                Comment = comment;
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            IList<byte> message;
            if (string.IsNullOrEmpty(_comment))
            {
                message = new byte[0];
            }
            else
            {
                message = Encoding.UTF8.GetBytes(_comment.Substring(0, Math.Min(128, _comment.Length)));
            }

            var self = ProtoService.Options.GetValue<string>("x_wallet_address");
            var publicKey = ProtoService.Options.GetValue<string>("x_wallet_public_key");
            var secret = Utils.StringToByteArray(ProtoService.Options.GetValue<string>("x_wallet_secret"));

            var local_password = Encoding.UTF8.GetBytes("local_passwordlocal_passwordlocal_passwordlocal_passwordlocal_pa");

            var privateKey = new InputKey(new Key(publicKey, secret), local_password);

            var address = _address.Replace("ton://", string.Empty);

            //var response = await TonlibService.SendAsync(new WalletSendGrams(privateKey, new AccountAddress(address), state.Seqno, long.MaxValue, _amount, message));
            var response = await TonlibService.SendAsync(new GenericSendGrams(privateKey, new AccountAddress(self), new AccountAddress(address), _amount, 0, false, message));
        }

        public bool TryParseUrl(string text)
        {
            if (Uri.TryCreate(text, UriKind.Absolute, out Uri result))
            {
                if (MessageHelper.IsTonScheme(result) && string.Equals(result.Host, "transfer", StringComparison.OrdinalIgnoreCase))
                {
                    Address = result.AbsolutePath.Replace("/", "");

                    var query = result.Query.ParseQueryString();
                    if (query.TryGetValue("amount", out string amountValue) && long.TryParse(amountValue, out long amount))
                    {
                        Amount = amount;
                    }
                    if (query.TryGetValue("text", out string comment))
                    {
                        Comment = comment;
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
