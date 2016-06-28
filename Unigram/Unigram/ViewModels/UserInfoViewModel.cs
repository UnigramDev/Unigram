﻿using System;
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

namespace Unigram.ViewModels
{
    public class UserInfoViewModel : UnigramViewModelBase,
        IHandle<TLUpdateUserBlocked>,
        IHandle<TLUpdateNotifySettings>,
        IHandle
    {
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
            var user = parameter as TLUser;
            if (user != null)
            {
                Item = user;
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

        #region Bot
        public Visibility AboutVisibility
        {
            get
            {
                var user = Item as TLUser;
                if (user != null && user.IsBot && Full != null && Full.BotInfo.Description != null)
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
