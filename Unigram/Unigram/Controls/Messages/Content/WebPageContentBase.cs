using System;
using System.Threading;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Unigram.Controls.Messages.Content
{
    public abstract class WebPageContentBase : StackPanel
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
                title.Text = Strings.Resources.ChatBackground;
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
                        run2.Text = $"  {Strings.Resources.InstantView}  ";
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
                    run2.Text = Strings.Resources.OpenGroup;
                    run3.Foreground = null;
                }

                button.Visibility = Visibility.Visible;
            }
            else if (string.Equals(webPage.Type, "telegram_channel", StringComparison.OrdinalIgnoreCase))
            {
                if (run1 != null)
                {
                    run1.Text = run3.Text = string.Empty;
                    run2.Text = Strings.Resources.OpenChannel;
                    run3.Foreground = null;
                }

                button.Visibility = Visibility.Visible;
            }
            else if (string.Equals(webPage.Type, "telegram_message", StringComparison.OrdinalIgnoreCase))
            {
                if (run1 != null)
                {
                    run1.Text = run3.Text = string.Empty;
                    run2.Text = Strings.Resources.OpenMessage;
                    run3.Foreground = null;
                }

                button.Visibility = Visibility.Visible;
            }
            else if (string.Equals(webPage.Type, "telegram_background", StringComparison.OrdinalIgnoreCase))
            {
                if (run1 != null)
                {
                    run1.Text = run3.Text = string.Empty;
                    run2.Text = Strings.Resources.OpenBackground;
                    run3.Foreground = null;
                }

                button.Visibility = Visibility.Visible;
            }
            else
            {
                button.Visibility = Visibility.Collapsed;
            }
        }

        protected async void UpdateInstantView(IProtoService protoService, CancellationToken token, WebPage webPage, Border border, TextBlock label)
        {
            if (webPage.IsInstantGallery())
            {
                var response = await protoService.SendAsync(new GetWebPageInstantView(webPage.Url, false));
                if (response is WebPageInstantView instantView && instantView.IsFull && !token.IsCancellationRequested)
                {
                    var count = CountWebPageMedia(instantView);

                    border.Visibility = Visibility.Visible;
                    label.Text = string.Format(Strings.Resources.Of, 1, count);
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
            if (pageBlock is PageBlockPhoto photoBlock)
            {
                return count + 1;
            }
            else if (pageBlock is PageBlockVideo videoBlock)
            {
                return count + 1;
            }
            else if (pageBlock is PageBlockAnimation animationBlock)
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
