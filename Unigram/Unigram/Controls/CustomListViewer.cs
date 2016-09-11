using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
   public class CustomListViewer: ListView
    {

        public CustomListViewer()
        {
            DefaultStyleKey = typeof(ListView);

        }

        public ScrollViewer ScrollingHost { get; private set; }
        protected override void OnApplyTemplate()
        {
            ScrollingHost = (ScrollViewer)GetTemplateChild("ScrollViewer");
            base.OnApplyTemplate();
        }
    }
}
