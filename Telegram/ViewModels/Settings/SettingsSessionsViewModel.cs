//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Settings.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using WinRT;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsSessionsViewModel : ViewModelBase
    {
        public SettingsSessionsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<KeyedList<KeyedGroup, Session>>();
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
            7,
            30,
            90,
            180,
            365
        };

        public List<SettingsOptionItem<int>> SessionTtlOptions { get; } = new()
        {
            new SettingsOptionItem<int>(7, Locale.Declension(Strings.R.Weeks, 1)),
            new SettingsOptionItem<int>(30, Locale.Declension(Strings.R.Months, 1)),
            new SettingsOptionItem<int>(90, Locale.Declension(Strings.R.Months, 3)),
            new SettingsOptionItem<int>(180, Locale.Declension(Strings.R.Months, 6)),
            new SettingsOptionItem<int>(365, Locale.Declension(Strings.R.Years, 1)),
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
                        new KeyedList<KeyedGroup, Session>(new KeyedGroup { Title = Strings.LoginAttempts }, pending),
                        new KeyedList<KeyedGroup, Session>(new KeyedGroup { Title = Strings.OtherSessions, Footer = Strings.LoginAttemptsInfo }, results)
                    });
                }
                else if (results.Count > 0)
                {
                    Items.ReplaceWith(new[]
                    {
                        new KeyedList<KeyedGroup, Session>(new KeyedGroup { Title = Strings.OtherSessions }, results)
                    });
                }
                else
                {
                    Items.Clear();
                }
            }
        }

        public MvxObservableCollection<KeyedList<KeyedGroup, Session>> Items { get; private set; }

        private Session _current;
        public Session Current
        {
            get => _current;
            set => Set(ref _current, value);
        }

        public async void Rename()
        {
            if (_current is not Session session)
            {
                return;
            }

            var popup = new InputPopup();
            popup.Title = Strings.RenameCurrentDevice;
            popup.PlaceholderText = Strings.DeviceName;
            popup.PrimaryButtonText = Strings.OK;
            popup.SecondaryButtonText = Strings.Cancel;
            popup.Text = session.DeviceModel;
            popup.MaxLength = 32;

            var confirm = await ShowPopupAsync(popup);
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
            var terminate = await ShowPopupAsync(Strings.AreYouSureSessions, Strings.AppName, Strings.OK, Strings.Cancel);
            if (terminate == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new TerminateAllOtherSessions());
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

        public void TerminateCurrent()
        {
            Terminate(_current);
        }

        public async void Terminate(Session session)
        {
            if (session == null)
            {
                return;
            }

            var dialog = new SettingsSessionPopup(session);

            var confirm = await ShowPopupAsync(dialog);
            if (confirm != ContentDialogResult.Primary)
            {
                if (session.CanAcceptCalls != dialog.CanAcceptCalls)
                {
                    session.CanAcceptCalls = dialog.CanAcceptCalls;
                    ClientService.Send(new ToggleSessionCanAcceptCalls(session.Id, dialog.CanAcceptCalls));
                }

                if (session.CanAcceptSecretChats != dialog.CanAcceptSecretChats)
                {
                    session.CanAcceptSecretChats = dialog.CanAcceptSecretChats;
                    ClientService.Send(new ToggleSessionCanAcceptSecretChats(session.Id, dialog.CanAcceptSecretChats));
                }

                return;
            }

            var terminate = await ShowPopupAsync(Strings.TerminateSessionQuestion, Strings.AppName, Strings.OK, Strings.Cancel);
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
                    Logger.Error(error.Message);
                }
            }
        }
    }

    [GeneratedBindableCustomProperty]
    public partial class KeyedGroup
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
