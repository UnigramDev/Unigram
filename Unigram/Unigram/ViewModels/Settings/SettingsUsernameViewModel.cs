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
                    var user = response.Result.FirstOrDefault() as TLUser;
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
            var response = await ProtoService.CheckUsernameAsync(text);
            if (response.IsSucceeded)
            {
                if (response.Result)
                {
                    IsLoading = false;
                    IsAvailable = true;
                    ErrorMessage = null;
                }
                else
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = "Sorry, this username is already taken";
                }
            }
            else
            {
                if (response.Error.TypeEquals(TLErrorType.USERNAME_INVALID))
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = "Sorry, this username is invalid";
                }
                else if (response.Error.TypeEquals(TLErrorType.USERNAME_OCCUPIED))
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
                if (string.IsNullOrEmpty(username))
                {
                    ErrorMessage = null;
                }
                else if (_username.Length < 5)
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
                if (!MessageHelper.IsValidUsernameSymbol(username[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public RelayCommand SendCommand => new RelayCommand(SendExecute);
        private async void SendExecute()
        {
            var response = await ProtoService.UpdateUsernameAsync(Username);
            if (response.IsSucceeded)
            {
                CacheService.SyncUser(response.Result, (callback) =>
                {
                    //Aggregator.Publish(new UserNameChangedEventArgs(result));
                });

                NavigationService.GoBack();
            }
            else
            {
                if (response.Error.CodeEquals(TLErrorCode.FLOOD))
                {
                    //this.HasError = true;
                    //this.Error = AppResources.FloodWaitString;
                    //Telegram.Api.Helpers.Execute.BeginOnUIThread(delegate
                    //{
                    //    MessageBox.Show(AppResources.FloodWaitString, AppResources.Error, 0);
                    //});
                }
                else if (response.Error.CodeEquals(TLErrorCode.INTERNAL))
                {
                    //StringBuilder messageBuilder = new StringBuilder();
                    //messageBuilder.AppendLine(AppResources.ServerErrorMessage);
                    //messageBuilder.AppendLine();
                    //messageBuilder.AppendLine("Method: account.updateUsername");
                    //messageBuilder.AppendLine("Result: " + error);
                    //this.HasError = true;
                    //this.Error = AppResources.ServerError;
                    //Telegram.Api.Helpers.Execute.BeginOnUIThread(delegate
                    //{
                    //    MessageBox.Show(messageBuilder.ToString(), AppResources.ServerError, 0);
                    //});
                }
                else if (response.Error.CodeEquals(TLErrorCode.BAD_REQUEST))
                {
                    if (response.Error.TypeEquals(TLErrorType.USERNAME_INVALID))
                    {
                        //this.HasError = true;
                        //this.Error = AppResources.UsernameInvalid;
                        //Telegram.Api.Helpers.Execute.BeginOnUIThread(delegate
                        //{
                        //    MessageBox.Show(AppResources.UsernameInvalid, AppResources.Error, 0);
                        //});
                    }
                    else if (response.Error.TypeEquals(TLErrorType.USERNAME_OCCUPIED))
                    {
                        //this.HasError = true;
                        //this.Error = AppResources.UsernameOccupied;
                        //Telegram.Api.Helpers.Execute.BeginOnUIThread(delegate
                        //{
                        //    MessageBox.Show(AppResources.UsernameOccupied, AppResources.Error, 0);
                        //});
                    }
                    else if (response.Error.TypeEquals(TLErrorType.USERNAME_NOT_MODIFIED))
                    {
                        NavigationService.GoBack();
                    }
                    else
                    {
                        //this.HasError = true;
                        //this.Error = error.ToString();
                    }
                }
                else
                {
                    //this.HasError = true;
                    //this.Error = string.Empty;
                    //Telegram.Api.Helpers.Execute.ShowDebugMessage("account.updateUsername error " + error);
                }
            }
        }
    }
}
