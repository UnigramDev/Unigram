using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Chats
{
    public class ChatListViewItem : LazoListViewItem
    {
        private readonly ChatListView _parent;
        private bool _pressed;
        private Visual _visual;
        private SpriteVisual _indicator;

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
            else if (e.OriginalSource is ListViewItemPresenter /*&& e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Touch*/)
            {
                _pressed = true;
            }

            //base.OnPointerPressed(e);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            _pressed = false;

            base.OnPointerReleased(e);
        }

        public override bool CantSelect()
        {
            return ContentTemplateRoot is FrameworkElement element && element.Tag is MessageViewModel message && message.IsService();
        }

        protected override void OnManipulationStarted(ManipulationStartedRoutedEventArgs e)
        {
            if (_pressed)
            {

            }
            else
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

            if (_indicator == null /*&& Math.Abs(e.Cumulative.Translation.X) >= 45*/)
            {
                _indicator = _visual.Compositor.CreateSpriteVisual();
                _indicator.Brush = _visual.Compositor.CreateColorBrush(Windows.UI.Colors.Red);
                _indicator.Size = new System.Numerics.Vector2(30, 30);
                _indicator.CenterPoint = new System.Numerics.Vector3(15);

                ElementCompositionPreview.SetElementChildVisual(this, _indicator);
            }

            var offset = Math.Min(0, Math.Max(-72, (float)e.Cumulative.Translation.X));

            var abs = Math.Abs(offset);
            var percent = abs / 72f;

            var width = (float)ActualWidth;
            var height = (float)ActualHeight;

            _visual.Offset = new System.Numerics.Vector3(offset, 0, 0);

            _indicator.Offset = new System.Numerics.Vector3(width - percent * 60, (height - 30) / 2, 0);
            _indicator.Scale = new System.Numerics.Vector3(0.8f + percent * 0.2f);
            _indicator.Opacity = percent;

            base.OnManipulationDelta(e);
        }

        protected override void OnManipulationCompleted(ManipulationCompletedRoutedEventArgs e)
        {
            e.Handled = true;

            if (_visual != null)
            {
                var animation = _visual.Compositor.CreateSpringVector3Animation();
                animation.InitialValue = _visual.Offset;
                animation.FinalValue = new System.Numerics.Vector3();

                _visual.StartAnimation("Offset", animation);
            }

            if (_indicator != null)
            {
                var animation = _visual.Compositor.CreateSpringVector3Animation();
                animation.InitialValue = _indicator.Offset;
                animation.FinalValue = new System.Numerics.Vector3((float)ActualWidth, ((float)ActualHeight - 30) / 2, 0);

                _indicator.Opacity = 1;
                _indicator.Scale = new System.Numerics.Vector3(1);
                _indicator.StartAnimation("Offset", animation);
            }

            if (Math.Abs(e.Cumulative.Translation.X) >= 45)
            {
                _parent.ViewModel.ReplyToMessage(_parent.ItemFromContainer(this) as MessageViewModel);
            }

            base.OnManipulationCompleted(e);
        }
    }
}
