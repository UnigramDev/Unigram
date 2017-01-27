using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            var channel = newValue as TLChannel;
            if (channel != null)
            {
                if (channel.IsBroadcast)
                {
                    if (channel.IsCreator || channel.IsEditor)
                    {
                        Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        var settings = channel.NotifySettings as TLPeerNotifySettings;
                        if (settings != null)
                        {
                            Content = settings.MuteUntil == 0 || !settings.IsSilent ? "Mute" : "Unmute";
                        }
                        else
                        {
                            Content = "Mute";
                        }

                        Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}
