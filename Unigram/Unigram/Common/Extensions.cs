using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Controls;
using Unigram.Controls.Messages;
using Unigram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace Unigram.Common
{
    public static class Extensions
    {
        public static bool IsAdmin(this TLMessageBase message)
        {
            // Kludge
            return message.Parent is TLChannel channel && DialogViewModel.Admins.TryGetValue(channel.Id, out IList<TLChannelParticipantBase> admins) && admins.Any(x => x.UserId == message.FromId);
        }

        public static void BeginOnUIThread(this DependencyObject element, Action action)
        {
            element.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(action));
        }

        public static bool IsCompactOverlaySupported(this ApplicationView view)
        {
            return ApiInformation.IsMethodPresent("Windows.UI.ViewManagement.ApplicationView", "IsViewModeSupported") && view.IsViewModeSupported(ApplicationViewMode.CompactOverlay);
        }

        public static Regex _pattern = new Regex("[\\-0-9]+", RegexOptions.Compiled);
        public static int ToInt32(this String value)
        {
            if (value == null)
            {
                return 0;
            }

            var val = 0;
            try
            {
                var matcher = _pattern.Match(value);
                if (matcher.Success)
                {
                    var num = matcher.Groups[0].Value;
                    val = int.Parse(num);
                }
            }
            catch (Exception e)
            {
                //FileLog.e(e);
            }

            return val;
        }

        public static int TryParseOrDefault(string value, int defaultValue)
        {
            int.TryParse(value, out defaultValue);
            return defaultValue;
        }

        public static Dictionary<string, string> ParseQueryString(this string query)
        {
            var first = query.Split('?');
            if (first.Length > 1)
            {
                query = first.Last();
            }

            var queryDict = new Dictionary<string, string>();
            foreach (var token in query.TrimStart(new char[] { '?' }).Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = token.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                    queryDict[parts[0].Trim()] = WebUtility.UrlDecode(parts[1]).Trim();
                else
                    queryDict[parts[0].Trim()] = "";
            }
            return queryDict;
        }

        public static string GetParameter(this Dictionary<string, string> query, string key)
        {
            query.TryGetValue(key, out string value);
            return value;
        }

        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source.IndexOf(toCheck, comp) >= 0;
        }

        public static bool StartsWith(this string source, string[] toCheck, StringComparison comp)
        {
            foreach (var item in toCheck)
            {
                if (source.StartsWith(item, comp))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsLike(this TLUser user, string[] query, StringComparison comp)
        {
            return IsLike(user.FullName, user.Username, query, comp);
        }

        public static bool IsLike(this TLChannel channel, string[] query, StringComparison comp)
        {
            return IsLike(channel.Title, channel.Username, query, comp);
        }

        public static bool IsLike(this TLChat chat, string[] query, StringComparison comp)
        {
            return IsLike(chat.Title, null, query, comp);
        }

        public static bool IsLike(string name, string username, string[] query, StringComparison comp)
        {
            var translit = LocaleHelper.Transliterate(name);
            if (translit.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                translit = null;
            }

            foreach (var q in query)
            {
                if (name.StartsWith(q, comp) || name.Contains(" " + q, comp) || translit != null && (translit.StartsWith(q, comp) || translit.Contains(" " + q, comp)))
                {
                    return true;
                }
                else if (username != null && username.StartsWith(q, comp))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsLike(this string source, string[] queries, StringComparison comp)
        {
            foreach (var query in queries)
            {
                if (query.Split(' ').All(x =>
                {
                    var index = source.IndexOf(x, comp);
                    if (index > -1)
                    {
                        return index == 0 || char.IsSeparator(source[index - 1]) || !char.IsLetterOrDigit(source[index - 1]);
                    }

                    return false;
                }))
                {
                    return true;
                }
            }

            return false;
        }

        public static string Format(this string input)
        {
            if (input != null)
            {
                return input.Trim().Replace("\r\n", "\n").Replace('\v', '\n').Replace('\r', '\n');
            }

            return input;
        }

        public static string TrimStart(this string target, string trimString)
        {
            string result = target;
            while (result.StartsWith(trimString))
            {
                result = result.Substring(trimString.Length);
            }

            return result;
        }

        public static string TrimEnd(this string target, string trimString)
        {
            string result = target;
            while (result.EndsWith(trimString))
            {
                result = result.Substring(0, result.Length - trimString.Length);
            }

            return result;
        }

        //public static string TrimEnd(this string input, string suffixToRemove)
        //{
        //    if (input != null && suffixToRemove != null && input.EndsWith(suffixToRemove))
        //    {
        //        return input.Substring(0, input.Length - suffixToRemove.Length);
        //    }
        //    else return input;
        //}

        public static void AddRange<T>(this IList<T> list, IEnumerable<T> source)
        {
            foreach (var item in source)
            {
                list.Add(item);
            }
        }

        public static void AddRange<T>(this IList<T> list, params T[] source)
        {
            foreach (var item in source)
            {
                list.Add(item);
            }
        }

        public static List<T> Buffered<T>(int count)
        {
            var result = new List<T>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(default(T));
            }

            return result;
        }

        public static Hyperlink GetHyperlinkFromPoint(this RichTextBlock text, Point point)
        {
            var position = text.GetPositionFromPoint(point);
            var hyperlink = GetHyperlink(position.Parent as TextElement);

            return hyperlink;
        }

        private static Hyperlink GetHyperlink(TextElement parent)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent is Hyperlink)
            {
                return parent as Hyperlink;
            }

            return GetHyperlink(parent.ElementStart.Parent as TextElement);
        }

        public static void FocusMaybe(this RichEditBox textBox, FocusState focusState)
        {
            if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
            {
                textBox.Focus(focusState);
            }
        }

        public static bool IsEmpty<T>(this IList<T> list)
        {
            return list.Count == 0;
        }

        public static List<Control> AllChildren(this DependencyObject parent)
        {
            var list = new List<Control>();

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var _Child = VisualTreeHelper.GetChild(parent, i);
                if (_Child is Control)
                {
                    list.Add(_Child as Control);
                }
                list.AddRange(AllChildren(_Child));
            }

            return list;
        }

        public static T GetChild<T>(this DependencyObject parentContainer, string controlName)
        {
            var childControls = AllChildren(parentContainer);
            var control = childControls.OfType<Control>().Where(x => x.Name.Equals(controlName)).Cast<T>().First();

            return control;
        }

        public static T GetParent<T>(this FrameworkElement element, string message = null) where T : DependencyObject
        {
            var parent = element.Parent as T;

            if (parent == null)
            {
                if (message == null)
                {
                    message = "Parent element should not be null! Check the default Generic.xaml.";
                }

                throw new NullReferenceException(message);
            }

            return parent;
        }

        public static T GetChild<T>(this Border element, string message = null) where T : DependencyObject
        {
            var child = element.Child as T;

            if (child == null)
            {
                if (message == null)
                {
                    message = $"{nameof(Border)}'s child should not be null! Check the default Generic.xaml.";
                }

                throw new NullReferenceException(message);
            }

            return child;
        }

        public static Storyboard GetStoryboard(this FrameworkElement element, string name, string message = null)
        {
            var storyboard = element.Resources[name] as Storyboard;

            if (storyboard == null)
            {
                if (message == null)
                {
                    message = $"Storyboard '{name}' cannot be found! Check the default Generic.xaml.";
                }

                throw new NullReferenceException(message);
            }

            return storyboard;
        }

        public static CompositeTransform GetCompositeTransform(this FrameworkElement element, string message = null)
        {
            var transform = element.RenderTransform as CompositeTransform;

            if (transform == null)
            {
                if (message == null)
                {
                    message = $"{element.Name}'s RenderTransform should be a CompositeTransform! Check the default Generic.xaml.";
                }

                throw new NullReferenceException(message);
            }

            return transform;
        }


        public static async Task UpdateLayoutAsync(this FrameworkElement element)
        {
            var tcs = new TaskCompletionSource<object>();

            EventHandler<object> layoutUpdated = (s1, e1) => tcs.TrySetResult(null);
            try
            {
                element.LayoutUpdated += layoutUpdated;
                element.UpdateLayout();
                await tcs.Task;
            }
            finally
            {
                element.LayoutUpdated -= layoutUpdated;
            }
        }
    }

    public static class ClipboardEx
    {
        public static void TrySetContent(DataPackage content)
        {
            try
            {
                Clipboard.SetContent(content);
                Clipboard.Flush();
            }
            catch { }
        }
    }

    // Modified from: https://stackoverflow.com/a/32559623/1680863
    public static class ListViewExtensions
    {
        public async static Task ScrollToItem(this ListViewBase listViewBase, object item, SnapPointsAlignment alignment, bool highlight, double? pixel = null)
        {
            // get the ScrollViewer withtin the ListView/GridView
            var scrollViewer = listViewBase.GetScrollViewer();
            // get the SelectorItem to scroll to
            var selectorItem = listViewBase.ContainerFromItem(item) as SelectorItem;

            // when it's null, means virtualization is on and the item hasn't been realized yet
            if (selectorItem == null)
            {
                // call task-based ScrollIntoViewAsync to realize the item
                await listViewBase.ScrollIntoViewAsync(item);

                // this time the item shouldn't be null again
                selectorItem = (SelectorItem)listViewBase.ContainerFromItem(item);
            }

            if (selectorItem == null)
            {
                return;
            }

            // calculate the position object in order to know how much to scroll to
            var transform = selectorItem.TransformToVisual((UIElement)scrollViewer.Content);
            var position = transform.TransformPoint(new Point(0, 0));

            if (alignment == SnapPointsAlignment.Near)
            {
                if (pixel is double adjust)
                {
                    position.Y -= adjust;
                }
            }
            else if (alignment == SnapPointsAlignment.Center)
            {
                position.Y -= (listViewBase.ActualHeight - selectorItem.ActualHeight) / 2d;
            }
            else if (alignment == SnapPointsAlignment.Far)
            {
                position.Y -= listViewBase.ActualHeight - selectorItem.ActualHeight;

                if (pixel is double adjust)
                {
                    position.Y += adjust;
                }
            }

            // scroll to desired position with animation!
            scrollViewer.ChangeView(position.X, position.Y, null, alignment != SnapPointsAlignment.Center);

            if (highlight)
            {
                var bubble = selectorItem.Descendants<MessageBubble>().FirstOrDefault() as MessageBubble;
                if (bubble == null)
                {
                    return;
                }

                bubble.Highlight();
            }
        }

        public static async Task ScrollIntoViewAsync(this ListViewBase listViewBase, object item)
        {
            var tcs = new TaskCompletionSource<object>();
            var scrollViewer = listViewBase.GetScrollViewer();

            EventHandler<object> layoutUpdated = (s1, e1) => tcs.TrySetResult(null);
            EventHandler<ScrollViewerViewChangedEventArgs> viewChanged = (s, e) =>
            {
                scrollViewer.LayoutUpdated += layoutUpdated;
                scrollViewer.UpdateLayout();
            };
            try
            {
                scrollViewer.ViewChanged += viewChanged;
                listViewBase.ScrollIntoView(item, ScrollIntoViewAlignment.Leading);
                await tcs.Task;
            }
            finally
            {
                scrollViewer.ViewChanged -= viewChanged;
                scrollViewer.LayoutUpdated -= layoutUpdated;
            }
        }

        public static async Task ChangeViewAsync(this ScrollViewer scrollViewer, double? horizontalOffset, double? verticalOffset, bool disableAnimation)
        {
            var tcs = new TaskCompletionSource<object>();

            EventHandler<object> layoutUpdated = (s1, e1) => tcs.TrySetResult(null);
            EventHandler<ScrollViewerViewChangedEventArgs> viewChanged = (s, e) =>
            {
                scrollViewer.LayoutUpdated += layoutUpdated;
                scrollViewer.UpdateLayout();
            };
            try
            {
                scrollViewer.ViewChanged += viewChanged;
                scrollViewer.ChangeView(horizontalOffset, verticalOffset, null, disableAnimation);
                await tcs.Task;
            }
            finally
            {
                scrollViewer.ViewChanged -= viewChanged;
                scrollViewer.LayoutUpdated -= layoutUpdated;
            }
        }

        public static ScrollViewer GetScrollViewer(this ListViewBase listViewBase)
        {
            if (listViewBase is BubbleListView bubble)
            {
                return bubble.ScrollingHost;
            }

            return listViewBase.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
        }
    }
}
