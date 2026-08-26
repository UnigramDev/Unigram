//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Common
{
    public static partial class Extensions
    {
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

        public static bool ViewportContains(this ScrollViewer destination, SelectorItem container)
        {
            var y1 = Math.Ceiling(container.ActualOffset.Y - destination.VerticalOffset);
            var y2 = Math.Truncate(container.ActualOffset.Y - destination.VerticalOffset + container.ActualSize.Y);

            var p1 = 0;
            var p2 = Math.Truncate(destination.ActualSize.Y);

            return y1 >= p1 && y2 <= p2;
        }

        public static bool IntersectsOrTouches(this Rect a, Rect b)
        {
            return a.Left <= b.Right &&
                   a.Right >= b.Left &&
                   a.Top <= b.Bottom &&
                   a.Bottom >= b.Top;
        }

        public static Size ToSize(this Rect rectangle)
        {
            return new Size(rectangle.Width, rectangle.Height);
        }

        public static Vector2 ToSizeF(this Rect rectangle)
        {
            return new Vector2((float)rectangle.Width, (float)rectangle.Height);
        }

        public static Vector3 ToOffset(this Rect rectangle)
        {
            return new Vector3((float)rectangle.X, (float)rectangle.Y, 0);
        }

        public static bool IntersectsWith(this Rect a, Rect b)
        {
            return (b.X <= a.X + a.Width) &&
                (a.X <= b.X + b.Width) &&
                (b.Y <= a.Y + a.Height) &&
                (a.Y <= b.Y + b.Height);
        }

        public static Vector2 TransformVector2(this GeneralTransform transform, Vector2 point)
        {
            return transform.TransformPoint(point.ToPoint()).ToVector2();
        }

        public static Vector2 TransformVector2(this GeneralTransform transform)
        {
            return transform.TransformPoint(new Point()).ToVector2();
        }

        public static Vector2 TransformToVector2(this UIElement element, UIElement visual)
        {
            return element.TransformToVisual(visual).TransformVector2();
        }

        public static Point TransformToPoint(this UIElement element, UIElement visual)
        {
            return element.TransformToVisual(visual).TransformPoint(new Point());
        }

        public static Point TransformToPointerPosition(this UIElement element)
        {
            var transform = element.TransformToPoint(null);

            var window = WindowContext.ForXamlRoot(element.XamlRoot);
            var bounds = window.Bounds;
            var point = window.PointerPosition;

            point = new Point(point.X - bounds.X, point.Y - bounds.Y);
            point = new Point(point.X - transform.X, point.Y - transform.Y);

            return point;
        }

        public static bool Contains(this FrameworkElement element, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(element);
            if (point.Position.X >= 0 && point.Position.Y >= 0 && point.Position.X <= element.ActualWidth && point.Position.Y <= element.ActualHeight)
            {
                return true;
            }

            return false;
        }
    }
}
