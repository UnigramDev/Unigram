//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public readonly struct CreateChatPhotoParameters
    {
        public long? ChatId { get; }

        public bool IsPublic { get; }

        public bool IsPersonal { get; }

        public CreateChatPhotoParameters(long? chatId, bool isPublic, bool isPersonal)
        {
            ChatId = chatId;
            IsPublic = isPublic;
            IsPersonal = isPersonal;
        }
    }

    public class CreateChatPhotoViewModel : ViewModelBase
    {
        private long? _chatId;
        private bool _isPublic;
        private bool _isPersonal;

        public TaskCompletionSource<object> Completion { get; set; }

        public CreateChatPhotoViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new ObservableCollection<BackgroundFill>
            {
                new BackgroundFillFreeformGradient(new[] { 0x5A7FFF, 0x2CA0F2, 0x4DFF89, 0x6BFCEB }),
                new BackgroundFillFreeformGradient(new[] { 0xFF011D, 0xFF530D, 0xFE64DC, 0xFFDC61 }),
                new BackgroundFillFreeformGradient(new[] { 0xFE64DC, 0xFF6847, 0xFFDD02, 0xFFAE10 }),
                new BackgroundFillFreeformGradient(new[] { 0x84EC00, 0x00B7C2, 0x00C217, 0xFFE600 }),
                new BackgroundFillFreeformGradient(new[] { 0x86B0FF, 0x35FFCF, 0x69FFFF, 0x76DEFF }),
                new BackgroundFillFreeformGradient(new[] { 0xFAE100, 0xFF54EE, 0xFC2B78, 0xFF52D9 }),
                new BackgroundFillFreeformGradient(new[] { 0x73A4FF, 0x5F55FF, 0xFF49F8, 0xEC76FF }),
                new BackgroundFillFreeformGradient(new[] { 0x73A4FF, 0x5F55FF, 0xFF49F8, 0xEC76FF }),
            };

            SelectedBackground = Items[0];
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is CreateChatPhotoParameters parameters)
            {
                _chatId = parameters.ChatId;
                _isPublic = parameters.IsPublic;
                _isPersonal = parameters.IsPersonal;
            }

            var foreground = await ClientService.SendAsync(new GetAnimatedEmoji("\U0001F916")) as AnimatedEmoji;
            if (foreground != null)
            {
                SelectedForeground = foreground.Sticker;
            }
        }

        private Sticker _selectedForeground;
        public Sticker SelectedForeground
        {
            get => _selectedForeground;
            set => Set(ref _selectedForeground, value);
        }

        private BackgroundFill _selectedBackground;
        public BackgroundFill SelectedBackground
        {
            get => _selectedBackground;
            set => Set(ref _selectedBackground, value);
        }

        public ObservableCollection<BackgroundFill> Items { get; private set; }

        public async void Send()
        {
            if (SelectedForeground is not Sticker foreground || SelectedBackground is not BackgroundFill background)
            {
                Completion?.SetResult(false);
                return;
            }

            ChatPhotoStickerType stickerType = foreground.FullType is StickerFullTypeCustomEmoji customEmoji
                ? new ChatPhotoStickerTypeCustomEmoji(customEmoji.CustomEmojiId)
                : new ChatPhotoStickerTypeRegularOrMask(foreground.SetId, foreground.Id);
            InputChatPhoto inputPhoto = new InputChatPhotoSticker(new ChatPhotoSticker(stickerType, background));

            if (_chatId is long chatId)
            {
                if (ClientService.TryGetUser(chatId, out User user))
                {
                    if (user.Type is UserTypeBot userTypeBot && userTypeBot.CanBeEdited)
                    {
                        ClientService.Send(new SetBotProfilePhoto(user.Id, inputPhoto));
                    }
                    else if (_isPersonal)
                    {
                        var confirm = await ShowPopupAsync(string.Format(Strings.SetUserPhotoAlertMessage, user.FirstName, user.FirstName), Strings.AppName, Strings.SuggestPhotoShort, Strings.Cancel);
                        if (confirm == ContentDialogResult.Primary)
                        {
                            ClientService.Send(new SetUserPersonalProfilePhoto(user.Id, inputPhoto));
                            Completion?.SetResult(true);
                        }
                        else
                        {
                            Completion?.SetResult(false);
                        }
                    }
                    else
                    {
                        var confirm = await ShowPopupAsync(string.Format(Strings.SuggestPhotoAlertMessage, user.FirstName), Strings.AppName, Strings.SuggestPhotoShort, Strings.Cancel);
                        if (confirm == ContentDialogResult.Primary)
                        {
                            ClientService.Send(new SuggestUserProfilePhoto(user.Id, inputPhoto));
                            Completion?.SetResult(true);
                        }
                        else
                        {
                            Completion?.SetResult(false);
                        }
                    }
                }
                else
                {
                    ClientService.Send(new SetChatPhoto(chatId, inputPhoto));
                    Completion?.SetResult(true);
                }
            }
            else
            {
                ClientService.Send(new SetProfilePhoto(inputPhoto, _isPublic));
                Completion?.SetResult(true);
            }
        }
    }

    public class BackgroundDiffHandler : IDiffHandler<Background>
    {
        public bool CompareItems(Background oldItem, Background newItem)
        {
            return oldItem.Id == newItem.Id;
        }

        public void UpdateItem(Background oldItem, Background newItem)
        {

        }
    }
}
