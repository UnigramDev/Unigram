using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Core.Helpers;
using Unigram.Core.Services;
using Unigram.Services;
using Unigram.Views;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Telegram.Td.Api;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Metadata;
using Unigram.ViewModels.Delegates;

namespace Unigram.ViewModels
{
   public class SettingsViewModel : UnigramViewModelBase,
        IDelegable<IUserDelegate>,
        IHandle<UpdateUser>,
        IHandle<UpdateUserFullInfo>
    {
        private readonly INotificationsService _pushService;
        private readonly IContactsService _contactsService;

        public IUserDelegate Delegate { get; set; }

        public SettingsViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator, INotificationsService pushService, IContactsService contactsService) 
            : base(protoService, cacheService, aggregator)
        {
            _pushService = pushService;
            _contactsService = contactsService;

            AskCommand = new RelayCommand(AskExecute);
            LogoutCommand = new RelayCommand(LogoutExecute);
            EditPhotoCommand = new RelayCommand<StorageFile>(EditPhotoExecute);
        }

        private Chat _chat;
        public Chat Chat
        {
            get
            {
                return _chat;
            }
            set
            {
                Set(ref _chat, value);
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var response = await ProtoService.SendAsync(new CreatePrivateChat(ProtoService.GetMyId(), false));
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
                        Delegate?.UpdateUserFullInfo(chat, item, cache, false);
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
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(chat, ProtoService.GetUser(update.UserId), update.UserFullInfo, false));
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
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AskAQuestionInfo, Strings.Resources.AskAQuestion, Strings.Resources.AskButton, Strings.Resources.Cancel);
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

        public RelayCommand LogoutCommand { get; }
        private async void LogoutExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureLogout, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            await _pushService.UnregisterAsync();
            await _contactsService.RemoveAsync();

            var response = await ProtoService.SendAsync(new LogOut());
            if (response is Error error)
            {
                // TODO:
            }

            if (ApiInformation.IsMethodPresent("Windows.ApplicationModel.Core.CoreApplication", "RequestRestartAsync"))
            {
                await CoreApplication.RequestRestartAsync(string.Empty);
            }
            else
            {
                App.Current.Exit();
            }
        }
    }
}
