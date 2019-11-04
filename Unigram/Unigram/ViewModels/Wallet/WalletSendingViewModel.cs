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
    public class WalletSendingViewModel : TonViewModelBase
    {
        public WalletSendingViewModel(ITonService tonService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonService, protoService, cacheService, settingsService, aggregator)
        {
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            state.TryGet("public_key", out string publicKey);
            state.TryGet("secret", out IList<byte> secret);
            state.TryGet("local_password", out IList<byte> localPassword);

            state.TryGet("address", out string address);
            state.TryGet("amount", out long amount);
            state.TryGet("comment", out string comment);

            IList<byte> message;
            if (string.IsNullOrEmpty(comment))
            {
                message = new byte[0];
            }
            else
            {
                message = Encoding.UTF8.GetBytes(comment.Substring(0, Math.Min(128, comment.Length)));
            }

            var sender = TonService.Execute(new WalletGetAccountAddress(new WalletInitialAccountState(publicKey))) as AccountAddress;
            var recipient = new AccountAddress(address);

            var privateKey = new InputKeyRegular(new Key(publicKey, secret), localPassword);

            var response = await TonService.SendAsync(new GenericSendGrams(privateKey, sender, recipient, amount, 0, true, message));
            if (response is SendGramsResult result)
            {
                var parameters = new Dictionary<string, object>
                {
                    { "amount", amount }
                };

                NavigationService.Navigate(typeof(WalletInfoPage), WalletInfoState.Sent, parameters);
            }
            else if (response is Error error)
            {
                await TLMessageDialog.ShowAsync(error.Message, error.Code.ToString(), Strings.Resources.OK);
            }
        }
    }
}
