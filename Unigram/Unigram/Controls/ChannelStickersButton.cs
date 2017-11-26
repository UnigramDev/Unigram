using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.ViewModels;
using Unigram.ViewModels.Dialogs;
using Windows.UI.Xaml;

namespace Unigram.Controls
{
    public class ChannelStickersButton : GlyphButton
    {
        #region StickerSet



        public TLChannelStickerSet StickerSet
        {
            get { return (TLChannelStickerSet)GetValue(StickerSetProperty); }
            set { SetValue(StickerSetProperty, value); }
        }

        // Using a DependencyProperty as the backing store for StickerSet.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StickerSetProperty =
            DependencyProperty.Register("StickerSet", typeof(TLChannelStickerSet), typeof(ChannelStickersButton), new PropertyMetadata(null, OnStickerSetChanged));

        private static void OnStickerSetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChannelStickersButton)d).OnStickerSetChanged(e.NewValue as TLChannelStickerSet);
        }

        private void OnStickerSetChanged(TLChannelStickerSet set)
        {
            if (set == null)
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            var channel = set.With as TLChannel;
            if (channel == null)
            {
                return;
            }

            var channelFull = set.Full as TLChannelFull;
            if (channelFull == null)
            {
                return;
            }

            if (channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsChangeInfo))
            {
                Glyph = "\uE115";
                Visibility = Visibility.Visible;
            }
            else
            {
                Glyph = "\uE10A";
                Visibility = Visibility.Visible;
            }
        }



        #endregion
    }
}
