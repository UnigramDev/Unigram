using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
   public class BubbleListView : ListView
    {
        public ScrollViewer ScrollingHost { get; private set; }

        public BubbleListView()
        {
            DefaultStyleKey = typeof(ListView);
        }

        protected override void OnApplyTemplate()
        {
            ScrollingHost = (ScrollViewer)GetTemplateChild("ScrollViewer");

            base.OnApplyTemplate();
        }

        private int count;
        protected override DependencyObject GetContainerForItemOverride()
        {
            Debug.WriteLine($"New listview item: {++count}");
            return new BubbleListViewItem(this);
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            var bubble = element as BubbleListViewItem;
            var messageCommon = item as TLMessageCommonBase;

            if (bubble != null && messageCommon != null)
            {
                if (item is TLMessageService)
                {
                    bubble.Padding = new Thickness(12, 0, 12, 0);
                }
                else
                {
                    var message = item as TLMessage;
                    if (message != null && message.ToId is TLPeerChat || message.ToId is TLPeerChannel && !message.IsPost)
                    {
                        //// WARNING: this is an hack. 
                        //// We should verify that this still works every Windows release!

                        //// ItemsStackPanel has a bug and it disregards
                        //// about headers width when measuring the items,
                        //// causing them to take all the ItemsStackPanel width
                        //// and getting truncated out of it at the right side.

                        //// The code below prevents this behavior adding
                        //// the exact padding/margin to compehensate the headers width
                        //if (message.IsOut)
                        //{
                        //    bubble.Padding = new Thickness(44, 0, 12, 0);
                        //    bubble.Margin = new Thickness(12, 0, 0, 0);
                        //    bubble.HorizontalAlignment = HorizontalAlignment.Right;
                        //}
                        //else
                        //{
                        //    bubble.Padding = new Thickness(12, 0, 56, 0);
                        //    bubble.Margin = new Thickness(0, 0, 44, 0);
                        //    bubble.HorizontalAlignment = HorizontalAlignment.Left;
                        //}

                        if (message.IsOut)
                        {
                            if (message.IsSticker())
                            {
                                bubble.Padding = new Thickness(12, 0, 12, 0);
                            }
                            else
                            {
                                bubble.Padding = new Thickness(56, 0, 12, 0);
                            }
                        }
                        else
                        {
                            if (message.IsSticker())
                            {
                                bubble.Padding = new Thickness(56, 0, 12, 0);
                            }
                            else
                            {
                                bubble.Padding = new Thickness(56, 0, 56, 0);
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
                                bubble.Padding = new Thickness(56, 0, 12, 0);
                            }
                            else
                            {
                                bubble.Padding = new Thickness(12, 0, 56, 0);
                            }
                        }
                    }
                }
            }

            base.PrepareContainerForItemOverride(element, item);
        }
    }
}
