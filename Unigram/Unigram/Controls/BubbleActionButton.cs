using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    /// <summary>
    /// This button is intended to be placed over the BubbleTextBox
    /// </summary>
    public class BubbleActionButton : Button
    {
        public BubbleActionButton()
        {
            DefaultStyleKey = typeof(BubbleActionButton);

            Click += OnClick;
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            var channel = With as TLChannel;
            if (channel != null)
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

            var user = With as TLUser;
            if (user != null)
            {
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
        }

        #region ToggleMuteCommand

        public ICommand ToggleMuteCommand
        {
            get { return (ICommand)GetValue(MyPropertyProperty); }
            set { SetValue(MyPropertyProperty, value); }
        }

        public static readonly DependencyProperty MyPropertyProperty =
            DependencyProperty.Register("MyProperty", typeof(ICommand), typeof(BubbleActionButton), new PropertyMetadata(null));

        #endregion

        #region JoinCommand

        public ICommand JoinCommand
        {
            get { return (ICommand)GetValue(JoinCommandProperty); }
            set { SetValue(JoinCommandProperty, value); }
        }

        public static readonly DependencyProperty JoinCommandProperty =
            DependencyProperty.Register("JoinCommand", typeof(ICommand), typeof(BubbleActionButton), new PropertyMetadata(null));

        #endregion

        #region DeleteAndExitCommand

        public ICommand DeleteAndExitCommand
        {
            get { return (ICommand)GetValue(DeleteAndExitCommandProperty); }
            set { SetValue(DeleteAndExitCommandProperty, value); }
        }

        public static readonly DependencyProperty DeleteAndExitCommandProperty =
            DependencyProperty.Register("DeleteAndExitCommand", typeof(ICommand), typeof(BubbleActionButton), new PropertyMetadata(null));

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
            DependencyProperty.Register("With", typeof(object), typeof(BubbleActionButton), new PropertyMetadata(null, OnWithChanged));

        private static void OnWithChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BubbleActionButton)d).OnWithChanged(e.NewValue, e.OldValue);
        }

        #endregion

        private void OnWithChanged(object newValue, object oldValue)
        {
            Visibility = UpdateVisibility(newValue);
        }

        private Visibility UpdateVisibility(object with)
        {
            var channel = with as TLChannel;
            if (channel != null)
            {
                if (channel.IsLeft)
                {
                    Content = channel.IsBroadcast ? "Join channel" : "Join";
                    return Visibility.Visible;
                }
                else if (channel.IsKicked)
                {
                    Content = "Delete and exit";
                    return Visibility.Visible;
                }
                else
                {
                    if (channel.IsBroadcast)
                    {
                        if (channel.IsCreator || channel.IsEditor)
                        {
                            return Visibility.Collapsed;
                        }
                        else
                        {
                            var settings = channel.NotifySettings as TLPeerNotifySettings;
                            if (settings != null)
                            {
                                Content = settings.IsSilent || settings.MuteUntil > 0 ? "Unmute" : "Mute";
                            }
                            else
                            {
                                Content = "Mute";
                            }

                            return Visibility.Visible;
                        }
                    }
                }
            }

            var user = with as TLUser;
            if (user != null)
            {
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

            return Visibility.Collapsed;
        }
    }
}
