using System;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

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
                CustomEmojiIcon.Add(BriefText, BriefLabel.Inlines, clientService, chatLink.Text, "InfoCustomEmojiStyle");
            }

            ViewCountLabel.Text = chatLink.ViewCount > 0
                ? Locale.Declension(Strings.R.Clicks, chatLink.ViewCount)
                : Strings.NoClicks;
        }
    }
}
