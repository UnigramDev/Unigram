using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Entities;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views;
using Unigram.Views.Settings;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using static Unigram.Services.GenerationService;

namespace Unigram.ViewModels
{
    public class SettingsViewModel : TLViewModelBase,
         IDelegable<ISettingsDelegate>,
         IHandle<UpdateUser>,
         IHandle<UpdateUserFullInfo>,
         IHandle<UpdateFile>
    {
        private readonly ISettingsSearchService _searchService;

        public ISettingsDelegate Delegate { get; set; }

        public SettingsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ISettingsSearchService searchService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _searchService = searchService;

            AskCommand = new RelayCommand(AskExecute);
            EditPhotoCommand = new RelayCommand<StorageMedia>(EditPhotoExecute);
            NavigateCommand = new RelayCommand<SettingsSearchEntry>(NavigateExecute);

            Results = new MvxObservableCollection<SettingsSearchEntry>();
        }

        private Chat _chat;
        public Chat Chat
        {
            get { return _chat; }
            set { Set(ref _chat, value); }
        }

        public MvxObservableCollection<SettingsSearchEntry> Results { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var response = await ProtoService.SendAsync(new CreatePrivateChat(CacheService.Options.MyId, false));
            if (response is Chat chat)
            {
                Chat = chat;

                Aggregator.Subscribe(this);
                Delegate?.UpdateChat(chat);

                if (chat.Type is ChatTypePrivate privata)
                {
                    var item = ProtoService.GetUser(privata.UserId);
                    if (item == null)
                    {
                        return;
                    }

                    Delegate?.UpdateUser(chat, item, false);

                    var cache = ProtoService.GetUserFull(privata.UserId);
                    if (cache == null)
                    {
                        ProtoService.Send(new GetUserFullInfo(privata.UserId));
                    }
                    else
                    {
                        Delegate?.UpdateUserFullInfo(chat, item, cache, false, false);
                    }
                }
            }
        }

        public void Handle(UpdateUser update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata && privata.UserId == update.User.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateUser(chat, update.User, false));
            }
            else if (chat.Type is ChatTypeSecret secret && secret.UserId == update.User.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateUser(chat, update.User, true));
            }
        }

        public void Handle(UpdateUserFullInfo update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata && privata.UserId == update.UserId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(chat, ProtoService.GetUser(update.UserId), update.UserFullInfo, false, false));
            }
        }

        public void Handle(UpdateFile update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var user = CacheService.GetUser(chat);
            if (user == null)
            {
                return;
            }

            if (user.UpdateFile(update.File))
            {
                BeginOnUIThread(() => Delegate?.UpdateFile(update.File));
            }
        }



        public RelayCommand<StorageMedia> EditPhotoCommand { get; }
        private async void EditPhotoExecute(StorageMedia media)
        {
            if (media is StorageVideo)
            {
                var props = await media.File.Properties.GetVideoPropertiesAsync();

                var duration = media.EditState.TrimStopTime - media.EditState.TrimStartTime;
                var seconds = duration.TotalSeconds;

                var conversion = new VideoConversion();
                conversion.Mute = true;
                conversion.TrimStartTime = media.EditState.TrimStartTime;
                conversion.TrimStopTime = media.EditState.TrimStartTime + TimeSpan.FromSeconds(Math.Min(seconds, 9.9));
                conversion.Transcode = true;
                conversion.Transform = true;
                //conversion.Rotation = file.EditState.Rotation;
                conversion.OutputSize = new Size(640, 640);
                //conversion.Mirror = transform.Mirror;
                conversion.CropRectangle = new Rect(
                    media.EditState.Rectangle.X * props.Width,
                    media.EditState.Rectangle.Y * props.Height,
                    media.EditState.Rectangle.Width * props.Width,
                    media.EditState.Rectangle.Height * props.Height);

                var rectangle = conversion.CropRectangle;
                rectangle.Width = Math.Min(conversion.CropRectangle.Width, conversion.CropRectangle.Height);
                rectangle.Height = rectangle.Width;

                conversion.CropRectangle = rectangle;

                var generated = await media.File.ToGeneratedAsync(ConversionType.Transcode, JsonConvert.SerializeObject(conversion));
                var response = await ProtoService.SendAsync(new SetProfilePhoto(new InputChatPhotoAnimation(generated, 0)));
            }
            else
            {
                var generated = await media.File.ToGeneratedAsync(ConversionType.Compress, JsonConvert.SerializeObject(media.EditState));
                var response = await ProtoService.SendAsync(new SetProfilePhoto(new InputChatPhotoStatic(generated)));
            }
        }

        public RelayCommand AskCommand { get; }
        private async void AskExecute()
        {
            var text = Regex.Replace(Strings.Resources.AskAQuestionInfo, "<!\\[CDATA\\[(.*?)\\]\\]>", "$1");

            var confirm = await MessagePopup.ShowAsync(text, Strings.Resources.AskAQuestion, Strings.Resources.AskButton, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ProtoService.SendAsync(new GetSupportUser());
                if (response is User user)
                {
                    response = await ProtoService.SendAsync(new CreatePrivateChat(user.Id, false));
                    if (response is Chat chat)
                    {
                        NavigationService.NavigateToChat(chat);
                    }
                }
            }
        }

        public void Search(string query)
        {
            Results.ReplaceWith(_searchService.Search(query));
        }

        public RelayCommand<SettingsSearchEntry> NavigateCommand { get; }
        private void NavigateExecute(SettingsSearchEntry entry)
        {
            if (entry is SettingsSearchPage page && page.Page != null)
            {
                if (page.Page == typeof(SettingsPasscodePage))
                {
                    NavigationService.NavigateToPasscode();
                }
                else if (page.Page == typeof(InstantPage))
                {
                    NavigationService.NavigateToInstant(Strings.Resources.TelegramFaqUrl);
                }
                //else if (page.Page == typeof(WalletPage))
                //{
                //    NavigationService.NavigateToWallet();
                //}
                else
                {
                    NavigationService.Navigate(page.Page);
                }
            }
            else if (entry is SettingsSearchFaq faq)
            {
                NavigationService.NavigateToInstant(faq.Url);
            }
            else if (entry is SettingsSearchAction action)
            {
                action.Action();
            }
        }
    }
}
