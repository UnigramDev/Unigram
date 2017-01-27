using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        }

        #region With

        public object With
        {
            get { return (object)GetValue(WithProperty); }
            set { SetValue(WithProperty, value); }
        }

        public static readonly DependencyProperty WithProperty =
            DependencyProperty.Register("With", typeof(object), typeof(BubbleActionButton), new PropertyMetadata(null, OnWithChanged));

        private static void OnWithChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BubbleActionButton)d).OnWidthChanged(e.NewValue, e.OldValue);
        }

        #endregion

        private void OnWidthChanged(object newValue, object oldValue)
        {
            Visibility = UpdateVisibility(newValue);
        }

        private Visibility UpdateVisibility(object with)
        {
            var channel = with as TLChannel;
            if (channel != null)
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
