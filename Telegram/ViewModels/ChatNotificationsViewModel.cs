//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public class ChatNotificationsViewModel : ChooseSoundViewModel
    {
        public ChatNotificationsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        private long _chatId;

        private string _title;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private bool _alwaysAlert;
        public bool AlwaysAlert
        {
            get => _alwaysAlert;
            set => Set(ref _alwaysAlert, value);
        }

        private bool? _alwaysPreview;
        public bool? AlwaysPreview
        {
            get => _alwaysPreview;
            set => Set(ref _alwaysPreview, value);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            await base.OnNavigatedToAsync(parameter, mode, state);

            if (parameter is long chatId && ClientService.TryGetChat(chatId, out Chat chat))
            {
                _chatId = chat.Id;
                Title = ClientService.GetTitle(chat);

                AlwaysAlert = Settings.Notifications.GetMutedFor(chat) == 0;

                if (chat.NotificationSettings.UseDefaultShowPreview)
                {
                    AlwaysPreview = null;
                }
                else
                {
                    AlwaysPreview = chat.NotificationSettings.ShowPreview;
                }

                if (chat.NotificationSettings.UseDefaultSound)
                {

                }
                else
                {
                    var sound = Items.FirstOrDefault(x => x.Id == chat.NotificationSettings.SoundId);
                    if (sound != null)
                    {
                        sound.IsSelected = true;
                    }
                }
            }
        }

        public void Confirm(long soundId)
        {
            var defaultPreview = true;
            var preview = true;

            if (AlwaysPreview is bool alwaysPreview)
            {
                defaultPreview = false;
                preview = alwaysPreview;
            }

            ClientService.Send(new SetChatNotificationSettings(_chatId, new ChatNotificationSettings
            {
                UseDefaultMuteFor = false,
                MuteFor = AlwaysAlert ? 0 : int.MaxValue,
                UseDefaultShowPreview = defaultPreview,
                ShowPreview = preview,
                UseDefaultSound = false,
                SoundId = soundId,
                UseDefaultDisableMentionNotifications = true,
                UseDefaultDisablePinnedMessageNotifications = true,
            }));
        }
    }
}
