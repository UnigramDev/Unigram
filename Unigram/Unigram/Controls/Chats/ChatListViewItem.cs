using LinqToVisualTree;
using System;
using System.Linq;
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

        private DependencyObject _originalSource;

        public ChatListViewItem(ChatListView parent)
            : base(parent)
        {
            _parent = parent;

            ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateRailsX | ManipulationModes.System;
            AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
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

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _originalSource = e.OriginalSource as DependencyObject;
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            if (_parent.SelectionMode == ListViewSelectionMode.Multiple && !IsSelected)
            {
                e.Handled = CantSelect();
            }

            base.OnPointerPressed(e);
        }

        public override bool CantSelect()
        {
            return ContentTemplateRoot is FrameworkElement element && element.Tag is MessageViewModel message && message.IsService();
        }

        private bool CantReply()
        {
            if (ContentTemplateRoot is FrameworkElement element && element.Tag is MessageViewModel message)
            {
                var chat = message.GetChat();
                if (chat != null && chat.Type is ChatTypeSupergroup supergroupType)
                {
                    var supergroup = _parent.ViewModel.ProtoService.GetSupergroup(supergroupType.SupergroupId);
                    if (supergroup.IsChannel)
                    {
                        return !(supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator);
                    }
                    else if (supergroup.Status is ChatMemberStatusRestricted restricted)
                    {
                        return !restricted.CanSendMessages;
                    }
                }

                return false;
            }

            return true;
        }

        protected override void OnManipulationStarted(ManipulationStartedRoutedEventArgs e)
        {
            if (e.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Touch || CantSelect() || CantReply())
            {
                e.Complete();
            }

            base.OnManipulationStarted(e);
        }

        protected override void OnManipulationDelta(ManipulationDeltaRoutedEventArgs e)
        {
            e.Handled = true;

            if (_visual == null)
            {
                var presenter = VisualTreeHelper.GetChild(this, 0) as ListViewItemPresenter;
                _visual = ElementCompositionPreview.GetElementVisual(presenter);
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

            var originalSource = _originalSource;
            if (originalSource != null)
            {
                _originalSource = null;

                var button = originalSource.AncestorsAndSelf<ButtonBase>().FirstOrDefault() as ButtonBase;
                if (button != null)
                {
                    button.ReleasePointerCaptures();
                }
            }

            base.OnManipulationDelta(e);
        }

        protected override void OnManipulationCompleted(ManipulationCompletedRoutedEventArgs e)
        {
            e.Handled = true;

            CompositionAnimation CreateAnimation(Compositor compositor, Vector3 initial, Vector3 final)
            {
                if (ApiInfo.CanUseDirectComposition)
                {
                    var temp = compositor.CreateSpringVector3Animation();
                    temp.InitialValue = initial;
                    temp.FinalValue = final;

                    return temp;
                }
                else
                {
                    var temp = compositor.CreateVector3KeyFrameAnimation();
                    temp.InsertKeyFrame(0, initial);
                    temp.InsertKeyFrame(1, final);

                    return temp;
                }
            }

            var visual = _visual;
            if (visual != null)
            {
                visual.StartAnimation("Offset", CreateAnimation(visual.Compositor, visual.Offset, new Vector3()));
            }

            var indicator = _indicator;
            if (indicator != null && visual != null)
            {
                var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                var width = (float)ActualWidth;
                var height = (float)ActualHeight;

                indicator.Opacity = 1;
                indicator.Scale = new Vector3(1);
                indicator.StartAnimation("Offset", CreateAnimation(visual.Compositor, indicator.Offset, new Vector3(width, (height - 30) / 2, 0)));

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
