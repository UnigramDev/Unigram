using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public struct CreateChatPhotoParameters
    {
        public long? ChatId { get; }

        public bool IsPublic { get; }

        public CreateChatPhotoParameters(long? chatId, bool isPublic)
        {
            ChatId = chatId;
            IsPublic = isPublic;
        }
    }

    public class CreateChatPhotoViewModel : TLViewModelBase
    {
        private long? _chatId;
        private bool _isPublic;

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
                ClientService.Send(new SetChatPhoto(chatId, inputPhoto));
            }
            else
            {
                ClientService.Send(new SetProfilePhoto(inputPhoto, _isPublic));
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
