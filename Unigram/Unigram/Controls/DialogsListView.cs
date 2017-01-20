using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

        private ListViewItem _currentContainer;
        private int _currentIndex;
        private int _lastIndex;
        private bool _lastIndexMoved;
        private double _drag;

        private Dictionary<int, int> _shift = new Dictionary<int, int>();
        private Dictionary<int, int> _override = new Dictionary<int, int>();

        private async void OnDrop(object sender, DragEventArgs e)
        {
            _shift.Clear();

            var position = e.GetPosition(this);
            var container = ContainerFromIndex(_currentIndex) as ListViewItem;

            var indexFloat = (position.Y - 48) / _currentContainer.ActualHeight;
            var indexDelta = indexFloat - Math.Truncate(indexFloat);

            Debug.WriteLine($"Drop, Index: {(int)Math.Truncate(indexFloat)}, Delta: {indexDelta}");

            var index = (int)Math.Max(0, Math.Min(ViewModel.Dialogs.Items.Count(x => x.IsPinned) - 1, Math.Truncate(indexFloat)));
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

        private double _dragStart;
        private int _rowHeight = 60;
        private int _dragging = -1;
        private ObservableCollection<TLDialog> _rows;

        private int floorclamp(double value, int step, int lowest, int highest)
        {
            return (int)Math.Min(Math.Max(Math.Floor(value / step), lowest), highest);
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            var position = e.GetPosition(this);

            #region test

            var firstSetIndex = 0;
            var shift = 0;

            if (_dragStart > position.Y && _dragging > 0)
            {
                shift = -floorclamp(_dragStart - position.Y + (_rowHeight / 2), _rowHeight, 0, _dragging - firstSetIndex);
                for (int from = _dragging, to = _dragging + shift; from > to; --from)
                {
                    //qSwap(_rows[from], _rows[from - 1]);
                    //_rows[from]->yadd = anim::value(_rows[from]->yadd.current() - _rowHeight, 0);
                    //_animStartTimes[from] = ms;

                    _rows.Move(from, from - 1);
                    Debug.WriteLine("Move -1");
                }
            }
            else if (_dragStart < position.Y && _dragging + 1 < _rows.Count)
            {
                shift = floorclamp(position.Y - _dragStart + (_rowHeight / 2), _rowHeight, 0, _rows.Count - _dragging - 1);
                for (int from = _dragging, to = _dragging + shift; from < to; ++from)
                {
                    //qSwap(_rows[from], _rows[from + 1]);
                    //_rows[from]->yadd = anim::value(_rows[from]->yadd.current() + _rowHeight, 0);
                    //_animStartTimes[from] = ms;

                    _rows.Move(from, from + 1);
                    Debug.WriteLine("Move +1");
                }
            }

            if (shift > 0)
            {
                _dragging += shift;
                //_above = _dragging;
                _dragStart += shift * _rowHeight;

                //if (!_a_shifting.animating())
                //{
                //    _a_shifting.start();
                //}
            }

            //_rows[_dragging]->yadd = anim::value(local.y() - _dragStart.y(), local.y() - _dragStart.y());
            //_animStartTimes[_dragging] = 0;
            //_a_shifting.step(getms(), true);

            #endregion

            var indexFloat = (position.Y - 48) / _currentContainer.ActualHeight;
            var indexDelta = indexFloat - Math.Truncate(indexFloat);

            //Debug.WriteLine($"Over, Index: {(int)Math.Truncate(indexFloat)}, Delta: {indexDelta}");

            var index = (int)Math.Max(0, Math.Min(ViewModel.Dialogs.Items.Count(x => x.IsPinned) - 1, Math.Truncate(indexFloat)));
            var original = index + 0;

            if (_shift.ContainsKey(index))
            {
                index = _shift[index];
            }

            if (index != _currentIndex && original != _lastIndex)
            {
                var item = ContainerFromIndex(index) as ListViewItem;
                if (item != null)
                {
                    var delta = position.Y - _drag;
                    var drag = 0;

                    if (delta < 0 && indexDelta < 0.5) drag = _currentIndex > index ? 1 : 0;
                    else if (delta > 0 && indexDelta > 0.5) drag = _currentIndex < index ? -1 : 0;

                    _lastIndexMoved = drag != 0;
                    _shift[original + drag] = original;

                    if (_shift.ContainsKey(original) && _shift[original] == original && drag != 0)
                    {
                        _shift.Remove(original);
                    }

                    var anim = drag == 1 ? item.ActualHeight : drag == -1 ? -item.ActualHeight : 0;
                    var visual = ElementCompositionPreview.GetElementVisual((ListViewItemPresenter)VisualTreeHelper.GetChild(item, 0));
                    var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
                    animation.InsertKeyFrame(1, new System.Numerics.Vector3(0, (float)anim, 0));
                    visual.StartAnimation("Offset", animation);
                }
            }

            _drag = position.Y;
            _lastIndex = _lastIndexMoved ? original : _lastIndex;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            _drag = e.GetPosition(this).Y;
            _dragStart = _drag;
            _lastIndex = -1;
            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;
            e.DragUIOverride.IsContentVisible = false;
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
                    var container = ContainerFromItem(item) as ListViewItem;
                    ElementCompositionPreview.GetElementVisual(container as ListViewItem).Opacity = 0;
                    //container.RenderTransform = new TranslateTransform { X = container.ActualWidth };

                    _currentContainer = container;
                    _currentIndex = IndexFromContainer(container);
                    _drag = 0;

                    _dragging = _currentIndex;
                    _rows = new ObservableCollection<TLDialog>(ViewModel.Dialogs.Items.Where(x => x.IsPinned));
                }
            }
        }

        #endregion

    }
}
