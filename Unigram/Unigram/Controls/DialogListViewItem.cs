using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class DialogListViewItem : ListViewItem
    {
        public DialogListViewItem()
        {
            RegisterPropertyChangedCallback(IsSelectedProperty, OnIsSelectedChanged);
        }

        private void OnIsSelectedChanged(DependencyObject sender, DependencyProperty dp)
        {
            var content = ContentTemplateRoot as UserControl;
            if (content != null)
            {
                VisualStateManager.GoToState(content, IsSelected ? "Selected" : "Normal", false);
            }
        }
    }
}
