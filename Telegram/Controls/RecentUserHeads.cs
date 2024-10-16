//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.Foundation;

namespace Telegram.Controls
{
    public delegate void RecentUserHeadChangedHandler(ProfilePicture sender, MessageSender messageSender);

    public partial class RecentUserHeadChangedEventArgs : EventArgs
    {

    }

    public partial class RecentUserHeads : Control
    {
        private readonly RecentUserCollection _items = new RecentUserCollection();
        private readonly HashSet<UIElement> _toBeRemoved = new HashSet<UIElement>();

        private Grid _layoutRoot;

        private readonly int _maxCount = 3;
        private readonly int _maxIndex = 2;

        private int _itemSize = 32;
        private int _itemOverlap = 10;

        public RecentUserHeads()
        {
            DefaultStyleKey = typeof(RecentUserHeads);
        }

        #region ItemSize

        public int ItemSize
        {
            get { return (int)GetValue(ItemSizeProperty); }
            set { SetValue(ItemSizeProperty, value); }
        }

        public static readonly DependencyProperty ItemSizeProperty =
            DependencyProperty.Register("ItemSize", typeof(int), typeof(RecentUserHeads), new PropertyMetadata(32, OnItemSizeChanged));

        private static void OnItemSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((RecentUserHeads)d)._itemSize = (int)e.NewValue;
        }

        #endregion

        #region ItemOverlap



        public int ItemOverlap
        {
            get { return (int)GetValue(ItemOverlapProperty); }
            set { SetValue(ItemOverlapProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ItemOverlap.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemOverlapProperty =
            DependencyProperty.Register("ItemOverlap", typeof(int), typeof(RecentUserHeads), new PropertyMetadata(10, OnItemOverlapChanged));

        private static void OnItemOverlapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((RecentUserHeads)d)._itemOverlap = (int)e.NewValue;
        }

        #endregion

        public RecentUserCollection Items => _items;

        public event RecentUserHeadChangedHandler RecentUserHeadChanged;

        protected override void OnApplyTemplate()
        {
            _layoutRoot = GetTemplateChild("LayoutRoot") as Grid;
            _items.CollectionChanged += OnCollectionChanged;

            Reset();
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewStartingIndex <= _maxIndex && e.NewItems.Count > 0)
            {
                InsertItem(e.NewStartingIndex, e.NewItems[0]);
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldStartingIndex <= _maxIndex)
            {
                RemoveItem(e.OldStartingIndex, _items.Count > _maxIndex ? _items[_maxIndex] : null);
            }
            else if (e.Action == NotifyCollectionChangedAction.Move)
            {
                if (e.OldStartingIndex > _maxIndex && e.NewStartingIndex <= _maxIndex)
                {
                    InsertItem(e.NewStartingIndex, e.NewItems[0]);
                }
                else if (e.OldStartingIndex <= _maxIndex && e.NewStartingIndex > _maxIndex)
                {
                    RemoveItem(e.OldStartingIndex, _items.Count > _maxIndex ? _items[_maxIndex] : null);
                }
                else if (e.OldStartingIndex <= _maxIndex && e.NewStartingIndex <= _maxIndex)
                {
                    MoveItem(e.OldStartingIndex, e.NewStartingIndex);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                Reset();
            }
        }

        private void Reset()
        {
            _layoutRoot.Children.Clear();

            for (int i = 0; i < _items.Count; i++)
            {
                var container = CreateContainer(_items[i]);
                var visual = ElementComposition.GetElementVisual(container);

                visual.Offset = new Vector3(i * (_itemSize + 4 - _itemOverlap), 0, 0);

                Canvas.SetZIndex(container, -i);
                _layoutRoot.Children.Insert(i, container);
            }
        }

        private void InsertItem(int index, object item)
        {
            var batch = CreateScopedBatch();

            var container = CreateContainer(item);
            AnimateAdding(container, index);

            for (int i = index, real = index; i < _layoutRoot.Children.Count; i++)
            {
                if (_toBeRemoved.Contains(_layoutRoot.Children[i]))
                {
                    continue;
                }

                if (real >= _maxCount - 1)
                {
                    _toBeRemoved.Add(_layoutRoot.Children[i]);

                    AnimateRemoving(_layoutRoot.Children[i], real + 1);
                    real++;
                }
                else
                {
                    AnimateMoving(_layoutRoot.Children[i], real, real + 1);
                    UpdateContainer(_layoutRoot.Children[i], real + 1);
                    real++;
                }
            }

            index = Math.Max(Math.Min(_layoutRoot.Children.Count - 1, index), 0);

            Canvas.SetZIndex(container, -index);
            _layoutRoot.Children.Insert(index, container);

            InvalidateMeasure();
            AnimateAlignment();

            batch.End();
        }

        private void RemoveItem(int index, object item)
        {
            var batch = CreateScopedBatch();
            var count = _layoutRoot.Children.Count;

            UIElement container = null;
            if (item != null)
            {
                container = CreateContainer(item);
                AnimateAdding(container, _maxIndex);
            }

            for (int i = index, real = index; i < _layoutRoot.Children.Count; i++)
            {
                if (_toBeRemoved.Contains(_layoutRoot.Children[i]))
                {
                    continue;
                }

                if (real == index || real >= _maxCount)
                {
                    _toBeRemoved.Add(_layoutRoot.Children[i]);
                    count--;

                    AnimateRemoving(_layoutRoot.Children[i], real + 1);
                    real++;
                }
                else
                {
                    AnimateMoving(_layoutRoot.Children[i], real, real - 1);
                    UpdateContainer(_layoutRoot.Children[i], real - 1);
                    real++;
                }
            }

            if (container != null && count < _maxCount)
            {
                Canvas.SetZIndex(container, -_maxCount);
                _layoutRoot.Children.Insert(count, container);
            }

            InvalidateMeasure();
            AnimateAlignment();

            batch.End();
        }

        private void MoveItem(int oldIndex, int newIndex)
        {
            if (oldIndex >= _layoutRoot.Children.Count || newIndex >= _layoutRoot.Children.Count)
            {
                return;
            }

            _layoutRoot.Children.Move((uint)oldIndex, (uint)newIndex);

            var compositor = BootStrapper.Current.Compositor;
            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            var start = Math.Min(oldIndex, newIndex);
            var end = Math.Max(oldIndex, newIndex);

            for (int i = start; i <= end; i++)
            {
                AnimateMoving(_layoutRoot.Children[i], -1, i);
            }

            batch.End();
        }

        private UIElement CreateContainer(object item)
        {
            var picture = new ProfilePicture();
            picture.Width = _itemSize;
            picture.Height = _itemSize;

            if (item is MessageSender sender)
            {
                RecentUserHeadChanged?.Invoke(picture, sender);
            }

            var borderBrush = new Binding();
            borderBrush.Source = this;
            borderBrush.Path = new PropertyPath(nameof(BorderBrush));

            var container = new Border();
            container.Width = _itemSize + 4;
            container.Height = _itemSize + 4;
            container.VerticalAlignment = VerticalAlignment.Top;
            container.HorizontalAlignment = HorizontalAlignment.Left;
            container.BorderThickness = new Thickness(2);
            container.CornerRadius = new CornerRadius((_itemSize + 4) / 2);
            container.Child = picture;
            container.Tag = item;
            container.SetBinding(Border.BorderBrushProperty, borderBrush);

            return container;
        }

        private void UpdateContainer(UIElement container, int newIndex)
        {
            if (_items.Count > newIndex &&
                container is Border border
                && border.Child is ProfilePicture picture
                && border.Tag is MessageSender previous)
            {
                if (_items[newIndex] is MessageSender sender && !sender.AreTheSame(previous))
                {
                    RecentUserHeadChanged?.Invoke(picture, sender);
                }
            }
        }

        private CompositionScopedBatch CreateScopedBatch()
        {
            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                foreach (var element in _toBeRemoved)
                {
                    _layoutRoot.Children.Remove(element);
                }

                _toBeRemoved.Clear();
                InvalidateMeasure();
            };

            return batch;
        }

        private void AnimateAdding(UIElement container, int index)
        {
            Canvas.SetZIndex(container, -index);

            var visual = ElementComposition.GetElementVisual(container);
            visual.Offset = new Vector3(index * (_itemSize + 4 - _itemOverlap), 0, 0);
            visual.CenterPoint = new Vector3((_itemSize + 4) / 2);

            var addingScale = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            addingScale.InsertKeyFrame(0.0f, new Vector3(0));
            addingScale.InsertKeyFrame(0.9f, new Vector3(1.1f, 1.1f, 1));
            addingScale.InsertKeyFrame(1.0f, new Vector3(1));
            //addingScale.Duration = TimeSpan.FromSeconds(1);

            var addingOpacity = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            addingOpacity.InsertKeyFrame(0.0f, 0);
            addingOpacity.InsertKeyFrame(1.0f, 1);
            //addingOpacity.Duration = TimeSpan.FromSeconds(1);

            visual.StartAnimation("Scale", addingScale);
            visual.StartAnimation("Opacity", addingOpacity);
        }

        private void AnimateRemoving(UIElement container, int index)
        {
            Canvas.SetZIndex(container, -index);

            var child = ElementComposition.GetElementVisual(container);
            child.CenterPoint = new Vector3((_itemSize + 4) / 2);

            var removingScale = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            removingScale.InsertKeyFrame(0.0f, new Vector3(1));
            removingScale.InsertKeyFrame(1.0f, new Vector3(0));
            //removingScale.Duration = TimeSpan.FromSeconds(1);

            var removingOpacity = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            removingOpacity.InsertKeyFrame(0.0f, 1);
            removingOpacity.InsertKeyFrame(1.0f, 0);
            //removingOpacity.Duration = TimeSpan.FromSeconds(1);

            child.StartAnimation("Scale", removingScale);
            child.StartAnimation("Opacity", removingOpacity);
        }

        private void AnimateMoving(UIElement container, int oldIndex, int newIndex)
        {
            Canvas.SetZIndex(container, -newIndex);

            var child = ElementComposition.GetElementVisual(container);
            var offset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (oldIndex >= 0)
            {
                offset.InsertKeyFrame(0, new Vector3(oldIndex * (_itemSize + 4 - _itemOverlap), 0, 0));
            }

            offset.InsertKeyFrame(1, new Vector3(newIndex * (_itemSize + 4 - _itemOverlap), 0, 0));
            //offset.Duration = TimeSpan.FromSeconds(1);

            child.StartAnimation("Offset", offset);
        }

        private void AnimateAlignment()
        {
            if (HorizontalAlignment == HorizontalAlignment.Center)
            {
                // Not needed in templated control
                ElementCompositionPreview.SetIsTranslationEnabled(_layoutRoot, true);

                var maxWidth = ((_itemSize + 4) * _maxCount) - (_itemOverlap * (_maxCount - 1));

                var count = Math.Min(_maxCount, Math.Max(1, _items.Count));
                var diff = maxWidth - (count * (float)(_itemSize + 4) - ((count - 1) * _itemOverlap));

                var offset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                offset.InsertKeyFrame(1, new Vector3(diff / 2, 0, 0));
                //offset.Duration = TimeSpan.FromSeconds(1);

                var visual = ElementComposition.GetElementVisual(_layoutRoot);
                visual.StartAnimation("Translation", offset);
            }
            else if (HorizontalAlignment == HorizontalAlignment.Right)
            {
                // Not needed in templated control
                ElementCompositionPreview.SetIsTranslationEnabled(_layoutRoot, true);

                var maxWidth = ((_itemSize + 4) * _maxCount) - (_itemOverlap * (_maxCount - 1));

                var count = Math.Min(_maxCount, Math.Max(1, _items.Count));
                var diff = maxWidth - (count * (float)(_itemSize + 4) - ((count - 1) * _itemOverlap));

                var offset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                offset.InsertKeyFrame(0, new Vector3(-diff, 0, 0));
                offset.InsertKeyFrame(1, new Vector3());
                //offset.Duration = TimeSpan.FromSeconds(10);

                var visual = ElementComposition.GetElementVisual(_layoutRoot);
                //visual.StartAnimation("Translation", offset);
            }
        }

        // TODO: properly implement measure and arrange overrides

        protected override Size MeasureOverride(Size availableSize)
        {
            var count = Math.Min(_maxCount, _items.Count);
            var width = count * (float)(_itemSize + 4) - ((count - 1) * _itemOverlap);

            base.MeasureOverride(availableSize);
            return new Size(width, _itemSize + 4);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return base.ArrangeOverride(finalSize);
        }
    }

    public partial class RecentUserCollection : ObservableCollection<MessageSender>
    {
        public RecentUserCollection()
        {

        }

        public void ReplaceDiff(IEnumerable<MessageSender> items)
        {
            var origin = items?.ToList();
            var destination = this;

            if (origin == null)
            {
                return;
            }
            else if (destination.Empty())
            {
                foreach (var item in origin)
                {
                    destination.Add(item);
                }

                return;
            }

            for (int i = 0; i < origin.Count; i++)
            {
                var item = origin[i];
                var index = -1;

                for (int j = 0; j < destination.Count; j++)
                {
                    if (destination[j].AreTheSame(item))
                    {
                        index = j;
                        break;
                    }
                }

                if (index > -1 && index != i)
                {
                    destination.Move(index, Math.Min(i, destination.Count - 1));
                }
                else if (index == -1)
                {
                    destination.Insert(Math.Min(i, destination.Count), item);
                }
            }

            for (int i = 0; i < destination.Count; i++)
            {
                var item = destination[i];
                var index = -1;

                for (int j = 0; j < origin.Count; j++)
                {
                    if (origin[j].AreTheSame(item))
                    {
                        index = j;
                        break;
                    }
                }

                if (index == -1)
                {
                    destination.Remove(item);
                    i--;
                }
            }
        }
    }
}
