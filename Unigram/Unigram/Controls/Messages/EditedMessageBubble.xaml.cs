using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Messages
{
    public sealed partial class EditedMessageBubble : MessageBubbleBase
    {
        public EditedMessageBubble()
        {
            InitializeComponent();

            DataContextChanged += (s, args) =>
            {
                if (ViewModel != null && ViewModel != _oldValue) Bindings.Update();
                if (ViewModel == null) Bindings.StopTracking();

                _oldValue = ViewModel;
            };
        }

        #region Adaptive part

        private Thickness UpdateFirst(bool isFirst)
        {
            OnMessageChanged(HeaderLabel, null, Header);
            return isFirst ? new Thickness(0, 2, 0, 0) : new Thickness();
        }

        private void OnMediaChanged(object sender, EventArgs e)
        {
            OnMediaChanged();
        }

        private void OnMediaChanged()
        {
            var message = DataContext as TLMessage;

            var empty = false;
            if (message.Media is TLMessageMediaWebPage webpageMedia)
            {
                empty = webpageMedia.WebPage is TLWebPageEmpty || webpageMedia.WebPage is TLWebPagePending;
            }

            if (message == null || message.Media == null || message.Media is TLMessageMediaEmpty || empty)
            {
                Media.Margin = new Thickness(0);
                Placeholder.Visibility = Visibility.Visible;
                FooterToNormal();
                Grid.SetRow(Footer, 2);
                Grid.SetRow(Message, 2);
            }
            else if (message != null && message.Media != null)
            {
                if (IsFullMedia(message.Media))
                {
                    var left = -8;
                    var top = -4;
                    var right = -10;
                    var bottom = -6;

                    if (message.Media.TypeId != TLType.MessageMediaVenue)
                    {
                        if (message.IsFirst && !message.IsOut && !message.IsPost && (message.ToId is TLPeerChat || message.ToId is TLPeerChannel))
                        {
                            top = 4;
                        }
                        if (message.IsFirst && message.IsSaved())
                        {
                            top = 4;
                        }
                        if ((message.HasFwdFrom && !message.IsSaved()) || message.HasViaBotId || message.HasReplyToMsgId || message.IsPost)
                        {
                            top = 4;
                        }
                    }

                    var caption = false;
                    if (message.Media is ITLMessageMediaCaption captionMedia)
                    {
                        caption = !string.IsNullOrWhiteSpace(captionMedia.Caption);
                    }
                    else if (message.Media is TLMessageMediaVenue)
                    {
                        caption = true;
                    }

                    if (caption)
                    {
                        FooterToNormal();
                        bottom = 4;
                    }
                    else if (message.Media is TLMessageMediaGeoLive)
                    {
                        FooterToHidden();
                    }
                    else
                    {
                        FooterToMedia();
                    }

                    Media.Margin = new Thickness(left, top, right, bottom);
                    Placeholder.Visibility = caption ? Visibility.Visible : Visibility.Collapsed;
                    Grid.SetRow(Footer, caption ? 4 : 3);
                    Grid.SetRow(Message, caption ? 4 : 2);
                }
                else if (message.Media is TLMessageMediaWebPage || message.Media is TLMessageMediaGame)
                {
                    Media.Margin = new Thickness(0);
                    Placeholder.Visibility = Visibility.Collapsed;
                    FooterToNormal();
                    Grid.SetRow(Footer, 4);
                    Grid.SetRow(Message, 2);
                }
                else if (message.Media is TLMessageMediaInvoice invoiceMedia)
                {
                    var caption = !invoiceMedia.HasPhoto;

                    Media.Margin = new Thickness(0);
                    Placeholder.Visibility = caption ? Visibility.Visible : Visibility.Collapsed;
                    FooterToNormal();
                    Grid.SetRow(Footer, caption ? 3 : 4);
                    Grid.SetRow(Message, 2);
                }
                else /*if (IsInlineMedia(message.Media))*/
                {
                    var caption = false;
                    if (message.Media is ITLMessageMediaCaption captionMedia)
                    {
                        caption = !string.IsNullOrWhiteSpace(captionMedia.Caption);
                    }

                    Media.Margin = new Thickness(0, 4, 0, caption ? 8 : 2);
                    Placeholder.Visibility = caption ? Visibility.Visible : Visibility.Collapsed;
                    FooterToNormal();
                    Grid.SetRow(Footer, caption ? 4 : 3);
                    Grid.SetRow(Message, caption ? 4 : 2);
                }
            }
        }

        private void FooterToMedia()
        {
            VisualStateManager.GoToState(LayoutRoot, "MediaState", false);
        }

        private void FooterToHidden()
        {
            VisualStateManager.GoToState(LayoutRoot, "HiddenState", false);
        }

        private void FooterToNormal()
        {
            VisualStateManager.GoToState(LayoutRoot, "Normal", false);
        }

        #endregion

        private void Footer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                Placeholder.Width = e.NewSize.Width;
            }
        }
    }
}
