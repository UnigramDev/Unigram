//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rg.DiffUtils;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;

namespace Unigram.ViewModels
{
    public struct CreateChatPhotoParameters
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

    public class CreateChatPhotoViewModel : TLViewModelBase
    {
        private long? _chatId;
        private bool _isPublic;
        private bool _isPersonal;

        public TaskCompletionSource<object> Completion { get; set; }

        public CreateChatPhotoViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new DiffObservableCollection<Background>(new BackgroundDiffHandler(), Constants.DiffOptions);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is CreateChatPhotoParameters parameters)
            {
                _chatId = parameters.ChatId;
                _isPublic = parameters.IsPublic;
                _isPersonal = parameters.IsPersonal;
            }

            var dark = Settings.Appearance.IsDarkTheme();
            var freeform = dark ? new[] { 0x1B2836, 0x121A22, 0x1B2836, 0x121A22 } : new[] { 0xDBDDBB, 0x6BA587, 0xD5D88D, 0x88B884 };

            var predefined = new Background(Constants.WallpaperColorId, true, dark, string.Empty,
                new Document(string.Empty, "application/x-tgwallpattern", null, null, TdExtensions.GetLocalFile("Assets\\Background.tgv", "Background")),
                new BackgroundTypePattern(new BackgroundFillFreeformGradient(freeform), dark ? 100 : 50, dark, false));

            var items = new List<Background>
            {
                predefined
            };

            SelectedBackground = predefined;
            Items.ReplaceDiff(items);

            var response = await ClientService.SendAsync(new GetBackgrounds(dark));
            if (response is Backgrounds wallpapers)
            {
                items.AddRange(wallpapers.BackgroundsValue.Where(x => x.Type is BackgroundTypePattern));
                Items.ReplaceDiff(items);
            }

            var foreground = await ClientService.SendAsync(new GetAnimatedEmoji("\U0001F916")) as AnimatedEmoji;
            if (foreground != null)
            {
                SelectedForeground = foreground.Sticker;
            }
        }

        private float _scale = 0.75f;
        public float Scale
        {
            get => _scale;
            set => Set(ref _scale, value);
        }

        private Sticker _selectedForeground;
        public Sticker SelectedForeground
        {
            get => _selectedForeground;
            set => Set(ref _selectedForeground, value);
        }

        private Background _selectedBackground;
        public Background SelectedBackground
        {
            get => _selectedBackground;
            set => Set(ref _selectedBackground, value);
        }

        public DiffObservableCollection<Background> Items { get; private set; }

        public async void Send()
        {
            if (SelectedForeground is not Sticker foreground || SelectedBackground is not Background background)
            {
                Completion?.SetResult(false);
                return;
            }

            var url = await ClientService.SendAsync(new GetBackgroundUrl(background.Name, background.Type)) as HttpUrl;
            var fileName = foreground.Format is StickerFormatWebp ? "static.jpg" : "animation.mp4";

            var arguments = new GenerationService.ChatPhotoConversion
            {
                StickerFileId = foreground.StickerValue.Id,
                StickerFileType = foreground.Format switch
                {
                    StickerFormatWebp => 0,
                    StickerFormatTgs => 1,
                    StickerFormatWebm => 2,
                    _ => -1
                },
                BackgroundUrl = url.Url,
                Scale = Scale
            };

            InputFile inputFile = new InputFileGenerated(fileName, "token" + "#" + ConversionType.ChatPhoto + "#" + Newtonsoft.Json.JsonConvert.SerializeObject(arguments) + "#" + DateTime.Now.ToString("s"), 0);
            InputChatPhoto inputPhoto = foreground.Format is StickerFormatWebp
                ? new InputChatPhotoStatic(inputFile)
                : new InputChatPhotoAnimation(inputFile, 0);

            if (_chatId is long chatId)
            {
                if (ClientService.TryGetUser(chatId, out User user))
                {
                    if (_isPersonal)
                    {
                        var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.SetUserPhotoAlertMessage, user.FirstName), Strings.Resources.AppName, Strings.Resources.SuggestPhotoShort, Strings.Resources.Cancel);
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
                        var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.SuggestPhotoAlertMessage, user.FirstName), Strings.Resources.AppName, Strings.Resources.SuggestPhotoShort, Strings.Resources.Cancel);
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
