using LinqToVisualTree;
using System;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Messages;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
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

        private bool _forward;
        private bool _reply;

        private ListViewItemPresenter _presenter;

        public ChatListViewItem(ChatListView parent)
            : base(parent)
        {
            _parent = parent;

            ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateRailsX | ManipulationModes.System;
            AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListViewAutomationPeer(this);
        }

        protected override void OnApplyTemplate()
        {
            _presenter = GetTemplateChild("Presenter") as ListViewItemPresenter;

            base.OnApplyTemplate();
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

        private bool CanReply()
        {
            if (ContentTemplateRoot is FrameworkElement element && element.Tag is MessageViewModel message)
            {
                var chat = message.GetChat();
                if (chat != null && chat.Type is ChatTypeSupergroup supergroupType)
                {
                    var supergroup = _parent.ViewModel.ProtoService.GetSupergroup(supergroupType.SupergroupId);
                    if (supergroup.IsChannel)
                    {
                        return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator;
                    }
                    else if (supergroup.Status is ChatMemberStatusRestricted restricted)
                    {
                        return restricted.CanSendMessages;
                    }
                }

                return true;
            }

            return false;
        }

        private bool CanForward()
        {
            if (ContentTemplateRoot is FrameworkElement element && element.Tag is MessageViewModel message)
            {
                return message.CanBeForwarded;
            }

            return false;
        }

        protected override void OnManipulationStarted(ManipulationStartedRoutedEventArgs e)
        {
            _reply = CanReply();
            _forward = CanForward();

            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse || CantSelect() || (!_reply && !_forward))
            {
                e.Complete();
            }

            base.OnManipulationStarted(e);
        }

        protected override void OnManipulationDelta(ManipulationDeltaRoutedEventArgs e)
        {
            e.Handled = true;

            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                e.Complete();
                base.OnManipulationDelta(e);
                return;
            }

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

            var offset = (e.Cumulative.Translation.X < 0 && !_reply) || (e.Cumulative.Translation.X >= 0 && !_forward) ? 0 : Math.Max(0, Math.Min(72, Math.Abs((float)e.Cumulative.Translation.X)));

            var abs = Math.Abs(offset);
            var percent = abs / 72f;

            var width = (float)ActualWidth;
            var height = (float)ActualHeight;

            _visual.Offset = new Vector3(e.Cumulative.Translation.X < 0 ? -offset : offset, 0, 0);

            if (_indicator != null)
            {
                _indicator.Offset = new Vector3(e.Cumulative.Translation.X < 0 ? width - percent * 60 : -30 + percent * 55, (height - 30) / 2, 0);
                _indicator.Scale = new Vector3(e.Cumulative.Translation.X < 0 ? 0.8f + percent * 0.2f : -(0.8f + percent * 0.2f), 0.8f + percent * 0.2f, 1);
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

            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                base.OnManipulationCompleted(e);
                return;
            }

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
                indicator.Scale = new Vector3(e.Cumulative.Translation.X < 0 ? 1 : -1, 1, 1);
                indicator.StartAnimation("Offset", CreateAnimation(visual.Compositor, indicator.Offset, new Vector3(e.Cumulative.Translation.X < 0 ? width : -30, (height - 30) / 2, 0)));

                batch.Completed += (s, args) =>
                {
                    _indicator?.Dispose();
                    _indicator = null;
                };

                batch.End();
            }

            if (ContentTemplateRoot is FrameworkElement element && element.Tag is MessageViewModel message)
            {
                if (e.Cumulative.Translation.X <= -45 && _reply)
                {
                    _parent.ViewModel.MessageReplyCommand.Execute(message);
                }
                else if (e.Cumulative.Translation.X >= 45 && _forward)
                {
                    _parent.ViewModel.MessageForwardCommand.Execute(message);
                }
            }

            base.OnManipulationCompleted(e);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (SettingsService.Current.IsAdaptiveWideEnabled && availableSize.Width >= 880)
            {
                return base.MeasureOverride(new Size(Math.Min(availableSize.Width, 542 /* 432 + 50 + 12 */), availableSize.Height));
            }

            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (SettingsService.Current.IsAdaptiveWideEnabled && finalSize.Width >= 880)
            {
                var size = new Size(Math.Min(finalSize.Width, 542 /* 432 + 50 + 12 */), finalSize.Height);
                _presenter.Arrange(new Rect(0, 0, size.Width, size.Height));

                return finalSize;
            }

            return base.ArrangeOverride(finalSize);

            //var content = ContentTemplateRoot as FrameworkElement;
            //var message = content.Tag as MessageViewModel;

            //if (content is Grid grid)
            //{
            //    content = grid.FindName("Bubble") as FrameworkElement;
            //}

            //if (content is MessageBubble bubble)
            //{
            //    var wide = message.IsOutgoing && finalSize.Width >= 880 && SettingsService.Current.IsAdaptiveWideEnabled;
            //    var alignment = wide || !message.IsOutgoing ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            //    if (alignment != bubble.HorizontalAlignment)
            //    {
            //        bubble.UpdateAdaptive(alignment);
            //        _parent.PrepareContainerForItem(this, message, wide);
            //    }
            //}

            return finalSize;
        }
    }

    public class ChatListViewAutomationPeer : ListViewItemAutomationPeer
    {
        private ChatListViewItem _owner;

        public ChatListViewAutomationPeer(ChatListViewItem owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            if (_owner.ContentTemplateRoot is Grid root)
            {
                var bubble = root.FindName("Bubble") as MessageBubble;
                if (bubble != null)
                {
                    return bubble.GetAutomationName() ?? base.GetNameCore();
                }
            }
            else if (_owner.ContentTemplateRoot is MessageBubble child)
            {
                return child.GetAutomationName() ?? base.GetNameCore();
            }

            return base.GetNameCore();
        }
    }
}
