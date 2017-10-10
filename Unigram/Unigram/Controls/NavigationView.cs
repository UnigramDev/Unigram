using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class NavigationView : ContentControl
    {
        private Button TogglePaneButton;
        private SplitView RootSplitView;
        private ListView MenuItemsHost;

        public NavigationView()
        {
            DefaultStyleKey = typeof(NavigationView);
        }

        protected override void OnApplyTemplate()
        {
            TogglePaneButton = GetTemplateChild("TogglePaneButton") as Button;
            RootSplitView = GetTemplateChild("RootSplitView") as SplitView;
            MenuItemsHost = GetTemplateChild("MenuItemsHost") as ListView;

            TogglePaneButton.Click += Toggle_Click;

            MenuItemsHost.ItemClick += Host_ItemClick;

            foreach (var items in MenuItems)
            {
                MenuItemsHost.Items.Add(items);
            }
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            IsPaneOpen = !IsPaneOpen;
        }

        private void Host_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = MenuItems.FirstOrDefault(x => (string)x.Content == (string)e.ClickedItem) as NavigationViewItem;
            if (item != null)
            {
                ItemClick?.Invoke(this, new NavigationViewItemClickEventArgs(item));
                IsPaneOpen = false;
            }
        }

        #region IsPaneOpen

        public bool IsPaneOpen
        {
            get { return (bool)GetValue(IsPaneOpenProperty); }
            set { SetValue(IsPaneOpenProperty, value); }
        }

        public static readonly DependencyProperty IsPaneOpenProperty =
            DependencyProperty.Register("IsPaneOpen", typeof(bool), typeof(NavigationView), new PropertyMetadata(false));

        #endregion

        #region MenuItems

        public MenuItemsCollection MenuItems
        {
            get
            {
                var value = (MenuItemsCollection)GetValue(MenuItemsProperty);
                if (value == null)
                {
                    value = new MenuItemsCollection();
                    SetValue(MenuItemsProperty, value);
                }

                return value;
            }
        }

        public static readonly DependencyProperty MenuItemsProperty =
            DependencyProperty.Register("MenuItems", typeof(IObservableVector<object>), typeof(Nullable), new PropertyMetadata(null));

        #endregion

        #region PaneFooter

        public object PaneFooter
        {
            get { return (object)GetValue(PaneFooterProperty); }
            set { SetValue(PaneFooterProperty, value); }
        }

        public static readonly DependencyProperty PaneFooterProperty =
            DependencyProperty.Register("PaneFooter", typeof(object), typeof(NavigationView), new PropertyMetadata(null));

        #endregion

        public event NavigationViewItemClickEventHandler ItemClick;
    }

    public delegate void NavigationViewItemClickEventHandler(object sender, NavigationViewItemClickEventArgs args);

    public class NavigationViewItemClickEventArgs : EventArgs
    {
        public NavigationViewItem ClickedItem { get; private set; }

        public NavigationViewItemClickEventArgs(NavigationViewItem item)
        {
            ClickedItem = item;
        }
    }

    public class MenuItemsCollection : ObservableCollection<NavigationViewItemBase>
    {

    }
}
