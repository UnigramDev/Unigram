using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Converters;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class PaddedListView : ListView
    {
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            var bubble = element as ListViewItem;
            var messageCommon = item as TLMessageCommonBase;

            if (bubble != null && messageCommon != null)
            {
                if (messageCommon.IsService())
                {
                    bubble.Padding = new Thickness(12, 0, 12, 0);
                }
                else
                {
                    var message = item as TLMessage;
                    if (message != null && message.ToId is TLPeerChat || message.ToId is TLPeerChannel && !message.IsPost)
                    {
                        if (message.IsOut)
                        {
                            if (message.IsSticker())
                            {
                                bubble.Padding = new Thickness(12, 0, 12, 0);
                            }
                            else
                            {
                                bubble.Padding = new Thickness(52, 0, 12, 0);
                            }
                        }
                        else
                        {
                            if (message.IsSticker())
                            {
                                bubble.Padding = new Thickness(52, 0, 12, 0);
                            }
                            else
                            {
                                bubble.Padding = new Thickness(52, 0, MessageToShareConverter.Convert(message) ? 12 : 52, 0);
                            }
                        }
                    }
                    else
                    {
                        if (message.IsSticker())
                        {
                            bubble.Padding = new Thickness(12, 0, 12, 0);
                        }
                        else
                        {
                            if (message.IsOut && !message.IsPost)
                            {
                                bubble.Padding = new Thickness(52, 0, 12, 0);
                            }
                            else
                            {
                                bubble.Padding = new Thickness(12, 0, MessageToShareConverter.Convert(message) ? 12 : 52, 0);
                            }
                        }
                    }
                }
            }

            base.PrepareContainerForItemOverride(element, item);
        }
    }
}
