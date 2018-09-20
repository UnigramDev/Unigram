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

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class EmojisView : UserControl
    {
        public event EventHandler Switch;
        public event ItemClickEventHandler ItemClick;

        public EmojisView()
        {
            this.InitializeComponent();

            var separator = ElementCompositionPreview.GetElementVisual(Separator);
            var shadow = separator.Compositor.CreateDropShadow();
            shadow.BlurRadius = 20;
            shadow.Opacity = 0.25f;
            //shadow.Offset = new Vector3(-20, 0, 0);
            shadow.Color = Colors.Black;

            var visual = separator.Compositor.CreateSpriteVisual();
            visual.Shadow = shadow;
            visual.Size = new Vector2(0, 0);
            visual.Offset = new Vector3(0, 0, 0);
            //visual.Clip = visual.Compositor.CreateInsetClip(-100, 0, 19, 0);

            ElementCompositionPreview.SetElementChildVisual(Separator, visual);

            Toolbar.SizeChanged += (s, args) =>
            {
                visual.Size = new Vector2((float)args.NewSize.Width, (float)args.NewSize.Height);
            };

            Pivot.ItemsSource = Emoji.Emojis.Keys;
            Toolbar.ItemsSource = Emoji.Emojis.Keys;
            Toolbar.SelectedIndex = 0;
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

            if (container.Content is string source)
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

            if (container.Content is string source)
            {
                list.ItemsSource = Emoji.Emojis[source];
            }
        }
    }
}
