using System;
using System.Collections.Generic;
using System.Linq;
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
    public class SettingsSessionsViewModel : TLViewModelBase
    {
        public SettingsSessionsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<KeyedList<SessionsGroup, Session>>();

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
                        var pending = new List<Session>();

                        foreach (var item in sessions.SessionsValue)
                        {
                            if (item.IsCurrent)
                            {
                                Current = item;
                            }
                            else if (item.IsPasswordPending)
                            {
                                pending.Add(item);
                            }
                            else
                            {
                                results.Add(item);
                            }
                        }

                        if (pending.Count > 0)
                        {
                            Items.ReplaceWith(new[]
                            {
                                new KeyedList<SessionsGroup, Session>(new SessionsGroup { Title = Strings.Resources.LoginAttempts }, pending.OrderByDescending(x => x.LastActiveDate)),
                                new KeyedList<SessionsGroup, Session>(new SessionsGroup { Title = Strings.Resources.OtherSessions, Footer = Strings.Resources.LoginAttemptsInfo }, results.OrderByDescending(x => x.LastActiveDate))
                            });
                        }
                        else if (results.Count > 0)
                        {
                            Items.ReplaceWith(new[]
                            {
                                new KeyedList<SessionsGroup, Session>(new SessionsGroup { Title = Strings.Resources.OtherSessions }, results.OrderByDescending(x => x.LastActiveDate))
                            });
                        }
                        else
                        {
                            Items.Clear();
                        }
                    });
                }
            });
        }

        public MvxObservableCollection<KeyedList<SessionsGroup, Session>> Items { get; private set; }

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
            var terminate = await MessagePopup.ShowAsync(Strings.Resources.TerminateSessionQuestion, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ProtoService.SendAsync(new TerminateSession(session.Id));
                if (response is Ok)
                {
                    foreach (var group in Items)
                    {
                        group.Remove(session);
                    }
                }
                else if (response is Error error)
                {
                    Logs.Logger.Error(Logs.Target.API, "auth.resetAuthotization error " + error);
                }
            }
        }

        public RelayCommand TerminateOthersCommand { get; }
        private async void TerminateOtherExecute()
        {
            var terminate = await MessagePopup.ShowAsync(Strings.Resources.AreYouSureSessions, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ProtoService.SendAsync(new TerminateAllOtherSessions());
                if (response is Ok)
                {
                    Items.Clear();
                }
                else if (response is Error error)
                {
                    Logs.Logger.Error(Logs.Target.API, "auth.resetAuthotizations error " + error);
                }
            }
        }
    }

    public class SessionsGroup
    {
        public string Title { get; set; }
        public string Footer { get; set; }
    }
}
