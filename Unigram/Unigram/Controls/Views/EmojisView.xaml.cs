using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class EmojisView : UserControl
    {
        public event EventHandler Switch;
        public event ItemClickEventHandler ItemClick;

        public EmojisView()
        {
            this.InitializeComponent();

            var shadow = DropShadowEx.Attach(Separator, 20, 0.25f);

            Toolbar.SizeChanged += (s, args) =>
            {
                shadow.Size = args.NewSize.ToVector2();
            };

            Pivot.ItemsSource = Emoji.Emojis.Keys;
            Toolbar.ItemsSource = Emoji.Emojis.Keys;
            Toolbar.SelectedIndex = 0;

            //Follodf.ShowMode = FlyoutShowMode.Transient;
        }

        public void SetView(bool widget)
        {
            VisualStateManager.GoToState(this, widget ? "FilledState" : "NarrowState", false);
        }

        private void Toolbar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Pivot.SelectedIndex = Toolbar.SelectedIndex;
        }

        private void Toolbar_ItemClick(object sender, ItemClickEventArgs e)
        {

        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ItemClick?.Invoke(this, e);
        }

        private void Switch_Click(object sender, RoutedEventArgs e)
        {
            Switch?.Invoke(this, EventArgs.Empty);
        }

        private void ListView_Loaded(object sender, RoutedEventArgs e)
        {
            var root = sender as UserControl;
            var container = Pivot.ContainerFromItem(root.DataContext) as PivotItem;
            var index = Pivot.IndexFromContainer(container);

            if (index != Pivot.SelectedIndex)
            {
                return;
            }

            var list = root.FindName("List") as GridView;
            if (list == null || list.ItemsSource != null)
            {
                return;
            }

            if (container.Content is EmojiGroup source)
            {
                list.ItemsSource = Emoji.Emojis[source];
            }
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var container = Pivot.ContainerFromIndex(Pivot.SelectedIndex) as PivotItem;
            if (container == null)
            {
                return;
            }

            var root = container.ContentTemplateRoot as UserControl;
            if (root == null)
            {
                return;
            }

            var list = root.FindName("List") as GridView;
            if (list == null || list.ItemsSource != null)
            {
                return;
            }

            if (container.Content is EmojiGroup source)
            {
                list.ItemsSource = Emoji.Emojis[source];
            }
        }
    }
}
