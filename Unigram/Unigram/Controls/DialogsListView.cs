using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class DialogsListView : ListView
    {
        public MainViewModel ViewModel => DataContext as MainViewModel;

        public DialogsListView()
        {
            DragItemsStarting += OnDragItemsStarting;
            DragItemsCompleted += OnDragItemsCompleted;
            DragEnter += OnDragEnter;
            DragOver += OnDragOver;
            Drop += OnDrop;
        }

        #region Drag & Drop

        private int _currentIndex;
        private double _drag;

        private async void OnDrop(object sender, DragEventArgs e)
        {
            var position = e.GetPosition(this);
            var container = ContainerFromIndex(_currentIndex) as ListViewItem;
            var index = Math.Max(0, Math.Min(ViewModel.Dialogs.Items.Count(x => x.IsPinned) - 1, (int)Math.Round((position.Y - 48 - (container.ActualHeight / 2)) / container.ActualHeight)));

            if (index != _currentIndex)
            {
                var source = ItemsSource as IList;
                if (source != null)
                {
                    var item = source[_currentIndex];
                    source.RemoveAt(_currentIndex);
                    source.Insert(index, item);

                    await ViewModel.Dialogs.UpdatePinnedItemsAsync();
                }
            }
        }

        private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            for (int i = 0; i < 5; i++)
            {
                var item = ContainerFromIndex(i) as ListViewItem;
                if (item != null)
                {
                    ElementCompositionPreview.GetElementVisual(item).Opacity = 1;
                    ElementCompositionPreview.GetElementVisual((ListViewItemPresenter)VisualTreeHelper.GetChild(item, 0)).Offset = new System.Numerics.Vector3();
                }
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            var pointer = e.GetPosition(this);
            var controls = VisualTreeHelper.FindElementsInHostCoordinates(pointer, this).OfType<ListViewItem>();
            foreach (var item in controls)
            {
                var index = IndexFromContainer(item);
                if (index < ViewModel.Dialogs.Items.Count(x => x.IsPinned) && index != _currentIndex)
                {
                    var visual = ElementCompositionPreview.GetElementVisual((ListViewItemPresenter)VisualTreeHelper.GetChild(item, 0));
                    var delta = pointer.Y - _drag;
                    var going = delta < 0;
                    var drag = 0d;

                    if (_currentIndex < index && delta > 0) drag = -item.ActualHeight;
                    else if (_currentIndex < index && delta < 0) drag = 0;
                    else if (_currentIndex > index && delta < 0) drag = item.ActualHeight;
                    else if (_currentIndex > index && delta > 0) drag = 0;

                    var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
                    animation.InsertKeyFrame(1, new System.Numerics.Vector3(0, (float)drag, 0));
                    visual.StartAnimation("Offset", animation);
                }
            }

            _drag = pointer.Y;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            var item = e.Items.FirstOrDefault() as TLDialog;
            if (item != null)
            {
                if (item.IsPinned == false)
                {
                    e.Cancel = true;
                }
                else
                {
                    var container = ContainerFromItem(item);
                    ElementCompositionPreview.GetElementVisual(container as ListViewItem).Opacity = 0;

                    _currentIndex = IndexFromContainer(container);
                    _drag = 0;
                }
            }
        }

        #endregion

    }
}
