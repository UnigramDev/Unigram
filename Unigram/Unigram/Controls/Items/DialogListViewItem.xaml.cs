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

                Photo.Visibility = ViewModel.With is TLUser user1 && user1.IsSelf ? Visibility.Collapsed : Visibility.Visible;
                SavedMessages.Visibility = ViewModel.With is TLUser user2 && user2.IsSelf ? Visibility.Visible : Visibility.Collapsed;

                FromLabel.Text = UpdateFromLabel(ViewModel);
                DraftLabel.Text = UpdateDraftLabel(ViewModel);
                BriefLabel.Text = UpdateBriefLabel(ViewModel);
                UpdateTimeLabel();
                //UpdateStateIcon();
                UpdateUnreadCount();
                UpdateUnreadMentionsCount();
                UpdatePicture();
                UpdateChannelType();
            }
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Self")
            {
                FromLabel.Text = UpdateFromLabel(ViewModel);
                DraftLabel.Text = UpdateDraftLabel(ViewModel);
                BriefLabel.Text = UpdateBriefLabel(ViewModel);
                UpdateTimeLabel();
                //UpdateStateIcon();
                UpdateUnreadCount();
                UpdateUnreadMentionsCount();
            }
            else if (e.PropertyName == "TopMessageItem")
            {
                FromLabel.Text = UpdateFromLabel(ViewModel);
                DraftLabel.Text = UpdateDraftLabel(ViewModel);
                BriefLabel.Text = UpdateBriefLabel(ViewModel);
                UpdateTimeLabel();
                //UpdateStateIcon();
                UpdateUnreadCount();
                UpdateUnreadMentionsCount();
            }
            else if (e.PropertyName == "UnreadCount")
            {
                UpdateUnreadCount();
            }
            else if (e.PropertyName == "UnreadMentionsCount")
            {
                UpdateUnreadMentionsCount();
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
            if (ViewModel.Draft is TLDraftMessage draft && !string.IsNullOrWhiteSpace(draft.Message))
            {
                return draft.Message;
            }

            if (value is TLMessageEmpty messageEmpty)
            {
                return string.Empty;
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
                        if (string.IsNullOrEmpty(documentMedia.Caption) || message.IsRoundVideo())
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
                    else if (message.Media is TLMessageMediaVenue venueMedia)
                    {
                        return result + venueMedia.Title;
                    }
                    else if (message.Media is TLMessageMediaGame || message.Media is TLMessageMediaGeoLive)
                    {
                        return string.Empty;
                    }
                }

                if (message.Message != null)
                {
                    if (showContent)
                    {
                        return result + message.Message.Replace("\r\n", "\n").Replace("\n", " ");
                    }

                    return result + Strings.Android.Message;
                }
            }

            return string.Empty;
        }

        private string UpdateDraftLabel(TLDialog dialog)
        {
            if (dialog.Draft is TLDraftMessage draft && !string.IsNullOrWhiteSpace(draft.Message))
            {
                return $"{Strings.Android.Draft}: ";
            }

            return string.Empty;
        }

        private string UpdateFromLabel(TLDialog dialog)
        {
            if (dialog.Draft is TLDraftMessage draft && !string.IsNullOrWhiteSpace(draft.Message))
            {
                return string.Empty;
            }

            if (dialog.TopMessageItem is TLMessage message)
            {
                var result = string.Empty;

                if (message.ShowFrom)
                {
                    var from = message.FromId;
                    if (from != null)
                    {
                        if (message.IsOut)
                        {
                            if (dialog.Id != from && !message.IsPost)
                            {
                                result = $"{Strings.Android.FromYou}: ";
                            }
                        }
                        else if (message.From is TLUser user)
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
                            else if (user.IsDeleted)
                            {
                                result = $"{Strings.Android.HiddenName}: ";
                            }
                            else
                            {
                                result = $"{user.Id}: ";
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
                        if (documentMedia.HasTTLSeconds && (documentMedia.Document is TLDocumentEmpty || !documentMedia.HasDocument))
                        {
                            return result + Strings.Android.AttachVideoExpired;
                        }
                        else if (message.IsRoundVideo())
                        {
                            return result + Strings.Android.AttachRound;
                        }
                        else if (message.IsSticker())
                        {
                            if (documentMedia.Document is TLDocument documentSticker)
                            {
                                var attribute = documentSticker.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                                if (attribute != null)
                                {
                                    return result + $"{attribute.Alt} {Strings.Android.AttachSticker}";
                                }
                            }

                            return result + Strings.Android.AttachSticker;
                        }

                        var caption = string.Empty;
                        if (!string.IsNullOrEmpty(documentMedia.Caption))
                        {
                            caption = ", ";
                        }

                        if (message.IsVoice())
                        {
                            return result + Strings.Android.AttachAudio + caption;
                        }
                        else if (message.IsVideo())
                        {
                            return result + Strings.Android.AttachVideo + caption;
                        }
                        else if (message.IsGif())
                        {
                            return result + Strings.Android.AttachGif + caption;
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
                                        return $"{result}{audioAttribute.Performer} - {audioAttribute.Title}" + caption;
                                    }
                                    else if (audioAttribute.HasPerformer && !audioAttribute.HasTitle)
                                    {
                                        return $"{result}{audioAttribute.Performer} - {Strings.Android.AudioUnknownTitle}" + caption;
                                    }
                                    else if (audioAttribute.HasTitle && !audioAttribute.HasPerformer)
                                    {
                                        return $"{result}{Strings.Android.AudioUnknownArtist} - {audioAttribute.Title}" + caption;
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
                                return result + document.FileName + caption;
                            }
                        }

                        return result + Strings.Android.AttachDocument + caption;
                    }
                    else if (message.Media is TLMessageMediaInvoice invoiceMedia)
                    {
                        return result + invoiceMedia.Title;
                    }
                    else if (message.Media is TLMessageMediaContact)
                    {
                        return result + Strings.Android.AttachContact;
                    }
                    else if (message.Media is TLMessageMediaGeo)
                    {
                        return result + Strings.Android.AttachLocation;
                    }
                    else if (message.Media is TLMessageMediaGeoLive)
                    {
                        return result + Strings.Android.AttachLiveLocation;
                    }
                    else if (message.Media is TLMessageMediaVenue)
                    {
                        return result + $"{Strings.Android.AttachLocation}, ";
                    }
                    else if (message.Media is TLMessageMediaPhoto photoMedia)
                    {
                        if (photoMedia.HasTTLSeconds && (photoMedia.Photo is TLPhotoEmpty || !photoMedia.HasPhoto))
                        {
                            return result + Strings.Android.AttachPhotoExpired;
                        }

                        if (string.IsNullOrEmpty(photoMedia.Caption))
                        {
                            return result + Strings.Android.AttachPhoto;
                        }

                        return result + $"{Strings.Android.AttachPhoto}, ";
                    }
                    else if (message.Media is TLMessageMediaUnsupported)
                    {
                        return result + Strings.Android.UnsupportedAttachment;
                    }
                }

                return result;
            }
            else if (dialog.TopMessageItem is TLMessageService messageService)
            {
                return ServiceHelper.Convert(messageService);
            }

            return string.Empty;
        }

        private bool IsOut(TLDialog dialog)
        {
            var topMessage = dialog.TopMessageItem as TLMessage;
            //if (topMessage != null /*&& topMessage.ShowFrom*/)
            //{
            //    var from = topMessage.FromId;
            //    if (from != null)
            //    {
            //        int currentUserId = MTProtoService.Current.CurrentUserId;
            //        if (currentUserId == from.Value)
            //        {
            //            return true;
            //        }
            //    }
            //}

            if (topMessage != null && topMessage.From is TLUser from && from.IsSelf)
            {
                return true;
            }

            return false;
        }

        private string UpdateStateIcon(TLDraftMessageBase draft, TLMessageBase message, TLMessageState state)
        {
            if (draft is TLDraftMessage)
            {
                return string.Empty;
            }

            if (message is TLMessage topMessage && topMessage.IsOut && IsOut(ViewModel))
            {
                if (topMessage.Parent is TLUser user && user.IsSelf)
                {
                    return state == TLMessageState.Sending ? "\uE600" : string.Empty;
                }

                switch (state)
                {
                    case TLMessageState.Sending:
                        return "\uE600";
                    case TLMessageState.Confirmed:
                        return "\uE602";
                    case TLMessageState.Read:
                        return "\uE601";
                }
            }

            return string.Empty;
        }

        private void UpdateTimeLabel()
        {
            var topMessage = ViewModel?.TopMessageItem as TLMessageBase;
            if (topMessage != null)
            {
                TimeLabel.Text = BindConvert.Current.DateExtended(topMessage.Date);
            }
            else
            {
                TimeLabel.Text = string.Empty;
            }
        }

        private void UpdateUnreadMentionsCount()
        {
            UnreadMentionsLabel.Visibility = ViewModel?.UnreadMentionsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
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
                return settings.MuteUntil == 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        private Visibility UpdateUnreadBadgeMutedBrush(TLPeerNotifySettingsBase settingsBase)
        {
            var settings = settingsBase as TLPeerNotifySettings;
            if (settings != null)
            {
                return settings.MuteUntil == 0 ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        private void UpdateChannelType()
        {
            if (ViewModel.With is TLChannel channel)
            {
                fiType.Text = channel.IsBroadcast ? "\uE789" : "\uE125";
            }
            else if (ViewModel.With is TLChannelForbidden channelForbidden)
            {
                fiType.Text = channelForbidden.IsBroadcast ? "\uE789" : "\uE125";
            }
            else if (ViewModel.With is TLChat || ViewModel.With is TLChatForbidden)
            {
                fiType.Text = "\uE125";
            }
        }

        private void ToolTip_Opened(object sender, RoutedEventArgs e)
        {
            var tooltip = sender as ToolTip;
            if (tooltip != null)
            {
                tooltip.Content = BriefInfo.Text;
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
