using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Command;
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

namespace Unigram.ViewModels
{
    
    public class UserInfoViewModel : UnigramViewModelBase,
        IHandle<TLUpdateUserBlocked>,
        IHandle<TLUpdateNotifySettings>,
        IHandle
    {
        public ObservableCollection<UsersPanelListItem> UsersList = new ObservableCollection<UsersPanelListItem>();
        public ObservableCollection<UsersPanelListItem> TempList = new ObservableCollection<UsersPanelListItem>();
        public object photo;
        public string FullNameField { get; internal set; }
        public string LastSeen { get; internal set; }
        public UserInfoViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        private TLUserBase _item;
        public TLUserBase Item
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
            //TODO : SET PROPERTY AND VISIBILITY BINDINGS FOR CHATS AND CHANNELS
            var user = parameter as TLUser;
            var channel = parameter as TLInputPeerChannel;
            var chat = parameter as TLInputPeerChat;
            if (channel != null)
            {
                TLInputChannel x = new TLInputChannel();
                x.ChannelId = channel.ChannelId;
                x.AccessHash = channel.AccessHash;
                var channelDetails = await ProtoService.GetFullChannelAsync(x);
                FullNameField = channelDetails.Value.Chats[0].FullName;
                photo = (TLChatPhotoBase)channelDetails.Value.Chats[0].Photo;
            }
            if (chat != null)
            {
                var chatDetails = await ProtoService.GetFullChatAsync(chat.ChatId);
                FullNameField = chatDetails.Value.Chats[0].FullName;
                photo = (TLChatPhotoBase)chatDetails.Value.Chats[0].Photo;
            }
            TempList.Clear();
            UsersList.Clear();
            getMembers(channel, chat);
            if (user != null)
            {
                FullNameField = user.FullName;
                Item = user;
                photo = (TLUserProfilePhotoBase)user.Photo;
                RaisePropertyChanged(() => AreNotificationsEnabled);
                RaisePropertyChanged(() => PhoneVisibility);
                RaisePropertyChanged(() => AddToGroupVisibility);
                RaisePropertyChanged(() => HelpVisibility);
                RaisePropertyChanged(() => ReportVisibility);

                var result = await ProtoService.GetFullUserAsync(user.ToInputUser());
                if (result.IsSucceeded)
                {
                    Full = result.Value;
                    RaisePropertyChanged(() => AboutVisibility);
                    RaisePropertyChanged(() => BlockVisibility);
                    RaisePropertyChanged(() => UnblockVisibility);
                    RaisePropertyChanged(() => StopVisibility);
                    RaisePropertyChanged(() => UnstopVisibility);
                }
                var Status = Unigram.Common.LastSeenHelper.getLastSeen(user);
                              
                LastSeen = Status.Item1;
                Aggregator.Subscribe(this);
            }
        }
        public async Task getMembers(TLInputPeerChannel channel, TLInputPeerChat chat)
        {
            
            if(channel!=null)
            {
                //set visibility
                TLInputChannel x = new TLInputChannel();                
                x.ChannelId = channel.ChannelId;
                x.AccessHash = channel.AccessHash;
                var participants = await ProtoService.GetParticipantsAsync(x, null, 0, int.MaxValue);
                foreach (var item in participants.Value.Users)
                {
                    var User = item as TLUser;
                    var TempX = new UsersPanelListItem(User);
                    var Status = LastSeenHelper.getLastSeen(User);
                    TempX.fullName = User.FullName;
                    TempX.lastSeen = Status.Item1;
                    TempX.lastSeenEpoch = Status.Item2;
                    TempX.Photo = TempX._parent.Photo;
                    TempList.Add(TempX);
                }               
            }

            if (chat != null)
            {
                //set visibility
                var chatDetails = await ProtoService.GetFullChatAsync(chat.ChatId);
                foreach (var item in chatDetails.Value.Users)
                {
                    var User = item as TLUser;
                    var TempX = new UsersPanelListItem(User);
                    var Status = LastSeenHelper.getLastSeen(User);
                    TempX.fullName = User.FullName;
                    TempX.lastSeen = Status.Item1;
                    TempX.lastSeenEpoch = Status.Item2;
                    TempX.Photo = TempX._parent.Photo;
                    TempList.Add(TempX);
                }
            }

            TempList = new ObservableCollection<ViewModels.UsersPanelListItem>(TempList.OrderByDescending(person => person.lastSeenEpoch));
            foreach (var item in TempList)
            {
                UsersList.Add(item);
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
                Item.Blocked = message.Blocked;
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
                        Item.NotifySettings = message.NotifySettings;
                        Item.RaisePropertyChanged(() => Item.NotifySettings);
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

        public RelayCommand PhotoCommand => new RelayCommand(PhotoExecute);
        private async void PhotoExecute()
        {
            var user = Item as TLUser;
            if (user.HasPhoto)
            {
                // TODO
                var test = new UserPhotosViewModel(ProtoService, CacheService, Aggregator);
                var dialog = BootStrapper.Current.ModalDialog;
                dialog.ModalContent = new PhotosView { DataContext = test };
                //dialog.ModalBackground = BootStrapper.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"] as SolidColorBrush;
                dialog.DisableBackButtonWhenModal = false;
                dialog.CanBackButtonDismiss = true;
                dialog.IsModal = true;

                await test.OnNavigatedToAsync(Item, NavigationMode.New, new Dictionary<string, object>());
            }
        }

        public RelayCommand MediaCommand => new RelayCommand(MediaExecute);
        private void MediaExecute()
        {
            var user = Item as TLUser;
            if (user != null)
            {
                NavigationService.Navigate(typeof(DialogSharedMediaPage), new TLInputPeerUser { UserId = user.Id, AccessHash = user.AccessHash.Value });
            }
        }

        public RelayCommand CallCommand => new RelayCommand(CallExecute);
        private void CallExecute()
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
                if (user.ProfilePhoto != null)
                {
                    // TODO Add user profile picture
                }

                // Set options for the Dialog-window
                FullContactCardOptions options = new FullContactCardOptions();
                options.DesiredRemainingView = Windows.UI.ViewManagement.ViewSizePreference.Default;

                // Show the card
                ContactManager.ShowFullContactCard(userContact, options);
            }
        }


        public RelayCommand BlockCommand => new RelayCommand(BlockExecute);
        private async void BlockExecute()
        {
            var user = Item as TLUser;
            if (user != null)
            {
                var result = await ProtoService.BlockAsync(user.ToInputUser());
                if (result.IsSucceeded && result.Value)
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
                if (result.IsSucceeded && result.Value)
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
                    if (result.IsSucceeded && result.Value)
                    {
                        await new MessageDialog("Resources.ReportSpamNotification", "Unigram").ShowAsync();
                    }
                }
            }
        }

        public bool AreNotificationsEnabled
        {
            get
            {
                var settings = Item?.NotifySettings as TLPeerNotifySettings;
                if (settings != null)
                {
                    return settings.MuteUntil == 0;
                }

                return false;
            }
        }

        #region Bot
        public Visibility AboutVisibility
        {
            get
            {
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
    }
}
