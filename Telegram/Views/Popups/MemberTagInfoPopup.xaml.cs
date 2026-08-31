//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Effects;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Native.Composition;
using Telegram.Navigation;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Popups
{
    public sealed partial class MemberTagInfoPopup : ContentPopup
    {
        public MemberTagInfoPopup(MessageViewModel message)
        {
            InitializeComponent();

            var tag = message.Delegate.GetMemberTag(message, out ChatMemberRank rank);
            var text = string.Empty;

            var sender = message.ClientService.GetTitle(message.SenderId);

            var inline = new InlineUIContainer();

            if (rank == ChatMemberRank.Owner)
            {
                Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.PersonTagFilled24, 2);
                TitleText.Text = Strings.TagInfoOwnerTitle;
                text = Strings.TagInfoOwnerText;

                var color = Color.FromArgb(0xFF, 0x65, 0x60, 0xF6);

                inline.Child = new BadgeControl
                {
                    Text = tag,
                    Background = new SolidColorBrush(color) { Opacity = 0.2 },
                    Foreground = new SolidColorBrush(color.Darken()),
                    Margin = new Thickness(0, 0, 0, -4)
                };
            }
            else if (rank == ChatMemberRank.Admin)
            {
                Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.PersonTagFilled24, 3);
                TitleText.Text = Strings.TagInfoAdminTitle;
                text = Strings.TagInfoAdminText;

                var color = Color.FromArgb(0xFF, 0x75, 0xC8, 0x73);

                inline.Child = new BadgeControl
                {
                    Text = tag,
                    Background = new SolidColorBrush(color) { Opacity = 0.2 },
                    Foreground = new SolidColorBrush(color.Darken()),
                    Margin = new Thickness(0, 0, 0, -4)
                };
            }
            else
            {
                Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.PersonTagFilled24, long.MinValue);
                TitleText.Text = Strings.TagInfoMemberTitle;
                text = Strings.TagInfoMemberText;

                inline.Child = new TextBlock
                {
                    Text = tag,
                    Style = BootStrapper.Current.Resources["InfoBodyTextBlockStyle"] as Style
                };
            }

            // TODO: fix FormattedText.Format, replace
            var formatted = string.Format(text, sender, message.Chat.Title);
            var builder = new FormattedTextBuilder(ClientEx.ParseMarkdown(formatted));

            var index = builder.IndexOf("un1");
            if (index != -1)
            {
                builder.AddEntity(index, 3, new TextEntityTypeMention());
            }

            var markdown = builder.ToFormattedText();

            //var prefix = text.Substring(0, index);
            //var suffix = text.Substring(index + 3);

            var paragraph = new Paragraph();
            //paragraph.Inlines.Add(prefix);
            //paragraph.Inlines.Add(inline);
            //paragraph.Inlines.Add(suffix);

            //Message.Blocks.Add(paragraph);

            var previous = 0;

            foreach (var entity in markdown.Entities.OrderBy(x => x.Offset))
            {
                if (entity.Offset > previous)
                {
                    paragraph.Inlines.Add(markdown.Text.Substring(previous, entity.Offset - previous));
                }

                if (entity.Type is TextEntityTypeMention)
                {
                    paragraph.Inlines.Add(inline);
                }
                else
                {
                    paragraph.Inlines.Add(markdown.Text.Substring(entity.Offset, entity.Length), FontWeights.SemiBold);
                }

                previous = entity.Offset + entity.Length;
            }

            if (markdown.Text.Length > previous)
            {
                paragraph.Inlines.Add(markdown.Text.Substring(previous, markdown.Text.Length - previous));
            }

            Message.Blocks.Add(paragraph);

            UpdateMessage(message, rank);
            UpdateCanEdit(message);

            PrimaryButtonText = Strings.StarRatingButtonUnderstood;
            ButtonsLayout = ContentPopupButtonsLayout.Vertical;
        }

        private void UpdateCanEdit(MessageViewModel message)
        {
            var visible = true;

            if (message.ClientService.TryGetSupergroup(message.Chat, out Supergroup supergroup))
            {
                if (supergroup.Status is ChatMemberStatusAdministrator or ChatMemberStatusCreator)
                {
                    visible = false;
                }
                else if (message.Chat.Permissions.CanEditTag)
                {
                    visible = false;
                }
            }
            else if (message.ClientService.TryGetBasicGroup(message.Chat, out BasicGroup basicGroup))
            {
                if (basicGroup.Status is ChatMemberStatusAdministrator or ChatMemberStatusCreator)
                {
                    visible = false;
                }
                else if (message.Chat.Permissions.CanEditTag)
                {
                    visible = false;
                }
            }

            CanEdit.Visibility = visible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateMessage(MessageViewModel message, ChatMemberRank rank)
        {
            BackgroundControl1.Update(message.ClientService, null);
            Message1.UpdateMockup(message.ClientService, message.Chat, null, Strings.TagInfoMemberTitle, ChatMemberRank.Other);
            Message1.Margin = new Thickness(0);

            if (rank == ChatMemberRank.Owner)
            {
                BackgroundControl2.Update(message.ClientService, null);
                Message2.UpdateMockup(message.ClientService, message.Chat, null, Strings.TagInfoOwnerTitle, ChatMemberRank.Owner);
                Message2.Margin = new Thickness(0);
            }
            else
            {
                BackgroundControl2.Update(message.ClientService, null);
                Message2.UpdateMockup(message.ClientService, message.Chat, null, Strings.TagInfoAdminTitle, ChatMemberRank.Admin);
                Message2.Margin = new Thickness(0);
            }
        }

        private void AlphaMask_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyAlphaMask(sender as UIElement, e.NewSize);
        }

        private void ApplyAlphaMask(UIElement element, Size newSize)
        {
            var layerVisual = CompositionDevice.GetElementLayerVisual(element);
            if (layerVisual == null)
            {
                return;
            }

            var compositor = layerVisual.Compositor;
            var alphaMask = new AlphaMaskEffect
            {
                Name = "AlphaMask",
                Source = new CompositionEffectSourceParameter("source"),
                AlphaMask = new CompositionEffectSourceParameter("mask")
            };

            var next = newSize.ToVector2();
            var top = 48 / next.X;

            var gradientBrush = compositor.CreateLinearGradientBrush();
            gradientBrush.StartPoint = new(0, 0);
            gradientBrush.EndPoint = new(1, 0);
            gradientBrush.MappingMode = CompositionMappingMode.Relative;
            //gradientBrush.ExtendMode = CompositionGradientExtendMode.Clamp;

            gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0, Colors.Transparent));
            gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(top, Colors.White));
            //gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(bottom, Colors.White));
            //gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(1, Colors.Transparent));

            var effectFactory = compositor.CreateEffectFactory(alphaMask);
            var effectBrush = effectFactory.CreateBrush();
            effectBrush.SetSourceParameter("mask", gradientBrush);
            layerVisual.Effect = effectBrush;

        }
    }
}
