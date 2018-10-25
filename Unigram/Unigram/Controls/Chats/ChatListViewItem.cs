using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.Foundation.Metadata;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls.Chats
{
    public class ChatListViewItem : LazoListViewItem
    {
        private readonly ChatListView _parent;

        private Visual _visual;
        private ContainerVisual _indicator;

        public ChatListViewItem(ChatListView parent)
            : base(parent)
        {
            _parent = parent;

            ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.System;
        }

        #region ContentMargin

        public Thickness ContentMargin
        {
            get { return (Thickness)GetValue(ContentMarginProperty); }
            set { SetValue(ContentMarginProperty, value); }
        }

        public static readonly DependencyProperty ContentMarginProperty =
            DependencyProperty.Register("ContentMargin", typeof(Thickness), typeof(ChatListViewItem), new PropertyMetadata(default(Thickness)));

        #endregion

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            if (_parent.SelectionMode == ListViewSelectionMode.Multiple && !IsSelected)
            {
                e.Handled = CantSelect();
            }

            //base.OnPointerPressed(e);
        }

        public override bool CantSelect()
        {
            return ContentTemplateRoot is FrameworkElement element && element.Tag is MessageViewModel message && message.IsService();
        }

        protected override void OnManipulationStarted(ManipulationStartedRoutedEventArgs e)
        {
            //if (e.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Touch || CantSelect())
            //{
            //    e.Complete();
            //}

            base.OnManipulationStarted(e);
        }

        protected override void OnManipulationDelta(ManipulationDeltaRoutedEventArgs e)
        {
            e.Handled = true;

            if (_visual == null)
            {
                var presenter = VisualTreeHelper.GetChild(this, 0) as ListViewItemPresenter;
                _visual = ElementCompositionPreview.GetElementVisual(presenter);

                if (e.Container.PointerCaptures != null && e.Container.PointerCaptures.Count > 0)
                {
                    e.Container.ReleasePointerCaptures();
                }
            }

            if (_indicator == null && ApiInfo.CanUseDirectComposition /*&& Math.Abs(e.Cumulative.Translation.X) >= 45*/)
            {
                var sprite = _visual.Compositor.CreateSpriteVisual();
                sprite.Size = new Vector2(30, 30);
                sprite.CenterPoint = new Vector3(15);

                var surface = LoadedImageSurface.StartLoadFromUri(new Uri("ms-appx:///Assets/Images/Reply.png"));
                surface.LoadCompleted += (s, args) =>
                {
                    sprite.Brush = _visual.Compositor.CreateSurfaceBrush(s);
                };

                var ellipse = _visual.Compositor.CreateEllipseGeometry();
                ellipse.Radius = new Vector2(15);

                var ellipseShape = _visual.Compositor.CreateSpriteShape(ellipse);
                ellipseShape.FillBrush = _visual.Compositor.CreateColorBrush((Windows.UI.Color)App.Current.Resources["MessageServiceBackgroundColor"]);
                ellipseShape.Offset = new Vector2(15);

                var shape = _visual.Compositor.CreateShapeVisual();
                shape.Shapes.Add(ellipseShape);
                shape.Size = new Vector2(30, 30);

                _indicator = _visual.Compositor.CreateContainerVisual();
                _indicator.Children.InsertAtBottom(shape);
                _indicator.Children.InsertAtTop(sprite);
                _indicator.Size = new Vector2(30, 30);
                _indicator.CenterPoint = new Vector3(15);

                ElementCompositionPreview.SetElementChildVisual(this, _indicator);
            }

            var offset = Math.Min(0, Math.Max(-72, (float)e.Cumulative.Translation.X));

            var abs = Math.Abs(offset);
            var percent = abs / 72f;

            var width = (float)ActualWidth;
            var height = (float)ActualHeight;

            _visual.Offset = new Vector3(offset, 0, 0);

            if (_indicator != null)
            {
                _indicator.Offset = new Vector3(width - percent * 60, (height - 30) / 2, 0);
                _indicator.Scale = new Vector3(0.8f + percent * 0.2f);
                _indicator.Opacity = percent;
            }

            base.OnManipulationDelta(e);
        }

        protected override void OnManipulationCompleted(ManipulationCompletedRoutedEventArgs e)
        {
            e.Handled = true;

            var visual = _visual;
            if (visual != null)
            {
                CompositionAnimation animation;
                if (ApiInfo.CanUseDirectComposition)
                {
                    var temp = visual.Compositor.CreateSpringVector3Animation();
                    temp.InitialValue = visual.Offset;
                    temp.FinalValue = new Vector3();
                    animation = temp;
                }
                else
                {
                    var temp = visual.Compositor.CreateVector3KeyFrameAnimation();
                    temp.InsertKeyFrame(0, visual.Offset);
                    temp.InsertKeyFrame(1, new Vector3());
                    animation = temp;
                }

                visual.StartAnimation("Offset", animation);
            }

            var indicator = _indicator;
            if (indicator != null && visual != null)
            {
                var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                CompositionAnimation animation;
                if (ApiInfo.CanUseDirectComposition)
                {
                    var temp = visual.Compositor.CreateSpringVector3Animation();
                    temp.InitialValue = indicator.Offset;
                    temp.FinalValue = new Vector3((float)ActualWidth, ((float)ActualHeight - 30) / 2, 0);
                    animation = temp;
                }
                else
                {
                    var temp = visual.Compositor.CreateVector3KeyFrameAnimation();
                    temp.InsertKeyFrame(0, indicator.Offset);
                    temp.InsertKeyFrame(1, new Vector3((float)ActualWidth, ((float)ActualHeight - 30) / 2, 0));
                    animation = temp;
                }

                indicator.Opacity = 1;
                indicator.Scale = new Vector3(1);
                indicator.StartAnimation("Offset", animation);

                batch.Completed += (s, args) =>
                {
                    _indicator?.Dispose();
                    _indicator = null;
                };

                batch.End();
            }

            if (e.Cumulative.Translation.X <= -45)
            {
                _parent.ViewModel.ReplyToMessage(_parent.ItemFromContainer(this) as MessageViewModel);
            }

            base.OnManipulationCompleted(e);
        }
    }
}
