//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Navigation;
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsUsernameViewModel : ViewModelBase, IHandle
    {
        private long _userId;

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

            if (parameter is not long)
            {
                parameter = ClientService.Options.MyId;
            }

            if (parameter is long userId && ClientService.TryGetUser(userId, out User user))
            {
                _userId = userId;
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
            if (update.User.Id == _userId)
            {
                BeginOnUIThread(() => UpdateUsernames(update.User.Usernames, Username));
            }
        }

        private void UpdateUsernames(Usernames usernames, string editable = null)
        {
            if (editable != null)
            {
                static IList<string> ReplaceEditable(IList<string> usernames, string original, string editable)
                {
                    if (usernames == null)
                    {
                        return Array.Empty<string>();
                    }
                    else original ??= string.Empty;

                    var clone = new string[usernames.Count];

                    for (int i = 0; i < usernames.Count; i++)
                    {
                        if (usernames[i] == original)
                        {
                            clone[i] = editable;
                        }
                        else
                        {
                            clone[i] = usernames[i];
                        }
                    }

                    return clone;
                }

                usernames = new Usernames
                {
                    ActiveUsernames = ReplaceEditable(usernames?.ActiveUsernames, usernames?.EditableUsername, editable),
                    DisabledUsernames = ReplaceEditable(usernames?.DisabledUsernames, usernames?.EditableUsername, editable),
                    EditableUsername = editable
                };
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

                    if (_userId == ClientService.Options.MyId)
                    {
                        ClientService.Send(new ReorderActiveUsernames(order));
                    }
                    else
                    {
                        ClientService.Send(new ReorderBotActiveUsernames(_userId, order));
                    }
                }
            }
        }

        public void ToggleUsername(UsernameInfo username)
        {
            if (_userId == ClientService.Options.MyId)
            {
                ClientService.Send(new ToggleUsernameIsActive(username.Value, !username.IsActive));
            }
            else
            {
                ClientService.Send(new ToggleBotUsernameIsActive(_userId, username.Value, !username.IsActive));
            }
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

        public bool CanBeEdited => _userId == ClientService.Options.MyId;

        public bool IsVisible => Items.Count < 2 && !string.IsNullOrWhiteSpace(_username);

        public bool IsMultiple => Items.Count > 0 && !IsVisible;

        public string Footer => _userId == ClientService.Options.MyId ? Strings.UsernamesProfileHelp : Strings.BotUsernamesHelp;

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
            if (_userId != ClientService.Options.MyId)
            {
                return;
            }

            var response = await ClientService.SendAsync(new SearchPublicChat(text ?? string.Empty));
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
                    ErrorMessage = Strings.UsernameInUse;
                }
            }
            else if (response is Error error)
            {
                if (error.MessageEquals(ErrorType.USERNAME_INVALID))
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = Strings.UsernameInvalid;
                }
                else if (error.MessageEquals(ErrorType.USERNAME_OCCUPIED))
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = Strings.UsernameInUse;
                }
                else if (error.MessageEquals(ErrorType.USERNAME_NOT_OCCUPIED))
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

            if (ClientService.TryGetUser(ClientService.Options.MyId, out User user))
            {
                UpdateUsernames(user.Usernames, username);
            }

            RaisePropertyChanged(nameof(IsVisible));
            RaisePropertyChanged(nameof(IsMultiple));
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
            if (_userId != ClientService.Options.MyId)
            {
                return true;
            }

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
                    if (error.MessageEquals(ErrorType.USERNAME_INVALID))
                    {
                        //this.HasError = true;
                        //this.Error = Strings.Additional.UsernameInvalid;
                        //Telegram.Api.Helpers.Dispatch(delegate
                        //{
                        //    MessageBox.Show(Strings.Additional.UsernameInvalid, Strings.Additional.Error, 0);
                        //});
                    }
                    else if (error.MessageEquals(ErrorType.USERNAME_OCCUPIED))
                    {
                        //this.HasError = true;
                        //this.Error = Strings.Additional.UsernameOccupied;
                        //Telegram.Api.Helpers.Dispatch(delegate
                        //{
                        //    MessageBox.Show(Strings.Additional.UsernameOccupied, Strings.Additional.Error, 0);
                        //});
                    }
                    else if (error.MessageEquals(ErrorType.USERNAME_NOT_MODIFIED))
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

        public void Copy()
        {
            MessageHelper.CopyLink(ClientService, XamlRoot, new InternalLinkTypePublicChat(_username, string.Empty, false));
        }
    }

    public partial class UsernameInfo : BindableBase
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

        public string DisplayValue => _tme ? MeUrlPrefixConverter.Convert(_clientService, _value, true) : $"@{_value}";

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => Set(ref _isActive, value);
        }

        public bool IsEditable { get; }
    }

    public partial class UsernameInfoDiffHandler : IDiffHandler<UsernameInfo>
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
