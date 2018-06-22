using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Telegram.Td.Api;

namespace Unigram.ViewModels.Settings
{
    public class SettingsSessionsViewModel : TLViewModelBase
    {
        public SettingsSessionsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator) 
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new SortedObservableCollection<Session>(new SessionComparer());

            TerminateCommand = new RelayCommand<Session>(TerminateExecute);
            TerminateOthersCommand = new RelayCommand(TerminateOtherExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await UpdateSessionsAsync();
        }

        //public void Handle(TLUpdateServiceNotification update)
        //{
        //    BeginOnUIThread(async () =>
        //    {
        //        await UpdateSessionsAsync();
        //    });
        //}

        private async Task UpdateSessionsAsync()
        {
            ProtoService.Send(new GetActiveSessions(), result =>
            {
                if (result is Sessions sessions)
                {
                    BeginOnUIThread(() =>
                    {
                        var results = new List<Session>();
                        foreach (var item in sessions.SessionsValue)
                        {
                            if (item.IsCurrent)
                            {
                                Current = item;
                            }
                            else
                            {
                                results.Add(item);
                            }
                        }

                        Items.AddRange(results);
                    });
                }
            });

            //var response = await LegacyService.GetAuthorizationsAsync();
            //if (response.IsSucceeded)
            //{
            //    foreach (var item in response.Result.Authorizations)
            //    {
            //        if (_cachedItems.ContainsKey(item.Hash))
            //        {
            //            if (item.IsCurrent)
            //            {
            //                var cached = _cachedItems[item.Hash];
            //                cached.Update(item);
            //                cached.RaisePropertyChanged(() => cached.AppName);
            //                cached.RaisePropertyChanged(() => cached.AppVersion);
            //                cached.RaisePropertyChanged(() => cached.DeviceModel);
            //                cached.RaisePropertyChanged(() => cached.Platform);
            //                cached.RaisePropertyChanged(() => cached.SystemVersion);
            //                cached.RaisePropertyChanged(() => cached.Ip);
            //                cached.RaisePropertyChanged(() => cached.Country);
            //                cached.RaisePropertyChanged(() => cached.DateActive);
            //            }
            //            else
            //            {
            //                Items.Remove(_cachedItems[item.Hash]);
            //                Items.Add(item);

            //                _cachedItems[item.Hash] = item;
            //            }
            //        }
            //        else
            //        {
            //            _cachedItems[item.Hash] = item;
            //            if (item.IsCurrent)
            //            {
            //                Current = item;
            //            }
            //            else
            //            {
            //                Items.Add(item);
            //            }
            //        }
            //    }
            //}
        }

        public ObservableCollection<Session> Items { get; private set; }

        private Session _current;
        public Session Current
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

        public RelayCommand<Session> TerminateCommand { get; }
        private async void TerminateExecute(Session session)
        {
            var terminate = await TLMessageDialog.ShowAsync(Strings.Resources.TerminateSessionQuestion, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ProtoService.SendAsync(new TerminateSession(session.Id));
                if (response is Ok)
                {
                    Items.Remove(session);
                }
                else if (response is Error error)
                {
                    Execute.ShowDebugMessage("auth.resetAuthotization error " + error);
                }
            }
        }

        public RelayCommand TerminateOthersCommand { get; }
        private async void TerminateOtherExecute()
        {
            var terminate = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureSessions, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ProtoService.SendAsync(new TerminateAllOtherSessions());
                if (response is Ok)
                {
                    Items.Clear();
                }
                else if (response is Error error)
                {
                    Execute.ShowDebugMessage("auth.resetAuthotizations error " + error);
                }
            }
        }

        public class SessionComparer : IComparer<Session>
        {
            public int Compare(Session x, Session y)
            {
                var epoch = y.LastActiveDate.CompareTo(x.LastActiveDate);
                if (epoch == 0)
                {
                    var appName = x.ApplicationName.CompareTo(y.ApplicationName);
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
