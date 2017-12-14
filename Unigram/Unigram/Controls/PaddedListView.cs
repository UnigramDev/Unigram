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
    public class PaddedListView : SelectListView
    {
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            var container = element as ListViewItem;
            var messageCommon = item as TLMessageCommonBase;

            if (container != null && messageCommon != null)
            {
                if (messageCommon.IsService())
                {
                    container.Padding = new Thickness(12, 0, 12, 0);

                    container.HorizontalAlignment = HorizontalAlignment.Stretch;
                    container.Width = double.NaN;
                    container.Height = double.NaN;
                    container.Margin = new Thickness();
                }
                else if (messageCommon is TLMessage message)
                {
                    if (message.IsSaved() || message.ToId is TLPeerChat || message.ToId is TLPeerChannel && !message.IsPost)
                    {
                        if (message.IsOut && !message.IsSaved())
                        {
                            if (message.IsSticker())
                            {
                                container.Padding = new Thickness(12, 0, 12, 0);
                            }
                            else
                            {
                                container.Padding = new Thickness(52, 0, 12, 0);
                            }
                        }
                        else
                        {
                            if (message.IsSticker())
                            {
                                container.Padding = new Thickness(52, 0, 12, 0);
                            }
                            else
                            {
                                container.Padding = new Thickness(52, 0, MessageToShareConverter.Convert(message) ? 12 : 52, 0);
                            }
                        }
                    }
                    else
                    {
                        if (message.IsSticker())
                        {
                            container.Padding = new Thickness(12, 0, 12, 0);
                        }
                        else
                        {
                            if (message.IsOut && !message.IsPost)
                            {
                                container.Padding = new Thickness(52, 0, 12, 0);
                            }
                            else
                            {
                                container.Padding = new Thickness(12, 0, MessageToShareConverter.Convert(message) ? 12 : 52, 0);
                            }
                        }
                    }
                }
            }

            base.PrepareContainerForItemOverride(element, item);
        }
    }
}
