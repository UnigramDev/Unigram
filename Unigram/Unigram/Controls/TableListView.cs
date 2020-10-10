using System;
using System.Numerics;
using Unigram.Common;
using Windows.Foundation.Metadata;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls
{
    public class TableListView : ListView
    {
        public TableListView()
        {
            DefaultStyleKey = typeof(TableListView);
            Loaded += OnLoaded;
            //ContainerContentChanging += OnContainerContentChanging;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (GroupStyle != null)
            {
                return;
            }

            var root = ItemsPanelRoot;
            if (root == null || !ApiInformation.IsMethodPresent("Windows.UI.Composition.Compositor", "CreateGeometricClip"))
            {
                return;
            }

            var visual = ElementCompositionPreview.GetElementVisual(root);
            var rect = visual.Compositor.CreateRoundedRectangleGeometry();

            var radius = ItemsPanelCornerRadius;
            var size = root.GetActualSize();
            var offset = new Vector2();

            if ((radius.TopLeft == 0 && radius.TopRight == 0) || (radius.BottomLeft == 0 || radius.BottomRight == 0))
            {
                size.Y += (float)radius.BottomLeft;
                offset.Y = radius.TopLeft == 0 ? -(float)radius.BottomLeft : 0;
            }

            rect.Size = size;
            rect.Offset = offset;
            rect.CornerRadius = new Vector2((float)Math.Max(radius.TopLeft, radius.BottomRight));

            visual.Clip = visual.Compositor.CreateGeometricClip(rect);

            root.SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var root = ItemsPanelRoot;
            var visual = ElementCompositionPreview.GetElementVisual(root);
            var clip = visual.Clip as CompositionGeometricClip;
            var rect = clip.Geometry as CompositionRoundedRectangleGeometry;

            var radius = ItemsPanelCornerRadius;
            var size = e.NewSize.ToVector2();
            var offset = new Vector2();

            if ((radius.TopLeft == 0 && radius.TopRight == 0) || (radius.BottomLeft == 0 || radius.BottomRight == 0))
            {
                size.Y += (float)radius.BottomLeft;
                offset.Y = radius.TopLeft == 0 ? -(float)radius.BottomLeft : 0;
            }

            rect.Size = size;
            rect.Offset = offset;
        }

        #region ItemsPanelCornerRadius

        public CornerRadius ItemsPanelCornerRadius
        {
            get { return (CornerRadius)GetValue(ItemsPanelCornerRadiusProperty); }
            set { SetValue(ItemsPanelCornerRadiusProperty, value); }
        }

        public static readonly DependencyProperty ItemsPanelCornerRadiusProperty =
            DependencyProperty.Register("ItemsPanelCornerRadius", typeof(CornerRadius), typeof(TableListView), new PropertyMetadata(default, OnItemsPanelCornerRadiusChanged));

        private static void OnItemsPanelCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (((TableListView)d).GroupStyle != null)
            {
                return;
            }

            var root = ((TableListView)d).ItemsPanelRoot;
            if (root == null || !ApiInformation.IsMethodPresent("Windows.UI.Composition.Compositor", "CreateGeometricClip"))
            {
                return;
            }

            var visual = ElementCompositionPreview.GetElementVisual(root);

            var clip = visual.Clip as CompositionGeometricClip;
            if (clip == null)
            {
                return;
            }

            var rect = clip.Geometry as CompositionRoundedRectangleGeometry;

            var radius = ((TableListView)d).ItemsPanelCornerRadius;
            var size = root.GetActualSize();
            var offset = new Vector2();

            if ((radius.TopLeft == 0 && radius.TopRight == 0) || (radius.BottomLeft == 0 || radius.BottomRight == 0))
            {
                size.Y += (float)radius.BottomLeft;
                offset.Y = radius.TopLeft == 0 ? -(float)radius.BottomLeft : 0;
            }

            rect.Size = size;
            rect.Offset = offset;
            rect.CornerRadius = new Vector2((float)Math.Max(radius.TopLeft, radius.BottomRight));
        }

        #endregion
    }
}
