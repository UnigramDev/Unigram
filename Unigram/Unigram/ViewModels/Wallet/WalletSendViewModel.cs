using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ton.Tonlib.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Views.Wallet;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Wallet
{
    public class WalletSendViewModel : TonViewModelBase
    {
        public WalletSendViewModel(ITonService tonService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonService, protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
        }

        private string _address;
        public string Address
        {
            get => _address;
            set => Set(ref _address, value);
        }

        private long _balance;
        public long Balance
        {
            get => _balance;
            set => Set(ref _balance, value);
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

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is string url && TryParseUrl(url))
            {
                //return;
            }
            else
            {
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
            }

            if (state.TryGet("balance", out long balance))
            {
                Balance = balance;
            }

            var self = TonService.Execute(new WalletGetAccountAddress(new WalletInitialAccountState(ProtoService.Options.WalletPublicKey))) as AccountAddress;
            if (self == null)
            {
                return;
            }

            var response = await TonService.SendAsync(new GenericGetAccountState(self));
            if (response is GenericAccountState accountState)
            {
                Balance = accountState.GetBalance();
            }
            else if (response is Error error)
            {
                await TLMessageDialog.ShowAsync(error.Message, error.Code.ToString(), Strings.Resources.OK);
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var publicKey = ProtoService.Options.WalletPublicKey;

            var secret = await TonService.Encryption.DecryptAsync(publicKey);
            if (secret == null)
            {
                // TODO:
                return;
            }

            var parameters = new Dictionary<string, object>
            {
                { "public_key", publicKey },
                { "secret", secret.Item1 },
                { "local_password", secret.Item2 },

                { "address", _address },
                { "amount", _amount },
                { "comment", _comment }
            };

            NavigationService.Navigate(typeof(WalletSendingPage), state: parameters);

            //IList<byte> message;
            //if (string.IsNullOrEmpty(_comment))
            //{
            //    message = new byte[0];
            //}
            //else
            //{
            //    message = Encoding.UTF8.GetBytes(_comment.Substring(0, Math.Min(128, _comment.Length)));
            //}

            

            //var localPassword = secret.Item2;
            //var privateKey = new InputKey(new Key(publicKey, secret.Item1), localPassword);

            //var self = TonService.Execute(new WalletGetAccountAddress(new WalletInitialAccountState(publicKey))) as AccountAddress;
            //var address = _address.Replace("ton://", string.Empty);

            //var response = await TonService.SendAsync(new GenericSendGrams(privateKey, self, new AccountAddress(address), _amount, 0, false, message));
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
