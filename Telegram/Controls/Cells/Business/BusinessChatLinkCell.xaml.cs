using System;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Telegram.Controls.Cells.Business
{
    public sealed partial class BusinessChatLinkCell : Grid
    {
        public BusinessChatLinkCell()
        {
            InitializeComponent();
        }

        public void UpdateContent(IClientService clientService, BusinessChatLink chatLink)
        {
            FromLabel.Text = string.IsNullOrEmpty(chatLink.Title)
                ? chatLink.Link
                : chatLink.Title;

            if (string.IsNullOrEmpty(chatLink.Text.Text))
            {
                BriefLabel.Inlines.Clear();
                BriefLabel.Inlines.Add(Strings.NoText);
            }
            else
            {
                UpdateBriefLabel(clientService, chatLink.Text);
            }

            ViewCountLabel.Text = chatLink.ViewCount > 0
                ? Locale.Declension(Strings.R.Clicks, chatLink.ViewCount)
                : Strings.NoClicks;
        }

        private void UpdateBriefLabel(IClientService clientService, FormattedText message)
        {
            BriefLabel.Inlines.Clear();

            if (message != null)
            {
                var clean = message.ReplaceSpoilers();
                var previous = 0;

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
                            BriefLabel.Inlines.Add(clean.Text.Substring(previous, entity.Offset - previous));
                        }

                        var player = new CustomEmojiIcon();
                        player.LoopCount = 0;
                        player.Source = new CustomEmojiFileSource(clientService, customEmoji.CustomEmojiId);
                        player.Style = BootStrapper.Current.Resources["InfoCustomEmojiStyle"] as Style;

                        var inline = new InlineUIContainer();
                        inline.Child = new CustomEmojiContainer(BriefText, player);

                        // If the Span starts with a InlineUIContainer the RichTextBlock bugs and shows ellipsis
                        if (BriefLabel.Inlines.Empty())
                        {
                            BriefLabel.Inlines.Add(Icons.ZWNJ);
                        }

                        BriefLabel.Inlines.Add(inline);
                        BriefLabel.Inlines.Add(Icons.ZWNJ);

                        previous = entity.Offset + entity.Length;
                    }
                }

                if (clean.Text.Length > previous)
                {
                    BriefLabel.Inlines.Add(clean.Text.Substring(previous));
                }
            }
        }
    }
}
