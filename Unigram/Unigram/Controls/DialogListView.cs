using System;
using System.Collections;
using System.Collections.ObjectModel;
using Telegram.Td.Api;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class DialogListView : ListView
    {
        public MainViewModel ViewModel => DataContext as MainViewModel;

        public DialogListView()
        {
            ContainerContentChanging += OnContainerContentChanging;
            RegisterPropertyChangedCallback(SelectionModeProperty, OnSelectionModeChanged);

            DragItemsStarting += OnDragItemsStarting;
            DragItemsCompleted += OnDragItemsCompleted;
            DragEnter += OnDragEnter;
            DragOver += OnDragOver;
            Drop += OnDrop;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            args.RegisterUpdateCallback(OnUpdateCallback);
        }

        private void OnUpdateCallback(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as UserControl;
            if (content != null)
            {
                VisualStateManager.GoToState(content, args.ItemContainer.IsSelected && SelectionMode == ListViewSelectionMode.Single ? "Selected" : "Normal", false);
            }
        }

        private void OnSelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var panel = ItemsPanelRoot as ItemsStackPanel;
            if (panel == null)
            {
                return;
            }

            for (int i = panel.FirstCacheIndex; i <= panel.LastCacheIndex; i++)
            {
                var container = ContainerFromIndex(i) as ListViewItem;
                if (container == null)
                {
                    continue;
                }

                var content = container.ContentTemplateRoot as UserControl;
                if (content != null)
                {
                    VisualStateManager.GoToState(content, container.IsSelected && SelectionMode == ListViewSelectionMode.Single ? "Selected" : "Normal", false);
                }
            }
        }

        #region Drag & Drop

        private ObservableCollection<Chat> _rows;
        private ListViewItem _currentContainer;
        private object _currentItem;
        private int _currentIndex;
        private int _originalIndex;
        private double _drag;

        private async void OnDrop(object sender, DragEventArgs e)
        {
            for (int i = 0; i < 5; i++)
            {
                var container = ContainerFromIndex(i) as ListViewItem;
                if (container != null)
                {
                    container.RenderTransform = null;
                    Canvas.SetZIndex(container, 0);

                    var visual = ElementCompositionPreview.GetElementVisual((ListViewItemPresenter)VisualTreeHelper.GetChild(container, 0));
                    visual.StopAnimation("Offset");
                    visual.Offset = new System.Numerics.Vector3();
                }
            }

            if (_currentContainer == null) return;

            //var position = e.GetPosition(this);

            //var indexFloat = (position.Y - 48) / _currentContainer.ActualHeight;
            //var indexDelta = indexFloat - Math.Truncate(indexFloat);

            ////Debug.WriteLine($"Drop, Index: {(int)Math.Truncate(indexFloat)}, Delta: {indexDelta}");

            //var index = (int)Math.Max(0, Math.Min(_rows.Count - 1, Math.Truncate(indexFloat)));
            if (_currentIndex != _originalIndex)
            {
                var source = ItemsSource as IList;
                if (source != null)
                {
                    var item = source[_originalIndex];
                    source.RemoveAt(_originalIndex);
                    source.Insert(_currentIndex, item);

                    //await ViewModel.Dialogs.UpdatePinnedItemsAsync();
                }
            }
        }

        private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            for (int i = 0; i < 5; i++)
            {
                var container = ContainerFromIndex(i) as ListViewItem;
                if (container != null)
                {
                    container.RenderTransform = null;
                    Canvas.SetZIndex(container, 0);

                    var visual = ElementCompositionPreview.GetElementVisual((ListViewItemPresenter)VisualTreeHelper.GetChild(container, 0));
                    visual.StopAnimation("Offset");
                    visual.Offset = new System.Numerics.Vector3();
                }
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (_currentContainer == null) return;

            var position = e.GetPosition(this);

            var indexFloat = (position.Y - 48) / _currentContainer.ActualHeight;
            var indexDelta = indexFloat - Math.Truncate(indexFloat);

            //Debug.WriteLine($"Over, Index: {(int)Math.Truncate(indexFloat)}, Delta: {indexDelta}");

            var index = (int)Math.Max(0, Math.Min(_rows.Count - 1, Math.Truncate(indexFloat)));
            if (index != _currentIndex)
            {
                var item = _rows[index];

                var container = ContainerFromItem(item) as ListViewItem;
                if (container != null)
                {
                    var original = IndexFromContainer(container);

                    var delta = position.Y - _drag;
                    var drag = 0;

                    if (delta < 0 && indexDelta < 0.5) drag = _currentIndex > original ? 1 : 0;
                    else if (delta > 0 && indexDelta > 0.5) drag = _currentIndex < original ? -1 : 0;

                    _rows.Move(index, original + drag);
                    _currentIndex = _rows.IndexOf(_currentItem as Chat);

                    var anim = drag == 1 ? container.ActualHeight : drag == -1 ? -container.ActualHeight : 0;
                    var visual = ElementCompositionPreview.GetElementVisual((ListViewItemPresenter)VisualTreeHelper.GetChild(container, 0));
                    var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
                    animation.InsertKeyFrame(1, new System.Numerics.Vector3(0, (float)anim, 0));
                    visual.StartAnimation("Offset", animation);
                }
            }

            var translate = _currentContainer.RenderTransform as TranslateTransform;
            translate.Y += position.Y - _drag;

            _drag = position.Y;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (_currentContainer == null) return;

            _drag = e.GetPosition(this).Y;

            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;
            e.DragUIOverride.IsContentVisible = false;
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            //var item = e.Items.FirstOrDefault() as Chat;
            //if (item != null)
            //{
            //    if (item.IsPinned == false)
            //    {
            //        e.Cancel = true;
            //    }
            //    else
            //    {
            //        var container = ContainerFromItem(item) as ListViewItem;
            //        //ElementCompositionPreview.GetElementVisual(container as ListViewItem).Opacity = 0;
            //        //container.RenderTransform = new TranslateTransform { X = container.ActualWidth };

            //        _currentContainer = container;
            //        _currentItem = item;
            //        _currentIndex = IndexFromContainer(container);
            //        _originalIndex = _currentIndex;
            //        _drag = 0;

            //        _currentContainer.RenderTransform = new TranslateTransform();
            //        Canvas.SetZIndex(_currentContainer, 100000);

            //        //var transform = _currentContainer.TransformToVisual(Window.Current.Content);
            //        //var point = transform.TransformPoint(new Point());

            //        //var center = _currentContainer.ActualHeight / 2d;
            //        //var difference = (Window.Current.CoreWindow.PointerPosition.Y - Window.Current.CoreWindow.Bounds.Y) - (point.Y + center);

            //        //var translate = _currentContainer.RenderTransform as TranslateTransform;
            //        //translate.Y += difference;

            //        _rows = new ObservableCollection<Chat>(ViewModel.Chats.Items.Where(x => x.IsPinned));
            //    }
            //}
        }

        #endregion

    }
}
