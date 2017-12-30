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
using Unigram.Common;
using Unigram.Native;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Composition;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
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

        private Thickness UpdateFirst(bool isFirst)
        {
            OnMessageChanged(HeaderLabel, AdminLabel, Header);
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
                else if (message.IsSticker() || message.IsRoundVideo())
                {
                    Media.Margin = new Thickness(-8, -4, -10, -6);
                    Placeholder.Visibility = Visibility.Collapsed;
                    FooterToLightMedia(message.IsOut);
                    Grid.SetRow(Footer, 3);
                    Grid.SetRow(Message, 2);
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

        private void FooterToLightMedia(bool isOut)
        {
            VisualStateManager.GoToState(LayoutRoot, "LightState" + (isOut ? "Out" : string.Empty), false);
            VisualStateManager.GoToState(Reply.Content as UserControl, "LightState", false);
        }

        private void FooterToMedia()
        {
            VisualStateManager.GoToState(LayoutRoot, "MediaState", false);
            VisualStateManager.GoToState(Reply.Content as UserControl, "Normal", false);
        }

        private void FooterToHidden()
        {
            VisualStateManager.GoToState(LayoutRoot, "HiddenState", false);
            VisualStateManager.GoToState(Reply.Content as UserControl, "Normal", false);
        }

        private void FooterToNormal()
        {
            VisualStateManager.GoToState(LayoutRoot, "Normal", false);
            VisualStateManager.GoToState(Reply.Content as UserControl, "Normal", false);
        }

        #endregion

        private void Footer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                Placeholder.Width = e.NewSize.Width;
            }
        }

        public void Highlight()
        {
            var message = DataContext as TLMessage;
            if (message == null)
            {
                return;
            }

            var sticker = message.IsSticker();
            var round = message.IsRoundVideo();
            var target = sticker || round ? Media : (FrameworkElement)ContentPanel;

            var overlay = ElementCompositionPreview.GetElementChildVisual(target) as SpriteVisual;
            if (overlay == null)
            {
                overlay = ElementCompositionPreview.GetElementVisual(this).Compositor.CreateSpriteVisual();
                ElementCompositionPreview.SetElementChildVisual(target, overlay);
            }

            var settings = new UISettings();
            var fill = overlay.Compositor.CreateColorBrush(settings.GetColorValue(UIColorType.Accent));
            var brush = (CompositionBrush)fill;

            if (sticker || round)
            {
                ImageLoader.Initialize(overlay.Compositor);
                ManagedSurface surface = null;
                if (sticker)
                {
                    var document = message.GetDocument();
                    var fileName = document.GetFileName();
                    if (File.Exists(FileUtils.GetTempFileName(fileName)))
                    {
                        var decoded = WebPImage.Encode(File.ReadAllBytes(FileUtils.GetTempFileName(fileName)));
                        surface = ImageLoader.Instance.LoadFromStream(decoded);
                    }
                }
                else
                {
                    surface = ImageLoader.Instance.LoadCircle(100, Windows.UI.Colors.White);
                }

                if (surface != null)
                {
                    var mask = overlay.Compositor.CreateMaskBrush();
                    mask.Mask = surface.Brush;
                    mask.Source = fill;
                    brush = mask;
                }
            }

            overlay.Size = new System.Numerics.Vector2((float)target.ActualWidth, (float)target.ActualHeight);
            overlay.Opacity = 0f;
            overlay.Brush = brush;

            var animation = overlay.Compositor.CreateScalarKeyFrameAnimation();
            animation.Duration = TimeSpan.FromSeconds(2);
            animation.InsertKeyFrame(300f / 2000f, 0.4f);
            animation.InsertKeyFrame(1700f / 2000f, 0.4f);
            animation.InsertKeyFrame(1, 0);

            overlay.StartAnimation("Opacity", animation);
        }
    }
}
