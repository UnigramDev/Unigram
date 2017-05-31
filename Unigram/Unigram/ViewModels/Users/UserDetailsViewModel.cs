using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Popups;
using Unigram.Controls;
using Template10.Common;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.Foundation.Metadata;
using Windows.ApplicationModel.Calls;
using System.Diagnostics;
using Unigram.Views;
using Windows.ApplicationModel.Contacts;
using System.Collections.ObjectModel;
using Unigram.Common;
using System.Linq;
using Unigram.Controls.Views;
using Unigram.Views.Users;
using Unigram.Converters;
using System.Runtime.CompilerServices;

namespace Unigram.ViewModels.Users
{
    public class UserDetailsViewModel : UnigramViewModelBase, IHandle<TLUpdateUserBlocked>, IHandle<TLUpdateNotifySettings>
    {
        public string LastSeen { get; internal set; }

        public UserDetailsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        private TLUser _item;
        public TLUser Item
        {
            get
            {
                return _item;
            }
            set
            {
                Set(ref _item, value);
            }
        }

        private TLUserFull _full;
        public TLUserFull Full
        {
            get
            {
                return _full;
            }
            set
            {
                Set(ref _full, value);
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Item = null;
            Full = null;

            var user = parameter as TLUser;
            var peer = parameter as TLPeerUser;
            if (peer != null)
            {
                user = CacheService.GetUser(peer.Id) as TLUser;
            }

            if (user != null)
            {
                Item = user;
                RaisePropertyChanged(() => IsEditEnabled);
                RaisePropertyChanged(() => AreNotificationsEnabled);
                RaisePropertyChanged(() => PhoneVisibility);
                RaisePropertyChanged(() => AddToGroupVisibility);
                RaisePropertyChanged(() => HelpVisibility);
                RaisePropertyChanged(() => ReportVisibility);
                RaisePropertyChanged(() => AddContactVisibility);

                var full = CacheService.GetFullUser(user.Id);
                if (full == null)
                {
                    var response = await ProtoService.GetFullUserAsync(user.ToInputUser());
                    if (response.IsSucceeded)
                    {
                        full = response.Result;
                    }
                }

                if (full != null)
                {
                    Full = full;
                    RaisePropertyChanged(() => IsPhoneCallsAvailable);
                    RaisePropertyChanged(() => AboutVisibility);
                    RaisePropertyChanged(() => BlockVisibility);
                    RaisePropertyChanged(() => UnblockVisibility);
                    RaisePropertyChanged(() => StopVisibility);
                    RaisePropertyChanged(() => UnstopVisibility);
                    RaisePropertyChanged(() => AddContactVisibility);
                }

                LastSeen = LastSeenConverter.GetLabel(user, true);

                Aggregator.Subscribe(this);
            }
        }
        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return Task.CompletedTask;
        }

        public void Handle(TLUpdateUserBlocked message)
        {
            if (Item != null && Full != null && Item.Id == message.UserId)
            {
                // TODO: 06/05/2017
                //Item.IsBlocked = message.Blocked;
                Full.IsBlocked = message.Blocked;
                Execute.BeginOnUIThread(() =>
                {
                    RaisePropertyChanged(() => BlockVisibility);
                    RaisePropertyChanged(() => UnblockVisibility);
                    RaisePropertyChanged(() => StopVisibility);
                    RaisePropertyChanged(() => UnstopVisibility);
                });
            }
        }

        public void Handle(TLUpdateNotifySettings message)
        {
            var notifyPeer = message.Peer as TLNotifyPeer;
            if (notifyPeer != null)
            {
                var peer = notifyPeer.Peer;
                if (peer is TLPeerUser && peer.Id == Item.Id)
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        Full.NotifySettings = message.NotifySettings;
                        Full.RaisePropertyChanged(() => Full.NotifySettings);
                        RaisePropertyChanged(() => AreNotificationsEnabled);

                        //var notifySettings = updateNotifySettings.NotifySettings as TLPeerNotifySettings;
                        //if (notifySettings != null)
                        //{
                        //    _suppressUpdating = true;
                        //    MuteUntil = notifySettings.MuteUntil.Value;
                        //    _suppressUpdating = false;
                        //}
                    });
                }
            }
        }

        public RelayCommand SendMessageCommand => new RelayCommand(SendMessageExecute);
        private void SendMessageExecute()
        {
            if (Item is TLUser user)
            {
                NavigationService.NavigateToDialog(user);
            }
        }

        public RelayCommand MediaCommand => new RelayCommand(MediaExecute);
        private void MediaExecute()
        {
            if (Item is TLUser user && user.HasAccessHash)
            {
                NavigationService.Navigate(typeof(DialogSharedMediaPage), new TLInputPeerUser { UserId = user.Id, AccessHash = user.AccessHash.Value });
            }
        }

        public RelayCommand CommonChatsCommand => new RelayCommand(CommonChatsExecute);
        private void CommonChatsExecute()
        {
            if (Item is TLUser user && user.HasAccessHash)
            {
                NavigationService.Navigate(typeof(UserCommonChatsPage), new TLInputUser { UserId = user.Id, AccessHash = user.AccessHash.Value });
            }
        }

        public RelayCommand SystemCallCommand => new RelayCommand(SystemCallExecute);
        private void SystemCallExecute()
        {
            var user = Item as TLUser;
            if (user != null)
            {
                if (ApiInformation.IsTypePresent("Windows.ApplicationModel.Calls.PhoneCallManager"))
                {
                    PhoneCallManager.ShowPhoneCallUI($"+{user.Phone}", user.FullName);
                }
                else
                {
                    // TODO
                }
            }
        }

        /// <summary>
        /// Opens People Hub-dialog to the add selected user as a new Contact
        /// </summary>
        public RelayCommand AddContactCommand => new RelayCommand(AddContact);
        private void AddContact()
        {
            var user = Item as TLUser;
            if (user != null)
            {
                // Create the contact-card
                Contact userContact = new Contact();

                // Check if the user has a normal name
                if (user.FullName != "" || user.FullName != null)
                {
                    if (user.FirstName != null)
                    {
                        userContact.FirstName = user.FirstName;
                    }
                    if (user.LastName != null)
                    {
                        userContact.LastName = user.LastName;
                    }
                }
                // if not, use username
                else if (user.HasUsername != false)
                {
                    if (user.Username != null)
                    {
                        userContact.LastName = user.Username;
                    }
                }
                // if all else fails, use phone number (where possible)
                else if (user.HasPhone != false)
                {
                    if (user.Username != null)
                    {
                        userContact.LastName = user.Phone;
                    }
                }
                // TODO Check why phone numbers are not being shown when the contact is not yet in the users People Hub
                if (user.Phone != null)
                {
                    ContactPhone userPhone = new ContactPhone();
                    userPhone.Number = user.Phone;
                    userContact.Phones.Add(userPhone);
                }

                // Set options for the Dialog-window
                FullContactCardOptions options = new FullContactCardOptions();
                options.DesiredRemainingView = Windows.UI.ViewManagement.ViewSizePreference.Default;

                // Show the card
                ContactManager.ShowFullContactCard(userContact, options);
            }
        }

        public Visibility AddContactVisibility
        {
            get
            {
                return Item != null && Item.HasPhone && !Item.IsSelf && !Item.IsContact && !Item.IsMutualContact ? Visibility.Visible : Visibility.Collapsed;
            }
        }


        public RelayCommand BlockCommand => new RelayCommand(BlockExecute);
        private async void BlockExecute()
        {
            var user = Item as TLUser;
            if (user != null)
            {
                var result = await ProtoService.BlockAsync(user.ToInputUser());
                if (result.IsSucceeded && result.Result)
                {
                    CacheService.Commit();
                    Aggregator.Publish(new TLUpdateUserBlocked { UserId = user.Id, Blocked = true });
                }
            }
        }

        public RelayCommand UnblockCommand => new RelayCommand(UnblockExecute);
        private async void UnblockExecute()
        {
            var user = Item as TLUser;
            if (user != null)
            {
                var result = await ProtoService.UnblockAsync(user.ToInputUser());
                if (result.IsSucceeded && result.Result)
                {
                    CacheService.Commit();
                    Aggregator.Publish(new TLUpdateUserBlocked { UserId = user.Id, Blocked = false });

                    if (user.IsBot)
                    {
                        NavigationService.GoBack();
                    }
                }
            }
        }

        public RelayCommand ReportCommand => new RelayCommand(ReportExecute);
        private async void ReportExecute()
        {
            var user = Item as TLUser;
            if (user != null)
            {
                var opt1 = new RadioButton { Content = "Spam", Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
                var opt2 = new RadioButton { Content = "Violence", Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
                var opt3 = new RadioButton { Content = "Pornography", Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
                var opt4 = new RadioButton { Content = "Other", Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch, IsChecked = true };
                var stack = new StackPanel();
                stack.Children.Add(opt1);
                stack.Children.Add(opt2);
                stack.Children.Add(opt3);
                stack.Children.Add(opt4);
                stack.Margin = new Thickness(0, 16, 0, 0);
                var dialog = new ContentDialog();
                dialog.Content = stack;
                dialog.Title = "Resources.Report";
                dialog.IsPrimaryButtonEnabled = true;
                dialog.IsSecondaryButtonEnabled = true;
                dialog.PrimaryButtonText = "Resources.OK";
                dialog.SecondaryButtonText = "Resources.Cancel";

                var dialogResult = await dialog.ShowAsync();
                if (dialogResult == ContentDialogResult.Primary)
                {
                    var reason = opt1.IsChecked == true
                        ? new TLInputReportReasonSpam()
                        : (opt2.IsChecked == true
                            ? new TLInputReportReasonViolence()
                            : (opt3.IsChecked == true
                                ? new TLInputReportReasonPornography()
                                : (TLReportReasonBase)new TLInputReportReasonOther()));

                    if (reason.TypeId == TLType.InputReportReasonOther)
                    {
                        var input = new InputDialog();
                        input.Title = "Resources.Report";
                        input.PlaceholderText = "Resources.Description";
                        input.IsPrimaryButtonEnabled = true;
                        input.IsSecondaryButtonEnabled = true;
                        input.PrimaryButtonText = "Resources.OK";
                        input.SecondaryButtonText = "Resources.Cancel";

                        var inputResult = await input.ShowAsync();
                        if (inputResult == ContentDialogResult.Primary)
                        {
                            reason = new TLInputReportReasonOther { Text = input.Text };
                        }
                        else
                        {
                            return;
                        }
                    }

                    var result = await ProtoService.ReportPeerAsync(user.ToInputPeer(), reason);
                    if (result.IsSucceeded && result.Result)
                    {
                        await new MessageDialog("Resources.ReportSpamNotification", "Unigram").ShowQueuedAsync();
                    }
                }
            }
        }

        public bool AreNotificationsEnabled
        {
            get
            {
                var settings = _full?.NotifySettings as TLPeerNotifySettings;
                if (settings != null)
                {
                    return settings.MuteUntil == 0;
                }

                return false;
            }
        }

        public bool IsPhoneCallsAvailable
        {
            get
            {
                return _full != null && _full.IsPhoneCallsAvailable && ApiInformation.IsApiContractPresent("Windows.ApplicationModel.Calls.CallsVoipContract", 1);
            }
        }

        public bool IsEditEnabled
        {
            get
            {
                return _item != null && (_item.IsContact || _item.IsMutualContact);
            }
        }

        #region Bot
        public Visibility AboutVisibility
        {
            get
            {
                return _full == null || string.IsNullOrEmpty(_full.About) ? Visibility.Collapsed : Visibility.Visible;

                var user = Item as TLUser;
                if (user != null && user.IsBot && Full != null && !string.IsNullOrWhiteSpace(Full.BotInfo.Description))
                {
                    return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }

        public Visibility AddToGroupVisibility
        {
            get
            {
                var user = Item as TLUser;
                if (user != null && user.IsBot && !user.IsBotNochats)
                {
                    return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }

        public Visibility HelpVisibility
        {
            get
            {
                var user = Item as TLUser;
                if (user != null && user.IsBot)
                {
                    return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }

        public Visibility ReportVisibility
        {
            get
            {
                var user = Item as TLUser;
                if (user != null && user.IsBot)
                {
                    return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }
        #endregion

        public Visibility PhoneVisibility
        {
            get
            {
                var user = Item as TLUser;
                if (user != null && (user.HasPhone || !string.IsNullOrWhiteSpace(user.Phone)))
                {
                    return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }

        public Visibility BlockVisibility
        {
            get
            {
                var user = Item as TLUser;
                if (user != null && user.IsBot)
                {
                    return Visibility.Collapsed;
                }

                if (Full != null && Full.IsBlocked)
                {
                    return Visibility.Collapsed;
                }

                return Visibility.Visible;
            }
        }

        public Visibility UnblockVisibility
        {
            get
            {
                var user = Item as TLUser;
                if (user != null && user.IsBot)
                {
                    return Visibility.Collapsed;
                }

                if (Full != null && Full.IsBlocked)
                {
                    return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }

        public Visibility StopVisibility
        {
            get
            {
                var user = Item as TLUser;
                if (user != null && !user.IsBot)
                {
                    return Visibility.Collapsed;
                }

                if (Full != null && Full.IsBlocked)
                {
                    return Visibility.Collapsed;
                }

                return Visibility.Visible;
            }
        }

        public Visibility UnstopVisibility
        {
            get
            {
                var user = Item as TLUser;
                if (user != null && !user.IsBot)
                {
                    return Visibility.Collapsed;
                }

                if (Full != null && Full.IsBlocked)
                {
                    return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }

        public RelayCommand ToggleMuteCommand => new RelayCommand(ToggleMuteExecute);
        private async void ToggleMuteExecute()
        {
            var notifySettings = _full.NotifySettings as TLPeerNotifySettings;
            if (notifySettings != null)
            {
                var muteUntil = notifySettings.MuteUntil == int.MaxValue ? 0 : int.MaxValue;
                var settings = new TLInputPeerNotifySettings
                {
                    MuteUntil = muteUntil,
                    IsShowPreviews = notifySettings.IsShowPreviews,
                    IsSilent = notifySettings.IsSilent,
                    Sound = notifySettings.Sound
                };

                var response = await ProtoService.UpdateNotifySettingsAsync(new TLInputNotifyPeer { Peer = _item.ToInputPeer() }, settings);
                if (response.IsSucceeded)
                {
                    notifySettings.MuteUntil = muteUntil;
                    RaisePropertyChanged(() => AreNotificationsEnabled);
                    Full.RaisePropertyChanged(() => Full.NotifySettings);

                    var dialog = CacheService.GetDialog(_item.ToPeer());
                    if (dialog != null)
                    {
                        dialog.NotifySettings = _full.NotifySettings;
                        dialog.RaisePropertyChanged(() => dialog.NotifySettings);
                        dialog.RaisePropertyChanged(() => dialog.Self);
                    }

                    CacheService.Commit();
                }
            }
        }

        #region Call

        public RelayCommand CallCommand => new RelayCommand(CallExecute);
        private async void CallExecute()
        {
            var user = _item;
            if (user == null)
            {
                return;
            }

            try
            {
                var coordinator = VoipCallCoordinator.GetDefault();
                var result = await coordinator.ReserveCallResourcesAsync("Unigram.Tasks.VoIPCallTask");
                if (result == VoipPhoneCallResourceReservationStatus.Success)
                {
                    await VoIPConnection.Current.SendRequestAsync("voip.startCall", user);
                }
            }
            catch
            {
                await TLMessageDialog.ShowAsync("Something went wrong. Please, try to close and relaunch the app.", "Unigram", "OK");
            }
        }

        #endregion

        public RelayCommand EditNameCommand => new RelayCommand(EditNameExecute);
        private async void EditNameExecute()
        {
            if (_item == null)
            {
                return;
            }

            var confirm = await EditUserNameView.Current.ShowAsync(_item.FirstName, _item.LastName);
            if (confirm == ContentDialogResult.Primary)
            {
                var contact = new TLInputPhoneContact
                {
                    FirstName = EditUserNameView.Current.FirstName,
                    LastName = EditUserNameView.Current.LastName,
                    Phone = _item.Phone
                };

                var response = await ProtoService.ImportContactsAsync(new TLVector<TLInputContactBase> { contact }, false);
                if (response.IsSucceeded)
                {
                    _item.RaisePropertyChanged(() => _item.FullName);
                    _item.RaisePropertyChanged(() => _item.FirstName);
                    _item.RaisePropertyChanged(() => _item.LastName);
                    _item.RaisePropertyChanged(() => _item.DisplayName);

                    var dialog = CacheService.GetDialog(_item.ToPeer());
                    if (dialog != null)
                    {
                        dialog.RaisePropertyChanged(() => dialog.With);
                    }
                }
            }
        }
    }
}
