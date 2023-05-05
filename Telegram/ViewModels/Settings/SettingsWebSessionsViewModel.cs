//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsWebSessionsViewModel : TLViewModelBase
    {
        public SettingsWebSessionsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new SortedObservableCollection<ConnectedWebsite>(new TLAuthorizationComparer());
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            await UpdateSessionsAsync();
        }

        private async Task UpdateSessionsAsync()
        {
            var response = await ClientService.SendAsync(new GetConnectedWebsites());
            if (response is ConnectedWebsites websites)
            {
                Items.Clear();

                foreach (var item in websites.Websites)
                {
                    Items.Add(item);

                    //if (_cachedItems.ContainsKey(item.Hash))
                    //{
                    //    if (item.IsCurrent)
                    //    {
                    //        var cached = _cachedItems[item.Hash];
                    //        cached.Update(item);
                    //        cached.RaisePropertyChanged(() => cached.AppName);
                    //        cached.RaisePropertyChanged(() => cached.AppVersion);
                    //        cached.RaisePropertyChanged(() => cached.DeviceModel);
                    //        cached.RaisePropertyChanged(() => cached.Platform);
                    //        cached.RaisePropertyChanged(() => cached.SystemVersion);
                    //        cached.RaisePropertyChanged(() => cached.Ip);
                    //        cached.RaisePropertyChanged(() => cached.Country);
                    //        cached.RaisePropertyChanged(() => cached.DateActive);
                    //    }
                    //    else
                    //    {
                    //        Items.Remove(_cachedItems[item.Hash]);
                    //        Items.Add(item);

                    //        _cachedItems[item.Hash] = item;
                    //    }
                    //}
                    //else
                    //{
                    //    _cachedItems[item.Hash] = item;
                    //    if (item.IsCurrent)
                    //    {
                    //        Current = item;
                    //    }
                    //    else
                    //    {
                    //        Items.Add(item);
                    //    }
                    //}
                }
            }
        }

        public ObservableCollection<ConnectedWebsite> Items { get; private set; }

        public async void Terminate(ConnectedWebsite session)
        {
            var bot = ClientService.GetUser(session.BotUserId);
            if (bot == null)
            {
                return;
            }

            var dialog = new MessagePopup();
            dialog.Title = Strings.AppName;
            dialog.Message = string.Format(Strings.TerminateWebSessionQuestion, session.DomainName);
            dialog.PrimaryButtonText = Strings.OK;
            dialog.SecondaryButtonText = Strings.Cancel;
            dialog.CheckBoxLabel = string.Format(Strings.TerminateWebSessionStop, bot.FullName());

            var terminate = await ShowPopupAsync(dialog);
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new DisconnectWebsite(session.Id));
                if (response is Ok)
                {
                    Items.Remove(session);
                }
                else if (response is Error error)
                {
                    Logger.Error(error.Message);
                }

                ClientService.Send(new ToggleMessageSenderIsBlocked(new MessageSenderUser(session.BotUserId), true));
            }
        }

        public async void TerminateOthers()
        {
            var terminate = await ShowPopupAsync(Strings.AreYouSureWebSessions, Strings.AppName, Strings.OK, Strings.Cancel);
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new DisconnectAllWebsites());
                if (response is Ok)
                {
                    Items.Clear();
                }
                else if (response is Error error)
                {
                    Logger.Error(error.Message);
                }
            }
        }

        public class TLAuthorizationComparer : IComparer<ConnectedWebsite>
        {
            public int Compare(ConnectedWebsite x, ConnectedWebsite y)
            {
                var epoch = y.LastActiveDate.CompareTo(x.LastActiveDate);
                if (epoch == 0)
                {
                    var appName = x.DomainName.CompareTo(y.DomainName);
                    if (appName == 0)
                    {
                        return x.Id.CompareTo(y.Id);
                    }

                    return appName;
                }

                return epoch;
            }
        }
    }
}
