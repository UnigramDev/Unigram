using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Telegram.Api.TL;

namespace Unigram.Controls
{
    public class SwipeListViewItem : ContentControl
    {
        public TLMessage ViewModel => DataContext as TLMessage;

        private TranslateTransform ContentDragTransform;
        private TranslateTransform RightTransform;

        private Border RightContainer;

        private ContentPresenter DragContainer;

        public SwipeListViewItem()
        {
            DefaultStyleKey = typeof(SwipeListViewItem);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            ContentDragTransform = (TranslateTransform)GetTemplateChild("ContentDragTransform");

            RightTransform = (TranslateTransform)GetTemplateChild("RightTransform");
            RightContainer = (Border)GetTemplateChild("RightContainer");

            DragContainer = (ContentPresenter)GetTemplateChild("DragContainer");

            DragContainer.PointerReleased += OnPointerReleased;
        }

        /// <summary>
        /// Resets the <see cref="SwipeListViewItem"/> swipe state.
        /// </summary>
        public void ResetSwipe()
        {
            Clip = null;

            if (ContentDragTransform != null)
            {
                ContentDragTransform.X = 0;
                RightTransform.X = (RightContainer.ActualWidth + 20);
            }
        }

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            ResetSwipe();

            base.OnContentChanged(oldContent, newContent);
        }

        private SwipeListDirection _direction = SwipeListDirection.None;

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            //if (_direction != SwipeListDirection.None)
            {
                e.Handled = true;
            }
        }

        protected override void OnManipulationDelta(ManipulationDeltaRoutedEventArgs e)
        {
#if !DEBUG
            if (e.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Touch)
            {
                e.Complete();
                return;
            }
#endif
            var cancel = false;

            var channel = ViewModel.Parent as TLChannel;
            if (channel != null)
            {
                if (channel.IsBroadcast)
                {
                    cancel = !(channel.IsCreator || channel.HasAdminRights);
                }
            }

            if (cancel)
            {
                e.Complete();
                return;
            }

            var delta = e.Delta.Translation;
            var cumulative = e.Cumulative.Translation;

            var target = ((ActualWidth / 5) * 1);

            if (_direction == SwipeListDirection.None)
            {
                _direction = SwipeListDirection.Right;

                RightTransform.X = (RightContainer.ActualWidth + 20);

                if (_direction == SwipeListDirection.Right && RightBehavior != SwipeListBehavior.Disabled)
                {
                    RightContainer.Visibility = Visibility.Visible;
                }
                else
                {
                    //e.Complete();
                    return;
                }
            }

            /*if (_direction == SwipeListDirection.Left)
            {
                var area1 = LeftBehavior == SwipeListBehavior.Collapse ? 1.5 : 2.5;
                var area2 = LeftBehavior == SwipeListBehavior.Collapse ? 2 : 3;

                ContentDragTransform.X = Math.Max(0, Math.Min(cumulative.X, ActualWidth));
                DragClipTransform.X = Math.Max(0, Math.Min(cumulative.X, ActualWidth));

                if (ContentDragTransform.X < target * area1)
                {
                    LeftTransform.X += (delta.X / 1.5);
                }
                else if (ContentDragTransform.X >= target * area1 && ContentDragTransform.X < target * area2)
                {
                    LeftTransform.X += (delta.X * 2.5);
                }
                else
                {
                    LeftTransform.X = Math.Max(0, Math.Min(cumulative.X, ActualWidth)) - LeftContainer.ActualWidth;
                }

                if (ContentDragTransform.X == 0 && delta.X < 0)
                {
                    _direction = SwipeListDirection.None;
                }
            }
            else*/
            if (_direction == SwipeListDirection.Right)
            {
                var area1 = RightBehavior == SwipeListBehavior.Collapse ? 1.5 : 2.5;
                var area2 = RightBehavior == SwipeListBehavior.Collapse ? 2 : 3;

                ContentDragTransform.X = Math.Max(-ActualWidth, Math.Min(cumulative.X, 0));

                if (ContentDragTransform.X > -(target * area1))
                {
                    RightTransform.X += (delta.X / 1.5);
                }
                else if (ContentDragTransform.X <= -(target * area1) && ContentDragTransform.X > -(target * area2))
                {
                    RightTransform.X += (delta.X * 2.5);
                }
                else
                {
                    RightTransform.X = Math.Max(-ActualWidth, Math.Min(cumulative.X, 0)) + RightContainer.ActualWidth;
                }

                //if (ContentDragTransform.X == 0 && delta.X > 0)
                //{
                //    _direction = SwipeListDirection.None;
                //}
            }

            base.OnManipulationDelta(e);
        }

        protected override void OnManipulationCompleted(ManipulationCompletedRoutedEventArgs e)
        {
            var target = (ActualWidth / 5) * 2;
            Storyboard currentAnim;

            if (_direction == SwipeListDirection.Right && ContentDragTransform.X <= -target)
            {
                if (RightBehavior == SwipeListBehavior.Collapse)
                    currentAnim = CollapseAnimation(SwipeListDirection.Right, true);
                else
                    currentAnim = ExpandAnimation(SwipeListDirection.Right);
            }
            else
            {
                currentAnim = CollapseAnimation(SwipeListDirection.Right, false);
            }

            currentAnim.Begin();
            _direction = SwipeListDirection.None;

            base.OnManipulationCompleted(e);
        }

        private Storyboard CollapseAnimation(SwipeListDirection direction, bool raise)
        {
            var animDrag = CreateDouble(0, 300, ContentDragTransform, "TranslateTransform.X", new ExponentialEase { EasingMode = EasingMode.EaseOut });
            var animRight = CreateDouble((RightContainer.ActualWidth + 20), 300, RightTransform, "TranslateTransform.X", new ExponentialEase { EasingMode = EasingMode.EaseOut });

            var currentAnim = new Storyboard();
            currentAnim.Children.Add(animDrag);
            currentAnim.Children.Add(animRight);

            currentAnim.Completed += (s, args) =>
            {
                ContentDragTransform.X = 0;
                RightTransform.X = (RightContainer.ActualWidth + 20);
            };

            if (raise)
            {
                ItemSwipe?.Invoke(this, new ItemSwipeEventArgs(Content, direction));
            }

            return currentAnim;
        }

        private Storyboard ExpandAnimation(SwipeListDirection direction)
        {
            var currentAnim = new Storyboard();
            if (direction == SwipeListDirection.Right)
            {
                var animDrag = CreateDouble(-ActualWidth - 100, 300, ContentDragTransform, "TranslateTransform.X", new ExponentialEase { EasingMode = EasingMode.EaseOut });
                var animRight = CreateDouble(-ActualWidth - 100, 300, RightTransform, "TranslateTransform.X", new ExponentialEase { EasingMode = EasingMode.EaseIn });

                currentAnim.Children.Add(animDrag);
                currentAnim.Children.Add(animRight);
            }

            currentAnim.Completed += (s, args) =>
            {
                ItemSwipe?.Invoke(this, new ItemSwipeEventArgs(Content, direction));
            };

            return currentAnim;
        }

        private DoubleAnimation CreateDouble(double to, int duration, DependencyObject target, string path, EasingFunctionBase easing)
        {
            var anim = new DoubleAnimation();
            anim.To = to;
            anim.Duration = new Duration(TimeSpan.FromMilliseconds(duration));
            anim.EasingFunction = easing;

            Storyboard.SetTarget(anim, target);
#if SILVERLIGHT
            Storyboard.SetTargetProperty(anim, new PropertyPath(path));
#else
            Storyboard.SetTargetProperty(anim, path);
#endif

            return anim;
        }

        /// <summary>
        /// Occurs when the item is swiped from left or right.
        /// </summary>
        public event ItemSwipeEventHandler ItemSwipe;

        #region RightContentTemplate
        public DataTemplate RightContentTemplate
        {
            get { return (DataTemplate)GetValue(RightContentTemplateProperty); }
            set { SetValue(RightContentTemplateProperty, value); }
        }

        /// <summary>
        /// Identifies the RightContentTemplate dependency property.
        /// </summary>
        public static readonly DependencyProperty RightContentTemplateProperty =
            DependencyProperty.Register("RightContentTemplate", typeof(DataTemplate), typeof(SwipeListViewItem), new PropertyMetadata(null));
        #endregion

        #region RightBackground
        public Brush RightBackground
        {
            get { return (Brush)GetValue(RightBackgroundProperty); }
            set { SetValue(RightBackgroundProperty, value); }
        }

        /// <summary>
        /// Identifies the RightBackground dependency property.
        /// </summary>
        public static readonly DependencyProperty RightBackgroundProperty =
            DependencyProperty.Register("RightBackground", typeof(Brush), typeof(SwipeListViewItem), new PropertyMetadata(null));
        #endregion

        #region RightBehavior
        public SwipeListBehavior RightBehavior
        {
            get { return (SwipeListBehavior)GetValue(RightBehaviorProperty); }
            set { SetValue(RightBehaviorProperty, value); }
        }

        /// <summary>
        /// Identifies the RightBehavior dependency property.
        /// </summary>
        public static readonly DependencyProperty RightBehaviorProperty =
            DependencyProperty.Register("RightBehavior", typeof(SwipeListBehavior), typeof(SwipeListViewItem), new PropertyMetadata(SwipeListBehavior.Collapse));
        #endregion
    }
}
