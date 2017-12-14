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
using Unigram.Views.Dialogs;

namespace Unigram.ViewModels.Users
{
    public class UserDetailsViewModel : UnigramViewModelBase, IHandle<TLUpdateUserBlocked>, IHandle<TLUpdateNotifySettings>
    {
        public string LastSeen { get; internal set; }

        public UserDetailsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            SendMessageCommand = new RelayCommand(SendMessageExecute);
            MediaCommand = new RelayCommand(MediaExecute);
            CommonChatsCommand = new RelayCommand(CommonChatsExecute);
            SystemCallCommand = new RelayCommand(SystemCallExecute);
            BlockCommand = new RelayCommand(BlockExecute);
            UnblockCommand = new RelayCommand(UnblockExecute);
            ReportCommand = new RelayCommand(ReportExecute);
            ToggleMuteCommand = new RelayCommand(ToggleMuteExecute);
            CallCommand = new RelayCommand(CallExecute);
            AddCommand = new RelayCommand(AddExecute);
            EditCommand = new RelayCommand(EditExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
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
                RaisePropertyChanged(() => IsAddEnabled);
                RaisePropertyChanged(() => IsMuted);
                RaisePropertyChanged(() => PhoneVisibility);
                RaisePropertyChanged(() => AddToGroupVisibility);
                RaisePropertyChanged(() => HelpVisibility);
                RaisePropertyChanged(() => ReportVisibility);

                var full = CacheService.GetFullUser(user.Id);
                if (full == null)
                {
                    var response = await ProtoService.GetFullUserAsync(user.ToInputUser());
                    if (response.IsSucceeded)
                    {
                        full = response.Result;
                    }
                }
                else
                {
                    ProtoService.GetFullUserAsync(user.ToInputUser(), null);
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
                BeginOnUIThread(() =>
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
                    BeginOnUIThread(() =>
                    {
                        Full.NotifySettings = message.NotifySettings;
                        Full.RaisePropertyChanged(() => Full.NotifySettings);
                        RaisePropertyChanged(() => IsMuted);

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

        public RelayCommand SendMessageCommand { get; }
        private void SendMessageExecute()
        {
            if (_item == null)
            {
                return;
            }

            NavigationService.NavigateToDialog(_item);
        }

        public RelayCommand MediaCommand { get; }
        private void MediaExecute()
        {
            if (_item == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(DialogSharedMediaPage), _item.ToInputPeer());


        }

        public RelayCommand CommonChatsCommand { get; }
        private void CommonChatsExecute()
        {
            if (_item == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(UserCommonChatsPage), _item.ToInputUser());
        }

        public RelayCommand SystemCallCommand { get; }
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

        public RelayCommand BlockCommand { get; }
        private async void BlockExecute()
        {
            if (Item is TLUser user)
            {
                var confirm = await TLMessageDialog.ShowAsync(Strings.Android.AreYouSureBlockContact, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                var result = await ProtoService.BlockAsync(user.ToInputUser());
                if (result.IsSucceeded && result.Result)
                {
                    if (Full is TLUserFull full)
                    {
                        full.IsBlocked = true;
                        full.RaisePropertyChanged(() => full.IsBlocked);
                    }

                    CacheService.Commit();
                    Aggregator.Publish(new TLUpdateUserBlocked { UserId = user.Id, Blocked = true });
                }
            }
        }

        public RelayCommand UnblockCommand { get; }
        private async void UnblockExecute()
        {
            if (Item is TLUser user)
            {
                var confirm = await TLMessageDialog.ShowAsync(Strings.Android.AreYouSureUnblockContact, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                var result = await ProtoService.UnblockAsync(user.ToInputUser());
                if (result.IsSucceeded && result.Result)
                {
                    if (Full is TLUserFull full)
                    {
                        full.IsBlocked = false;
                        full.RaisePropertyChanged(() => full.IsBlocked);
                    }

                    CacheService.Commit();
                    Aggregator.Publish(new TLUpdateUserBlocked { UserId = user.Id, Blocked = false });

                    if (user.IsBot)
                    {
                        NavigationService.GoBack();
                    }
                }
            }
        }

        public RelayCommand ReportCommand { get; }
        private async void ReportExecute()
        {
            var user = Item as TLUser;
            if (user != null)
            {
                var opt1 = new RadioButton { Content = Strings.Android.ReportChatSpam, Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
                var opt2 = new RadioButton { Content = Strings.Android.ReportChatViolence, Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
                var opt3 = new RadioButton { Content = Strings.Android.ReportChatPornography, Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
                var opt4 = new RadioButton { Content = Strings.Android.ReportChatOther, Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch, IsChecked = true };
                var stack = new StackPanel();
                stack.Children.Add(opt1);
                stack.Children.Add(opt2);
                stack.Children.Add(opt3);
                stack.Children.Add(opt4);
                stack.Margin = new Thickness(12, 16, 12, 0);

                var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
                dialog.Content = stack;
                dialog.Title = Strings.Android.ReportChat;
                dialog.IsPrimaryButtonEnabled = true;
                dialog.IsSecondaryButtonEnabled = true;
                dialog.PrimaryButtonText = Strings.Android.OK;
                dialog.SecondaryButtonText = Strings.Android.Cancel;

                var dialogResult = await dialog.ShowQueuedAsync();
                if (dialogResult == ContentDialogResult.Primary)
                {
                    var reason = opt1.IsChecked == true
                        ? new TLInputReportReasonSpam()
                        : (opt2.IsChecked == true
                            ? new TLInputReportReasonViolence()
                            : (opt3.IsChecked == true
                                ? new TLInputReportReasonPornography()
                                : (TLReportReasonBase)new TLInputReportReasonOther()));

                    if (reason is TLInputReportReasonOther other)
                    {
                        var input = new InputDialog();
                        input.Title = Strings.Android.ReportChat;
                        input.PlaceholderText = Strings.Android.ReportChatDescription;
                        input.IsPrimaryButtonEnabled = true;
                        input.IsSecondaryButtonEnabled = true;
                        input.PrimaryButtonText = Strings.Android.OK;
                        input.SecondaryButtonText = Strings.Android.Cancel;

                        var inputResult = await input.ShowQueuedAsync();
                        if (inputResult == ContentDialogResult.Primary)
                        {
                            other.Text = input.Text;
                        }
                        else
                        {
                            return;
                        }
                    }

                    var result = await ProtoService.ReportPeerAsync(user.ToInputPeer(), reason);
                    if (result.IsSucceeded && result.Result)
                    {
                        await new TLMessageDialog("Resources.ReportSpamNotification", "Unigram").ShowQueuedAsync();
                    }
                }
            }
        }

        public bool IsMuted
        {
            get
            {
                var notifySettings = _full?.NotifySettings as TLPeerNotifySettings;
                if (notifySettings == null)
                {
                    return false;
                }

                return notifySettings.IsMuted;
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

        public bool IsAddEnabled
        {
            get
            {
                return _item != null && (_item.HasAccessHash && _item.HasPhone && !_item.IsSelf && !_item.IsContact && !_item.IsMutualContact);
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

        public RelayCommand ToggleMuteCommand { get; }
        private async void ToggleMuteExecute()
        {
            if (_item == null || _full == null)
            {
                return;
            }

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
                    if (_item == null || _full == null)
                    {
                        return;
                    }

                    notifySettings.MuteUntil = muteUntil;
                    RaisePropertyChanged(() => IsMuted);
                    Full.RaisePropertyChanged(() => Full.NotifySettings);

                    var dialog = CacheService.GetDialog(_item.ToPeer());
                    if (dialog != null)
                    {
                        dialog.NotifySettings = _full.NotifySettings;
                        dialog.RaisePropertyChanged(() => dialog.NotifySettings);
                        dialog.RaisePropertyChanged(() => dialog.IsMuted);
                        dialog.RaisePropertyChanged(() => dialog.Self);
                    }

                    CacheService.Commit();
                }
            }
        }

        #region Call

        public RelayCommand CallCommand { get; }
        private async void CallExecute()
        {
            if (_item == null || _full == null)
            {
                return;
            }

            if (_full.IsPhoneCallsAvailable && !_item.IsSelf && ApiInformation.IsApiContractPresent("Windows.ApplicationModel.Calls.CallsVoipContract", 1))
            {
                try
                {
                    var coordinator = VoipCallCoordinator.GetDefault();
                    var result = await coordinator.ReserveCallResourcesAsync("Unigram.Tasks.VoIPCallTask");
                    if (result == VoipPhoneCallResourceReservationStatus.Success)
                    {
                        await VoIPConnection.Current.SendRequestAsync("voip.startCall", _item);
                    }
                }
                catch
                {
                    await TLMessageDialog.ShowAsync("Something went wrong. Please, try to close and relaunch the app.", "Unigram", "OK");
                }
            }
        }

        #endregion

        public RelayCommand AddCommand { get; }
        private async void AddExecute()
        {
            var user = _item as TLUser;
            if (user == null)
            {
                return;
            }

            var confirm = await EditUserNameView.Current.ShowAsync(user.FirstName, user.LastName);
            if (confirm == ContentDialogResult.Primary)
            {
                var contact = new TLInputPhoneContact
                {
                    ClientId = _item.Id,
                    FirstName = EditUserNameView.Current.FirstName,
                    LastName = EditUserNameView.Current.LastName,
                    Phone = _item.Phone
                };

                var response = await ProtoService.ImportContactsAsync(new TLVector<TLInputContactBase> { contact });
                if (response.IsSucceeded)
                {
                    if (response.Result.Users.Count > 0)
                    {
                        Aggregator.Publish(new TLUpdateContactLink
                        {
                            UserId = response.Result.Users[0].Id,
                            MyLink = new TLContactLinkContact(),
                            ForeignLink = new TLContactLinkUnknown()
                        });
                    }

                    user.RaisePropertyChanged(() => user.HasFirstName);
                    user.RaisePropertyChanged(() => user.HasLastName);
                    user.RaisePropertyChanged(() => user.FirstName);
                    user.RaisePropertyChanged(() => user.LastName);
                    user.RaisePropertyChanged(() => user.FullName);
                    user.RaisePropertyChanged(() => user.DisplayName);

                    user.RaisePropertyChanged(() => user.HasPhone);
                    user.RaisePropertyChanged(() => user.Phone);

                    RaisePropertyChanged(() => IsEditEnabled);
                    RaisePropertyChanged(() => IsAddEnabled);

                    var dialog = CacheService.GetDialog(_item.ToPeer());
                    if (dialog != null)
                    {
                        dialog.RaisePropertyChanged(() => dialog.With);
                    }
                }
            }
        }

        public RelayCommand EditCommand { get; }
        private async void EditExecute()
        {
            var user = _item as TLUser;
            if (user == null)
            {
                return;
            }

            var confirm = await EditUserNameView.Current.ShowAsync(user.FirstName, user.LastName);
            if (confirm != ContentDialogResult.Primary)
            {
                return; 
            }

            var contact = new TLInputPhoneContact
            {
                FirstName = EditUserNameView.Current.FirstName,
                LastName = EditUserNameView.Current.LastName,
                Phone = user.Phone
            };

            var response = await ProtoService.ImportContactsAsync(new TLVector<TLInputContactBase> { contact });
            if (response.IsSucceeded)
            {
                user.RaisePropertyChanged(() => user.HasFirstName);
                user.RaisePropertyChanged(() => user.HasLastName);
                user.RaisePropertyChanged(() => user.FirstName);
                user.RaisePropertyChanged(() => user.LastName);
                user.RaisePropertyChanged(() => user.FullName);
                user.RaisePropertyChanged(() => user.DisplayName);

                var dialog = CacheService.GetDialog(user.ToPeer());
                if (dialog != null)
                {
                    dialog.RaisePropertyChanged(() => dialog.With);
                }
            }
        }

        public RelayCommand DeleteCommand { get; }
        private async void DeleteExecute()
        {
            var user = _item as TLUser;
            if (user == null)
            {
                return;
            }

            var confirm = await TLMessageDialog.ShowAsync(Strings.Android.AreYouSureDeleteContact, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ProtoService.DeleteContactAsync(user.ToInputUser());
            if (response.IsSucceeded)
            {
                // TODO: delete from synced contacts

                Aggregator.Publish(new TLUpdateContactLink
                {
                    UserId = response.Result.User.Id,
                    MyLink = response.Result.MyLink,
                    ForeignLink = response.Result.ForeignLink
                });

                user.RaisePropertyChanged(() => user.HasFirstName);
                user.RaisePropertyChanged(() => user.HasLastName);
                user.RaisePropertyChanged(() => user.FirstName);
                user.RaisePropertyChanged(() => user.LastName);
                user.RaisePropertyChanged(() => user.FullName);
                user.RaisePropertyChanged(() => user.DisplayName);

                user.RaisePropertyChanged(() => user.HasPhone);
                user.RaisePropertyChanged(() => user.Phone);

                RaisePropertyChanged(() => IsEditEnabled);
                RaisePropertyChanged(() => IsAddEnabled);

                var dialog = CacheService.GetDialog(_item.ToPeer());
                if (dialog != null)
                {
                    dialog.RaisePropertyChanged(() => dialog.With);
                }
            }
        }
    }
}
