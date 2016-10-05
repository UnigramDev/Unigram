using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (ViewModel.Peer is TLInputPeerUser)
            {
                lvDialogs.ScrollingHost.ViewChanged -= OnViewChanged;
                return;
            }

            var index0 = _panel.FirstVisibleIndex;
            var index1 = _panel.LastVisibleIndex;

            if (index0 > -1 && index1 > -1 /*&& (index0 != _lastIndex0 || index1 != _lastIndex1)*/)
            {
                //Cache();
                Cache(index0 + 1, index1);

                var itemsPerGroup = 0;
                var compositor = ElementCompositionPreview.GetElementVisual(lvDialogs);
                var props = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(lvDialogs.ScrollingHost);

                for (int i = index1; i >= index0; i--)
                {
                    var container = lvDialogs.ContainerFromIndex(i) as ListViewItem;
                    if (container != null)
                    {
                        var item = container.Content as TLMessage;
                        if (item != null && (item.IsFirst || i == index0) && (!item.IsOut || item.IsPost))
                        {
                            var text = "0";
                            if (i == 0)
                            {
                                text = $"Max(0, Reference.Y + Scrolling.Translation.Y)"; // Compression effect
                            }
                            else if (i == index0 && itemsPerGroup > 0)
                            {
                                text = "0";
                            }
                            else if (i == index0)
                            {
                                text = $"Min(0, Reference.Y + Scrolling.Translation.Y)";
                            }
                            else
                            {
                                text = "Reference.Y + Scrolling.Translation.Y";
                            }

                            var visual = ElementCompositionPreview.GetElementVisual(container);
                            var expression = visual.Compositor.CreateExpressionAnimation(text);
                            expression.SetVector3Parameter("Reference", visual.Offset);
                            expression.SetReferenceParameter("Scrolling", props);

                            if (_inUse.ContainsKey(i))
                            {
                                var border = _inUse[i] as Border;
                                var ellipse = ElementCompositionPreview.GetElementVisual(border.Child);
                                ellipse.StopAnimation("Offset.Y");
                                ellipse.StartAnimation("Offset.Y", expression);
                            }
                            else if (!_inUse.ContainsKey(i))
                            {
                                var ellipse = Push(i, item.FromId ?? 0, item);
                                ellipse.StopAnimation("Offset.Y");
                                ellipse.StartAnimation("Offset.Y", expression);
                            }

                            itemsPerGroup = 0;
                        }
                        else
                        {
                            itemsPerGroup++;
                        }
                    }
                }

                //Update();
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
        private Dictionary<int, FrameworkElement> _inUse = new Dictionary<int, FrameworkElement>();

        private void Cache()
        {
            foreach (var ellipse in Headers.Children)
            {
                _cache.Push(ellipse as Border);
            }
        }

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
                    Headers.Children.Remove(item.Value);
                }
            }
        }

        private void Update()
        {
            foreach (var item in _cache)
            {
                Headers.Children.Remove(item);
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
                Headers.Children.Add(border);

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
                Headers.Children.Add(border);

                return ElementCompositionPreview.GetElementVisual(ellipse);
            }
        }

        private void Headers_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Headers.Clip.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
        }
    }
}
