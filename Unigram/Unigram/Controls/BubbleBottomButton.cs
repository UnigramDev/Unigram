using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
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
                else if (channel.IsKicked)
                {
                    DeleteAndExitCommand?.Execute(null);
                }
                else
                {
                    if (channel.IsBroadcast)
                    {
                        if (channel.IsCreator || channel.IsEditor)
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
                TextArea.IsEnabled = enabled;
            }

            Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        }

        private bool UpdateView(object with)
        {
            if (with is TLChannel channel)
            {
                if (channel.IsLeft)
                {
                    Content = channel.IsBroadcast ? "Join channel" : "Join";
                    return false;
                }
                else if (channel.IsKicked)
                {
                    Content = "Delete and exit";
                    return false;
                }
                else
                {
                    if (channel.IsBroadcast)
                    {
                        if (channel.IsCreator || channel.IsEditor)
                        {
                            return true;
                        }
                        else
                        {
                            var dialog = InMemoryCacheService.Current.GetDialog(channel.ToPeer());
                            if (dialog != null)
                            {
                                var settings = dialog.NotifySettings as TLPeerNotifySettings;
                                if (settings != null)
                                {
                                    Content = settings.MuteUntil > 0 ? "Unmute" : "Mute";
                                }
                                else
                                {
                                    Content = "Mute";
                                }
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
                Content = "Delete and exit";
                return false;
            }

            if (with is TLUser user)
            {
                var full = InMemoryCacheService.Current.GetFullUser(user.Id);
                if (full != null && full.IsBlocked)
                {
                    Content = "Unblock";
                    return false;
                }

                // TODO: check if blocked
                // WARNING: this is absolutely the wrong way
                //var response = await MTProtoService.Current.GetFullUserAsync(new TLInputUser { UserId = user.Id, AccessHash = user.AccessHash ?? 0 });
                //if (response.IsSucceeded)
                //{
                //    var blocked = response.Result.IsBlocked;
                //    if (blocked)
                //    {
                //        Content = "Unblock";
                //        return Visibility.Visible;
                //    }
                //}
            }

            return true;
        }
    }
}
