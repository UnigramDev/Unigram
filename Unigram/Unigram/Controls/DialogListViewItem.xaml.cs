using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls
{
    public sealed partial class DialogListViewItem : UserControl
    {
        public TLDialog ViewModel => DataContext as TLDialog;
        private TLDialog _oldViewModel;

        public DialogListViewItem()
        {
            InitializeComponent();

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (_oldViewModel != null)
            {
                _oldViewModel.PropertyChanged -= OnPropertyChanged;
                _oldViewModel = null;
            }

            if (ViewModel != null)
            {
                _oldViewModel = ViewModel;
                ViewModel.PropertyChanged += OnPropertyChanged;

                UpdateSnippet();
                UpdateTimeLabel();
                UpdateStateIcon();
                UpdateUnreadCount();
                UpdatePicture();
            }
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Self")
            {
                UpdateSnippet();
                UpdateTimeLabel();
                UpdateStateIcon();
                UpdateUnreadCount();
            }
            else if (e.PropertyName == "TopMessageItem")
            {
                UpdateSnippet();
                UpdateTimeLabel();
                UpdateStateIcon();
                UpdateUnreadCount();
            }
            else if (e.PropertyName == "UnreadCount")
            {
                UpdateUnreadCount();
            }
            else if (e.PropertyName == "With")
            {
                UpdatePicture();
            }
        }

        private void UpdatePicture()
        {
            switch (GetColorIndex(ViewModel.WithId))
            {
                case 0:
                    Placeholder.Fill = Application.Current.Resources["RedBrush"] as SolidColorBrush;
                    break;
                case 1:
                    Placeholder.Fill = Application.Current.Resources["GreenBrush"] as SolidColorBrush;
                    break;
                case 2:
                    Placeholder.Fill = Application.Current.Resources["YellowBrush"] as SolidColorBrush;
                    break;
                case 3:
                    Placeholder.Fill = Application.Current.Resources["BlueBrush"] as SolidColorBrush;
                    break;
                case 4:
                    Placeholder.Fill = Application.Current.Resources["PurpleBrush"] as SolidColorBrush;
                    break;
                case 5:
                    Placeholder.Fill = Application.Current.Resources["PinkBrush"] as SolidColorBrush;
                    break;
                case 6:
                    Placeholder.Fill = Application.Current.Resources["CyanBrush"] as SolidColorBrush;
                    break;
                case 7:
                    Placeholder.Fill = Application.Current.Resources["OrangeBrush"] as SolidColorBrush;
                    break;
                default:
                    Placeholder.Fill = Application.Current.Resources["ListViewItemPlaceholderBackgroundThemeBrush"] as SolidColorBrush;
                    break;
            }

            if (ViewModel.FullName.Length > 0) //TESTING, a better one is incoming....
            {
                foreach (var item in ViewModel.FullName.Split(' '))
                {
                    if (InitialName.Text.Length >= 2) break;
                    InitialName.Text += item[0];
                }
            }
            else //This mean the account is deleted
            {
                InitialName.Text = "\\";
            }
        }

        private int GetColorIndex(int id)
        {
            if (id < 0)
            {
                id += 256;
            }

            try
            {
                var str = string.Format("{0}{1}", id, MTProtoService.Instance.CurrentUserId);
                if (str.Length > 15)
                {
                    str = str.Substring(0, 15);
                }

                var input = CryptographicBuffer.ConvertStringToBinary(str, BinaryStringEncoding.Utf8);
                var hasher = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
                var hashed = hasher.HashData(input);
                byte[] digest;
                CryptographicBuffer.CopyToByteArray(hashed, out digest);

                var boh = ((id & 0x300000000) == 0x300000000);

                return digest[id % 0x0F] & ((ViewModel.With is TLPeerUser) ? 0x07 : 0x03);
            }
            catch { }

            return id % 8;
        }

        private void UpdateSnippet()
        {
            var topMessage = ViewModel?.TopMessageItem as TLMessageBase;
            if (topMessage != null)
            {
                var message = topMessage as TLMessage;
                if (message != null)
                {
                    FromLabel.Text = GetFromLabel(message);
                }

                BriefLabel.Text = GetBriefLabel(topMessage, true);
            }
        }

        private string GetBriefLabel(TLMessageBase value, bool showContent)
        {
            var draft = ViewModel.Draft as TLDraftMessage;
            if (draft != null)
            {
                return draft.Message;
            }

            var messageEmpty = value as TLMessageEmpty;
            if (messageEmpty != null)
            {
                return "Resources.EmptyMessage";
            }

            var messageService = value as TLMessageService;
            if (messageService != null)
            {
                // TODO: return ServiceMessageToTextConverter.Convert(messageService);
            }

            var message = value as TLMessage;
            if (message != null)
            {
                var text = string.Empty;
                if (message.State == TLMessageState.Failed)
                {
                    //text = string.Format("{0}: ", Resources.SendingFailed);
                    //text = $"{Resources.SendingFailed}: ";
                    text = "Failed: ";
                }

                if (message.Media != null)
                {
                    if (message.Media is TLMessageMediaDocument)
                    {
                        if (message.IsVoice())
                        {
                            return text + "Voice";
                        }
                        else if (message.IsVideo())
                        {
                            return text + "Video";
                        }
                        else if (message.IsGif())
                        {
                            return text + "GIF";
                        }
                        else if (message.IsSticker())
                        {
                            var documentSticker = (message.Media as TLMessageMediaDocument).Document as TLDocument;
                            if (documentSticker != null)
                            {
                                var attribute = documentSticker.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                                if (attribute != null)
                                {
                                    //return $"{text}{attribute.Alt} ({Resources.Sticker.ToLower()})";
                                    return $"{text}{attribute.Alt} (sticker)";
                                }
                            }

                            //return text + Resources.Sticker;
                            return text + "Sticker";
                        }

                        var document = (message.Media as TLMessageMediaDocument).Document as TLDocument;
                        if (document != null)
                        {
                            var attribute = document.Attributes.OfType<TLDocumentAttributeFilename>().FirstOrDefault();
                            if (attribute != null)
                            {
                                //return $"{text}{attribute.Alt} ({Resources.Sticker.ToLower()})";
                                return text + attribute.FileName;
                            }
                        }

                        //return text + Resources.Document;
                        return text + "Document";
                    }
                    else
                    {
                        if (message.Media is TLMessageMediaContact)
                        {
                            //return text + Resources.Contact;
                            return text + "Contact";
                        }
                        else if (message.Media is TLMessageMediaGeo)
                        {
                            //return text + Resources.GeoPoint;
                            return text + "GeoPoint";
                        }
                        else if (message.Media is TLMessageMediaPhoto)
                        {
                            if (!string.IsNullOrEmpty(message.Media.Caption))
                            {
                                return text + message.Media.Caption;
                            }

                            //return text + Resources.Photo;
                            return text + "Photo";
                        }
                        //else if (message.Media is TLMessageMediaVideo)
                        //{
                        //    return text + Resources.Video;
                        //}
                        //else if (message.Media is TLMessageMediaAudio)
                        //{
                        //    return text + Resources.Audio;
                        //}
                        else if (message.Media is TLMessageMediaUnsupported)
                        {
                            //return text + Resources.UnsupportedMedia;
                            return text + "Unsupported media";
                        }
                    }
                }

                if (message.Message != null)
                {
                    if (showContent)
                    {
                        return text + message.Message;
                    }

                    //return text + Resources.Message;
                    return text + "Message";
                }
            }

            return string.Empty;
        }

        private string GetFromLabel(TLMessage message)
        {
            var draft = ViewModel.Draft as TLDraftMessage;
            if (draft != null)
            {
                FromLabel.Foreground = Application.Current.Resources["TelegramDialogLabelDraftBrush"] as SolidColorBrush;
                return "Draft: ";
            }

            if (message.ShowFrom || IsOut(ViewModel))
            {
                FromLabel.Foreground = Application.Current.Resources["TelegramDialogLabelFromBrush"] as SolidColorBrush;

                var from = message.FromId;
                if (from != null)
                {
                    int currentUserId = MTProtoService.Instance.CurrentUserId;
                    if (currentUserId == from)
                    {
                        return "You: ";
                        //return Resources.You + ": ";
                    }
                    else
                    {
                        var user = InMemoryCacheService.Instance.GetUser(from.Value) as TLUser;
                        if (user != null)
                        {
                            return $"{user.FirstName.Trim()}: ";
                        }
                    }
                }
            }

            return string.Empty;
        }

        private bool IsOut(TLDialog dialog)
        {
            var topMessage = dialog.TopMessageItem as TLMessage;
            if (topMessage != null /*&& topMessage.ShowFrom*/)
            {
                var from = topMessage.FromId;
                if (from != null)
                {
                    int currentUserId = MTProtoService.Instance.CurrentUserId;
                    if (currentUserId == from.Value)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void UpdateStateIcon()
        {
            var draft = ViewModel.Draft as TLDraftMessage;
            if (draft != null)
            {
                StateIcon.Visibility = Visibility.Collapsed;
                return;
            }

            var topMessage = ViewModel?.TopMessageItem as TLMessage;
            if (topMessage != null)
            {
                if (topMessage.IsOut && IsOut(ViewModel))
                {
                    StateIcon.Visibility = Visibility.Visible;

                    switch (topMessage.State)
                    {
                        case TLMessageState.Sending:
                            StateIcon.Glyph = "\uE600";
                            break;
                        case TLMessageState.Confirmed:
                            StateIcon.Glyph = "\uE602";
                            break;
                        case TLMessageState.Read:
                            StateIcon.Glyph = "\uE601";
                            break;
                    }
                }
                else
                {
                    StateIcon.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                StateIcon.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateTimeLabel()
        {
            var topMessage = ViewModel?.TopMessageItem as TLMessageBase;
            if (topMessage != null)
            {
                var clientDelta = MTProtoService.Instance.ClientTicksDelta;
                var utc0SecsLong = topMessage.Date * 4294967296 - clientDelta;
                var utc0SecsInt = utc0SecsLong / 4294967296.0;
                var dateTime = Utils.UnixTimestampToDateTime(utc0SecsInt);

                var cultureInfo = (CultureInfo)CultureInfo.CurrentUICulture.Clone();
                var shortTimePattern = Utils.GetShortTimePattern(ref cultureInfo);

                //Today
                if (dateTime.Date == DateTime.Now.Date)
                {
                    TimeLabel.Text = dateTime.ToString(string.Format("{0}", shortTimePattern), cultureInfo);
                    return;
                }

                //Week
                if (dateTime.Date.AddDays(7) >= DateTime.Now.Date)
                {
                    TimeLabel.Text = dateTime.ToString(string.Format("ddd", shortTimePattern), cultureInfo);
                    return;
                }

                //Long time ago (no more than one year ago)
                if (dateTime.Date.AddDays(365) >= DateTime.Now.Date)
                {
                    TimeLabel.Text = dateTime.ToString(string.Format("d MMM", shortTimePattern), cultureInfo);
                    return;
                }

                //Long long time ago
                TimeLabel.Text = dateTime.ToString(string.Format("d.MM.yyyy", shortTimePattern), cultureInfo);
            }
        }

        private void UpdateUnreadCount()
        {
            UnreadLabel.Visibility = ViewModel?.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            Highlight.Visibility = ViewModel?.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Context menu
        private static MenuFlyout _menuFlyout = new MenuFlyout();
        private static MenuFlyoutItem _menuItemClearHistory;
        private static MenuFlyoutItem _menuItemDeleteDialog;
        private static MenuFlyoutItem _menuItemDeleteAndStop;
        private static MenuFlyoutItem _menuItemDeleteAndExit;
        private static MenuFlyoutItem _menuItemPinToStart;

        protected override void OnRightTapped(RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType != PointerDeviceType.Touch)
            {
                UpdateContextMenu();
                _menuFlyout.ShowAt(this, e.GetPosition(this));
                e.Handled = true;
            }

            base.OnRightTapped(e);
        }

        protected override void OnHolding(HoldingRoutedEventArgs e)
        {
            if (e.PointerDeviceType == PointerDeviceType.Touch && e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                UpdateContextMenu();
                _menuFlyout.ShowAt(this);
                e.Handled = true;
            }

            base.OnHolding(e);
        }

        private void UpdateContextMenu()
        {
            FlyoutBase.SetAttachedFlyout(this, _menuFlyout);

            var canClearHistory = CanClearHistory(ref _menuItemClearHistory);
            var canDeleteDialog = CanDeleteDialog(ref _menuItemDeleteDialog);
            var canDeleteAndStop = CanDeleteAndStop(ref _menuItemDeleteAndStop);
            var canDeleteAndExit = CanDeleteAndExit(ref _menuItemDeleteAndExit);
            var canPinToStart = CanPinToStart(ref _menuItemPinToStart);

            AddMenuItem(canClearHistory, _menuFlyout, ref _menuItemClearHistory, "Clear History", null);
            AddMenuItem(canDeleteDialog, _menuFlyout, ref _menuItemDeleteDialog, "Delete Dialog", null);
            AddMenuItem(canDeleteAndStop, _menuFlyout, ref _menuItemDeleteAndStop, "Delete and Stop", null);
            AddMenuItem(canDeleteAndExit, _menuFlyout, ref _menuItemDeleteAndExit, "Delete and Exit", null);
            AddMenuItem(canPinToStart, _menuFlyout, ref _menuItemPinToStart, "Pin to Start", null);
        }

        private bool CanClearHistory(ref MenuFlyoutItem menuItem)
        {
            if (ViewModel != null)
            {
                var peerChannel = ViewModel.Peer as TLPeerChannel;
                return peerChannel == null;
            }

            return false;
        }

        private bool CanDeleteDialog(ref MenuFlyoutItem menuItem)
        {
            if (ViewModel != null)
            {
                var peerChannel = ViewModel.Peer as TLPeerChannel;
                if (peerChannel != null)
                {
                    var channel = ViewModel.With as TLChannel;
                    if (channel != null)
                    {
                        if (channel.IsCreator)
                        {
                            menuItem.Text = channel.IsMegagroup ? "AppResources.DeleteGroup" : "AppResources.DeleteChannel";
                        }
                        else
                        {
                            menuItem.Text = channel.IsMegagroup ? "AppResources.LeaveGroup" : "AppResources.LeaveChannel";
                        }
                    }

                    return true;
                }

                var peerUser = ViewModel.Peer as TLPeerUser;
                if (peerUser != null)
                {
                    return true;
                }

                var peerChat = ViewModel.Peer as TLPeerChat;
                if (peerChat != null)
                {
                    return ViewModel.With is TLChatForbidden || ViewModel.With is TLChatEmpty;
                }
            }

            return false;
        }

        private bool CanDeleteAndStop(ref MenuFlyoutItem menuItem)
        {
            if (ViewModel != null)
            {
                var user = ViewModel.With as TLUser;
                return user != null && user.IsBot;
                //menuItem.set_Visibility((user != null && user.IsBot && (user.Blocked == null || !user.Blocked)) ? 0 : 1);
            }

            return false;
        }

        private bool CanDeleteAndExit(ref MenuFlyoutItem menuItem)
        {
            if (ViewModel != null)
            {
                var peerChat = ViewModel.Peer as TLPeerChat;
                if (peerChat != null)
                {
                    return true;
                }

                //var peerEncryptedChat = tLDialogBase.Peer as TLPeerEncryptedChat;
                //if (peerEncryptedChat != null)
                //{
                //    menuItem.Header = AppResources.DeleteChat.ToLowerInvariant();
                //    menuItem.set_Visibility(0);
                //    return;
                //}
            }

            return false;
        }

        private bool CanPinToStart(ref MenuFlyoutItem menuItem)
        {
            // TODO:
            return true;
        }

        private void AddMenuItem(bool enabled, MenuFlyout menu, ref MenuFlyoutItem menuItem, string header, RoutedEventHandler handler)
        {
            if (!enabled)
            {
                if (menuItem != null)
                {
                    menuItem.Visibility = Visibility.Collapsed;
                }

                return;
            }

            if (menuItem == null)
            {
                menuItem = new MenuFlyoutItem();
                menuItem.Text = header;
                menuItem.Click += handler;
                menu.Items.Add(menuItem);
            }

            menuItem.Visibility = Visibility.Visible;
        }
        #endregion
    }
}
