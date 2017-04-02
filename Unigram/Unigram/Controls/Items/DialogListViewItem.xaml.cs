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
using Unigram.Common;
using Unigram.Converters;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization.DateTimeFormatting;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Items
{
    public sealed partial class DialogListViewItem : HackUserControl
    {
        public TLDialog ViewModel => DataContext as TLDialog;
        private TLDialog _oldViewModel;
        private TLDialog _oldValue;

        public DialogListViewItem()
        {
            InitializeComponent();

            DataContextChanged += (s, args) =>
            {
                if (ViewModel != null && ViewModel != _oldValue) Bindings.Update();
                if (ViewModel == null) Bindings.StopTracking();

                _oldValue = ViewModel;
            };

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

                FromLabel.Text = UpdateFromLabel(ViewModel);
                BriefLabel.Text = UpdateBriefLabel(ViewModel);
                UpdateTimeLabel();
                UpdateStateIcon();
                UpdateUnreadCount();
                UpdatePicture();
                UpdateChannelType();
            }
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Self")
            {
                FromLabel.Text = UpdateFromLabel(ViewModel);
                BriefLabel.Text = UpdateBriefLabel(ViewModel);
                UpdateTimeLabel();
                UpdateStateIcon();
                UpdateUnreadCount();
            }
            else if (e.PropertyName == "TopMessageItem")
            {
                FromLabel.Text = UpdateFromLabel(ViewModel);
                BriefLabel.Text = UpdateBriefLabel(ViewModel);
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

        private Visibility UpdateIsPinned(bool isPinned, int unreadCount)
        {
            return isPinned && unreadCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdatePicture()
        {
            //Placeholder.Fill = Application.Current.Resources[$"Placeholder{Utils.GetColorIndex(ViewModel.WithId)}ImageBrush"] as ImageBrush;

            //Placeholder.Fill = BindConvert.Current.Bubble(ViewModel.WithId);
        }

        private string UpdateBriefLabel(TLDialog dialog)
        {
            var topMessage = ViewModel?.TopMessageItem as TLMessageBase;
            if (topMessage != null)
            {
                var message = topMessage as TLMessageCommonBase;
                if (message != null)
                {
                    return GetBriefLabel(message, true);
                }
            }

            return string.Empty;
        }

        private string GetBriefLabel(TLMessageBase value, bool showContent)
        {
            var draft = ViewModel.Draft as TLDraftMessage;
            if (draft != null)
            {
                return draft.Message;
            }

            if (value is TLMessageEmpty messageEmpty)
            {
                return "Resources.EmptyMessage";
            }

            if (value is TLMessageService messageService)
            {
                return string.Empty;
            }

            if (value is TLMessage message)
            {
                var result = string.Empty;
                if (message.Media != null)
                {
                    if (message.Media is TLMessageMediaDocument documentMedia)
                    {
                        if (string.IsNullOrEmpty(documentMedia.Caption))
                        {
                            return result;
                        }

                        return result + documentMedia.Caption.Replace("\r\n", "\n").Replace("\n", " ");
                    }
                    else if (message.Media is TLMessageMediaPhoto photoMedia)
                    {
                        if (string.IsNullOrEmpty(photoMedia.Caption))
                        {
                            return result;
                        }

                        return result + photoMedia.Caption.Replace("\r\n", "\n").Replace("\n", " ");
                    }
                }

                if (message.Message != null)
                {
                    if (showContent)
                    {
                        return result + message.Message.Replace("\r\n", "\n").Replace("\n", " ");
                    }

                    //return text + Resources.Message;
                    return result + "Message";
                }
            }

            return string.Empty;
        }

        private string UpdateFromLabel(TLDialog dialog)
        {
            if (dialog.Draft is TLDraftMessage draft)
            {
                FromLabel.Foreground = Application.Current.Resources["TelegramDialogLabelDraftBrush"] as SolidColorBrush;
                return "Draft: ";
            }

            if (dialog.TopMessageItem is TLMessage message)
            {
                var result = string.Empty;

                if (message.ShowFrom)
                {
                    FromLabel.Foreground = Application.Current.Resources["TelegramDialogLabelFromBrush"] as SolidColorBrush;

                    var from = message.FromId;
                    if (from != null)
                    {
                        int currentUserId = MTProtoService.Current.CurrentUserId;
                        if (currentUserId == from)
                        {
                            if (dialog.Id != from && !message.IsPost)
                            {
                                result = "You: ";
                            }
                        }
                        else
                        {
                            if (InMemoryCacheService.Current.GetUser(from.Value) is TLUser user)
                            {
                                if (user.HasFirstName)
                                {
                                    result = $"{user.FirstName.Trim()}: ";
                                }
                                else if (user.HasLastName)
                                {
                                    result = $"{user.LastName.Trim()}: ";
                                }
                                else if (user.HasUsername)
                                {
                                    result = $"{user.Username.Trim()}: ";
                                }
                                else
                                {
                                    result = $"{user.Id}: ";
                                }
                            }
                        }
                    }
                }

                if (message.State == TLMessageState.Failed && message.IsOut)
                {
                    result = "Failed: ";
                }

                if (message.Media != null)
                {
                    if (message.Media is TLMessageMediaGame gameMedia)
                    {
                        return result + "\uD83C\uDFAE " + gameMedia.Game.Title;
                    }
                    else if (message.Media is TLMessageMediaDocument documentMedia)
                    {
                        var caption = string.Empty;
                        if (!string.IsNullOrEmpty(documentMedia.Caption))
                        {
                            caption = ", ";
                        }

                        if (message.IsVoice())
                        {
                            return result + "Voice" + caption;
                        }
                        else if (message.IsVideo())
                        {
                            return result + "Video" + caption;
                        }
                        else if (message.IsGif())
                        {
                            return result + "GIF" + caption;
                        }
                        else if (message.IsSticker())
                        {
                            if (documentMedia.Document is TLDocument documentSticker)
                            {
                                var attribute = documentSticker.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                                if (attribute != null)
                                {
                                    return result + $"{attribute.Alt} Sticker";
                                }
                            }

                            return result + "Sticker";
                        }
                        else if (message.IsAudio())
                        {
                            if (documentMedia.Document is TLDocument documentAudio)
                            {
                                var audioAttribute = documentAudio.Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
                                if (audioAttribute != null)
                                {
                                    if (audioAttribute.HasPerformer && audioAttribute.HasTitle)
                                    {
                                        return $"{result}{audioAttribute.Performer} - {audioAttribute.Title}";
                                    }
                                    else if (audioAttribute.HasPerformer && !audioAttribute.HasTitle)
                                    {
                                        return $"{result}{audioAttribute.Performer} - Unknown Track";
                                    }
                                    else if (audioAttribute.HasTitle && !audioAttribute.HasPerformer)
                                    {
                                        return $"{result}{audioAttribute.Title}";
                                    }
                                }
                            }
                        }

                        if (documentMedia.Document is TLDocument document)
                        {
                            var attribute = document.Attributes.OfType<TLDocumentAttributeFilename>().FirstOrDefault();
                            if (attribute != null)
                            {
                                //return $"{text}{attribute.Alt} ({Resources.Sticker.ToLower()})";
                                return result + attribute.FileName + caption;
                            }
                        }

                        return result + "Document" + caption;
                    }
                    else if (message.Media is TLMessageMediaInvoice invoiceMedia)
                    {
                        return result + invoiceMedia.Title;
                    }
                    else if (message.Media is TLMessageMediaContact)
                    {
                        return result + "Contact";
                    }
                    else if (message.Media is TLMessageMediaGeo)
                    {
                        return result + "GeoPoint";
                    }
                    else if (message.Media is TLMessageMediaVenue)
                    {
                        return result + "Venue";
                    }
                    else if (message.Media is TLMessageMediaPhoto photoMedia)
                    {
                        if (string.IsNullOrEmpty(photoMedia.Caption))
                        {
                            return result + "Photo";
                        }

                        return result + "Photo, ";
                    }
                    else if (message.Media is TLMessageMediaUnsupported)
                    {
                        return result + "Unsupported media";
                    }
                }

                return result;
            }
            else if (dialog.TopMessageItem is TLMessageService messageService)
            {
                FromLabel.Foreground = Application.Current.Resources["TelegramDialogLabelFromBrush"] as SolidColorBrush;
                return ServiceHelper.Convert(messageService);
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
                    int currentUserId = MTProtoService.Current.CurrentUserId;
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
                var clientDelta = MTProtoService.Current.ClientTicksDelta;
                var utc0SecsLong = topMessage.Date * 4294967296 - clientDelta;
                var utc0SecsInt = utc0SecsLong / 4294967296.0;
                var dateTime = Utils.UnixTimestampToDateTime(utc0SecsInt);

                var cultureInfo = (CultureInfo)CultureInfo.CurrentUICulture.Clone();
                var shortTimePattern = Utils.GetShortTimePattern(ref cultureInfo);

                //Today
                if (dateTime.Date == DateTime.Now.Date)
                {
                    //TimeLabel.Text = dateTime.ToString(string.Format("{0}", shortTimePattern), cultureInfo);
                    TimeLabel.Text = BindConvert.Current.ShortTime.Format(dateTime);
                    return;
                }

                //Week
                if (dateTime.Date.AddDays(7) >= DateTime.Now.Date)
                {
                    TimeLabel.Text = dateTime.ToString(string.Format("ddd", shortTimePattern), cultureInfo);
                    return;
                }

                //Long long time ago
                //TimeLabel.Text = dateTime.ToString(string.Format("d.MM.yyyy", shortTimePattern), cultureInfo);
                TimeLabel.Text = BindConvert.Current.ShortDate.Format(dateTime);
            }
            else
            {
                TimeLabel.Text = string.Empty;
            }
        }

        private void UpdateUnreadCount()
        {
            UnreadBadge.Text = ViewModel?.UnreadCount.ToString() ?? string.Empty;
            UnreadLabel.Visibility = ViewModel?.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility UpdateUnreadBadgeBrush(TLPeerNotifySettingsBase settingsBase)
        {
            var settings = settingsBase as TLPeerNotifySettings;
            if (settings != null)
            {
                return settings.MuteUntil == 0 && !settings.IsSilent ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        private Visibility UpdateUnreadBadgeMutedBrush(TLPeerNotifySettingsBase settingsBase)
        {
            var settings = settingsBase as TLPeerNotifySettings;
            if (settings != null)
            {
                return settings.MuteUntil == 0 && !settings.IsSilent ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        private void UpdateChannelType()
        {
            if (ViewModel != null)
            {
                var channel = ViewModel.With as TLChannel;
                if (channel != null)
                {
                    fiType.Glyph = channel.IsBroadcast ? "\uE789" : "\uE125";
                }
            }
        }
    }

    public class HackUserControl : UserControl
    {
        /// <summary>
        /// x:Bind hack
        /// </summary>
        public new event TypedEventHandler<FrameworkElement, object> Loading;
    }
}
