﻿using System;
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
    public sealed partial class MessageBubble : MessageBubbleBase
    {
        public MessageBubble()
        {
            InitializeComponent();
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (ViewModel != null && ViewModel != _oldValue) Bindings.Update();
            if (ViewModel == null) Bindings.StopTracking();

            _oldValue = ViewModel;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();
        }

        #region Adaptive part

        private Visibility UpdateFirst(bool isFirst)
        {
            OnMessageChanged(HeaderLabel, AdminLabel, Header);
            return isFirst ? Visibility.Visible : Visibility.Collapsed;
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
                StatusToDefault();
                Grid.SetRow(StatusBar, 2);
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
                        if (message.HasFwdFrom || message.HasViaBotId || message.HasReplyToMsgId || message.IsPost)
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
                        StatusToDefault();
                        bottom = 4;
                    }
                    else if (message.Media is TLMessageMediaGeoLive)
                    {
                        StatusToHidden();
                    }
                    else
                    {
                        StatusToFullMedia();
                    }

                    Media.Margin = new Thickness(left, top, right, bottom);
                    Placeholder.Visibility = caption ? Visibility.Visible : Visibility.Collapsed;
                    Grid.SetRow(StatusBar, caption ? 4 : 3);
                    Grid.SetRow(Message, caption ? 4 : 2);
                }
                else if (message.IsSticker() || message.IsRoundVideo())
                {
                    Media.Margin = new Thickness(-8, -4, -10, -6);
                    Placeholder.Visibility = Visibility.Collapsed;
                    StatusToEmbedMedia(message.IsOut);
                    Grid.SetRow(StatusBar, 3);
                    Grid.SetRow(Message, 2);
                }
                else if (message.Media is TLMessageMediaWebPage || message.Media is TLMessageMediaGame)
                {
                    Media.Margin = new Thickness(0);
                    Placeholder.Visibility = Visibility.Collapsed;
                    StatusToDefault();
                    Grid.SetRow(StatusBar, 4);
                    Grid.SetRow(Message, 2);
                }
                else if (message.Media is TLMessageMediaInvoice invoiceMedia)
                {
                    var caption = !invoiceMedia.HasPhoto;

                    Media.Margin = new Thickness(0);
                    Placeholder.Visibility = caption ? Visibility.Visible : Visibility.Collapsed;
                    StatusToDefault();
                    Grid.SetRow(StatusBar, caption ? 3 : 4);
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
                    StatusToDefault();
                    Grid.SetRow(StatusBar, caption ? 4 : 3);
                    Grid.SetRow(Message, caption ? 4 : 2);
                }
                //else
                //{
                //    Debug.WriteLine("NE UNO NE L'ALTRO");
                //    MediaControl.Margin = new Thickness(0);
                //    StatusToDefault();
                //    Grid.SetRow(StatusBar, 4);
                //    Grid.SetRow(MessageControl, 2);
                //}
            }
        }

        private void StatusToEmbedMedia(bool isOut)
        {
            VisualStateManager.GoToState(LayoutRoot, "LightMedia" + (isOut ? "Out" : string.Empty), false);
            VisualStateManager.GoToState(Reply.Content as UserControl, "LightMedia", false);
        }

        private void StatusToFullMedia()
        {
            VisualStateManager.GoToState(LayoutRoot, "FullMedia", false);
            VisualStateManager.GoToState(Reply.Content as UserControl, "Normal", false);
        }

        private void StatusToDefault()
        {
            VisualStateManager.GoToState(LayoutRoot, "Normal", false);
            VisualStateManager.GoToState(Reply.Content as UserControl, "Normal", false);
        }

        private void StatusToHidden()
        {
            VisualStateManager.GoToState(LayoutRoot, "Hidden", false);
            VisualStateManager.GoToState(Reply.Content as UserControl, "Normal", false);
        }

        #endregion

        private void StatusBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                Placeholder.Width = e.NewSize.Width;
            }
        }
    }
}
