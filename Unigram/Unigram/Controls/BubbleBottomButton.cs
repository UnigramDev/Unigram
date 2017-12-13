using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Converters;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    /// <summary>
    /// This button is intended to be placed over the BubbleTextBox
    /// </summary>
    public class BubbleBottomButton : Button
    {
        public BubbleBottomButton()
        {
            DefaultStyleKey = typeof(BubbleBottomButton);

            Click += OnClick;
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (With is TLChannel channel)
            {
                if (channel.IsLeft)
                {
                    JoinCommand?.Execute(null);
                }
                else if (channel.HasBannedRights && channel.BannedRights != null && channel.BannedRights.IsViewMessages)
                {
                    DeleteAndExitCommand?.Execute(null);
                }
                else
                {
                    if (channel.IsBroadcast)
                    {
                        if (channel.IsCreator || (channel.HasAdminRights && channel.AdminRights != null && channel.AdminRights.IsPostMessages))
                        {

                        }
                        else
                        {
                            ToggleMuteCommand?.Execute(null);
                        }
                    }
                }
            }

            if (With is TLChatForbidden forbiddenChat)
            {
                DeleteAndExitCommand?.Execute(null);
            }

            if (With is TLUser user)
            {
                var full = InMemoryCacheService.Current.GetFullUser(user.Id);
                if (full != null && full.IsBlocked)
                {
                    UnblockCommand?.Execute(null);
                }

                if (user.IsBot && HasAccessToken)
                {
                    StartCommand?.Execute(null);
                }

                if (user.IsDeleted)
                {
                    DeleteAndExitCommand?.Execute(null);
                }
            }
        }

        #region ToggleMuteCommand

        public ICommand ToggleMuteCommand
        {
            get { return (ICommand)GetValue(MyPropertyProperty); }
            set { SetValue(MyPropertyProperty, value); }
        }

        public static readonly DependencyProperty MyPropertyProperty =
            DependencyProperty.Register("MyProperty", typeof(ICommand), typeof(BubbleBottomButton), new PropertyMetadata(null));

        #endregion

        #region JoinCommand

        public ICommand JoinCommand
        {
            get { return (ICommand)GetValue(JoinCommandProperty); }
            set { SetValue(JoinCommandProperty, value); }
        }

        public static readonly DependencyProperty JoinCommandProperty =
            DependencyProperty.Register("JoinCommand", typeof(ICommand), typeof(BubbleBottomButton), new PropertyMetadata(null));

        #endregion

        #region DeleteAndExitCommand

        public ICommand DeleteAndExitCommand
        {
            get { return (ICommand)GetValue(DeleteAndExitCommandProperty); }
            set { SetValue(DeleteAndExitCommandProperty, value); }
        }

        public static readonly DependencyProperty DeleteAndExitCommandProperty =
            DependencyProperty.Register("DeleteAndExitCommand", typeof(ICommand), typeof(BubbleBottomButton), new PropertyMetadata(null));

        #endregion

        #region UnblockCommand

        public ICommand UnblockCommand
        {
            get { return (ICommand)GetValue(UnblockCommandProperty); }
            set { SetValue(UnblockCommandProperty, value); }
        }

        public static readonly DependencyProperty UnblockCommandProperty =
            DependencyProperty.Register("UnblockCommand", typeof(ICommand), typeof(BubbleBottomButton), new PropertyMetadata(null));

        #endregion

        #region StartCommand

        public ICommand StartCommand
        {
            get { return (ICommand)GetValue(StartCommandProperty); }
            set { SetValue(StartCommandProperty, value); }
        }

        public static readonly DependencyProperty StartCommandProperty =
            DependencyProperty.Register("StartCommand", typeof(ICommand), typeof(BubbleBottomButton), new PropertyMetadata(null));

        #endregion

        #region With

        public object With
        {
            get { return (object)GetValue(WithProperty); }
            //set { SetValue(WithProperty, value); }
            set
            {
                // TODO: shitty hack!!!
                var oldValue = GetValue(WithProperty);
                SetValue(WithProperty, value);

                if (oldValue == value)
                {
                    OnWithChanged(value, oldValue);
                }

            }
        }

        public static readonly DependencyProperty WithProperty =
            DependencyProperty.Register("With", typeof(object), typeof(BubbleBottomButton), new PropertyMetadata(null, OnWithChanged));

        private static void OnWithChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BubbleBottomButton)d).OnWithChanged(e.NewValue, e.OldValue);
        }

        #endregion

        #region HasAccessToken

        public bool HasAccessToken
        {
            get { return (bool)GetValue(HasAccessTokenProperty); }
            set { SetValue(HasAccessTokenProperty, value); }
        }

        public static readonly DependencyProperty HasAccessTokenProperty =
            DependencyProperty.Register("HasAccessToken", typeof(bool), typeof(BubbleBottomButton), new PropertyMetadata(true, OnHasAccessTokenChanged));

        private static void OnHasAccessTokenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BubbleBottomButton)d).OnWithChanged(((BubbleBottomButton)d).With, null);
        }

        #endregion

        #region TextArea

        public ContentControl TextArea
        {
            get { return (ContentControl)GetValue(TextAreaProperty); }
            set { SetValue(TextAreaProperty, value); }
        }

        public static readonly DependencyProperty TextAreaProperty =
            DependencyProperty.Register("TextArea", typeof(ContentControl), typeof(BubbleBottomButton), new PropertyMetadata(null));

        #endregion

        private void OnWithChanged(object newValue, object oldValue)
        {
            var enabled = UpdateView(newValue);
            if (TextArea != null)
            {
                TextArea.IsEnabled = enabled == true;
            }

            Visibility = enabled == true ? Visibility.Collapsed : Visibility.Visible;
            IsEnabled = enabled.HasValue;
        }

        private bool? UpdateView(object with)
        {
            if (with is TLChannel channel)
            {
                if (channel.IsLeft)
                {
                    Content = Strings.Android.ChannelJoin;
                    return false;
                }
                else if (channel.HasBannedRights && channel.BannedRights != null)
                {
                    if (channel.BannedRights.IsViewMessages)
                    {
                        Content = Strings.Android.DeleteChat;
                        return false;
                    }
                    else if (channel.BannedRights.IsSendMessages)
                    {
                        if (channel.BannedRights.IsForever())
                        {
                            Content = Strings.Android.SendMessageRestrictedForever;
                        }
                        else
                        {
                            Content = string.Format(Strings.Android.SendMessageRestricted, BindConvert.Current.BannedUntil(channel.BannedRights.UntilDate));
                        }
                        return null;
                    }
                }
                else
                {
                    if (channel.IsBroadcast)
                    {
                        if (channel.IsCreator || (channel.HasAdminRights && channel.AdminRights != null && channel.AdminRights.IsPostMessages))
                        {
                            return true;
                        }
                        else
                        {
                            var dialog = InMemoryCacheService.Current.GetDialog(channel.ToPeer());
                            if (dialog != null)
                            {
                                Content = dialog.IsMuted ? Strings.Android.ChannelUnmute : Strings.Android.ChannelMute;
                            }

                            var full = InMemoryCacheService.Current.GetFullChat(channel.Id);
                            if (full != null && full.NotifySettings is TLPeerNotifySettings notifySettings)
                            {
                                Content = notifySettings.IsMuted ? Strings.Android.ChannelUnmute : Strings.Android.ChannelMute;
                            }

                            return false;
                        }
                    }
                }
            }

            if (with is TLChat chat)
            {

            }

            if (with is TLChatForbidden forbiddenChat)
            {
                Content = Strings.Android.DeleteChat;
                return false;
            }

            if (with is TLUser user)
            {
                var full = InMemoryCacheService.Current.GetFullUser(user.Id);
                if (full != null && full.IsBlocked)
                {
                    Content = user.IsBot ? Strings.Android.BotUnblock : Strings.Android.Unblock;
                    return false;
                }

                if (user.IsBot && HasAccessToken)
                {
                    Content = Strings.Android.BotStart;
                    return false;
                }

                if (user.IsDeleted)
                {
                    Content = Strings.Android.DeleteThisChat;
                    return false;
                }
            }

            return true;
        }
    }
}
