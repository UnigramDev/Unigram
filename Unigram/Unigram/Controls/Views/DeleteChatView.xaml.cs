using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class DeleteChatView : ContentDialog
    {
        public DeleteChatView(IProtoService protoService, Chat chat, bool clear)
        {
            InitializeComponent();

            Photo.Source = PlaceholderHelper.GetChat(protoService, chat, 72);

            Title.Text = clear ? Strings.Resources.ClearHistory : Strings.Resources.DeleteChatUser; // protoService.GetTitle(chat);

            var user = protoService.GetUser(chat);
            var basicGroup = protoService.GetBasicGroup(chat);
            var supergroup = protoService.GetSupergroup(chat);

            if (clear)
            {
                if (user != null)
                {
                    if (chat.Type is ChatTypeSecret)
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureClearHistoryWithSecretUser, user.GetFullName()));
                    }
                    else
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureClearHistoryWithUser, user.GetFullName()));
                    }
                }
                else if (basicGroup != null)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureClearHistoryGroup));
                }
                else if (supergroup != null)
                {
                    if (supergroup.IsChannel)
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureClearHistoryChannel));
                    }
                    else if (string.IsNullOrEmpty(supergroup.Username))
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureClearHistoryWithChat, chat.Title));
                    }
                    else
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureClearHistoryGroup));
                    }
                }
            }
            else if (user != null)
            {
                if (chat.Type is ChatTypeSecret)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureDeleteThisChatWithSecretUser, user.GetFullName()));
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureDeleteThisChatWithUser, user.GetFullName()));
                }
            }
            else if (basicGroup != null)
            {
                TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureDeleteAndExitName, chat.Title));
            }
            else if (supergroup != null)
            {
                if (supergroup.IsChannel)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.ChannelLeaveAlertWithName, chat.Title));
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.MegaLeaveAlertWithName, chat.Title));
                }
            }

            if (clear)
            {
                PrimaryButtonText = Strings.Resources.ClearHistory;
            }
            else if (user != null || basicGroup != null)
            {
                PrimaryButtonText = Strings.Resources.DeleteChatUser;
            }
            else if (supergroup != null)
            {
                if (supergroup.IsChannel)
                {
                    PrimaryButtonText = Strings.Resources.LeaveChannelMenu;
                }
                else
                {
                    PrimaryButtonText = Strings.Resources.LeaveMegaMenu;
                }
            }

            SecondaryButtonText = Strings.Resources.Cancel;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
