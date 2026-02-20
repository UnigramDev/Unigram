//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Settings;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsPasskeysViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        public SettingsPasskeysViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<Passkey>(this);
            Items.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(CanAdd));
        }

        public ObservableCollection<Passkey> Items { get; private set; }

        public bool CanAdd => Items.Count < ClientService.Options.LoginPasskeyCountMax;

        public async void Info()
        {
            if (!BridgeApplicationContext.IsPasskeySupported())
            {
                ShowPopup(Strings.PasskeyNotSupportedText, Strings.AppName, Strings.OK);
                return;
            }

            var confirm = await ShowPopupAsync(new SettingsPasskeysIntroPopup());
            if (confirm == ContentDialogResult.Primary)
            {
                CreateImpl();
            }
        }

        public async void Create()
        {
            if (!BridgeApplicationContext.IsPasskeySupported())
            {
                ShowPopup(Strings.PasskeyNotSupportedText, Strings.AppName, Strings.OK);
                return;
            }

            CreateImpl();
        }

        private async void CreateImpl()
        {
            var response = await BridgeApplicationContext.AddLoginPasskeyAsync(ClientService);
            if (response is Passkey passkey)
            {
                Items.Insert(0, passkey);
                ShowToast(string.Format("**{0}**\n{1}", Strings.PasskeyAddedTitle, string.Format(Strings.PasskeyAddedText, passkey.Name)));
            }
            else if (response is Error { Code: not -2147023673 and not -2146893770 } error)
            {
                ShowToast(error);
            }
        }

        public async void Delete(Passkey passkey)
        {
            var confirm = await ShowPopupAsync(Strings.PasskeyDeleteText, Strings.PasskeyDeleteTitle, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                Items.Remove(passkey);
                ClientService.Send(new RemoveLoginPasskey(passkey.Id));
            }
        }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await ClientService.SendAsync(new GetLoginPasskeys());
            if (response is Passkeys passkeys)
            {
                foreach (var passkey in passkeys.PasskeysValue)
                {
                    Items.Add(passkey);
                    totalCount++;
                }
            }

            HasMoreItems = false;
            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems { get; private set; } = true;
    }
}
