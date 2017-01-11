using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsUsernameViewModel : UnigramViewModelBase
    {
        public SettingsUsernameViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            IsValid = false;
            IsLoading = false;
            ErrorMessage = null;

            var cached = CacheService.GetUser(SettingsHelper.UserId) as TLUser;
            if (cached != null)
            {
                _username = cached.HasUsername ? cached.Username : string.Empty;
            }
            else
            {
                var response = await ProtoService.GetUsersAsync(new TLVector<TLInputUserBase> { new TLInputUserSelf() });
                if (response.IsSucceeded)
                {
                    var user = response.Value.FirstOrDefault() as TLUser;
                    if (user != null)
                    {
                        _username = user.HasUsername ? user.Username : string.Empty;
                    }
                }
            }

            RaisePropertyChanged(() => Username);
        }

        private string _username;
        public string Username
        {
            get
            {
                return _username;
            }
            set
            {
                Set(ref _username, value);
                UpdateIsValid(value);
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get
            {
                return _isLoading;
            }
            set
            {
                Set(ref _isLoading, value);
            }
        }

        private bool _isValid;
        public bool IsValid
        {
            get
            {
                return _isValid;
            }
            set
            {
                Set(ref _isValid, value);
            }
        }

        private bool _isAvailable;
        public bool IsAvailable
        {
            get
            {
                return _isAvailable;
            }
            set
            {
                Set(ref _isAvailable, value);
            }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get
            {
                return _errorMessage;
            }
            set
            {
                Set(ref _errorMessage, value);
            }
        }

        public async Task CheckIfAvailableAsync(string text)
        {
            var result = await ProtoService.CheckUsernameAsync(text);
            if (result.IsSucceeded)
            {
                IsLoading = false;
                IsAvailable = true;
                ErrorMessage = null;
            }
            else
            {
                if (result.Error != null)
                {
                    if (result.Error.TypeEquals(TLErrorType.USERNAME_INVALID))
                    {
                        IsLoading = false;
                        IsAvailable = false;
                        ErrorMessage = "Sorry, this username is invalid";
                    }
                    else if (result.Error.TypeEquals(TLErrorType.USERNAME_OCCUPIED))
                    {
                        IsLoading = false;
                        IsAvailable = false;
                        ErrorMessage = "Sorry, this username is already taken";
                    }
                }
                else
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = "Sorry, this username is already taken";
                }
            }
        }

        public bool UpdateIsValid(string username)
        {
            IsValid = IsValidUsername(username);
            IsLoading = false;
            IsAvailable = false;

            if (!IsValid)
            {
                if (_username.Length < 5)
                {
                    ErrorMessage = "A username must have at least 5 characters";
                }
                else
                {
                    ErrorMessage = "Sorry, this username is invalid";
                }
            }
            else
            {
                IsLoading = true;
                ErrorMessage = null;
            }

            return IsValid;
        }

        public bool IsValidUsername(string username)
        {
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
                if (!MessageHelper.IsValidUsernameSymbol(username[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
