﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Services.NavigationService;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Selectors;
using Unigram.ViewModels;
using Unigram.Views;
using Unigram.Views.Chats;
using Unigram.Views.Users;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Globalization.DateTimeFormatting;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages
{
    public class MessageBubbleBase : StackPanel
    {
        public TLMessage ViewModel => DataContext as TLMessage;

        public BindConvert Convert => BindConvert.Current;

        private UnigramViewModelBase _contextBase;
        public UnigramViewModelBase ContextBase
        {
            get
            {
                if (_contextBase == null)
                {
                    //var parent = VisualTreeHelper.GetParent(this);
                    //while (parent as BubbleListViewItem == null && parent != null)
                    //{
                    //    parent = VisualTreeHelper.GetParent(parent);
                    //}

                    //var item = parent as BubbleListViewItem;
                    //if (item != null)
                    //{
                    //    _contextBase = item.Owner.DataContext as DialogViewModel;
                    //}

                    var parent = VisualTreeHelper.GetParent(this);
                    while (parent as ListView == null && parent != null)
                    {
                        parent = VisualTreeHelper.GetParent(parent);
                    }

                    var item = parent as ListView;
                    if (item != null)
                    {
                        _contextBase = item.DataContext as UnigramViewModelBase;
                    }
                }

                return _contextBase;
            }
        }

        public INavigationService NavigationService => ContextBase?.NavigationService;

        public DialogViewModel Context => ContextBase as DialogViewModel;

        protected TLMessage _oldValue;

        //public MessageControlBase()
        //{
        //    DataContextChanged += (s, args) =>
        //    {
        //        if (ViewModel != null)
        //        {
        //            Loading(s, null);
        //        }
        //    };
        //}

        #region Binding

        protected Visibility EditedVisibility(bool hasEditDate, bool hasViaBotId, TLReplyMarkupBase replyMarkup)
        {
            return hasEditDate && !hasViaBotId && replyMarkup?.TypeId != TLType.ReplyInlineMarkup ? Visibility.Visible : Visibility.Collapsed;
        }

        protected object ConvertMedia(TLMessageMediaBase media)
        {
            return ViewModel;
        }

        protected Visibility ConvertShare(TLMessage message)
        {
            if (message.IsSticker())
            {
                return Visibility.Collapsed;
            }
            else if (message.HasFwdFrom && message.FwdFrom.HasChannelId && !message.IsOut)
            {
                return Visibility.Visible;
            }
            else if (message.HasFromId && !message.IsPost)
            {
                if (message.Media is TLMessageMediaEmpty || message.Media == null || message.Media is TLMessageMediaWebPage webpageMedia && !(webpageMedia.WebPage is TLWebPage))
                {
                    return Visibility.Collapsed;
                }

                var user = message.From;
                if (user != null && user.IsBot)
                {
                    return Visibility.Visible;
                }

                if (!message.IsOut)
                {
                    if (message.Media is TLMessageMediaGame || message.Media is TLMessageMediaInvoice)
                    {
                        return Visibility.Visible;
                    }

                    var parent = message.Parent as TLChannel;
                    if (parent != null && parent.IsMegaGroup)
                    {
                        //TLRPC.Chat chat = MessagesController.getInstance().getChat(messageObject.messageOwner.to_id.channel_id);
                        //return chat != null && chat.username != null && chat.username.length() > 0 && !(messageObject.messageOwner.media instanceof TLRPC.TL_messageMediaContact) && !(messageObject.messageOwner.media instanceof TLRPC.TL_messageMediaGeo);

                        return parent.HasUsername && !(message.Media is TLMessageMediaContact) && !(message.Media is TLMessageMediaGeo) ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
            //else if (messageObject.messageOwner.from_id < 0 || messageObject.messageOwner.post)
            else if (message.IsPost)
            {
                //if (messageObject.messageOwner.to_id.channel_id != 0 && (messageObject.messageOwner.via_bot_id == 0 && messageObject.messageOwner.reply_to_msg_id == 0 || messageObject.type != 13))
                //{
                //    return Visibility.Visible;
                //}

                if (message.ToId is TLPeerChannel && (!message.HasViaBotId && !message.HasReplyToMsgId))
                {
                    return Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
        }

        #endregion

        protected void OnMessageChanged(TextBlock paragraph, TextBlock admin, Grid parent)
        {
            paragraph.Inlines.Clear();

            var message = DataContext as TLMessage;
            if (message == null)
            {
                return;
            }

            var sticker = message.IsSticker();
            var light = sticker || message.IsRoundVideo();

            if (!light && message.IsFirst && !message.IsOut && !message.IsPost && (message.ToId is TLPeerChat || message.ToId is TLPeerChannel))
            {
                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(new Run { Text = message.From?.FullName ?? string.Empty });
                hyperlink.UnderlineStyle = UnderlineStyle.None;
                hyperlink.Foreground = Convert.Bubble(message.FromId ?? 0);
                hyperlink.Click += (s, args) => From_Click(message);

                paragraph.Inlines.Add(hyperlink);
            }
            else if (!light && message.IsPost && (message.ToId is TLPeerChat || message.ToId is TLPeerChannel))
            {
                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(new Run { Text = message.Parent?.DisplayName ?? string.Empty });
                hyperlink.UnderlineStyle = UnderlineStyle.None;
                hyperlink.Foreground = Convert.Bubble(message.ToId.Id);
                hyperlink.Click += (s, args) => From_Click(message);

                    paragraph.Inlines.Add(hyperlink);
                }

                if (message.HasFwdFrom)
                {
                    if (paragraph.Inlines.Count > 0)
                        paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Run { Text = "Forwarded from " });

                var name = string.Empty;

                var channel = message.FwdFromChannel;
                if (channel != null)
                {
                    name = channel.DisplayName;

                    //if (message.FwdFrom.HasPostAuthor && message.FwdFrom.PostAuthor != null)
                    //{
                    //    name += $" ({message.FwdFrom.PostAuthor})";
                    //}
                }

                var user = message.FwdFromUser;
                if (user != null)
                {
                    if (name.Length > 0)
                    {
                        name += $" ({user.FullName})";
                    }
                    else
                    {
                        name = user.FullName;
                    }
                }

                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(new Run { Text = name });
                hyperlink.UnderlineStyle = UnderlineStyle.None;
                hyperlink.Foreground = light ? new SolidColorBrush(Colors.White) : paragraph.Foreground;
                hyperlink.Click += (s, args) => FwdFrom_Click(message);

                paragraph.Inlines.Add(hyperlink);
            }

            if (message.HasViaBotId && message.ViaBot != null && !message.ViaBot.IsDeleted && message.ViaBot.HasUsername)
            {
                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(new Run { Text = (paragraph.Inlines.Count > 0 ? " via @" : "via @") + message.ViaBot.Username });
                hyperlink.UnderlineStyle = UnderlineStyle.None;
                hyperlink.Foreground = light ? new SolidColorBrush(Colors.White) : paragraph.Foreground;
                hyperlink.Click += (s, args) => ViaBot_Click(message);

                paragraph.Inlines.Add(hyperlink);
            }

            if (paragraph.Inlines.Count > 0)
            {
                if (paragraph != admin && !message.IsOut && message.IsAdmin())
                {
                    admin.Visibility = Visibility.Visible;
                }
                else
                {
                    admin.Visibility = Visibility.Collapsed;
                }

                paragraph.Inlines.Add(new Run { Text = " " });
                paragraph.Visibility = Visibility.Visible;
                parent.Visibility = Visibility.Visible;
            }
            else
            {
                paragraph.Visibility = Visibility.Collapsed;
                admin.Visibility = Visibility.Collapsed;
                parent.Visibility = message.ReplyToMsgId.HasValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void From_Click(TLMessage message)
        {
            if (message.IsPost)
            {
                NavigationService?.Navigate(typeof(ChatDetailsPage), new TLPeerChannel { ChannelId = message.ToId.Id });
            }
            else if (message.From != null)
            {
                NavigationService?.Navigate(typeof(UserDetailsPage), new TLPeerUser { UserId = message.From.Id });
            }
        }

        private void FwdFrom_Click(TLMessage message)
        {
            if (message.FwdFromChannel != null)
            {
                if (message.FwdFrom.HasChannelPost)
                {
                    NavigationService?.NavigateToDialog(message.FwdFromChannel, message.FwdFrom.ChannelPost);
                }
                else
                {
                    NavigationService?.NavigateToDialog(message.FwdFromChannel);
                }
            }
            else if (message.FwdFromUser != null)
            {
                NavigationService?.Navigate(typeof(UserDetailsPage), new TLPeerUser { UserId = message.FwdFromUser.Id });
            }
        }

        private void ViaBot_Click(TLMessage message)
        {
            Context?.SetText($"@{message.ViaBot.Username} ", focus: true);
            Context?.ResolveInlineBot(message.ViaBot.Username);
        }

        protected void ReplyMarkup_ButtonClick(object sender, ReplyMarkupButtonClickEventArgs e)
        {
            Context?.KeyboardButtonExecute(e.Button, ViewModel);
        }

        protected void Reply_Click(object sender, RoutedEventArgs e)
        {
            Context?.MessageOpenReplyCommand.Execute(ViewModel);
        }

        protected void Share_Click(object sender, RoutedEventArgs e)
        {
            Context?.MessageShareCommand.Execute(ViewModel);
        }

        protected void Message_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            MessageHelper.Hyperlink_ContextRequested(sender, args);
        }

        protected void Message_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
        }

        /// <summary>
        /// x:Bind hack
        /// </summary>
        public new event TypedEventHandler<FrameworkElement, object> Loading;

        private FrameworkElement _statusBar;

        protected override Size MeasureOverride(Size availableSize)
        {
            if (ViewModel?.Media == null || !IsFullMedia(ViewModel?.Media, true))
            {
                return base.MeasureOverride(availableSize);
            }

            var sumWidth = 0.0;
            var fixedSize = false;

            object constraint = null;
            if (ViewModel?.Media is TLMessageMediaPhoto photoMedia)
            {
                if (photoMedia.HasTTLSeconds)
                {
                    fixedSize = true;
                }
                else
                {
                    constraint = photoMedia.Photo;
                }
            }
            else if (ViewModel?.Media is TLMessageMediaDocument documentMedia)
            {
                if (documentMedia.HasTTLSeconds)
                {
                    fixedSize = true;
                }
                else
                {
                    constraint = documentMedia.Document;
                }
            }
            else if (ViewModel?.Media is TLMessageMediaInvoice invoiceMedia)
            {
                constraint = invoiceMedia.Photo;
            }
            //else if (ViewModel?.Media is TLMessageMediaWebPage webPageMedia)
            //{
            //    if (webPageMedia.WebPage is TLWebPage webPage && MediaTemplateSelector.IsWebPagePhotoTemplate(webPage))
            //    {
            //        sumWidth = 8 + 10 + 10;
            //        constraint = webPage.Photo;
            //    }
            //}
            else if (ViewModel?.Media is TLMessageMediaGeo || ViewModel?.Media is TLMessageMediaGeoLive || ViewModel?.Media is TLMessageMediaVenue)
            {
                constraint = ViewModel?.Media;
            }

            if (constraint == null)
            {
                return base.MeasureOverride(availableSize);
            }

            var availableWidth = Math.Min(availableSize.Width, Math.Min(double.IsNaN(Width) ? double.PositiveInfinity : Width, 320 + sumWidth));
            var availableHeight = Math.Min(availableSize.Height, Math.Min(double.IsNaN(Height) ? double.PositiveInfinity : Height, 420));

            var width = 0.0;
            var height = 0.0;

            if (constraint is TLMessageMediaGeo || constraint is TLMessageMediaGeoLive || constraint is TLMessageMediaVenue)
            {
                width = 320;
                height = 240;

                goto Calculate;
            }

            var photo = constraint as TLPhoto;
            if (photo != null)
            {
                if (fixedSize)
                {
                    width = 240;
                    height = 240;

                    goto Calculate;
                }

                //var photoSize = photo.Sizes.OrderByDescending(x => x.W).FirstOrDefault();
                var photoSize = photo.Sizes.OfType<TLPhotoSize>().OrderByDescending(x => x.W).FirstOrDefault();
                if (photoSize != null)
                {
                    width = photoSize.W;
                    height = photoSize.H;

                    goto Calculate;
                }
            }

            if (constraint is TLDocument document)
            {
                if (fixedSize)
                {
                    width = 240;
                    height = 240;

                    goto Calculate;
                }

                constraint = document.Attributes;
            }

            if (constraint is TLWebDocument webDocument)
            {
                constraint = webDocument.Attributes;
            }

            if (constraint is TLVector<TLDocumentAttributeBase> attributes)
            {
                var imageSize = attributes.OfType<TLDocumentAttributeImageSize>().FirstOrDefault();
                if (imageSize != null)
                {
                    width = imageSize.W;
                    height = imageSize.H;

                    goto Calculate;
                }

                var video = attributes.OfType<TLDocumentAttributeVideo>().FirstOrDefault();
                if (video != null)
                {
                    width = video.W;
                    height = video.H;

                    goto Calculate;
                }
            }

            Calculate:

            if (_statusBar == null)
                _statusBar = FindName("StatusBar") as FrameworkElement;
            if (_statusBar.DesiredSize.IsEmpty)
                _statusBar.Measure(availableSize);

            width = Math.Max(_statusBar.DesiredSize.Width + /*margin left*/ 8 + /*padding right*/ 6 + /*margin right*/ 6, Math.Max(width, 96) + sumWidth);

            if (width > availableWidth || height > availableHeight)
            {
                var ratioX = availableWidth / width;
                var ratioY = availableHeight / height;
                var ratio = Math.Min(ratioX, ratioY);

                return base.MeasureOverride(new Size(Math.Max(96, width * ratio), availableSize.Height));
            }
            else
            {
                return base.MeasureOverride(new Size(Math.Max(96, width), availableSize.Height));
            }
        }


        #region Static
        protected static SolidColorBrush StatusDarkBackgroundBrush { get { return (SolidColorBrush)App.Current.Resources["MessageOverlayBackgroundBrush"]; } }
        protected static SolidColorBrush StatusDarkForegroundBrush { get { return new SolidColorBrush(Colors.White); } }
        protected static SolidColorBrush StatusLightLabelForegroundBrush { get { return (SolidColorBrush)App.Current.Resources["MessageSubtleLabelBrush"]; } }
        protected static SolidColorBrush StatusLightGlyphForegroundBrush { get { return (SolidColorBrush)App.Current.Resources["MessageSubtleGlyphBrush"]; } }

        protected static bool IsFullMedia(TLMessageMediaBase media, bool width = false)
        {
            if (media == null) return false;

            if (media.TypeId == TLType.MessageMediaGeo) return true;
            else if (media.TypeId == TLType.MessageMediaGeoLive) return true;
            else if (media.TypeId == TLType.MessageMediaVenue) return true;
            else if (media.TypeId == TLType.MessageMediaPhoto) return true;
            else if (media.TypeId == TLType.MessageMediaDocument)
            {
                var documentMedia = media as TLMessageMediaDocument;
                if (TLMessage.IsGif(documentMedia.Document)) return true;
                else if (TLMessage.IsVideo(documentMedia.Document)) return true;
            }
            else if (media.TypeId == TLType.MessageMediaInvoice && width)
            {
                var invoiceMedia = media as TLMessageMediaInvoice;
                if (invoiceMedia.HasPhoto && invoiceMedia.Photo != null)
                {
                    return true;
                }
            }
            //else if (media.TypeId == TLType.MessageMediaWebPage && width)
            //{
            //    var webPageMedia = media as TLMessageMediaWebPage;
            //    var webPage = webPageMedia.WebPage as TLWebPage;
            //    if (webPage != null && MediaTemplateSelector.IsWebPagePhotoTemplate(webPage))
            //    {
            //        return true;
            //    }
            //}

            return false;
        }

        protected static bool IsInlineMedia(TLMessageMediaBase media)
        {
            if (media == null) return false;

            if (media.TypeId == TLType.MessageMediaContact) return true;
            else if (media.TypeId == TLType.MessageMediaGeoLive) return true;
            else if (media.TypeId == TLType.MessageMediaVenue) return true;
            else if (media.TypeId == TLType.MessageMediaPhoto)
            {
                var photoMedia = media as TLMessageMediaPhoto;
                if (string.IsNullOrWhiteSpace(photoMedia.Caption))
                {
                    return false;
                }

                return true;
            }
            else if (media.TypeId == TLType.MessageMediaDocument)
            {
                var documentMedia = media as TLMessageMediaDocument;
                if (TLMessage.IsMusic(documentMedia.Document)) return true;
                else if (TLMessage.IsVoice(documentMedia.Document)) return true;
                else if (TLMessage.IsVideo(documentMedia.Document))
                {
                    if (string.IsNullOrWhiteSpace(documentMedia.Caption))
                    {
                        return false;
                    }
                }
                else if (TLMessage.IsGif(documentMedia.Document))
                {
                    if (string.IsNullOrWhiteSpace(documentMedia.Caption))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }
        #endregion
    }
}
