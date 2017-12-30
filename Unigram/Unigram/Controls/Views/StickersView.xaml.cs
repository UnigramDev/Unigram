using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using LinqToVisualTree;
using Unigram.Common;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages;
using System.Threading.Tasks;
using Unigram.Views.Settings;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class StickersView : UserControl
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public ItemClickEventHandler StickerClick { get; set; }
        public ItemClickEventHandler GifClick { get; set; }

        public StickersView()
        {
            InitializeComponent();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();
        }

        private void Gifs_ItemClick(object sender, ItemClickEventArgs e)
        {
            GifClick?.Invoke(sender, e);

            if (Window.Current.Bounds.Width >= 500)
            {
                Focus(FocusState.Programmatic);
            }
        }

        private void Stickers_ItemClick(object sender, ItemClickEventArgs e)
        {
            StickerClick?.Invoke(sender, e);

            if (Window.Current.Bounds.Width >= 500)
            {
                Focus(FocusState.Programmatic);
            }
        }

        private async void Featured_ItemClick(object sender, ItemClickEventArgs e)
        {
            await StickerSetView.Current.ShowAsync(((TLDocument)e.ClickedItem).StickerSet);
        }

        private void Stickers_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = Stickers.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
            if (scrollingHost != null)
            {
                // Syncronizes GridView with the toolbar ListView
                scrollingHost.ViewChanged += ScrollingHost_ViewChanged;
                ScrollingHost_ViewChanged(null, null);
            }
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Pivot.SelectedIndex != 2)
            {
                Toolbar.SelectedItem = null;
            }
            else
            {
                ScrollingHost_ViewChanged(null, null);
            }

            //if (Pivot.SelectedIndex == 0)
            //{
            //    var text = ViewModel.GetText();
            //    if (string.IsNullOrWhiteSpace(text))
            //    {
            //        ViewModel.SetText("@gif ");
            //        ViewModel.ResolveInlineBot("gif");
            //    }
            //}
        }

        private void Toolbar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Toolbar.SelectedItem != null)
            {
                Pivot.SelectedIndex = 2;
            }

            //Stickers.ScrollIntoView(((TLMessagesStickerSet)Toolbar.SelectedItem).Documents[0]);

            //Pivot.SelectedIndex = Math.Min(1, Toolbar.SelectedIndex);
            //Stickers.ScrollIntoView(ViewModel.StickerSets[Toolbar.SelectedIndex][0]);
        }

        private void Toolbar_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TLMessagesStickerSet set && set.Cover != null)
            {
                //Stickers.ScrollIntoView(set.Cover, ScrollIntoViewAlignment.Leading);
                Stickers.ScrollIntoView(e.ClickedItem, ScrollIntoViewAlignment.Leading);
            }
        }

        public async void Refresh()
        {
            // TODO: memes

            await Task.Delay(100);
            Pivot_SelectionChanged(null, null);
        }

        private void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var scrollingHost = Stickers.ItemsPanelRoot as ItemsWrapGrid;
            if (scrollingHost != null && Pivot.SelectedIndex == 2)
            {
                var first = Stickers.ContainerFromIndex(scrollingHost.FirstVisibleIndex);
                if (first != null)
                {
                    var header = Stickers.GroupHeaderContainerFromItemContainer(first) as GridViewHeaderItem;
                    if (header != null && header != Toolbar.SelectedItem)
                    {
                        Toolbar.SelectedItem = header.Content;
                        Toolbar.ScrollIntoView(header.Content);
                    }
                }
            }
        }

        public bool ToggleActiveView()
        {
            //if (Pivot.SelectedIndex == 2 && !SemanticStickers.IsZoomedInViewActive && SemanticStickers.CanChangeViews)
            //{
            //    SemanticStickers.ToggleActiveView();
            //    return true;
            //}

            return false;
        }

        private void GroupStickers_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.GroupStickersCommand.Execute(null);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(SettingsStickersPage));
        }

        private void Install_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Stickers.InstallCommand.Execute(((Button)sender).DataContext);
        }
    }

    public class ItemsTestPanel : Panel
    {
        private ListViewBase _listView;
        private ScrollViewer _scrollingHost;

        private Dictionary<int, int> itemSpans = new Dictionary<int, int>();
        private List<List<int>> rows;
        private Dictionary<int, int> itemsToRow = new Dictionary<int, int>();
        private int calculatedWidth;
        private double lineHeight = 100;

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_listView == null)
                _listView = this.Ancestors<ListViewBase>().FirstOrDefault() as ListViewBase;

            var count = Children.Count + 0;
            var width = availableSize.Width - _listView.Padding.Left - _listView.Padding.Right;
            var preferredRowSize = prepareLayout(width);

            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Measure(new Size(itemSpans[i] / preferredRowSize * width, lineHeight));
            }

            return new Size(width, rows.Count * lineHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var count = Children.Count + 0;
            var width = finalSize.Width - _listView.Padding.Left - _listView.Padding.Right;
            var preferredRowSize = prepareLayout(width);

            var top = 0d;
            var left = 0d;

            var index = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                for (int j = 0; j < rows[i].Count; j++)
                {
                    if (Children.Count > index)
                    {
                        Children[index].Arrange(new Rect(left, top, itemSpans[index] / preferredRowSize * width, lineHeight));
                    }

                    left += itemSpans[index] / preferredRowSize * width;
                    index++;
                }

                top += lineHeight;
                left = 0;
            }

            return new Size(finalSize.Width, rows.Count * lineHeight);
        }

        private double prepareLayout(double viewPortAvailableSize)
        {
            itemSpans.Clear();
            itemsToRow.Clear();
            int preferredRowSize = Math.Min(getSpanCount(viewPortAvailableSize), 100);

            double totalItemSize = 0;
            int itemsCount = getFlowItemCount();
            int[] weights = new int[itemsCount];
            for (int j = 0; j < itemsCount; j++)
            {
                Size size = sizeForItem(j);
                totalItemSize += (size.Width / size.Height) * preferredRowSize;
                weights[j] = (int)Math.Round(size.Width / size.Height * 100);
            }

            int numberOfRows = (int)Math.Max(Math.Round(totalItemSize / viewPortAvailableSize), 1);

            rows = getLinearPartitionForSequence(weights, numberOfRows);

            int i = 0, a;
            for (a = 0; a < rows.Count; a++)
            {
                List<int> row = rows[a];

                double summedRatios = 0;
                for (int j = i, n = i + row.Count; j < n; j++)
                {
                    Size preferredSize = sizeForItem(j);
                    summedRatios += preferredSize.Width / preferredSize.Height;
                }

                double rowSize = viewPortAvailableSize;

                if (rows.Count == 1 && a == rows.Count - 1)
                {
                    if (row.Count < 2)
                    {
                        rowSize = (float)Math.Floor(viewPortAvailableSize / 3.0f);
                    }
                    else if (row.Count < 3)
                    {
                        rowSize = (float)Math.Floor(viewPortAvailableSize * 2.0f / 3.0f);
                    }
                }

                int spanLeft = getSpanCount(viewPortAvailableSize);
                for (int j = i, n = i + row.Count; j < n; j++)
                {
                    Size preferredSize = sizeForItem(j);
                    int width = (int)Math.Round(rowSize / summedRatios * (preferredSize.Width / preferredSize.Height));
                    int itemSpan;
                    if (itemsCount < 3 || j != n - 1)
                    {
                        itemSpan = (int)(width / viewPortAvailableSize * getSpanCount(viewPortAvailableSize));
                        spanLeft -= itemSpan;
                    }
                    else
                    {
                        itemsToRow[j] = a;
                        itemSpan = spanLeft;
                    }
                    itemSpans[j] = itemSpan;
                }
                i += row.Count;
            }

            return preferredRowSize;
        }

        private int[] getLinearPartitionTable(int[] sequence, int numPartitions)
        {
            int n = sequence.Length;
            int i, j, x;

            int[] tmpTable = new int[n * numPartitions];
            int[] solution = new int[(n - 1) * (numPartitions - 1)];

            for (i = 0; i < n; i++)
            {
                tmpTable[i * numPartitions] = sequence[i] + (i != 0 ? tmpTable[(i - 1) * numPartitions] : 0);
            }

            for (j = 0; j < numPartitions; j++)
            {
                tmpTable[j] = sequence[0];
            }

            for (i = 1; i < n; i++)
            {
                for (j = 1; j < numPartitions; j++)
                {
                    int currentMin = 0;
                    int minX = int.MaxValue;

                    for (x = 0; x < i; x++)
                    {
                        int cost = Math.Max(tmpTable[x * numPartitions + (j - 1)], tmpTable[i * numPartitions] - tmpTable[x * numPartitions]);
                        if (x == 0 || cost < currentMin)
                        {
                            currentMin = cost;
                            minX = x;
                        }
                    }
                    tmpTable[i * numPartitions + j] = currentMin;
                    solution[(i - 1) * (numPartitions - 1) + (j - 1)] = minX;
                }
            }

            return solution;
        }

        private List<List<int>> getLinearPartitionForSequence(int[] sequence, int numberOfPartitions)
        {
            int n = sequence.Length;
            int k = numberOfPartitions;

            if (k <= 0)
            {
                return new List<List<int>>();
            }

            if (k >= n || n == 1)
            {
                List<List<int>> partition = new List<List<int>>(sequence.Length);
                for (int i = 0; i < sequence.Length; i++)
                {
                    List<int> arrayList = new List<int>(1);
                    arrayList.Add(sequence[i]);
                    partition.Add(arrayList);
                }
                return partition;
            }

            int[] solution = getLinearPartitionTable(sequence, numberOfPartitions);
            int solutionRowSize = numberOfPartitions - 1;

            k = k - 2;
            n = n - 1;
            List<List<int>> answer = new List<List<int>>();

            while (k >= 0)
            {
                if (n < 1)
                {
                    answer.Insert(0, new List<int>());
                }
                else
                {
                    List<int> currentAnswer1 = new List<int>();
                    for (int i = solution[(n - 1) * solutionRowSize + k] + 1, range = n + 1; i < range; i++)
                    {
                        currentAnswer1.Add(sequence[i]);
                    }
                    answer.Insert(0, currentAnswer1);
                    n = solution[(n - 1) * solutionRowSize + k];
                }
                k = k - 1;
            }

            List<int> currentAnswer = new List<int>();
            for (int i = 0, range = n + 1; i < range; i++)
            {
                currentAnswer.Add(sequence[i]);
            }
            answer.Insert(0, currentAnswer);
            return answer;
        }










        private Size sizeForItem(int i)
        {
            Size size = getSizeForItem(i);
            if (size.Width == 0)
            {
                size.Width = 100;
            }
            if (size.Height == 0)
            {
                size.Height = 100;
            }
            double aspect = size.Width / size.Height;
            if (aspect > 4.0f || aspect < 0.6f)
            {
                size.Height = size.Width = Math.Max(size.Width, size.Height);
            }
            return size;
        }

        protected Size getSizeForItem(int i)
        {
            if (Children[i] is FrameworkElement child)
            {
                if (child.DataContext is TLDocument document)
                {
                    var size = new Size(100, 100);
                    if (document.Thumb is TLPhotoSize photoSize)
                    {
                        size.Width = photoSize.W;
                        size.Height = photoSize.H;
                    }
                    else if (document.Thumb is TLPhotoCachedSize photoCachedSize)
                    {
                        size.Width = photoCachedSize.W;
                        size.Height = photoCachedSize.H;
                    }

                    for (int j = 0; j < document.Attributes.Count; j++)
                    {
                        if (document.Attributes[j] is TLDocumentAttributeVideo videoAttribute)
                        {
                            size.Width = videoAttribute.W;
                            size.Height = videoAttribute.H;
                            break;
                        }
                        else if (document.Attributes[j] is TLDocumentAttributeImageSize imageSizeAttribute)
                        {
                            size.Width = imageSizeAttribute.W;
                            size.Height = imageSizeAttribute.H;
                            break;
                        }
                    }

                    return size;
                }
            }

            //    TLRPC.Document document = recentGifs.get(i);
            //    size.width = document.thumb != null && document.thumb.w != 0 ? document.thumb.w : 100;
            //    size.height = document.thumb != null && document.thumb.h != 0 ? document.thumb.h : 100;
            //    for (int b = 0; b < document.attributes.size(); b++)
            //    {
            //        TLRPC.DocumentAttribute attribute = document.attributes.get(b);
            //        if (attribute instanceof TLRPC.TL_documentAttributeImageSize || attribute instanceof TLRPC.TL_documentAttributeVideo) {
            //        size.width = attribute.w;
            //        size.height = attribute.h;
            //        break;
            //    }
            //}
            //                return size;

            return new Size(100, 100);
        }

        protected int getSpanCount(double viewPortAvailableSize)
        {
            return (int)(viewPortAvailableSize / 5d);
        }

        protected int getFlowItemCount()
        {
            return Children.Count;
            //return getItemCount();
        }
    }

}
