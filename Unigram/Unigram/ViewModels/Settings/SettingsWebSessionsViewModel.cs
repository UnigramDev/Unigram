using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsWebSessionsViewModel : UnigramViewModelBase
    {
        private Dictionary<long, TLAuthorization> _cachedItems;

        public SettingsWebSessionsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            _cachedItems = new Dictionary<long, TLAuthorization>();
            Items = new SortedObservableCollection<TLWebAuthorization>(new TLAuthorizationComparer());

            TerminateCommand = new RelayCommand<TLWebAuthorization>(TerminateExecute);
            TerminateOthersCommand = new RelayCommand(TerminateOtherExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await UpdateSessionsAsync();
            Aggregator.Subscribe(this);
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return Task.CompletedTask;
        }

        public void Handle(TLUpdateServiceNotification update)
        {
            BeginOnUIThread(async () =>
            {
                await UpdateSessionsAsync();
            });
        }

        private async Task UpdateSessionsAsync()
        {
            var response = await ProtoService.GetWebAuthorizationsAsync();
            if (response.IsSucceeded)
            {
                Items.Clear();

                foreach (var item in response.Result.Authorizations)
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

        public ObservableCollection<TLWebAuthorization> Items { get; private set; }

        public RelayCommand<TLWebAuthorization> TerminateCommand { get; }
        private async void TerminateExecute(TLWebAuthorization session)
        {
            var dialog = new TLMessageDialog();
            dialog.Title = Strings.Android.AppName;
            dialog.Message = string.Format(Strings.Android.TerminateWebSessionQuestion, session.Domain);
            dialog.PrimaryButtonText = Strings.Android.OK;
            dialog.SecondaryButtonText = Strings.Android.Cancel;
            dialog.CheckBoxLabel = string.Format(Strings.Android.TerminateWebSessionStop, session.Bot.FirstName);

            var terminate = await dialog.ShowQueuedAsync();
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ProtoService.ResetWebAuthorizationAsync(session.Hash);
                if (response.IsSucceeded)
                {
                    Items.Remove(session);
                }
                else
                {
                    Execute.ShowDebugMessage("auth.resetWebAuthotization error " + response.Error);
                }

                ProtoService.BlockAsync(session.Bot.ToInputUser(), r => { });
            }
        }

        public RelayCommand TerminateOthersCommand { get; }
        private async void TerminateOtherExecute()
        {
            var terminate = await TLMessageDialog.ShowAsync(Strings.Android.AreYouSureWebSessions, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ProtoService.ResetWebAuthorizationsAsync();
                if (response.IsSucceeded)
                {
                    Items.Clear();
                }
                else
                {
                    Execute.ShowDebugMessage("auth.resetWebAuthotizations error " + response.Error);
                }
            }
        }

        public class TLAuthorizationComparer : IComparer<TLWebAuthorization>
        {
            public int Compare(TLWebAuthorization x, TLWebAuthorization y)
            {
                var epoch = y.DateActive.CompareTo(x.DateActive);
                if (epoch == 0)
                {
                    var appName = x.Domain.CompareTo(y.Domain);
                    if (appName == 0)
                    {
                        return x.Hash.CompareTo(y.Hash);
                    }

                    return appName;
                }

                return epoch;
            }
        }
    }
}
