using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsWebSessionsViewModel : TLViewModelBase
    {
        public SettingsWebSessionsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new SortedObservableCollection<ConnectedWebsite>(new TLAuthorizationComparer());

            TerminateCommand = new RelayCommand<ConnectedWebsite>(TerminateExecute);
            TerminateOthersCommand = new RelayCommand(TerminateOtherExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await UpdateSessionsAsync();
        }

        private async Task UpdateSessionsAsync()
        {
            var response = await ProtoService.SendAsync(new GetConnectedWebsites());
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

        public RelayCommand<ConnectedWebsite> TerminateCommand { get; }
        private async void TerminateExecute(ConnectedWebsite session)
        {
            var bot = await ProtoService.SendAsync(new CreatePrivateChat(session.BotUserId, false)) as Chat;
            if (bot == null)
            {
                return;
            }

            var dialog = new MessagePopup();
            dialog.Title = Strings.Resources.AppName;
            dialog.Message = string.Format(Strings.Resources.TerminateWebSessionQuestion, session.DomainName);
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;
            dialog.CheckBoxLabel = string.Format(Strings.Resources.TerminateWebSessionStop, bot.Title);

            var terminate = await dialog.ShowQueuedAsync();
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ProtoService.SendAsync(new DisconnectWebsite(session.Id));
                if (response is Ok)
                {
                    Items.Remove(session);
                }
                else if (response is Error error)
                {
                    Logs.Logger.Error(Logs.Target.API, "auth.resetWebAuthotization error " + error);
                }

                ProtoService.Send(new ToggleChatIsBlocked(bot.Id, true));
            }
        }

        public RelayCommand TerminateOthersCommand { get; }
        private async void TerminateOtherExecute()
        {
            var terminate = await MessagePopup.ShowAsync(Strings.Resources.AreYouSureWebSessions, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ProtoService.SendAsync(new DisconnectAllWebsites());
                if (response is Ok)
                {
                    Items.Clear();
                }
                else if (response is Error error)
                {
                    Logs.Logger.Error(Logs.Target.API, "auth.resetWebAuthotizations error " + error);
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
