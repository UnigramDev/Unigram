using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Services;
using Unigram.Views;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Telegram.Td.Api;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Metadata;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Settings;
using Unigram.Collections;
using System.Text.RegularExpressions;
using Unigram.Views.Settings.Privacy;
using System.Diagnostics;
using Unigram.Views.Wallet;

namespace Unigram.ViewModels
{
    public class SettingsViewModel : TLViewModelBase,
         IDelegable<ISettingsDelegate>,
         IHandle<UpdateUser>,
         IHandle<UpdateUserFullInfo>,
         IHandle<UpdateFile>
    {
        private readonly INotificationsService _pushService;
        private readonly IContactsService _contactsService;
        private readonly ISettingsSearchService _searchService;

        public ISettingsDelegate Delegate { get; set; }

        public SettingsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, INotificationsService pushService, IContactsService contactsService, ISettingsSearchService searchService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _pushService = pushService;
            _contactsService = contactsService;
            _searchService = searchService;

            AskCommand = new RelayCommand(AskExecute);
            EditPhotoCommand = new RelayCommand<StorageFile>(EditPhotoExecute);
            NavigateCommand = new RelayCommand<SettingsSearchEntry>(NavigateExecute);

            Results = new MvxObservableCollection<SettingsSearchEntry>();
        }

        private Chat _chat;
        public Chat Chat
        {
            get { return _chat; }
            set { Set(ref _chat, value); }
        }

        private bool _hasPassportData;
        public bool HasPassportData
        {
            get { return _hasPassportData; }
            set { Set(ref _hasPassportData, value); }
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
                    var cache = ProtoService.GetUserFull(privata.UserId);

                    Delegate?.UpdateUser(chat, item, false);

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

            var passport = await ProtoService.SendAsync(new GetPasswordState());
            if (passport is PasswordState passwordState)
            {
                HasPassportData = passwordState.HasPassportData;
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



        public RelayCommand<StorageFile> EditPhotoCommand { get; }
        private async void EditPhotoExecute(StorageFile file)
        {
            var props = await file.GetBasicPropertiesAsync();
            var response = await ProtoService.SendAsync(new SetProfilePhoto(await file.ToGeneratedAsync()));
        }

        public RelayCommand AskCommand { get; }
        private async void AskExecute()
        {
            var text = Regex.Replace(Strings.Resources.AskAQuestionInfo, "<!\\[CDATA\\[(.*?)\\]\\]>", "$1");

            var confirm = await TLMessageDialog.ShowAsync(text, Strings.Resources.AskAQuestion, Strings.Resources.AskButton, Strings.Resources.Cancel);
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
                    NavigationService.Navigate(typeof(InstantPage), Strings.Resources.TelegramFaqUrl);
                }
                else if (page.Page == typeof(WalletPage))
                {
                    NavigationService.NavigateToWallet();
                }
                else
                {
                    NavigationService.Navigate(page.Page);
                }
            }
            else if (entry is SettingsSearchFaq faq)
            {
                NavigationService.Navigate(typeof(InstantPage), faq.Url);
            }
            else if (entry is SettingsSearchAction action)
            {
                action.Action();
            }
        }
    }
}
