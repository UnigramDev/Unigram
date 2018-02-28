using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
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

            if (!string.IsNullOrWhiteSpace(webPage.Description))
            {
                if (title.Text.Length > 0 || subtitle.Text.Length > 0)
                {
                    content.Text = Environment.NewLine;
                }

                empty = false;
                content.Text += webPage.Description;
            }
            else
            {
                content.Text = string.Empty;
            }

            label.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        }

        protected async void UpdateInstantView(IProtoService protoService, WebPage webPage, Border yolo, TextBlock yololol, Button button, Run run1, Run run2, Run run3)
        {
            var response = await protoService.SendAsync(new GetWebPageInstantView(webPage.Url, false));
            if (response is WebPageInstantView instantView && instantView.IsFull)
            {

            }
        }

        protected void UpdateInstantView(WebPage webPage, Button button, Run run1, Run run2, Run run3)
        {
            if (webPage.HasInstantView)
            {
                //if (webPage.IsInstantGallery())
                //{
                //    Visibility = Visibility.Collapsed;
                //}
                //else
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
            else
            {
                button.Visibility = Visibility.Collapsed;
            }
        }
    }
}
