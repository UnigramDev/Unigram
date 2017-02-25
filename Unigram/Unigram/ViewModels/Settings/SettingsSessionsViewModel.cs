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
    public class SettingsSessionsViewModel : UnigramViewModelBase, IHandle<TLUpdateServiceNotification>, IHandle
    {
        private Dictionary<long, TLAuthorization> _cachedItems;

        public SettingsSessionsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            _cachedItems = new Dictionary<long, TLAuthorization>();
            Items = new SortedObservableCollection<TLAuthorization>(new TLAuthorizationComparer());
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
            Execute.BeginOnUIThread(async () =>
            {
                await UpdateSessionsAsync();
            });
        }

        private async Task UpdateSessionsAsync()
        {
            var response = await ProtoService.GetAuthorizationsAsync();
            if (response.IsSucceeded)
            {
                foreach (var item in response.Result.Authorizations)
                {
                    if (_cachedItems.ContainsKey(item.Hash))
                    {
                        if (item.IsCurrent)
                        {
                            var cached = _cachedItems[item.Hash];
                            cached.Update(item);
                            cached.RaisePropertyChanged(() => cached.AppName);
                            cached.RaisePropertyChanged(() => cached.AppVersion);
                            cached.RaisePropertyChanged(() => cached.DeviceModel);
                            cached.RaisePropertyChanged(() => cached.Platform);
                            cached.RaisePropertyChanged(() => cached.SystemVersion);
                            cached.RaisePropertyChanged(() => cached.Ip);
                            cached.RaisePropertyChanged(() => cached.Country);
                            cached.RaisePropertyChanged(() => cached.DateActive);
                        }
                        else
                        {
                            Items.Remove(_cachedItems[item.Hash]);
                            Items.Add(item);

                            _cachedItems[item.Hash] = item;
                        }
                    }
                    else
                    {
                        _cachedItems[item.Hash] = item;
                        if (item.IsCurrent)
                        {
                            Current = item;
                        }
                        else
                        {
                            Items.Add(item);
                        }
                    }
                }
            }
        }

        public ObservableCollection<TLAuthorization> Items { get; private set; }

        private TLAuthorization _current;
        public TLAuthorization Current
        {
            get
            {
                return _current;
            }
            set
            {
                Set(ref _current, value);
            }
        }

        public RelayCommand<TLAuthorization> TerminateCommand => new RelayCommand<TLAuthorization>(TerminateExecute);
        private async void TerminateExecute(TLAuthorization session)
        {
            var terminate = await TLMessageDialog.ShowAsync("Terminate this session?", "Telegram", "Yes", "No");
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ProtoService.ResetAuthorizationAsync(session.Hash);
                if (response.IsSucceeded)
                {
                    Items.Remove(session);
                }
                else
                {
                    Execute.ShowDebugMessage("auth.resetAuthotization error " + response.Error);
                }
            }
        }

        public RelayCommand TerminateOthersCommand => new RelayCommand(TerminateOtherExecute);
        private async void TerminateOtherExecute()
        {
            var terminate = await TLMessageDialog.ShowAsync("Are you sure you want to terminate all other sessions?", "Telegram", "Yes", "No");
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ProtoService.ResetAuthorizationsAsync();
                if (response.IsSucceeded)
                {
                    Items.Clear();
                }
                else
                {
                    Execute.ShowDebugMessage("auth.resetAuthotization error " + response.Error);
                }
            }
        }
    }

    public class TLAuthorizationComparer : IComparer<TLAuthorization>
    {
        public int Compare(TLAuthorization x, TLAuthorization y)
        {
            var epoch = y.DateActive.CompareTo(x.DateActive);
            if (epoch == 0)
            {
                var appName = x.AppName.CompareTo(y.AppName);
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
