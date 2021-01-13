using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Numerics;
using Unigram.Collections;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class RecentUserHeads : Control
    {
        private RecentUserCollection _items = new RecentUserCollection();
        private HashSet<UIElement> _toBeRemoved = new HashSet<UIElement>();

        private Grid _layoutRoot;

        private int _maxCount = 3;
        private int _maxIndex = 2;

        public RecentUserHeads()
        {
            DefaultStyleKey = typeof(RecentUserHeads);
        }

        public RecentUserCollection Items => _items;

        public Func<int, ImageSource> GetPicture { get; set; }

        protected override void OnApplyTemplate()
        {
            _layoutRoot = GetTemplateChild("LayoutRoot") as Grid;
            _items.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewStartingIndex <= _maxIndex)
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
                _layoutRoot.Children.Clear();

                for (int i = 0; i < _items.Count; i++)
                {
                    var container = CreateContainer(_items[i]);

                    Canvas.SetZIndex(container, -i);
                    _layoutRoot.Children.Insert(i, container);
                }
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
                    real++;
                }
            }

            Canvas.SetZIndex(container, -index);
            _layoutRoot.Children.Insert(index, container);

            AnimateAlignment();

            batch.End();
        }

        private void RemoveItem(int index, object item)
        {
            var batch = CreateScopedBatch();

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

                    AnimateRemoving(_layoutRoot.Children[i], real + 1);
                    real++;
                }
                else
                {
                    AnimateMoving(_layoutRoot.Children[i], real, real - 1);
                    real++;
                }
            }

            if (container != null)
            {
                Canvas.SetZIndex(container, -_maxCount);
                _layoutRoot.Children.Insert(_maxCount, container);
            }

            AnimateAlignment();

            batch.End();
        }

        private void MoveItem(int oldIndex, int newIndex)
        {
            _layoutRoot.Children.Move((uint)oldIndex, (uint)newIndex);

            var compositor = Window.Current.Compositor;
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
            var picture = new Image();
            picture.Width = 32;
            picture.Height = 32;
            picture.Stretch = Stretch.UniformToFill;

            if (item is int userId && GetPicture != null)
            {
                picture.Source = GetPicture(userId);
            }

            var rounder = new Border();
            rounder.Width = 32;
            rounder.Height = 32;
            rounder.CornerRadius = new CornerRadius(32 / 2);
            rounder.Child = picture;

            var container = new Border();
            container.Width = 36;
            container.Height = 36;
            container.VerticalAlignment = VerticalAlignment.Top;
            container.HorizontalAlignment = HorizontalAlignment.Left;
            container.BorderBrush = new SolidColorBrush(Colors.White);
            container.BorderThickness = new Thickness(2);
            container.CornerRadius = new CornerRadius(36 / 2);
            container.Child = rounder;
            container.Tag = item;

            return container;
        }

        private CompositionScopedBatch CreateScopedBatch()
        {
            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                lock (_toBeRemoved)
                {
                    foreach (var element in _toBeRemoved)
                    {
                        _layoutRoot.Children.Remove(element);
                    }

                    _toBeRemoved.Clear();
                }
            };

            return batch;
        }

        private void AnimateAdding(UIElement container, int index)
        {
            Canvas.SetZIndex(container, -index);

            var visual = ElementCompositionPreview.GetElementVisual(container);
            visual.Offset = new Vector3(index * 26, 0, 0);
            visual.CenterPoint = new Vector3(36 / 2);

            var addingScale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            addingScale.InsertKeyFrame(0.0f, new Vector3(0));
            addingScale.InsertKeyFrame(0.9f, new Vector3(1.1f, 1.1f, 1));
            addingScale.InsertKeyFrame(1.0f, new Vector3(1));
            //addingScale.Duration = TimeSpan.FromSeconds(1);

            var addingOpacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            addingOpacity.InsertKeyFrame(0.0f, 0);
            addingOpacity.InsertKeyFrame(1.0f, 1);
            //addingOpacity.Duration = TimeSpan.FromSeconds(1);

            visual.StartAnimation("Scale", addingScale);
            visual.StartAnimation("Opacity", addingOpacity);
        }

        private void AnimateRemoving(UIElement container, int index)
        {
            Canvas.SetZIndex(container, -index);

            var child = ElementCompositionPreview.GetElementVisual(container);

            var removingScale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            removingScale.InsertKeyFrame(0.0f, new Vector3(1));
            removingScale.InsertKeyFrame(1.0f, new Vector3(0));
            //removingScale.Duration = TimeSpan.FromSeconds(1);

            var removingOpacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            removingOpacity.InsertKeyFrame(0.0f, 1);
            removingOpacity.InsertKeyFrame(1.0f, 0);
            //removingOpacity.Duration = TimeSpan.FromSeconds(1);

            child.StartAnimation("Scale", removingScale);
            child.StartAnimation("Opacity", removingOpacity);
        }

        private void AnimateMoving(UIElement container, int oldIndex, int newIndex)
        {
            Canvas.SetZIndex(container, -newIndex);

            var child = ElementCompositionPreview.GetElementVisual(container);
            var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (oldIndex >= 0)
            {
                offset.InsertKeyFrame(0, new Vector3(oldIndex * 26, 0, 0));
            }

            offset.InsertKeyFrame(1, new Vector3(newIndex * 26, 0, 0));
            //offset.Duration = TimeSpan.FromSeconds(1);

            child.StartAnimation("Offset", offset);
        }

        private void AnimateAlignment()
        {
            if (HorizontalAlignment == HorizontalAlignment.Center)
            {
                // Not needed in templated control
                ElementCompositionPreview.SetIsTranslationEnabled(_layoutRoot, true);

                var count = Math.Min(_maxCount, Math.Max(1, _items.Count));
                var diff = 88f - (count * 36f - ((count - 1) * 10f));

                var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                offset.InsertKeyFrame(1, new Vector3(diff / 2, 0, 0));
                //offset.Duration = TimeSpan.FromSeconds(1);

                var visual = ElementCompositionPreview.GetElementVisual(_layoutRoot);
                visual.StartAnimation("Translation", offset);
            }
        }

    }

    public class RecentUserCollection  : MvxObservableCollection<int>
    {

    }
}
