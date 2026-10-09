//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Create;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Create
{
    public partial class NewBotViewModel : ViewModelBase, IHandle
    {
        private long _managedBotUserId;
        private bool _viaLink;

        public NewBotViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _username = new DebouncedProperty<string>(Constants.TypingTimeout, CheckAvailability, UpdateIsValid);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            IsValid = false;
            IsLoading = false;
            ErrorMessage = null;

            if (parameter is NewBotArgs args)
            {
                _managedBotUserId = args.BotUserId;
                _viaLink = args.ViaLink;

                Name = args.SuggestedName;

                if (args.SuggestedUsername.EndsWith("bot", StringComparison.OrdinalIgnoreCase))
                {
                    _username.Value = args.SuggestedUsername[..^3];
                    Suffix = args.SuggestedUsername[^3..];
                }
                else
                {
                    _username.Value = args.SuggestedUsername;
                    Suffix = "bot";
                }
            }

            if (UpdateIsValid(Username))
            {
                CheckAvailability(Username);
            }

            return Task.CompletedTask;
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => Set(ref _name, value);
        }

        private string _suffix;
        public string Suffix
        {
            get => _suffix;
            set => Set(ref _suffix, value);
        }

        private readonly DebouncedProperty<string> _username;
        public string Username
        {
            get => _username;
            set => _username.Set(value);
        }

        public bool IsVisible => !string.IsNullOrWhiteSpace(_username);

        public string Footer => Strings.BotUsernamesHelp;

        public string ComputedUsername => _username + _suffix;

        private bool _isValid;
        public bool IsValid
        {
            get => _isValid;
            set => Set(ref _isValid, value);
        }

        private bool _isAvailable;
        public bool IsAvailable
        {
            get => _isAvailable;
            set => Set(ref _isAvailable, value);
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => Set(ref _errorMessage, value);
        }

        public async void CheckAvailability(string text)
        {
            var response = await ClientService.SendAsync(new CheckBotUsername(ComputedUsername, false));
            if (response is CheckChatUsernameResultOk)
            {
                IsLoading = false;
                IsAvailable = true;
                ErrorMessage = null;
            }
            else if (response is CheckChatUsernameResultUsernameInvalid)
            {
                IsLoading = false;
                IsAvailable = false;
                ErrorMessage = Strings.UsernameInvalid;
            }
            else if (response is CheckChatUsernameResultUsernameOccupied)
            {
                IsLoading = false;
                IsAvailable = false;
                ErrorMessage = Strings.UsernameInUse;
            }
            else if (response is CheckChatUsernameResultUsernamePurchasable)
            {
                IsLoading = false;
                IsAvailable = false;
                ErrorMessage = Strings.UsernameInUsePurchase;
            }
            else if (response is CheckChatUsernameResultPublicChatsTooMany)
            {
                //HasTooMuchUsernames = true;
                //NavigationService.ShowLimitReached(new PremiumLimitTypeCreatedPublicChatCount());
            }
            else if (response is Error error)
            {
                IsLoading = false;
                IsAvailable = false;
                ErrorMessage = error.Message;
            }

            RaisePropertyChanged(nameof(Username));
        }

        public bool UpdateIsValid(string username)
        {
            IsValid = IsValidUsername(username);
            IsLoading = false;
            IsAvailable = false;

            if (!IsValid)
            {
                if (string.IsNullOrEmpty(username))
                {
                    ErrorMessage = null;
                }
                else if (username.Length < 5)
                {
                    ErrorMessage = Strings.UsernameInvalidShort;
                }
                else if (username.Length > 32)
                {
                    ErrorMessage = Strings.UsernameInvalidLong;
                }
                else if (username[0] is >= '0' and <= '9')
                {
                    ErrorMessage = Strings.UsernameInvalidStartNumber;
                }
                else
                {
                    ErrorMessage = Strings.UsernameInvalid;
                }
            }
            else
            {
                IsLoading = true;
                ErrorMessage = null;
            }

            RaisePropertyChanged(nameof(IsVisible));
            return IsValid;
        }

        public bool IsValidUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return false;
            }

            if (username.Length < 5)
            {
                return false;
            }

            if (username.Length > 32)
            {
                return false;
            }

            for (int i = 0; i < username.Length; i++)
            {
                if (i == 0 && char.IsDigit(username[0]))
                {
                    return false;
                }
                else if (!MessageHelper.IsValidUsernameSymbol(username[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public async Task<bool> SendAsync()
        {
            var response = await ClientService.SendAsync(new CreateBot(_managedBotUserId, _name, ComputedUsername, _viaLink));
            if (response is User user)
            {
                NavigationService.NavigateToUser(user.Id, true);
                return true;
            }
            else if (response is Error error)
            {
                if (error.MessageEquals(ErrorType.BOT_CREATE_LIMIT_EXCEEDED))
                {
                    //this.HasError = true;
                    //this.Error = Strings.Additional.UsernameInvalid;
                    //Telegram.Api.Helpers.Dispatch(delegate
                    //{
                    //    MessageBox.Show(Strings.Additional.UsernameInvalid, Strings.Additional.Error, 0);
                    //});
                }
            }

            return false;
        }
    }
}
