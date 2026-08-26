//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using LinqToVisualTree;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Common
{
    public static partial class Extensions
    {
        public static void Add(this ColumnDefinitionCollection columns, double pixels)
        {
            columns.Add(new ColumnDefinition { Width = new GridLength(pixels) });
        }

        public static void Add(this ColumnDefinitionCollection columns, double value, GridUnitType type)
        {
            columns.Add(new ColumnDefinition { Width = new GridLength(value, type) });
        }

        public static void Add(this RowDefinitionCollection rows, double pixels)
        {
            rows.Add(new RowDefinition { Height = new GridLength(pixels) });
        }

        public static void Add(this RowDefinitionCollection rows, double value, GridUnitType type)
        {
            rows.Add(new RowDefinition { Height = new GridLength(value, type) });
        }

        public static void SetToolTip(DependencyObject element, object value, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            if (ApiInfo.IsStoreRelease || value == null)
            {
                ToolTipService.SetToolTip(element, value);
            }
            else
            {
                var tooltip = new ToolTip
                {
                    Content = value
                };

                tooltip.Opened += (s, args) =>
                {
                    Logger.Info("ToolTip opened", member, filePath, line);
                };

                ToolTipService.SetToolTip(element, tooltip);
            }
        }

        // TODO: this is a duplicat of INavigationService.ShowPopupAsync, and it's needed by GamePage, GroupCallWindow and LiveStreamWindow.
        // Must be removed at some point.
        public static void ShowPopup(this UserControl frame, ISession session, ContentPopup popup, object parameter = null)
        {
            var viewModel = BootStrapper.Current.ViewModelForPage(popup, session);
            if (viewModel != null)
            {
                viewModel.XamlRoot = frame.XamlRoot;

                void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
                {
                    popup.Opened -= OnOpened;
                }

                void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
                {
                    viewModel.NavigatedFrom(null, false);
                    popup.OnNavigatedFrom();
                    popup.Closed -= OnClosed;
                }

                popup.DataContext = viewModel;

                _ = viewModel.NavigatedToAsync(parameter, NavigationMode.New, null);
                popup.OnNavigatedTo(parameter);
                popup.Closed += OnClosed;
            }

            _ = popup.ShowQueuedAsync(frame.XamlRoot);
        }

        public static void AddCubicBezier(this PathFigure figure, Point controlPoint1, Point controlPoint2, Point endPoint)
        {
            figure.Segments.Add(new BezierSegment
            {
                Point1 = controlPoint1,
                Point2 = controlPoint2,
                Point3 = endPoint
            });
        }

        public static void AddLine(this PathFigure figure, double x, double y)
        {
            figure.Segments.Add(new LineSegment
            {
                Point = new Point(x, y),
            });
        }

        public static void ForEach<TContent, TValue>(this ListViewBase listView, Action<TContent, TValue> handler) where TContent : class where TValue : class
        {
            int lastCacheIndex;
            int firstCacheIndex;

            if (listView.ItemsPanelRoot is ItemsStackPanel stack)
            {
                lastCacheIndex = stack.LastCacheIndex;
                firstCacheIndex = stack.FirstCacheIndex;
            }
            else if (listView.ItemsPanelRoot is ItemsWrapGrid wrap)
            {
                lastCacheIndex = wrap.LastCacheIndex;
                firstCacheIndex = wrap.FirstCacheIndex;
            }
            else
            {
                return;
            }

            for (int i = firstCacheIndex; i <= lastCacheIndex; i++)
            {
                var container = listView.ContainerFromIndex(i) as SelectorItem;
                var content = container?.ContentTemplateRoot as TContent;

                if (content == null)
                {
                    continue;
                }

                var item = listView.ItemFromContainer(container) as TValue;
                if (item == null)
                {
                    continue;
                }

                handler(content, item);
            }
        }

        public static void ForEach<T>(this ListViewBase listView, Action<SelectorItem, T> handler) where T : class
        {
            int lastCacheIndex;
            int firstCacheIndex;

            if (listView.ItemsPanelRoot is ItemsStackPanel stack)
            {
                lastCacheIndex = stack.LastCacheIndex;
                firstCacheIndex = stack.FirstCacheIndex;
            }
            else if (listView.ItemsPanelRoot is ItemsWrapGrid wrap)
            {
                lastCacheIndex = wrap.LastCacheIndex;
                firstCacheIndex = wrap.FirstCacheIndex;
            }
            else
            {
                return;
            }

            for (int i = firstCacheIndex; i <= lastCacheIndex; i++)
            {
                var container = listView.ContainerFromIndex(i) as SelectorItem;
                if (container == null)
                {
                    continue;
                }

                var item = listView.ItemFromContainer(container) as T;
                if (item == null)
                {
                    continue;
                }

                handler(container, item);
            }
        }

        public static void ForEach(this ListViewBase listView, Action<SelectorItem> handler)
        {
            int lastCacheIndex;
            int firstCacheIndex;

            if (listView.ItemsPanelRoot is ItemsStackPanel stack)
            {
                lastCacheIndex = stack.LastCacheIndex;
                firstCacheIndex = stack.FirstCacheIndex;
            }
            else if (listView.ItemsPanelRoot is ItemsWrapGrid wrap)
            {
                lastCacheIndex = wrap.LastCacheIndex;
                firstCacheIndex = wrap.FirstCacheIndex;
            }
            else
            {
                return;
            }

            for (int i = firstCacheIndex; i <= lastCacheIndex; i++)
            {
                var container = listView.ContainerFromIndex(i) as SelectorItem;
                if (container == null)
                {
                    continue;
                }

                handler(container);
            }
        }

        public static void RegisterColorChangedCallback(this Brush brush, DependencyPropertyChangedCallback callback, ref long token)
        {
            if (brush is SolidColorBrush solidColorBrush && token == 0)
            {
                token = solidColorBrush.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, callback);
            }
        }

        public static void UnregisterColorChangedCallback(this Brush brush, ref long token)
        {
            if (brush is SolidColorBrush solidColorBrush && token != 0)
            {
                solidColorBrush.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, token);
                token = 0;
            }
        }

        public static void RegisterPropertyChangedCallback(this DependencyObject obj, DependencyProperty property, DependencyPropertyChangedCallback callback, ref long token)
        {
            if (obj is not null && token == 0)
            {
                token = obj.RegisterPropertyChangedCallback(property, callback);
            }
        }

        public static void UnregisterPropertyChangedCallback(this DependencyObject obj, DependencyProperty property, ref long token)
        {
            if (obj is not null && token != 0)
            {
                obj.UnregisterPropertyChangedCallback(property, token);
                token = 0;
            }
        }

        public static void CreateInsetClip(this UIElement element)
        {
            var visual = ElementComposition.GetElementVisual(element);
            visual.Clip = visual.Compositor.CreateInsetClip();
        }

        public static void CreateInsetClip(this UIElement element, float leftInset, float topInset, float rightInset, float bottomInset)
        {
            var visual = ElementComposition.GetElementVisual(element);
            visual.Clip = visual.Compositor.CreateInsetClip(leftInset, topInset, rightInset, bottomInset);
        }

        public static Color ToColor(this int color, bool alpha = false)
        {
            byte a;
            if (alpha)
            {
                a = (byte)((color & 0xff000000) >> 24);
            }
            else
            {
                a = 255;
            }

            byte r = (byte)((color & 0x00ff0000) >> 16);
            byte g = (byte)((color & 0x0000ff00) >> 8);
            byte b = (byte)(color & 0x000000ff);

            return Color.FromArgb(a, r, g, b);
        }

        public static Color ToColor(this int color, double alpha)
        {
            byte a = (byte)(alpha * 255);
            byte r = (byte)((color & 0x00ff0000) >> 16);
            byte g = (byte)((color & 0x0000ff00) >> 8);
            byte b = (byte)(color & 0x000000ff);

            return Color.FromArgb(a, r, g, b);
        }

        public static int ToValue(this Color color, bool alpha = false)
        {
            if (alpha)
            {
                return (color.A << 24) + (color.R << 16) + (color.G << 8) + color.B;
            }

            return (color.R << 16) + (color.G << 8) + color.B;
        }

        public static Brush WithOpacity(this Brush brush, double opacity)
        {
            if (brush is SolidColorBrush solid)
            {
                return new SolidColorBrush(solid.Color) { Opacity = opacity };
            }

            return brush;
        }

        /// <summary>
        /// Hands a popup's content the window's message brushes. Popups live under the PopupRoot,
        /// a sibling of the window's content, so their lookup reaches Application without ever
        /// passing through it - the chat override has to be forwarded rather than inherited.
        /// </summary>
        public static void ApplyChatTheme(this FrameworkElement element, XamlRoot xamlRoot)
        {
            if (WindowContext.TryGetForXamlRoot(xamlRoot, out var window))
            {
                element.Resources.MergedDictionaries.Add(window.Incoming.CreateDictionary());
            }
        }

        public static bool HasThreadAccess(this DependencyObject element)
        {
            return element.Dispatcher.HasThreadAccess;
        }

        public static void BeginOnUIThread(this DependencyObject element, Action action)
        {
            try
            {
                if (element.Dispatcher.HasThreadAccess)
                {
                    action();
                }
                else
                {
                    _ = element.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        try
                        {
                            action();
                        }
                        catch
                        {
                            // Most likely Excep_InvalidComObject_NoRCW_Wrapper, so we can just ignore it
                        }
                    });
                }
            }
            catch
            {
                // Most likely Excep_InvalidComObject_NoRCW_Wrapper, so we can just ignore it
            }
        }

        public static T GetChild<T>(this DependencyObject parentContainer)
        {
            return parentContainer.Descendants<T>().FirstOrDefault();
        }

        public static T GetChild<T>(this DependencyObject parentContainer, Func<T, bool> predicate)
        {
            return parentContainer.Descendants<T>().FirstOrDefault(predicate);
        }

        public static T GetLastChild<T>(this DependencyObject parentContainer)
        {
            return parentContainer.Descendants<T>(true).FirstOrDefault();
        }

        public static T GetLastChild<T>(this DependencyObject parentContainer, Func<T, bool> predicate)
        {
            return parentContainer.Descendants<T>(true).FirstOrDefault(predicate);
        }

        public static T GetChildOrSelf<T>(this DependencyObject parentContainer)
        {
            if (parentContainer is T child)
            {
                return child;
            }

            return parentContainer.Descendants<T>().FirstOrDefault();
        }

        public static T GetChildOrSelf<T>(this DependencyObject parentContainer, Func<T, bool> predicate)
        {
            if (parentContainer is T child)
            {
                return child;
            }

            return parentContainer.Descendants<T>().FirstOrDefault(predicate);
        }

        public static T GetParent<T>(this DependencyObject childContainer)
        {
            return childContainer.Ancestors<T>().FirstOrDefault();
        }

        public static T GetParent<T>(this DependencyObject childContainer, Func<T, bool> predicate)
        {
            return childContainer.Ancestors<T>().FirstOrDefault(predicate);
        }

        public static T GetParentOrSelf<T>(this DependencyObject childContainer)
        {
            if (childContainer is T parent)
            {
                return parent;
            }

            return childContainer.Ancestors<T>().FirstOrDefault();
        }

        public static T GetParentOrSelf<T>(this DependencyObject childContainer, Func<T, bool> predicate)
        {
            if (childContainer is T parent)
            {
                return parent;
            }

            return childContainer.Ancestors<T>().FirstOrDefault(predicate);
        }

        public static Task UpdateLayoutAsync(this FrameworkElement element)
        {
            var tcs = new TaskCompletionSource<bool>();
            void layoutUpdated(object s1, object e1)
            {
                element.LayoutUpdated -= layoutUpdated;
                tcs.TrySetResult(true);
            }

            element.LayoutUpdated += layoutUpdated;
            return tcs.Task;
        }

        public static Task UpdateLayoutAsync(this FrameworkElement element, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            // layoutUpdated below captures registration, and converting a local function to a
            // delegate requires everything it captures to be definitely assigned - which it is
            // not inside the very expression that assigns it.
            CancellationTokenRegistration registration = default;
            registration = token.Register(() =>
            {
                element.LayoutUpdated -= layoutUpdated;
                tcs.TrySetResult(false);
            });

            void layoutUpdated(object s1, object e1)
            {
                element.LayoutUpdated -= layoutUpdated;
                tcs.TrySetResult(true);

                registration.Dispose();
            }

            element.LayoutUpdated += layoutUpdated;
            return tcs.Task;
        }

        public static Task DispatchAsync(this FrameworkElement element)
        {
            var tcs = new TaskCompletionSource<bool>();

            _ = element.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                tcs.TrySetResult(true);
            });

            return tcs.Task;
        }
    }
}
