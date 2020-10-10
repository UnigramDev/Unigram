using Telegram.Td.Api;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class PaddedListView : LazoListView
    {
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            var container = element as ListViewItem;
            var message = item as MessageViewModel;
            var chat = message?.GetChat();

            if (container != null && message != null && chat != null)
            {
                var action = message.IsSaved() || message.IsShareable();

                if (message.IsService())
                {
                    container.Padding = new Thickness(12, 0, 12, 0);

                    container.HorizontalAlignment = HorizontalAlignment.Stretch;
                    container.Width = double.NaN;
                    container.Height = double.NaN;
                    container.Margin = new Thickness();
                }
                else if (message.IsSaved() || (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup) && !message.IsChannelPost)
                {
                    if (message.IsOutgoing && !message.IsSaved())
                    {
                        if (message.Content is MessageSticker || message.Content is MessageVideoNote)
                        {
                            container.Padding = new Thickness(12, 0, 12, 0);
                        }
                        else
                        {
                            container.Padding = new Thickness(50, 0, 12, 0);
                        }
                    }
                    else
                    {
                        if (message.Content is MessageSticker || message.Content is MessageVideoNote)
                        {
                            container.Padding = new Thickness(12, 0, 12, 0);
                        }
                        else
                        {
                            container.Padding = new Thickness(12, 0, action ? 14 : 50, 0);
                        }
                    }
                }
                else
                {
                    if (message.Content is MessageSticker || message.Content is MessageVideoNote)
                    {
                        container.Padding = new Thickness(12, 0, 12, 0);
                    }
                    else
                    {
                        if (message.IsOutgoing && !message.IsChannelPost)
                        {
                            container.Padding = new Thickness(50, 0, 12, 0);
                        }
                        else
                        {
                            container.Padding = new Thickness(12, 0, action ? 14 : 50, 0);
                        }
                    }
                }
            }

            base.PrepareContainerForItemOverride(element, item);
        }
    }
}
