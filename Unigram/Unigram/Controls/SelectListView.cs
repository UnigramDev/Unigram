using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class SelectListView : ListView
    {
        public SelectListView()
        {
            ContainerContentChanging += OnContainerContentChanging;
            RegisterPropertyChangedCallback(SelectionModeProperty, OnSelectionModeChanged);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            args.RegisterUpdateCallback(OnUpdateCallback);
        }

        private void OnUpdateCallback(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot;
            if (content != null)
            {
                content.IsHitTestVisible = SelectionMode == ListViewSelectionMode.None;
            }
        }

        private void OnSelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var panel = ItemsPanelRoot as ItemsStackPanel;
            if (panel == null)
            {
                return;
            }

            for (int i = panel.FirstCacheIndex; i <= panel.LastCacheIndex; i++)
            {
                var container = ContainerFromIndex(i) as ListViewItem;
                if (container == null)
                {
                    continue;
                }

                var content = container.ContentTemplateRoot;
                if (content != null)
                {
                    content.IsHitTestVisible = SelectionMode == ListViewSelectionMode.None;
                }
            }
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);

            var container = element as ListViewItem;
            if (container == null)
            {
                return;
            }

            var content = container.ContentTemplateRoot;
            if (content != null)
            {
                content.IsHitTestVisible = SelectionMode == ListViewSelectionMode.None;
            }
        }
    }
}
