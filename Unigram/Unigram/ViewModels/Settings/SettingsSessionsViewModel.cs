using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Views.Popups;
using Unigram.Views.Settings.Popups;
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
            
            SetInactiveSessionTtlCommand = new RelayCommand(SetInactiveSessionTtlExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
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

        private int _inactiveSessionTtlDays;
        public int InactiveSessionTtlDays
        {
            get => _inactiveSessionTtlDays;
            set => Set(ref _inactiveSessionTtlDays, value);
        }

        private async Task UpdateSessionsAsync()
        {
            var response = await ProtoService.SendAsync(new GetActiveSessions());
            if (response is Sessions sessions)
            {
                InactiveSessionTtlDays = sessions.InactiveSessionTtlDays;

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
            }
        }

        public MvxObservableCollection<KeyedList<SessionsGroup, Session>> Items { get; private set; }

        private Session _current;
        public Session Current
        {
            get => _current;
            set => Set(ref _current, value);
        }

        public RelayCommand<Session> TerminateCommand { get; }
        private async void TerminateExecute(Session session)
        {
            var dialog = new SettingsSessionPopup(session);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                if (session.CanAcceptCalls != dialog.CanAcceptCalls && confirm == ContentDialogResult.Secondary)
                {
                    session.CanAcceptCalls = dialog.CanAcceptCalls;
                    ProtoService.Send(new ToggleSessionCanAcceptCalls(session.Id, dialog.CanAcceptCalls));
                }

                if (session.CanAcceptSecretChats != dialog.CanAcceptSecretChats && confirm == ContentDialogResult.Secondary)
                {
                    session.CanAcceptSecretChats = dialog.CanAcceptSecretChats;
                    ProtoService.Send(new ToggleSessionCanAcceptSecretChats(session.Id, dialog.CanAcceptSecretChats));
                }

                return;
            }

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
                    Logs.Logger.Error(Logs.LogTarget.API, "auth.resetAuthotization error " + error);
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
                    Logs.Logger.Error(Logs.LogTarget.API, "auth.resetAuthotizations error " + error);
                }
            }
        }

        public RelayCommand SetInactiveSessionTtlCommand { get; }
        private async void SetInactiveSessionTtlExecute()
        {
            SelectRadioItem GetSelectedPeriod(SelectRadioItem[] periods, SelectRadioItem defaultPeriod)
            {
                if (_inactiveSessionTtlDays == 0)
                {
                    return defaultPeriod;
                }

                SelectRadioItem period = null;

                var max = 2147483647;
                foreach (var current in periods)
                {
                    var days = (int)current.Value;
                    int abs = Math.Abs(_inactiveSessionTtlDays - days);
                    if (abs < max)
                    {
                        max = abs;
                        period = current;
                    }
                }

                return period ?? defaultPeriod;
            };

            var items = new[]
            {
                new SelectRadioItem(7, Locale.Declension("Weeks", 1), false),
                new SelectRadioItem(30, Locale.Declension("Months", 1), false),
                new SelectRadioItem(90, Locale.Declension("Months", 3), false),
                new SelectRadioItem(180, Locale.Declension("Months", 6), false)
            };

            var selected = GetSelectedPeriod(items, items[2]);
            if (selected != null)
            {
                selected.IsChecked = true;
            }

            var dialog = new ChooseRadioPopup(items);
            dialog.Title = Strings.Resources.SessionsSelfDestruct;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && dialog.SelectedIndex is int days)
            {
                var response = await ProtoService.SendAsync(new SetInactiveSessionTtl(days));
                if (response is Ok)
                {
                    InactiveSessionTtlDays = days;
                }
            }
        }
    }

    public class SessionsGroup
    {
        public string Title { get; set; }
        public string Footer { get; set; }

        public override string ToString()
        {
            if (Footer != null)
            {
                return Footer + ", " + Title;
            }

            return Title;
        }
    }
}
