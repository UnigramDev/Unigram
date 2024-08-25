//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LinqToVisualTree;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Calls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using static Telegram.Services.GenerationService;
using Point = Windows.Foundation.Point;
using RichEditTextDocument = Microsoft.UI.Text.RichEditTextDocument;

namespace Telegram.Common
{
    public static class Extensions
    {
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

        // TODO: this is a duplicat of INavigationService.ShowPopupAsync, and it's needed by GamePage, GroupCallPage and LiveStreamPage.
        // Must be removed at some point.
        public static Task<ContentDialogResult> ShowPopupAsync(this Page frame, int sessionId, Type sourcePopupType, object parameter = null, TaskCompletionSource<object> tsc = null)
        {
            var popup = (tsc != null ? Activator.CreateInstance(sourcePopupType, tsc) : Activator.CreateInstance(sourcePopupType)) as ContentPopup;
            if (popup != null)
            {
                var viewModel = BootStrapper.Current.ViewModelForPage(popup, sessionId);
                if (viewModel != null)
                {
                    //viewModel.NavigationService = this;

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
                    popup.OnNavigatedTo();
                    popup.Closed += OnClosed;
                }

                return popup.ShowQueuedAsync(frame.XamlRoot);
            }

            return Task.FromResult(ContentDialogResult.None);
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

        public static int OffsetToIndex(this TextPointer pointer, StyledText text)
        {
            if (pointer.VisualParent is not RichTextBlock textBlock || text == null || textBlock.Blocks.Count != text.Paragraphs.Count)
            {
                return -1;
            }

            var index = 0;

            for (int i = 0; i < textBlock.Blocks.Count; i++)
            {
                var block = textBlock.Blocks[i] as Paragraph;
                var paragraph = text.Paragraphs[i];

                if (pointer.Offset == block.ElementStart.Offset)
                {
                    break;
                }

                // Element start
                index++;

                if (OffsetToIndex(textBlock, block.Inlines, pointer, ref index))
                {
                    break;
                }

                if (pointer.Offset < block.ElementEnd.Offset)
                {
                    if (pointer.Offset == block.ContentEnd.Offset)
                    {
                        if (i == textBlock.Blocks.Count - 1)
                        {
                            // Always close when ending on the last paragraph
                            index++;
                        }
                        else
                        {
                            index += paragraph.Padding;
                        }
                    }

                    break;
                }

                // Element end
                index += paragraph.Padding;
            }

            // Adjust the offset if the selection ends on the text block itself
            if (pointer.Offset == textBlock.ContentEnd.Offset && pointer.Parent is RichTextBlock)
            {
                index += 2;
            }

            return pointer.Offset - index;
        }

        private static bool OffsetToIndex(RichTextBlock textBlock, InlineCollection inlines, TextPointer pointer, ref int index)
        {
            foreach (var element in inlines)
            {
                if (pointer.Offset == element.ElementStart.Offset)
                {
                    return true;
                }

                // Element start
                index++;

                if (element is Span span && OffsetToIndex(textBlock, span.Inlines, pointer, ref index))
                {
                    return true;
                }
                if (element is Run { Text: Icons.ZWNJ or Icons.RTL or Icons.LTR })
                {
                    index++;
                }
                else if (element is InlineUIContainer container && container.Child is CustomEmojiIcon icon)
                {
                    index -= icon.Emoji.Length;
                }

                if (pointer.Offset < element.ElementEnd.Offset)
                {
                    return true;
                }

                // Element end
                index++;
            }

            return false;
        }

        public static int OffsetToIndex(this TextPointer pointer)
        {
            if (pointer.VisualParent is not RichTextBlock textBlock)
            {
                return -1;
            }

            var index = 0;

            for (int i = 0; i < textBlock.Blocks.Count; i++)
            {
                var block = textBlock.Blocks[i] as Paragraph;

                if (pointer.Offset == block.ElementStart.Offset)
                {
                    break;
                }

                // Element start
                index++;

                if (OffsetToIndex(textBlock, block.Inlines, pointer, ref index))
                {
                    break;
                }

                if (pointer.Offset < block.ElementEnd.Offset)
                {
                    if (pointer.Offset == block.ContentEnd.Offset)
                    {
                        if (i == textBlock.Blocks.Count - 1)
                        {
                            // Always close when ending on the last paragraph
                            index++;
                        }
                        else
                        {
                            index += 1;//paragraph.Padding;
                        }
                    }

                    break;
                }

                // Element end
                index += 1;//paragraph.Padding;
            }

            // Adjust the offset if the selection ends on the text block itself
            if (pointer.Offset == textBlock.ContentEnd.Offset && pointer.Parent is RichTextBlock)
            {
                index += 2;
            }

            return pointer.Offset - index;
        }

        public static bool HasExtension(this IStorageFile file, params string[] extensions)
        {
            foreach (var ext in extensions)
            {
                if (file.FileType.Equals(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasExtension(this string path, params string[] extensions)
        {
            foreach (var ext in extensions)
            {
                if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static T Random<T>(this IList<T> source)
        {
            if (source.Count > 0)
            {
                return source[new Random().Next(source.Count)];
            }

            return default;
        }

        public static StringBuilder Prepend(this StringBuilder builder, string text, string prefix)
        {
            if (builder.Length > 0)
            {
                builder.Append(prefix);
            }

            return builder.Append(text);
        }

        public static IAsyncOperation<AppServiceResponse> SendMessageAsync(this AppServiceConnection connection, string message, object parameter = null)
        {
            return connection.SendMessageAsync(new ValueSet { { message, parameter ?? true } });
        }

        public static string GetDuration(this TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return duration.ToString("h\\:mm\\:ss");
            }
            else
            {
                return duration.ToString("mm\\:ss");
            }
        }

        public static void TryProcessDownEvent(this GestureRecognizer recognizer, PointerPoint value)
        {
            try
            {
                recognizer.ProcessDownEvent(value);
            }
            catch
            {
                recognizer.CompleteGesture();
            }
        }

        public static void TryProcessMoveEvents(this GestureRecognizer recognizer, IList<PointerPoint> value)
        {
            try
            {
                recognizer.ProcessMoveEvents(value);
            }
            catch
            {
                recognizer.CompleteGesture();
            }
        }

        public static void TryProcessUpEvent(this GestureRecognizer recognizer, PointerPoint value)
        {
            try
            {
                recognizer.ProcessUpEvent(value);
            }
            catch
            {
                recognizer.CompleteGesture();
            }
        }

        public static async Task<VoipPhoneCallResourceReservationStatus> TryReserveCallResourcesAsync(this VoipCallCoordinator coordinator)
        {
            var status = VoipPhoneCallResourceReservationStatus.ResourcesNotAvailable;
            try
            {
                status = await coordinator.ReserveCallResourcesAsync();
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147024713)
                {
                    // CPU and memory resources have already been reserved for the app.
                    // Ignore the return value from your call to ReserveCallResourcesAsync,
                    // and proceed to handle a new VoIP call.
                    status = VoipPhoneCallResourceReservationStatus.Success;
                }
            }

            return status;
        }

        public static void TryNotifyMutedChanged(this VoipCallCoordinator coordinator, bool muted)
        {
            try
            {
                if (muted)
                {
                    coordinator?.NotifyMuted();
                }
                else
                {
                    coordinator?.NotifyUnmuted();
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public static void TryNotifyCallActive(this VoipPhoneCall call)
        {
            try
            {
                call.NotifyCallActive();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public static void TryNotifyCallEnded(this VoipPhoneCall call)
        {
            try
            {
                call.NotifyCallEnded();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
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

        public static void Clear(this RichEditTextDocument document)
        {
            using (var stream = new InMemoryRandomAccessStream())
            {
                document.LoadFromStream(Microsoft.UI.Text.TextSetOptions.None, stream);
            }
        }

        public static FormattedText ReplacePremiumLink(string text, PremiumFeature feature)
        {
            var markdown = ClientEx.ParseMarkdown(text);
            if (markdown.Entities.Count == 1)
            {
                // TODO: premium source
                markdown.Entities[0].Type = new TextEntityTypeTextUrl("tg://premium_offer");
            }

            return markdown;
        }

        public static Color ToColor(this int color, bool alpha = false)
        {
            if (alpha)
            {
                byte a = (byte)((color & 0xff000000) >> 24);
                byte r = (byte)((color & 0x00ff0000) >> 16);
                byte g = (byte)((color & 0x0000ff00) >> 8);
                byte b = (byte)(color & 0x000000ff);

                return Color.FromArgb(a, r, g, b);
            }

            return Color.FromArgb(0xFF, (byte)((color >> 16) & 0xFF), (byte)((color >> 8) & 0xFF), (byte)(color & 0xFF));
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
        /// Test for almost equality to 0.
        /// </summary>
        /// <param name="number"></param>
        /// <param name="epsilon"></param>
        public static bool AlmostEqualsToZero(this double number, double epsilon = 1e-5)
        {
            return number > -epsilon && number < epsilon;
        }

        /// <summary>
        /// Test for almost equality.
        /// </summary>
        /// <param name="number"></param>
        /// <param name="other"></param>
        /// <param name="epsilon"></param>
        public static bool AlmostEquals(this double number, double other, double epsilon = 1e-5)
        {
            return (number - other).AlmostEqualsToZero(epsilon);
        }

        /// <summary>
        /// Test for almost equality to 0.
        /// </summary>
        /// <param name="number"></param>
        /// <param name="epsilon"></param>
        public static bool AlmostEqualsToZero(this float number, float epsilon = 1e-5f)
        {
            return number > -epsilon && number < epsilon;
        }

        /// <summary>
        /// Test for almost equality.
        /// </summary>
        /// <param name="number"></param>
        /// <param name="other"></param>
        /// <param name="epsilon"></param>
        public static bool AlmostEquals(this float number, float other, float epsilon = 1e-5f)
        {
            return (number - other).AlmostEqualsToZero(epsilon);
        }

        public static bool VisualContains(this FrameworkElement destination, FrameworkElement source)
        {
            var transform = source.TransformToVisual(destination);
            var point = transform.TransformPoint(new Point());

            var y1 = Math.Ceiling(point.Y);
            var y2 = Math.Truncate(point.Y + source.ActualHeight);

            var p1 = 0;
            var p2 = Math.Truncate(destination.ActualHeight);

            return y1 >= p1 && y2 <= p2;
        }

        public static int ToTimestamp(this DateTime dateTime)
        {
            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

            return (int)(dateTime.ToUniversalTime() - dtDateTime).TotalSeconds;
        }

        public static long ToTimestampMilliseconds(this DateTime dateTime)
        {
            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

            return (long)(dateTime.ToUniversalTime() - dtDateTime).TotalMilliseconds;
        }

        public static Size ToSize(this Rect rectangle)
        {
            return new Size(rectangle.Width, rectangle.Height);
        }

        public static bool IntersectsWith(this Rect a, Rect b)
        {
            return (b.X <= a.X + a.Width) &&
                (a.X <= b.X + b.Width) &&
                (b.Y <= a.Y + a.Height) &&
                (a.Y <= b.Y + b.Height);
        }

        public static bool TryGet<T>(this IDictionary<object, object> dict, object key, out T value)
        {
            if (dict.TryGetValue(key, out object tryGetValue) && tryGetValue is T tryGet)
            {
                value = tryGet;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        public static bool TryGet<T>(this IDictionary<string, object> dict, string key, out T value)
        {
            if (dict.TryGetValue(key, out object tryGetValue) && tryGetValue is T tryGet)
            {
                value = tryGet;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
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

        public static void Add(this InlineCollection inline, string text)
        {
            inline.Add(new Run
            {
                Text = text
            });
        }

        public static void Add(this InlineCollection inline, string text, FontWeight fontWeight)
        {
            inline.Add(new Run
            {
                Text = text,
                FontWeight = fontWeight
            });
        }

        public static void Add(this InlineCollection inline, string text, FontStyle fontStyle)
        {
            inline.Add(new Run
            {
                Text = text,
                FontStyle = fontStyle
            });
        }

        public static void Add(this InlineCollection inline, string text, TextDecorations textDecorations)
        {
            inline.Add(new Run
            {
                Text = text,
                TextDecorations = textDecorations
            });
        }


        public static uint GetHeight(this VideoProperties props)
        {
            return props.Orientation is VideoOrientation.Rotate180 or VideoOrientation.Normal ? props.Height : props.Width;
        }

        public static uint GetWidth(this VideoProperties props)
        {
            return props.Orientation is VideoOrientation.Rotate180 or VideoOrientation.Normal ? props.Width : props.Height;
        }

        public static void Shiftino<T>(this T[] array, int offset)
        {
            if (offset < 0)
            {
                while (offset < 0)
                {
                    var element = array[array.Length - 1];
                    Array.Copy(array, 0, array, 1, array.Length - 1);
                    array[0] = element;
                    offset += 1;
                }
            }
            else if (offset > 0)
            {
                while (offset > 0)
                {
                    var element = array[0];
                    Array.Copy(array, 1, array, 0, array.Length - 1);
                    array[array.Length - 1] = element;
                    offset -= 1;
                }
            }
        }


        public static T[] Shift<T>(this T[] array, int offset)
        {
            var output = new T[array.Length];

            if (offset < 0)
            {
                while (offset < 0)
                {
                    var element = array[output.Length - 1];
                    Array.Copy(array, 0, output, 1, array.Length - 1);
                    output[0] = element;
                    offset += 1;

                    array = output;
                }
            }
            else if (offset > 0)
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

        public static bool IsRelativePath(string relativeTo, string path, out string relative)
        {
            var relativeFull = Path.GetFullPath(relativeTo);
            var pathFull = Path.GetFullPath(path);

            if (pathFull.Length > relativeFull.Length && pathFull[relativeFull.Length] == '\\')
            {
                if (pathFull.StartsWith(relativeFull, StringComparison.OrdinalIgnoreCase))
                {
                    relative = pathFull.Substring(relativeFull.Length + 1);
                    return true;
                }
            }

            relative = null;
            return string.Equals(relativeFull, pathFull, StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<InputFile> ToGeneratedAsync(this StorageFile file, ConversionType conversion = ConversionType.Copy, string arguments = null, bool forceCopy = false)
        {
            var token = StorageService.Future.Add(file);
            var path = file.Path;

            if (NativeUtils.IsFileReadable(path, out long fileSize, out long fileTime))
            {
                if (conversion == ConversionType.Copy && arguments == null && !forceCopy)
                {
                    return new InputFileLocal(path);
                }

                if (conversion == ConversionType.Compress)
                {
                    path = Path.ChangeExtension(path, ".jpg");
                }

                return new InputFileGenerated(path, string.Format("{0}#{1}#{2}#{3}", token, conversion, arguments, fileTime), fileSize);
            }

            if (string.IsNullOrEmpty(path))
            {
                path = file.FolderRelativeId;
            }

            if (conversion == ConversionType.Compress)
            {
                path = Path.ChangeExtension(path, ".jpg");
            }

            try
            {
                var props = await file.GetBasicPropertiesAsync();
                return new InputFileGenerated(path, string.Format("{0}#{1}#{2}#{3:s}", token, conversion, arguments, props.DateModified), (long)props.Size);
            }
            catch
            {
                return new InputFileGenerated(path, string.Format("{0}#{1}#{2}#{3}", token, conversion, arguments, 0), 0);
            }
        }

        public static async Task<InputThumbnail> ToVideoThumbnailAsync(this StorageFile file, VideoConversion video = null, ConversionType conversion = ConversionType.Copy, string arguments = null)
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

        //public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        //{
        //    HashSet<TKey> seenKeys = new();
        //    foreach (TSource element in source)
        //    {
        //        if (seenKeys.Add(keySelector(element)))
        //        {
        //            yield return element;
        //        }
        //    }
        //}

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

        public static long GetInt64(this ApplicationDataContainer container, string key, long defaultValue)
        {
            if (container.Values.TryGetValue(key, out object value))
            {
                if (value is long result64)
                {
                    return result64;
                }
                else if (value is int result32)
                {
                    return result32;
                }
            }

            return defaultValue;
        }

        public static void BeginOnUIThread(this DependencyObject element, Action action)
        {
            try
            {
                if (element.DispatcherQueue.HasThreadAccess)
                {
                    action();
                }
                else
                {
                    _ = element.DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            action();
                        }
                        catch (InvalidComObjectException)
                        {

                        }
                    });
                }
            }
            catch (InvalidComObjectException)
            {
                // Most likely Excep_InvalidComObject_NoRCW_Wrapper, so we can just ignore it
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

        public static bool IsBetween(this TimeSpan value, TimeSpan minimum, TimeSpan maximum)
        {
            // see if start comes before end
            if (minimum < maximum)
            {
                return minimum <= value && value <= maximum;
            }

            // start is after end, so do the inverse comparison
            return !(maximum < value && value < minimum);
        }

        public static bool IsValidUrl(this string text)
        {
            return IsValidEntity<TextEntityTypeUrl>(text);
        }

        public static bool IsValidEmailAddress(this string text)
        {
            return IsValidEntity<TextEntityTypeEmailAddress>(text);
        }

        public static bool IsValidEntity<T>(this string text)
        {
            var entities = ClientEx.GetTextEntities(text);
            return entities.Count == 1 && entities[0].Offset == 0 && entities[0].Length == text.Length && entities[0].Type is T;
        }

        public static string Format(this string input)
        {
            if (input != null)
            {
                return input.Trim().Replace("\r\n", "\n").Replace('\v', '\n').Replace('\r', '\n');
            }

            return string.Empty;
        }

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

        public static int BinarySearch<TItem, TSearch>(this IList<TItem> list, TSearch value, Func<TSearch, TItem, int> comparer)
        {
            int lower = 0;
            int upper = list.Count - 1;

            while (lower <= upper)
            {
                int middle = lower + (upper - lower) / 2;
                int comparisonResult = comparer(value, list[middle]);
                if (comparisonResult < 0)
                {
                    upper = middle - 1;
                }
                else if (comparisonResult > 0)
                {
                    lower = middle + 1;
                }
                else
                {
                    return middle;
                }
            }

            return ~lower;
        }

        public static int BinarySearch<TItem>(this IList<TItem> list, TItem value)
        {
            return BinarySearch(list, value, Comparer<TItem>.Default);
        }

        public static int BinarySearch<TItem>(this IList<TItem> list, TItem value, IComparer<TItem> comparer)
        {
            return list.BinarySearch(value, comparer.Compare);
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

        public static bool Empty<T>(this IList<T> list)
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

        public static T GetChild<T>(this DependencyObject parentContainer)
        {
            return parentContainer.Descendants<T>().FirstOrDefault();
        }

        public static T GetChild<T>(this DependencyObject parentContainer, Func<T, bool> predicate)
        {
            return parentContainer.Descendants<T>().FirstOrDefault(predicate);
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

        public static async Task UpdateLayoutAsync(this FrameworkElement element, bool update = false)
        {
            var tcs = new TaskCompletionSource<bool>();
            void layoutUpdated(object s1, object e1)
            {
                tcs.TrySetResult(true);
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
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }
    }

    public static class UriEx
    {
        public static BitmapImage ToBitmap(string path, int width = 0, int height = 0)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return new BitmapImage(ToLocal(path))
            {
                DecodePixelWidth = width,
                DecodePixelHeight = height,
                DecodePixelType = width > 0 || height > 0
                    ? DecodePixelType.Logical
                    : DecodePixelType.Logical
            };
        }

        public static SvgImageSource ToSvg(string path, int width = 0, int height = 0)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return new SvgImageSource(ToLocal(path))
            {

            };
        }

        public static Uri ToLocal(string path)
        {
            return new Uri(path);

            string directory;
            string file;

            var index = path.LastIndexOf('\\');
            if (index >= 0)
            {
                directory = path.Substring(0, index);
                file = path.Substring(index + 1);
            }
            else
            {
                directory = Path.GetDirectoryName(path);
                file = Path.GetFileName(path);
            }

            return new Uri("file:///" + directory + "\\" + Uri.EscapeDataString(file));
        }
    }
}
