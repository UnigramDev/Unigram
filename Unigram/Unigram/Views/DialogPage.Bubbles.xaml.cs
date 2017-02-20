using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Converters;
using Unigram.Core.Dependency;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization.DateTimeFormatting;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Views
{
    public partial class DialogPage : Page
    {
        private ItemsStackPanel _panel;

        private async void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (lvDialogs.ScrollingHost.ScrollableHeight - lvDialogs.ScrollingHost.VerticalOffset < 120)
            {
                ViewModel.UpdatingScrollMode = ItemsUpdatingScrollMode.KeepLastItemInView;
                Arrow.Visibility = Visibility.Collapsed;
            }
            else
            {
                Arrow.Visibility = Visibility.Visible;
            }

            //if (ViewModel.Peer is TLInputPeerUser)
            //{
            //    lvDialogs.ScrollingHost.ViewChanged -= OnViewChanged;
            //    return;
            //}

            var index0 = _panel.FirstVisibleIndex;
            var index1 = _panel.LastVisibleIndex;

            var show = false;
            var date = DateTime.Now;

            if (index0 > -1 && index1 > -1 /*&& (index0 != _lastIndex0 || index1 != _lastIndex1)*/)
            {
                var container0 = lvDialogs.ContainerFromIndex(index0);
                if (container0 != null)
                {
                    var item0 = lvDialogs.ItemFromContainer(container0);
                    if (item0 != null)
                    {
                        var message0 = item0 as TLMessageCommonBase;
                        var date0 = BindConvert.Current.DateTime(message0.Date);

                        var service0 = message0 as TLMessageService;
                        if (service0 != null)
                        {
                            show = !(service0.Action is TLMessageActionDate);
                        }
                        else
                        {
                            show = true;
                        }

                        date = date0.Date;
                    }
                }

                #region OLD

                //////Cache();
                ////Cache(index0 + 1, index1);

                ////var itemsPerGroup = 0;
                ////var compositor = ElementCompositionPreview.GetElementVisual(lvDialogs);
                ////var props = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(lvDialogs.ScrollingHost);

                ////for (int i = index1; i >= index0; i--)
                ////{
                ////    var container = lvDialogs.ContainerFromIndex(i) as ListViewItem;
                ////    if (container != null)
                ////    {
                ////        var item = container.Content as TLMessage;
                ////        if (item != null && (item.IsFirst || i == index0) && (!item.IsOut || item.IsPost))
                ////        {
                ////            var text = "0";
                ////            if (i == 0)
                ////            {
                ////                _wasFirst[i] = true;
                ////                text = "Max(0, Reference.Y + Scrolling.Translation.Y)"; // Compression effect
                ////                text = "0";
                ////            }
                ////            else if (i == index0 && itemsPerGroup > 0)
                ////            {
                ////                _wasFirst[i] = true;
                ////                text = "0";
                ////            }
                ////            else if (i == index0)
                ////            {
                ////                _wasFirst[i] = true;
                ////                text = "Min(0, Reference.Y + Scrolling.Translation.Y)";
                ////            }
                ////            else
                ////            {
                ////                text = "Reference.Y + Scrolling.Translation.Y";
                ////            }

                ////            var visual = ElementCompositionPreview.GetElementVisual(container);
                ////            var offset = visual.Offset;
                ////            if (offset.Y == 0)
                ////            {
                ////                var transform = container.TransformToVisual(lvDialogs);
                ////                var point = transform.TransformPoint(new Point());
                ////                offset = new Vector3(0, (float)point.Y, 0);
                ////            }

                ////            var expression = visual.Compositor.CreateExpressionAnimation(text);
                ////            expression.SetVector3Parameter("Reference", offset); //visual.Offset);
                ////            expression.SetReferenceParameter("Scrolling", props);

                ////            if (_inUse.ContainsKey(i) && _wasFirst.ContainsKey(i) && i != index0)
                ////            {
                ////                _wasFirst.Remove(i);

                ////                var border = _inUse[i] as Border;
                ////                var ellipse = ElementCompositionPreview.GetElementVisual(border.Child);
                ////                ellipse.StopAnimation("Offset.Y");
                ////                ellipse.StartAnimation("Offset.Y", expression);
                ////            }
                ////            else if (!_inUse.ContainsKey(i))
                ////            {
                ////                var ellipse = Push(i, item.FromId ?? 0, item);
                ////                ellipse.StopAnimation("Offset.Y");
                ////                ellipse.StartAnimation("Offset.Y", expression);
                ////            }

                ////            itemsPerGroup = 0;
                ////        }
                ////        else if (item != null && item.IsOut)
                ////        {

                ////        }
                ////        else
                ////        {
                ////            itemsPerGroup++;
                ////        }
                ////    }
                ////}


                #endregion

                //Update();
            }

            if (show)
            {
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run { Text = new DateTimeFormatter("day month").Format(date) });

                DateHeader.Visibility = Visibility.Visible;
                DateHeaderLabel.Blocks.Clear();
                DateHeaderLabel.Blocks.Add(paragraph);
            }
            else
            {
                DateHeader.Visibility = Visibility.Collapsed;
            }

            if (e.IsIntermediate == false)
            {
                await Task.Delay(2000);
                DateHeader.Visibility = Visibility.Collapsed;
            }
        }

        private Color[] colors = new Color[]
        {
            Colors.Red,
            Colors.Green,
            Colors.Blue
        };

        private List<int> _items = new List<int>();
        private Stack<UIElement> _cache = new Stack<UIElement>();
        private Dictionary<int, bool> _wasFirst = new Dictionary<int, bool>();
        private Dictionary<int, FrameworkElement> _inUse = new Dictionary<int, FrameworkElement>();

        private void Cache(int first, int last)
        {
            _items.RemoveAll(x => x < first || x > last);

            foreach (var item in _inUse.ToArray())
            {
                var message = item.Value.Tag as TLMessageBase;

                if (item.Key < first || item.Key > last || !message.IsFirst)
                {
                    _cache.Push(item.Value);
                    _inUse.Remove(item.Key);
                    ////Headers.Children.Remove(item.Value);
                }
            }
        }

        public Visual Push(int index, int group, TLMessageBase message)
        {
            if (_cache.Count > 0)
            {
                var border = _cache.Pop() as Border;
                var ellipse = border.Child as Ellipse;
                ellipse.Fill = Convert.Bubble(group);
                border.Tag = message;

                _inUse[index] = border;
                ////Headers.Children.Add(border);

                return ElementCompositionPreview.GetElementVisual(ellipse);
            }
            else
            {
                var ellipse = new Ellipse();
                ellipse.Fill = Convert.Bubble(group);
                ellipse.Tag = group;

                var border = new Border();
                border.Width = 32;
                border.Height = 32;
                border.HorizontalAlignment = HorizontalAlignment.Left;
                border.VerticalAlignment = VerticalAlignment.Top;
                border.Margin = new Thickness(12, 18, 0, 0);
                border.Child = ellipse;
                border.Tag = message;

                _inUse[index] = border;
                ////Headers.Children.Add(border);

                return ElementCompositionPreview.GetElementVisual(ellipse);
            }
        }

        //private void Headers_SizeChanged(object sender, SizeChangedEventArgs e)
        //{
        //    Headers.Clip.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
        //}
    }
}
