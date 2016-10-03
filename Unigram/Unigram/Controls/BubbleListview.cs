using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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
    }
}
