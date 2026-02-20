//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Controls.Media;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    public partial class CustomEmojiIcon : AnimatedImage
    {
        public CustomEmojiIcon()
        {
            DefaultStyleKey = typeof(CustomEmojiIcon);
            FrameSize = new Size(20, 20);
            DecodeFrameType = DecodePixelType.Logical;
        }

        public string Emoji { get; set; }

        public static void Add(RichTextBlock parent, InlineCollection inliness, IClientService clientService, FormattedText message, string style = null)
        {
            var direct = XamlDirect.GetDefault();
            var inlines = direct.GetXamlDirectObject(inliness);

            direct.ClearCollection(inlines);

            if (message != null)
            {
                var clean = message.ReplaceSpoilers();
                var previous = 0;

                // TODO: support more entities
                if (message.Entities != null)
                {
                    foreach (var entity in clean.Entities)
                    {
                        if (entity.Type is not TextEntityTypeCustomEmoji customEmoji)
                        {
                            continue;
                        }

                        if (entity.Offset > previous)
                        {
                            NativeUtils.AddRunToCollection(direct, inlines, clean.Text, previous, entity.Offset - previous, FlowDirection.LeftToRight, TextStyle.None, null, 0, false);
                        }

                        var player = new CustomEmojiIcon();
                        player.LoopCount = 0;
                        player.Source = new CustomEmojiFileSource(clientService, customEmoji.CustomEmojiId);
                        player.HorizontalAlignment = HorizontalAlignment.Left;
                        player.FlowDirection = FlowDirection.LeftToRight;
                        player.IsHitTestVisible = false;
                        player.Margin = new Thickness(0, -2, 0, -6);

                        if (style != null)
                        {
                            // "InfoCustomEmojiStyle"
                            player.Style = BootStrapper.Current.Resources[style] as Style;
                        }

                        var baseline = parent.FontSize switch
                        {
                            11 => -3,
                            12 => -2,
                            _ => 0
                        };

                        var inline = new InlineUIContainer();
                        inline.Child = player;

                        // If the Span starts with a InlineUIContainer the RichTextBlock bugs and shows ellipsis
                        if (previous == 0)
                        {
                            NativeUtils.AddRunToCollection(direct, inlines, Icons.ZWNJ, FlowDirection.LeftToRight, TextStyle.None, null, 0, true);
                        }

                        direct.AddToCollection(inlines, direct.GetXamlDirectObject(inline));
                        NativeUtils.AddRunToCollection(direct, inlines, Icons.ZWNJ, FlowDirection.LeftToRight, TextStyle.None, null, 0, true);

                        previous = entity.Offset + entity.Length;
                    }
                }

                if (clean.Text.Length > previous)
                {
                    NativeUtils.AddRunToCollection(direct, inlines, clean.Text, previous, clean.Text.Length - previous, FlowDirection.LeftToRight, TextStyle.None, null, 0, false);
                }
            }
        }

        public static void Add(RichTextBlock parent, InlineCollection inliness, IClientService clientService, ChatFolderName name, double size = 20)
        {
            var direct = XamlDirect.GetDefault();
            var inlines = direct.GetXamlDirectObject(inliness);

            direct.ClearCollection(inlines);

            if (name?.Text != null)
            {
                var clean = name.Text.ReplaceSpoilers();
                var previous = 0;

                // TODO: support more entities
                if (name.Text.Entities != null)
                {
                    foreach (var entity in clean.Entities)
                    {
                        if (entity.Type is not TextEntityTypeCustomEmoji customEmoji)
                        {
                            continue;
                        }

                        if (entity.Offset > previous)
                        {
                            NativeUtils.AddRunToCollection(direct, inlines, clean.Text, previous, entity.Offset - previous, FlowDirection.LeftToRight, TextStyle.None, null, 0, false);
                        }

                        var player = new CustomEmojiIcon();
                        player.IsViewportAware = name.AnimateCustomEmoji;
                        player.LoopCount = name.AnimateCustomEmoji ? 0 : 1;
                        player.Width = size;
                        player.Height = size;
                        player.FrameSize = new Size(size, size);
                        player.Source = new CustomEmojiFileSource(clientService, customEmoji.CustomEmojiId);
                        player.HorizontalAlignment = HorizontalAlignment.Left;
                        player.FlowDirection = FlowDirection.LeftToRight;
                        player.IsHitTestVisible = false;

                        if (size == 20)
                        {
                            player.Margin = new Thickness(0, -2, 0, -6);
                        }
                        else
                        {
                            player.Margin = new Thickness(0, -4, 0, -4);
                        }

                        player.Width = size;
                        player.Height = size;

                        //if (style != null)
                        //{
                        //    // "InfoCustomEmojiStyle"
                        //    player.Style = BootStrapper.Current.Resources[style] as Style;
                        //}

                        var baseline = parent.FontSize == 11 ? -3 : 0;

                        var inline = new InlineUIContainer();
                        inline.Child = player;

                        // If the Span starts with a InlineUIContainer the RichTextBlock bugs and shows ellipsis
                        if (previous == 0)
                        {
                            NativeUtils.AddRunToCollection(direct, inlines, Icons.ZWNJ, FlowDirection.LeftToRight, TextStyle.None, null, 0, true);
                        }

                        direct.AddToCollection(inlines, direct.GetXamlDirectObject(inline));
                        NativeUtils.AddRunToCollection(direct, inlines, Icons.ZWNJ, FlowDirection.LeftToRight, TextStyle.None, null, 0, true);

                        previous = entity.Offset + entity.Length;
                    }
                }

                if (clean.Text.Length > previous)
                {
                    NativeUtils.AddRunToCollection(direct, inlines, clean.Text, previous, clean.Text.Length - previous, FlowDirection.LeftToRight, TextStyle.None, null, 0, false);
                }
            }
        }
    }
}
