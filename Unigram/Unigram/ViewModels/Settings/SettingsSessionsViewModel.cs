//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
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

namespace Unigram.ViewModels.Settings
{
    public class SettingsSessionsViewModel : TLViewModelBase
    {
        public SettingsSessionsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<KeyedList<SessionsGroup, Session>>();

            TerminateCommand = new RelayCommand<Session>(TerminateExecute);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            await UpdateSessionsAsync();
        }

        private int _sessionTtl;
        public int SessionTtl
        {
            get => Array.IndexOf(_sessionTtlIndexer, _sessionTtl);
            set
            {
                if (value >= 0 && value < _sessionTtlIndexer.Length && _sessionTtl != _sessionTtlIndexer[value])
                {
                    ClientService.SendAsync(new SetInactiveSessionTtl(_sessionTtl = _sessionTtlIndexer[value]));
                    RaisePropertyChanged();
                }
            }
        }

        private readonly int[] _sessionTtlIndexer = new[]
        {
            30,
            90,
            180,
            365
        };

        public List<SettingsOptionItem<int>> SessionTtlOptions { get; } = new()
        {
            new SettingsOptionItem<int>(7, Locale.Declension("Weeks", 1)),
            new SettingsOptionItem<int>(30, Locale.Declension("Months", 1)),
            new SettingsOptionItem<int>(90, Locale.Declension("Months", 3)),
            new SettingsOptionItem<int>(180, Locale.Declension("Months", 6)),
        };

        private async Task UpdateSessionsAsync()
        {
            var response = await ClientService.SendAsync(new GetActiveSessions());
            if (response is Sessions sessions)
            {
                int? period = null;

                var max = 2147483647;
                foreach (var days in _sessionTtlIndexer)
                {
                    int abs = Math.Abs(sessions.InactiveSessionTtlDays - days);
                    if (abs < max)
                    {
                        max = abs;
                        period = days;
                    }
                }

                _sessionTtl = period ?? _sessionTtlIndexer[2];
                RaisePropertyChanged(nameof(SessionTtl));

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

        public RelayCommand RenameCommand { get; }
        public async void Rename()
        {
            if (_current is not Session session)
            {
                return;
            }

            var popup = new InputPopup();
            popup.Title = Strings.Resources.lng_settings_rename_device_title;
            popup.PlaceholderText = Strings.Resources.lng_settings_device_name;
            popup.PrimaryButtonText = Strings.Resources.OK;
            popup.SecondaryButtonText = Strings.Resources.Cancel;
            popup.Text = session.DeviceModel;
            popup.MaxLength = 32;

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary && popup.Text != Settings.Diagnostics.DeviceName)
            {
                session.DeviceModel = popup.Text;
                RaisePropertyChanged(nameof(Current));

                Settings.Diagnostics.DeviceName = popup.Text;
                ClientService.Close(true);
            }
        }

        public async void TerminateOthers()
        {
            var terminate = await MessagePopup.ShowAsync(XamlRoot, Strings.Resources.AreYouSureSessions, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new TerminateAllOtherSessions());
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

        public RelayCommand<Session> TerminateCommand { get; }
        private async void TerminateExecute(Session session)
        {
            if (session == null)
            {
                return;
            }

            var dialog = new SettingsSessionPopup(session);

            var confirm = await dialog.ShowQueuedAsync(XamlRoot);
            if (confirm != ContentDialogResult.Primary)
            {
                if (session.CanAcceptCalls != dialog.CanAcceptCalls && confirm == ContentDialogResult.Secondary)
                {
                    session.CanAcceptCalls = dialog.CanAcceptCalls;
                    ClientService.Send(new ToggleSessionCanAcceptCalls(session.Id, dialog.CanAcceptCalls));
                }

                if (session.CanAcceptSecretChats != dialog.CanAcceptSecretChats && confirm == ContentDialogResult.Secondary)
                {
                    session.CanAcceptSecretChats = dialog.CanAcceptSecretChats;
                    ClientService.Send(new ToggleSessionCanAcceptSecretChats(session.Id, dialog.CanAcceptSecretChats));
                }

                return;
            }

            var terminate = await MessagePopup.ShowAsync(XamlRoot, Strings.Resources.TerminateSessionQuestion, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new TerminateSession(session.Id));
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
