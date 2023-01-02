//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Entities;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsUsernameViewModel : TLViewModelBase, IHandle
    {
        public SettingsUsernameViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _username = new DebouncedProperty<string>(Constants.TypingTimeout, CheckAvailability, UpdateIsValid);
            Items = new DiffObservableCollection<UsernameInfo>(new UsernameInfoDiffHandler(), new DiffOptions { AllowBatching = false, DetectMoves = true });
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            IsValid = false;
            IsLoading = false;
            ErrorMessage = null;

            if (ClientService.TryGetUser(ClientService.Options.MyId, out User user))
            {
                _username.Value = user.EditableUsername();
                RaisePropertyChanged(nameof(Username));

                UpdateUsernames(user.Usernames);
            }

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateUser>(this, Handle);
        }

        public DiffObservableCollection<UsernameInfo> Items { get; private set; }

        public void Handle(UpdateUser update)
        {
            if (update.User.Id == ClientService.Options.MyId)
            {
                BeginOnUIThread(() => UpdateUsernames(update.User.Usernames, Username));
            }
        }

        private void UpdateUsernames(Usernames usernames, string editable = null)
        {
            if (editable != null)
            {
                static void ReplaceEditable(IList<string> usernames, string original, string editable)
                {
                    for (int i = 0; i < usernames.Count; i++)
                    {
                        if (usernames[i] == original)
                        {
                            usernames[i] = editable;
                            break;
                        }
                    }
                }

                usernames ??= new Usernames
                {
                    ActiveUsernames = Array.Empty<string>(),
                    DisabledUsernames = Array.Empty<string>(),
                    EditableUsername = string.Empty
                };

                ReplaceEditable(usernames.ActiveUsernames, usernames.EditableUsername, editable);
                ReplaceEditable(usernames.DisabledUsernames, usernames.EditableUsername, editable);

                usernames.EditableUsername = editable;
            }

            if (usernames?.ActiveUsernames.Count + usernames?.DisabledUsernames.Count > 1)
            {
                Items.ReplaceDiff(UsernameInfo.FromUsernames(ClientService, usernames, false));
            }
            else if (Items.Count > 0)
            {
                Items.Clear();
            }
        }

        public void ReorderUsernames(UsernameInfo username)
        {
            if (ClientService.TryGetUser(ClientService.Options.MyId, out User user))
            {
                var active = user.Usernames?.ActiveUsernames;

                var index = Items.IndexOf(username);
                if (index >= active.Count)
                {
                    UpdateUsernames(user.Usernames, Username);
                }
                else
                {
                    var order = new List<string>();

                    foreach (var info in Items)
                    {
                        if (info.IsActive || info.IsEditable)
                        {
                            order.Add(info.Value);
                        }
                    }

                    ClientService.Send(new ReorderActiveUsernames(order));
                }
            }
        }

        public void ToggleUsername(UsernameInfo username)
        {
            ClientService.Send(new ToggleUsernameIsActive(username.Value, !username.IsActive));
        }

        //private string _username = string.Empty;
        //public string Username
        //{
        //    get => _username;
        //    set
        //    {
        //        Set(ref _username, value);
        //        UpdateIsValid(value);
        //    }
        //}

        private readonly DebouncedProperty<string> _username;
        public string Username
        {
            get => _username;
            set => _username.Set(value);
        }

        public bool IsVisible
        {
            get => Items.Count < 2 && !string.IsNullOrWhiteSpace(_username);
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
                    ErrorMessage = Strings.Resources.UsernameInvalidShort;
                }
                else if (username.Length > 32)
                {
                    ErrorMessage = Strings.Resources.UsernameInvalidLong;
                }
                else if (username[0] is >= '0' and <= '9')
                {
                    ErrorMessage = Strings.Resources.UsernameInvalidStartNumber;
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

            if (ClientService.TryGetUser(ClientService.Options.MyId, out User user))
            {
                UpdateUsernames(user.Usernames, username);
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

        public async void Copy()
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(MeUrlPrefixConverter.Convert(ClientService, _username));
            ClipboardEx.TrySetContent(dataPackage);

            await MessagePopup.ShowAsync(Strings.Resources.LinkCopied, Strings.Resources.AppName, Strings.Resources.OK);
        }
    }


    public class UsernameInfo : BindableBase
    {
        private readonly IClientService _clientService;
        private readonly bool _tme;

        private UsernameInfo(IClientService clientService, string value, bool active, bool editable, bool tme)
        {
            _clientService = clientService;
            _tme = tme;

            Value = value;
            IsActive = active;
            IsEditable = editable;
        }

        public static IEnumerable<UsernameInfo> FromUsernames(IClientService clientService, User user)
        {
            return FromUsernames(clientService, user.Usernames, false);
        }

        public static IEnumerable<UsernameInfo> FromUsernames(IClientService clientService, Supergroup supergroup)
        {
            return FromUsernames(clientService, supergroup.Usernames, true);
        }

        public static IEnumerable<UsernameInfo> FromUsernames(IClientService clientService, Usernames usernames, bool tme)
        {
            if (usernames == null)
            {
                yield break;
            }

            foreach (var item in usernames.ActiveUsernames)
            {
                yield return new UsernameInfo(clientService, item, true, item == usernames.EditableUsername, tme);
            }

            foreach (var item in usernames.DisabledUsernames)
            {
                yield return new UsernameInfo(clientService, item, false, item == usernames.EditableUsername, tme);
            }
        }

        private string _value;
        public string Value
        {
            get => _value;
            set
            {
                if (Set(ref _value, value))
                {
                    RaisePropertyChanged(nameof(DisplayValue));
                }
            }
        }

        public string DisplayValue => _tme? MeUrlPrefixConverter.Convert(_clientService, _value, true): $"@{_value}";

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => Set(ref _isActive, value);
        }

        public bool IsEditable { get; }
    }

    public class UsernameInfoDiffHandler : IDiffHandler<UsernameInfo>
    {
        public bool CompareItems(UsernameInfo oldItem, UsernameInfo newItem)
        {
            return oldItem.Value == newItem.Value || (oldItem.IsEditable && newItem.IsEditable);
        }

        public void UpdateItem(UsernameInfo oldItem, UsernameInfo newItem)
        {
            oldItem.Value = newItem.Value;
            oldItem.IsActive = newItem.IsActive;
        }
    }
}
