//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Chats;
using Telegram.Views.Popups;
using Telegram.Views.Settings.Popups;
using Telegram.Views.Users;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Users
{
    public partial class UserEditViewModel : ViewModelBase, IDelegable<IUserDelegate>, IHandle
    {
        public IUserDelegate Delegate { get; set; }

        private readonly IProfilePhotoService _profilePhotoService;

        private bool _confirmed;

        public UserEditViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IProfilePhotoService profilePhotoService)
            : base(clientService, settingsService, aggregator)
        {
            _profilePhotoService = profilePhotoService;

            SendCommand = new RelayCommand(Send, CanSend);
        }

        private string _title;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private string _originalFirstName = string.Empty;

        private string _firstName = string.Empty;
        public string FirstName
        {
            get => _firstName;
            set
            {
                if (Set(ref _firstName, value))
                {
                    SendCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _lastName = string.Empty;
        public string LastName
        {
            get => _lastName;
            set
            {
                if (Set(ref _lastName, value))
                {
                    SendCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _originalDescription = string.Empty;

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set => Set(ref _description, value);
        }

        private FormattedText _note;
        public FormattedText Note
        {
            get => _note;
            set => Set(ref _note, value);
        }

        private bool _sharePhoneNumber;
        public bool SharePhoneNumber
        {
            get => _sharePhoneNumber;
            set
            {
                if (Set(ref _sharePhoneNumber, value))
                {
                    SendCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private StarAmount _starCount;
        public StarAmount StarCount
        {
            get => _starCount;
            set => Set(ref _starCount, value);
        }

        private long _userId;

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long userId && ClientService.TryGetUser(userId, out User user))
            {
                _userId = userId;

                if (user.Type is UserTypeBot)
                {
                    Title = Strings.ChannelEdit;

                    _originalFirstName = user.FirstName;
                    FirstName = user.FirstName;
                }
                else
                {
                    Title = Strings.EditContact;

                    FirstName = user.FirstName;
                    LastName = user.LastName;
                }

                ClientService.TryGetUserFull(user.Id, out UserFullInfo userFull);
                Delegate?.UpdateUser(null, user, userFull, false, false);

                ClientService.Send(new GetUserFullInfo(user.Id));

                if (user.Type is UserTypeBot)
                {
                    if (userFull != null)
                    {
                        _originalDescription = userFull.BotInfo.ShortDescription;
                        Description = userFull.BotInfo.ShortDescription;
                    }

                    var response = await ClientService.SendAsync(new GetBotName(userId, string.Empty));
                    if (response is Text text1)
                    {
                        _originalFirstName = text1.TextValue;
                        FirstName = text1.TextValue;
                    }

                    var response1 = await ClientService.SendAsync(new GetBotInfoShortDescription(userId, string.Empty));
                    if (response1 is Text text2)
                    {
                        _originalDescription = text2.TextValue;
                        Description = text2.TextValue;
                    }

                    var response2 = await ClientService.GetStarTransactionsAsync(new MessageSenderUser(userId), string.Empty, null, string.Empty, 1);
                    if (response2 is StarTransactions transactions)
                    {
                        StarCount = transactions.StarAmount;
                    }
                }
            }
        }

        public override async void NavigatingFrom(NavigatingEventArgs args)
        {
            if (_confirmed || args.NavigationMode != NavigationMode.Back)
            {
                return;
            }

            if (ClientService.TryGetUser(_userId, out User user) && ClientService.TryGetUserFull(user.Id, out UserFullInfo userFull))
            {
                if (user.Type is UserTypeBot userTypeBot && userTypeBot.CanBeEdited)
                {
                    if (_originalFirstName != _firstName || _originalDescription != _description)
                    {
                        args.Cancel = true;

                        var confirm = await ShowPopupAsync(Strings.BotSettingsChangedAlert, Strings.UnsavedChanges, Strings.ApplyTheme, Strings.Discard);
                        if (confirm == ContentDialogResult.Primary)
                        {
                            Continue(args);
                        }
                        else
                        {
                            _confirmed = true;
                            NavigationService.GoBack(args);
                        }
                    }
                }
            }
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateUser>(this, Handle)
                .Subscribe<UpdateUserFullInfo>(Handle);
        }

        public void Handle(UpdateUser update)
        {
            if (update.User.Id == _userId)
            {
                ClientService.TryGetUserFull(update.User.Id, out UserFullInfo fullInfo);

                BeginOnUIThread(() =>
                {
                    FirstName = update.User.FirstName;
                    LastName = update.User.LastName;

                    Delegate?.UpdateUser(null, update.User, fullInfo, false, false);
                });
            }
        }

        public void Handle(UpdateUserFullInfo update)
        {
            if (update.UserId == _userId && ClientService.TryGetUser(_userId, out User user))
            {
                BeginOnUIThread(() =>
                {
                    Description = update.UserFullInfo.BotInfo?.ShortDescription ?? string.Empty;

                    Delegate?.UpdateUser(null, user, update.UserFullInfo, false, false);
                });
            }
        }

        public RelayCommand SendCommand { get; }
        private void Send()
        {
            Continue(null);
        }

        private void Continue(NavigatingEventArgs args)
        {
            if (ClientService.TryGetUser(_userId, out User user) && ClientService.TryGetUserFull(user.Id, out UserFullInfo userFull))
            {
                if (user.Type is UserTypeBot userTypeBot && userTypeBot.CanBeEdited)
                {
                    if (user.FirstName != _firstName)
                    {
                        ClientService.Send(new SetBotName(user.Id, string.Empty, _firstName));
                    }

                    if (userFull.BotInfo?.ShortDescription != _description)
                    {
                        ClientService.Send(new SetBotInfoShortDescription(user.Id, string.Empty, _description));
                    }
                }
                else
                {
                    ClientService.Send(new AddContact(user.Id, new ImportedContact(user.PhoneNumber, _firstName, _lastName, _note),
                        userFull.NeedPhoneNumberPrivacyException && SharePhoneNumber));
                }

                _confirmed = true;
                NavigationService.GoBack(args);
            }
        }

        private bool CanSend()
        {
            return _firstName.Length > 0
                && _firstName.Length <= 64
                && _lastName.Length <= 64;
        }

        public async void SuggestBirthday()
        {
            if (ClientService.TryGetUser(_userId, out User user))
            {
                var popup = new SettingsBirthdatePopup(user);

                var confirm = await ShowPopupAsync(popup);
                if (confirm == ContentDialogResult.Primary)
                {
                    ClientService.Send(new SuggestUserBirthdate(user.Id, popup.Value));

                    NavigationService.NavigateToUser(user.Id, true);
                }
            }
        }

        public async void SetPhoto()
        {
            var success = await _profilePhotoService.SetPhotoAsync(NavigationService, _userId, isPersonal: false);
            if (success)
            {
                NavigationService.NavigateToChat(_userId);
            }
        }

        public async void CreatePhoto()
        {
            var success = await _profilePhotoService.CreatePhotoAsync(NavigationService, _userId, isPersonal: false);
            if (success)
            {
                NavigationService.NavigateToChat(_userId);
            }
        }

        public async void SetPersonalPhoto()
        {
            await _profilePhotoService.SetPhotoAsync(NavigationService, _userId, isPersonal: true);
        }

        public async void CreatePersonalPhoto()
        {
            await _profilePhotoService.CreatePhotoAsync(NavigationService, _userId, isPersonal: true);
        }

        public async void ResetPhoto()
        {
            if (ClientService.TryGetUser(_userId, out User user))
            {
                var confirm = await ShowPopupAsync(string.Format(Strings.ResetToOriginalPhotoMessage, user.FirstName), Strings.ResetToOriginalPhotoTitle, Strings.Reset, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    ClientService.Send(new SetUserPersonalProfilePhoto(user.Id, null));
                }
            }
        }

        public async void ChangeUsername()
        {
            await ShowPopupAsync(new SettingsUsernamePopup(), _userId);
        }

        public void OpenAffiliate()
        {
            NavigationService.Navigate(typeof(UserAffiliatePage), _userId);
        }

        public void ShowBalance()
        {
            NavigationService.Navigate(typeof(ChatStarsPage), new MessageSenderUser(_userId));
        }

        public void EditCommands()
        {
            if (ClientService.TryGetUserFull(_userId, out UserFullInfo fullInfo) && fullInfo.BotInfo != null)
            {
                MessageHelper.OpenTelegramUrl(ClientService, NavigationService, fullInfo.BotInfo.EditCommandsLink);
            }
        }

        public void EditDescription()
        {
            if (ClientService.TryGetUserFull(_userId, out UserFullInfo fullInfo) && fullInfo.BotInfo != null)
            {
                MessageHelper.OpenTelegramUrl(ClientService, NavigationService, fullInfo.BotInfo.EditDescriptionLink);
            }
        }

        public void EditSettings()
        {
            if (ClientService.TryGetUserFull(_userId, out UserFullInfo fullInfo) && fullInfo.BotInfo != null)
            {
                MessageHelper.OpenTelegramUrl(ClientService, NavigationService, fullInfo.BotInfo.EditSettingsLink);
            }
        }

        public void VerifyAccounts()
        {
            ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationVerifyChat(_userId));
        }
    }
}
