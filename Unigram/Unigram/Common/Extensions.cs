using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Controls.Messages;
using Unigram.Entities;
using Unigram.Native;
using Unigram.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using static Unigram.Services.GenerationService;
using Point = Windows.Foundation.Point;

namespace Unigram.Common
{
    public static class Extensions
    {
        public static async Task<StorageMedia> PickSingleMediaAsync(this FileOpenPicker picker)
        {
            var file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return null;
            }

            return await StorageMedia.CreateAsync(file);
        }

        public static Version ToVersion(this PackageVersion version)
        {
            return new Version(version.Major, version.Minor, version.Build, version.Revision);
        }

        public static string ToQuery(this Dictionary<string, string> dictionary)
        {
            var result = string.Empty;

            foreach (var item in dictionary)
            {
                result += $"{item.Key}={item.Value}&";
            }

            return result.TrimEnd('&');
        }

        public static Vector2 GetActualSize(this FrameworkElement element)
        {
            return new Vector2((float)element.ActualWidth, (float)element.ActualHeight);
        }

        public static void ShowTeachingTip(this Window app, FrameworkElement target, string text, Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode placement = Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.TopRight)
        {
            ShowTeachingTip(app, target, new FormattedText(text, new TextEntity[0]), placement);
        }

        public static void ShowTeachingTip(this Window app, FrameworkElement target, FormattedText text, Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode placement = Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.TopRight)
        {
            var label = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap
            };
            var tip = new Microsoft.UI.Xaml.Controls.TeachingTip
            {
                Target = target,
                PreferredPlacement = placement,
                IsLightDismissEnabled = true,
                Content = label,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch
            };

            TextBlockHelper.SetFormattedText(label, text);

            if (app.Content is FrameworkElement element)
            {
                element.Resources["TeachingTip"] = tip;
            }
            else
            {
                target.Resources["TeachingTip"] = tip;
            }
            tip.IsOpen = true;
        }

        public static Color ToColor(this int color)
        {
            return Color.FromArgb(0xFF, (byte)((color >> 16) & 0xFF), (byte)((color >> 8) & 0xFF), (byte)(color & 0xFF));
        }

        public static int ToValue(this Color color)
        {
            return (color.R << 16) + (color.G << 8) + color.B;
        }

        public static int ToTimestamp(this DateTime dateTime)
        {
            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

            return (int)(dateTime.ToUniversalTime() - dtDateTime).TotalSeconds;
        }

        public static bool TryGet<T>(this ResourceDictionary dict, string key, out T value)
        {
            bool success;
            if (success = dict.TryGetValue(key, out object tryGetValue))
            {
                value = (T)tryGetValue;
            }
            else
            {
                value = default;
            }
            return success;
        }

        public static bool TryGet<T>(this IDictionary<string, object> dict, string key, out T value)
        {
            bool success;
            if (success = dict.TryGetValue(key, out object tryGetValue))
            {
                value = (T)tryGetValue;
            }
            else
            {
                value = default;
            }
            return success;
        }

        public static void Put<T>(this IList<T> source, bool begin, T item)
        {
            if (begin)
            {
                source.Insert(0, item);
            }
            else
            {
                source.Add(item);
            }
        }

        public static string Enqueue(this StorageItemAccessList list, IStorageItem item)
        {
            try
            {
                if (list.Entries.Count >= list.MaximumItemsAllowed - 10)
                {
                    var first = list.Entries.LastOrDefault();
                    if (first.Token != null)
                    {
                        list.Remove(first.Token);
                    }
                }
            }
            catch { }

            try
            {
                return list.Add(item);
            }
            catch
            {
                return null;
            }
        }

        public static string RegexReplace(this string input, string pattern, string replacement)
        {
            return Regex.Replace(input, pattern, replacement);
        }

        public static uint GetHeight(this ImageProperties props)
        {
            return props.Height;
            return props.Orientation == PhotoOrientation.Rotate180 ? props.Height : props.Width;
        }

        public static uint GetWidth(this ImageProperties props)
        {
            return props.Width;
            return props.Orientation == PhotoOrientation.Rotate180 ? props.Width : props.Height;
        }



        public static uint GetHeight(this VideoProperties props)
        {
            return props.Orientation is VideoOrientation.Rotate180 or VideoOrientation.Normal ? props.Height : props.Width;
        }

        public static uint GetWidth(this VideoProperties props)
        {
            return props.Orientation is VideoOrientation.Rotate180 or VideoOrientation.Normal ? props.Width : props.Height;
        }

        public static T[] Shift<T>(this T[] array, int offset)
        {
            var output = new T[array.Length];

            if (offset > 0)
            {
                while (offset > 0)
                {
                    var element = array[0];
                    Array.Copy(array, 1, output, 0, array.Length - 1);
                    output[output.Length - 1] = element;
                    offset -= 1;

                    array = output;
                }
            }
            else
            {
                Array.Copy(array, 0, output, 0, array.Length);
            }

            return output;
        }

        /// <summary>
        /// Applies the action to each element in the list.
        /// </summary>
        /// <typeparam name="T">The enumerable item's type.</typeparam>
        /// <param name="enumerable">The elements to enumerate.</param>
        /// <param name="action">The action to apply to each item in the list.</param>
        public static void Apply<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (var item in enumerable)
            {
                action(item);
            }
        }

        public static string Substr(this string source, int startIndex, int endIndex)
        {
            return source.Substring(startIndex, endIndex - startIndex);
        }

        /// <summary>
        /// Creates a relative path from one file or folder to another.
        /// </summary>
        /// <param name="fromPath">Contains the directory that defines the start of the relative path.</param>
        /// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
        /// <returns>The relative path from the start directory to the end path or <c>toPath</c> if the paths are not related.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="UriFormatException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static string MakeRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath))
            {
                throw new ArgumentNullException("fromPath");
            }

            if (string.IsNullOrEmpty(toPath))
            {
                throw new ArgumentNullException("toPath");
            }

            var fromUri = new Uri(fromPath);
            var toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme) { return toPath; } // path can't be made relative.

            var relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        public static async Task<InputFile> ToGeneratedAsync(this StorageFile file, ConversionType conversion = ConversionType.Copy, string arguments = null, bool forceCopy = false)
        {
            var token = StorageApplicationPermissions.FutureAccessList.Enqueue(file);
            var path = file.Path;

            if (conversion == ConversionType.Copy && arguments == null && NativeUtils.IsFileReadable(file.Path) && !forceCopy)
            {
                return new InputFileLocal(path);
            }

            if (conversion == ConversionType.Compress)
            {
                path = Path.GetFileNameWithoutExtension(path) + ".jpg";
            }

            var props = await file.GetBasicPropertiesAsync();
            return new InputFileGenerated(path, token + "#" + conversion + (arguments != null ? "#" + arguments : string.Empty) + "#" + props.DateModified.ToString("s"), (int)props.Size);
        }

        public static async Task<InputThumbnail> ToThumbnailAsync(this StorageFile file, VideoConversion video = null, ConversionType conversion = ConversionType.Copy, string arguments = null)
        {
            var props = await file.Properties.GetVideoPropertiesAsync();

            double originalWidth = props.GetWidth();
            double originalHeight = props.GetHeight();

            if (!video.CropRectangle.IsEmpty)
            {
                originalWidth = video.CropRectangle.Width;
                originalHeight = video.CropRectangle.Height;
            }

            double ratioX = 90 / originalWidth;
            double ratioY = 90 / originalHeight;
            double ratio = Math.Min(ratioX, ratioY);

            int width = (int)(originalWidth * ratio);
            int height = (int)(originalHeight * ratio);

            return new InputThumbnail(await file.ToGeneratedAsync(conversion, arguments), width, height);
        }

        public static T RemoveLast<T>(this List<T> list)
        {
            if (list.Count > 0)
            {
                var last = list[list.Count - 1];
                list.Remove(last);

                return last;
            }

            return default;
        }

        public static bool IsEmpty<T>(this ICollection<T> items)
        {
            return items.Count == 0;
        }

        public static void PutRange<TKey, TItem>(this IDictionary<TKey, TItem> list, IDictionary<TKey, TItem> source)
        {
            foreach (var item in source)
            {
                list[item.Key] = item.Value;
            }
        }


        public static bool Equals(this string input, params string[] check)
        {
            foreach (var str in check)
            {
                if (input.Equals(str))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsEmpty(this Rect rect)
        {
            return rect == default || (rect.Width == 0 && rect.Height == 0);
        }

        public static bool GetBoolean(this ApplicationDataContainer container, string key, bool defaultValue)
        {
            if (container.Values.TryGetValue(key, out object value) && value is bool result)
            {
                return result;
            }

            return defaultValue;
        }

        public static int GetInt32(this ApplicationDataContainer container, string key, int defaultValue)
        {
            if (container.Values.TryGetValue(key, out object value) && value is int result)
            {
                return result;
            }

            return defaultValue;
        }

        public static bool GetBoolean(this ApplicationDataCompositeValue container, string key, bool defaultValue)
        {
            if (container.TryGetValue(key, out object value) && value is bool result)
            {
                return result;
            }

            return defaultValue;
        }

        public static int GetInt32(this ApplicationDataCompositeValue container, string key, int defaultValue)
        {
            if (container.TryGetValue(key, out object value) && value is int result)
            {
                return result;
            }

            return defaultValue;
        }



        public static async void BeginOnUIThread(this DependencyObject element, Action action)
        {
            try
            {
                await element.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(action));
            }
            catch
            {
                // Most likey Excep_InvalidComObject_NoRCW_Wrapper, so we can just ignore it
            }
        }

        public static bool TypeEquals(this object o1, object o2)
        {
            if (o1 == null || o2 == null)
            {
                return false;
            }

            return Equals(o1.GetType(), o2.GetType());
        }

        public static Regex _pattern = new Regex("[\\-0-9]+", RegexOptions.Compiled);
        public static int ToInt32(this string value)
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
            catch (Exception)
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

        public static Dictionary<string, string> ParseQueryString(this string query, char separator = '&')
        {
            var first = query.Split('?');
            if (first.Length > 1)
            {
                query = first.Last();
            }

            var queryDict = new Dictionary<string, string>();
            foreach (var token in query.TrimStart(new char[] { '?' }).Split(new char[] { separator }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = token.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    queryDict[parts[0].Trim()] = WebUtility.UrlDecode(parts[1]).Trim();
                }
                else
                {
                    queryDict[parts[0].Trim()] = "";
                }
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

        public static string Format(this string input)
        {
            if (input != null)
            {
                return input.Trim().Replace("\r\n", "\n").Replace('\v', '\n').Replace('\r', '\n');
            }

            return string.Empty;
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
                result.Add(default);
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

        public static bool IsEmpty<T>(this IList<T> list)
        {
            return list.Count == 0;
        }

        public static void ForEach<T>(this IEnumerable<T> list, Action<T> action)
        {
            foreach (var item in list)
            {
                action?.Invoke(item);
            }
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

        public static IEnumerable<T> AllChildren<T>(this DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var _Child = VisualTreeHelper.GetChild(parent, i);
                if (_Child is T)
                {
                    yield return (T)(object)_Child;
                }
            }
        }

        public static T GetChild<T>(this DependencyObject parentContainer, string controlName)
        {
            var childControls = AllChildren(parentContainer);
            var control = childControls.OfType<Control>().Where(x => x.Name.Equals(controlName)).Cast<T>().First();

            return control;
        }

        public static async Task UpdateLayoutAsync(this FrameworkElement element, bool update = false)
        {
            var tcs = new TaskCompletionSource<object>();

            void layoutUpdated(object s1, object e1)
            {
                tcs.TrySetResult(null);
            }

            try
            {
                element.LayoutUpdated += layoutUpdated;

                if (update)
                {
                    element.UpdateLayout();
                }

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
        public static async Task ScrollToItem2(this ListViewBase listViewBase, object item, VerticalAlignment alignment, bool highlight, double? pixel = null)
        {
            var scrollViewer = listViewBase.GetScrollViewer();
            if (scrollViewer == null)
            {
                return;
            }

            //listViewBase.SelectionMode = ListViewSelectionMode.Single;
            //listViewBase.SelectedItem = item;

            var selectorItem = listViewBase.ContainerFromItem(item) as SelectorItem;
            if (selectorItem == null)
            {
                // call task-based ScrollIntoViewAsync to realize the item
                await listViewBase.ScrollIntoViewAsync(item, ScrollIntoViewAlignment.Leading, true);

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

            if (alignment == VerticalAlignment.Top)
            {
                if (pixel is double adjust)
                {
                    position.Y -= adjust;
                }
            }
            else if (alignment == VerticalAlignment.Center)
            {
                position.Y -= (listViewBase.ActualHeight - selectorItem.ActualHeight) / 2d;
            }
            else if (alignment == VerticalAlignment.Bottom)
            {
                position.Y -= listViewBase.ActualHeight - selectorItem.ActualHeight;

                if (pixel is double adjust)
                {
                    position.Y += adjust;
                }
            }

            // scroll to desired position with animation!
            scrollViewer.ChangeView(position.X, position.Y, null, alignment != VerticalAlignment.Center);

            if (highlight)
            {
                var bubble = selectorItem.Descendants<MessageBubble>().FirstOrDefault();
                if (bubble == null)
                {
                    return;
                }

                bubble.Highlight();
            }
        }

        public static async Task ScrollIntoViewAsync(this ListViewBase listViewBase, object item, ScrollIntoViewAlignment alignment, bool updateLayout)
        {
            var tcs = new TaskCompletionSource<object>();
            var scrollViewer = listViewBase.GetScrollViewer();

            void layoutUpdated(object s1, object e1)
            {
                tcs.TrySetResult(null);
            }

            void viewChanged(object s, ScrollViewerViewChangedEventArgs e)
            {
                scrollViewer.LayoutUpdated += layoutUpdated;

                if (updateLayout)
                {
                    scrollViewer.UpdateLayout();
                }
            }
            try
            {
                scrollViewer.ViewChanged += viewChanged;
                listViewBase.ScrollIntoView(item, alignment);
                await tcs.Task;
            }
            finally
            {
                scrollViewer.ViewChanged -= viewChanged;
                scrollViewer.LayoutUpdated -= layoutUpdated;
            }
        }

        public static async Task ChangeViewAsync(this ScrollViewer scrollViewer, double? horizontalOffset, double? verticalOffset, bool disableAnimation, bool updateLayout)
        {
            var tcs = new TaskCompletionSource<object>();

            void layoutUpdated(object s1, object e1)
            {
                tcs.TrySetResult(null);
            }

            void viewChanged(object s, ScrollViewerViewChangedEventArgs e)
            {
                if (e.IsIntermediate)
                {
                    return;
                }

                scrollViewer.LayoutUpdated += layoutUpdated;

                if (updateLayout)
                {
                    scrollViewer.UpdateLayout();
                }
            }
            try
            {
                scrollViewer.ViewChanged += viewChanged;
                if (scrollViewer.ChangeView(horizontalOffset, verticalOffset, null, disableAnimation))
                {
                    await tcs.Task;
                }
            }
            finally
            {
                scrollViewer.ViewChanged -= viewChanged;
                scrollViewer.LayoutUpdated -= layoutUpdated;
            }
        }

        public static ScrollViewer GetScrollViewer(this ListViewBase listViewBase)
        {
            //if (listViewBase is ChatsListView bubble)
            //{
            //    return bubble.ScrollingHost;
            //}

            return listViewBase.Descendants<ScrollViewer>().FirstOrDefault();
        }

        public static ScrollViewer GetScrollViewer(this Pivot listViewBase)
        {
            return listViewBase.Descendants<ScrollViewer>().FirstOrDefault();
        }

        public static async Task ConsolidateAsync(this ApplicationView view)
        {
            if (await view.TryConsolidateAsync())
            {
                return;
            }

            Window.Current.Close();
        }
    }

    public static class UriEx
    {
        public static BitmapImage ToBitmap(string path, int width, int height)
        {
            return new BitmapImage(ToLocal(path)) { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };
        }

        public static Uri ToLocal(string path)
        {
            //return new Uri("file:///" + Uri.EscapeUriString(path.Replace('\\', '/')));

            var directory = Path.GetDirectoryName(path);
            var file = Path.GetFileName(path);

            return new Uri("file:///" + directory + "\\" + Uri.EscapeUriString(file));
        }
    }
}
