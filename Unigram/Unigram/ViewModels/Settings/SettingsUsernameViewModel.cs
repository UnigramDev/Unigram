using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Entities;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsUsernameViewModel : TLViewModelBase
    {
        public SettingsUsernameViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            CopyCommand = new RelayCommand(CopyExecute);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            IsValid = false;
            IsLoading = false;
            ErrorMessage = null;

            if (ClientService.TryGetUser(ClientService.Options.MyId, out User user))
            {
                Set(ref _username, user.Username, nameof(Username));
            }

            return Task.CompletedTask;
        }

        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set
            {
                Set(ref _username, value);
                UpdateIsValid(value);
            }
        }

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
            var response = await ClientService.SendAsync(new SearchPublicChat(text));
            if (response is Chat chat)
            {
                if (chat.Type is ChatTypePrivate privata && privata.UserId == ClientService.Options.MyId)
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = null;
                }
                else
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = Strings.Resources.UsernameInUse;
                }
            }
            else if (response is Error error)
            {
                if (error.TypeEquals(ErrorType.USERNAME_INVALID))
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = Strings.Resources.UsernameInvalid;
                }
                else if (error.TypeEquals(ErrorType.USERNAME_OCCUPIED))
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = Strings.Resources.UsernameInUse;
                }
                else if (error.TypeEquals(ErrorType.USERNAME_NOT_OCCUPIED))
                {
                    IsLoading = false;
                    IsAvailable = true;
                    ErrorMessage = null;
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
                    ErrorMessage = Strings.Resources.UsernameInvalidShort;
                }
                else if (_username.Length > 32)
                {
                    ErrorMessage = Strings.Resources.UsernameInvalidLong;
                }
                else
                {
                    ErrorMessage = Strings.Resources.UsernameInvalid;
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

        public async Task<bool> SendAsync()
        {
            var response = await ClientService.SendAsync(new SetUsername(_username ?? string.Empty));
            if (response is Ok)
            {
                return true;
            }
            else if (response is Error error)
            {
                if (error.CodeEquals(ErrorCode.FLOOD))
                {
                    //this.HasError = true;
                    //this.Error = Strings.Additional.FloodWaitString;
                    //Telegram.Api.Helpers.Dispatch(delegate
                    //{
                    //    MessageBox.Show(Strings.Additional.FloodWaitString, Strings.Additional.Error, 0);
                    //});
                }
                else if (error.CodeEquals(ErrorCode.INTERNAL))
                {
                    //StringBuilder messageBuilder = new StringBuilder();
                    //messageBuilder.AppendLine(Strings.Additional.ServerErrorMessage);
                    //messageBuilder.AppendLine();
                    //messageBuilder.AppendLine("Method: account.updateUsername");
                    //messageBuilder.AppendLine("Result: " + error);
                    //this.HasError = true;
                    //this.Error = Strings.Additional.ServerError;
                    //Telegram.Api.Helpers.Dispatch(delegate
                    //{
                    //    MessageBox.Show(messageBuilder.ToString(), Strings.Additional.ServerError, 0);
                    //});
                }
                else if (error.CodeEquals(ErrorCode.BAD_REQUEST))
                {
                    if (error.TypeEquals(ErrorType.USERNAME_INVALID))
                    {
                        //this.HasError = true;
                        //this.Error = Strings.Additional.UsernameInvalid;
                        //Telegram.Api.Helpers.Dispatch(delegate
                        //{
                        //    MessageBox.Show(Strings.Additional.UsernameInvalid, Strings.Additional.Error, 0);
                        //});
                    }
                    else if (error.TypeEquals(ErrorType.USERNAME_OCCUPIED))
                    {
                        //this.HasError = true;
                        //this.Error = Strings.Additional.UsernameOccupied;
                        //Telegram.Api.Helpers.Dispatch(delegate
                        //{
                        //    MessageBox.Show(Strings.Additional.UsernameOccupied, Strings.Additional.Error, 0);
                        //});
                    }
                    else if (error.TypeEquals(ErrorType.USERNAME_NOT_MODIFIED))
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
                    //Telegram.Api.Helpers.Logs.Log.Write("account.updateUsername error " + error);
                }
            }

            return false;
        }

        public RelayCommand CopyCommand { get; }
        private async void CopyExecute()
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(MeUrlPrefixConverter.Convert(ClientService, _username));
            ClipboardEx.TrySetContent(dataPackage);

            await MessagePopup.ShowAsync(Strings.Resources.LinkCopied, Strings.Resources.AppName, Strings.Resources.OK);
        }
    }
}
