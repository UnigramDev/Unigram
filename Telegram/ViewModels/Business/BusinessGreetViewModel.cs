//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Business
{
    public partial class BusinessGreetViewModel : BusinessRecipientsViewModelBase, IHandle
    {
        public BusinessGreetViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (Invalidate(ref _isEnabled, value) && value)
                {
                    Replies ??= ClientService.GetQuickReplyShortcut("hello");
                }
            }
        }

        private QuickReplyShortcut _replies;
        public QuickReplyShortcut Replies
        {
            get => _replies;
            set => Invalidate(ref _replies, value);
        }

        private int _inactivityDays;
        public int InactivityDays
        {
            get => _inactivityDays;
            set => Invalidate(ref _inactivityDays, value);
        }

        protected override Task OnNavigatedToAsync(UserFullInfo cached, NavigationMode mode, NavigationState state)
        {
            var settings = cached?.BusinessInfo?.GreetingMessageSettings;
            if (settings != null)
            {
                _cached = settings;

                IsEnabled = true;
                Replies = ClientService.GetQuickReplyShortcut(settings.ShortcutId);

                InactivityDays = settings.InactivityDays / 7 - 1;

                UpdateRecipients(settings.Recipients);
            }
            else if (mode == NavigationMode.Back)
            {
                IsEnabled = true;
                Replies = ClientService.GetQuickReplyShortcut("hello");
            }

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateQuickReplyShortcut>(this, Handle);
        }

        private void Handle(UpdateQuickReplyShortcut update)
        {
            if (update.Shortcut.Name == "hello")
            {
                BeginOnUIThread(() => Replies = update.Shortcut);
            }
        }

        public void Create()
        {
            _completed = true;
            NavigationService.Navigate(typeof(ChatBusinessRepliesPage), new ChatBusinessRepliesIdNavigationArgs("hello"));
        }

        public override bool HasChanged => !_cached.AreTheSame(GetSettings());

        protected override async void ContinueImpl(NavigatingEventArgs args)
        {
            if (IsEnabled && Replies == null)
            {
                RaisePropertyChanged("REPLIES_MISSING");
                return;
            }

            var settings = GetSettings();
            if (settings.AreTheSame(_cached))
            {
                _completed = true;
                NavigationService.GoBack(args);
                return;
            }

            var response = await ClientService.SendAsync(new SetBusinessGreetingMessageSettings(settings));
            if (response is Ok)
            {
                _completed = true;
                NavigationService.GoBack(args);
            }
            else if (response is Error error)
            {
                ShowToast(error);
            }
        }

        private BusinessGreetingMessageSettings _cached;
        private BusinessGreetingMessageSettings GetSettings()
        {
            if (IsEnabled)
            {
                return new BusinessGreetingMessageSettings
                {
                    ShortcutId = Replies?.Id ?? 0,
                    InactivityDays = (InactivityDays + 1) * 7,
                    Recipients = GetRecipients()
                };
            }

            return null;
        }
    }
}
