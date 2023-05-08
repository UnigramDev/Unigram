//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Telegram.Controls.Messages.Content
{
    public abstract class WebPageContentBase : Control
    {
        public WebPageContentBase()
        {

        }

        protected void UpdateWebPage(WebPage webPage, RichTextBlock label, Run title, Run subtitle, Run content)
        {
            var empty = true;

            if (string.Equals(webPage.Type, "telegram_background", StringComparison.OrdinalIgnoreCase))
            {
                empty = false;
                title.Text = Strings.ChatBackground;
                subtitle.Text = string.Empty;
                content.Text = string.Empty;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(webPage.SiteName))
                {
                    empty = false;
                    title.Text = webPage.SiteName;
                }
                else
                {
                    title.Text = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(webPage.Title))
                {
                    if (title.Text.Length > 0)
                    {
                        subtitle.Text = Environment.NewLine;
                    }

                    empty = false;
                    subtitle.Text += webPage.Title;
                }
                else if (!string.IsNullOrWhiteSpace(webPage.Author))
                {
                    if (title.Text.Length > 0)
                    {
                        subtitle.Text = Environment.NewLine;
                    }

                    empty = false;
                    subtitle.Text += webPage.Author;
                }
                else
                {
                    subtitle.Text = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(webPage.Description?.Text))
                {
                    if (title.Text.Length > 0 || subtitle.Text.Length > 0)
                    {
                        content.Text = Environment.NewLine;
                    }

                    empty = false;
                    content.Text += webPage.Description.Text;
                }
                else
                {
                    content.Text = string.Empty;
                }
            }

            label.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        }

        protected void UpdateInstantView(WebPage webPage, Button button, Run run1, Run run2, Run run3)
        {
            if (webPage.InstantViewVersion != 0)
            {
                if (webPage.IsInstantGallery())
                {
                    button.Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (run1 != null)
                    {
                        run1.Text = run3.Text = "\uE611";
                        run2.Text = $"  {Strings.InstantView}  ";
                        run3.Foreground = null;
                    }

                    button.Visibility = Visibility.Visible;
                }
            }
            else if (string.Equals(webPage.Type, "telegram_megagroup", StringComparison.OrdinalIgnoreCase))
            {
                if (run1 != null)
                {
                    run1.Text = run3.Text = string.Empty;
                    run2.Text = Strings.OpenGroup;
                    run3.Foreground = null;
                }

                button.Visibility = Visibility.Visible;
            }
            else if (string.Equals(webPage.Type, "telegram_channel", StringComparison.OrdinalIgnoreCase))
            {
                if (run1 != null)
                {
                    run1.Text = run3.Text = string.Empty;
                    run2.Text = Strings.OpenChannel;
                    run3.Foreground = null;
                }

                button.Visibility = Visibility.Visible;
            }
            else if (string.Equals(webPage.Type, "telegram_message", StringComparison.OrdinalIgnoreCase))
            {
                if (run1 != null)
                {
                    run1.Text = run3.Text = string.Empty;
                    run2.Text = Strings.OpenMessage;
                    run3.Foreground = null;
                }

                button.Visibility = Visibility.Visible;
            }
            else if (string.Equals(webPage.Type, "telegram_voicechat", StringComparison.OrdinalIgnoreCase))
            {
                if (run1 != null)
                {
                    run1.Text = run3.Text = string.Empty;
                    run2.Text = webPage.Url.Contains("?voicechat=") ? Strings.VoipGroupJoinAsSpeaker : Strings.VoipGroupJoinAsLinstener;
                    run3.Foreground = null;
                }

                button.Visibility = Visibility.Visible;
            }
            else if (string.Equals(webPage.Type, "telegram_background", StringComparison.OrdinalIgnoreCase))
            {
                if (run1 != null)
                {
                    run1.Text = run3.Text = string.Empty;
                    run2.Text = Strings.OpenBackground;
                    run3.Foreground = null;
                }

                button.Visibility = Visibility.Visible;
            }
            else if (string.Equals(webPage.Type, "telegram_chatlist", StringComparison.OrdinalIgnoreCase))
            {
                if (run1 != null)
                {
                    run1.Text = run3.Text = string.Empty;
                    run2.Text = Strings.ViewChatList.ToUpper();
                    run3.Foreground = null;
                }

                button.Visibility = Visibility.Visible;
            }
            else if (string.Equals(webPage.Type, "telegram_botapp", StringComparison.OrdinalIgnoreCase))
            {
                if (run1 != null)
                {
                    run1.Text = run3.Text = string.Empty;
                    run2.Text = Strings.BotWebAppInstantViewOpen.ToUpper();
                    run3.Foreground = null;
                }

                button.Visibility = Visibility.Visible;
            }
            else
            {
                button.Visibility = Visibility.Collapsed;
            }
        }

        protected async void UpdateInstantView(IClientService clientService, CancellationToken token, WebPage webPage, Border border, TextBlock label)
        {
            if (webPage.IsInstantGallery())
            {
                var response = await clientService.SendAsync(new GetWebPageInstantView(webPage.Url, false));
                if (response is WebPageInstantView instantView && instantView.IsFull && !token.IsCancellationRequested)
                {
                    var count = CountWebPageMedia(instantView);

                    border.Visibility = Visibility.Visible;
                    label.Text = string.Format(Strings.Of, 1, count);
                }
                else
                {
                    border.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                border.Visibility = Visibility.Collapsed;
            }
        }

        private static int CountBlock(WebPageInstantView webPage, PageBlock pageBlock, int count)
        {
            if (pageBlock is PageBlockPhoto)
            {
                return count + 1;
            }
            else if (pageBlock is PageBlockVideo)
            {
                return count + 1;
            }
            else if (pageBlock is PageBlockAnimation)
            {
                return count + 1;
            }

            return count;
        }

        public static int CountWebPageMedia(WebPageInstantView webPage)
        {
            var result = 0;

            foreach (var block in webPage.PageBlocks)
            {
                if (block is PageBlockSlideshow slideshow)
                {
                    foreach (var item in slideshow.PageBlocks)
                    {
                        result = CountBlock(webPage, item, result);
                    }
                }
                else if (block is PageBlockCollage collage)
                {
                    foreach (var item in collage.PageBlocks)
                    {
                        result = CountBlock(webPage, item, result);
                    }
                }
            }

            return result;
        }
    }
}
