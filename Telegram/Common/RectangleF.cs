//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;

namespace Telegram.Common
{
    public struct RectangleF : IEquatable<RectangleF>
    {
        public static readonly RectangleF Empty = new(0, 0, 0, 0);

        private float x;
        private float y;
        private float width;
        private float height;

        public RectangleF(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        public RectangleF(Vector2 location, Vector2 size)
        {
            this.x = location.X;
            this.y = location.Y;
            this.width = size.X;
            this.height = size.Y;
        }

        /// <summary>
        /// Gets or sets the x-coordinate of the upper-left corner.
        /// </summary>
        public float X
        {
            get => x;
            set => x = value;
        }

        /// <summary>
        /// Gets or sets the y-coordinate of the upper-left corner.
        /// </summary>
        public float Y
        {
            get => y;
            set => y = value;
        }

        /// <summary>
        /// Gets or sets the width of the rectangle.
        /// </summary>
        public float Width
        {
            get => width;
            set => width = value;
        }

        /// <summary>
        /// Gets or sets the height of the rectangle.
        /// </summary>
        public float Height
        {
            get => height;
            set => height = value;
        }

        /// <summary>
        /// Gets the x-coordinate of the left edge.
        /// </summary>
        public float Left => x;

        /// <summary>
        /// Gets the y-coordinate of the top edge.
        /// </summary>
        public float Top => y;

        /// <summary>
        /// Gets the x-coordinate of the right edge.
        /// </summary>
        public float Right => x + width;

        /// <summary>
        /// Gets the y-coordinate of the bottom edge.
        /// </summary>
        public float Bottom => y + height;

        /// <summary>
        /// Gets the x-coordinate that is the sum of X and Width/2.
        /// </summary>
        public float CenterX => x + width / 2f;

        /// <summary>
        /// Gets the y-coordinate that is the sum of Y and Height/2.
        /// </summary>
        public float CenterY => y + height / 2f;

        /// <summary>
        /// Gets the center point of the rectangle.
        /// </summary>
        public Vector2 Center => new(CenterX, CenterY);

        /// <summary>
        /// Gets the x-coordinate of the left edge (alias for Left).
        /// </summary>
        public float MinX => Left;

        /// <summary>
        /// Gets the y-coordinate of the top edge (alias for Top).
        /// </summary>
        public float MinY => Top;

        /// <summary>
        /// Gets the x-coordinate of the right edge (alias for Right).
        /// </summary>
        public float MaxX => Right;

        /// <summary>
        /// Gets the y-coordinate of the bottom edge (alias for Bottom).
        /// </summary>
        public float MaxY => Bottom;

        /// <summary>
        /// Gets the location of the upper-left corner.
        /// </summary>
        public Vector2 Location
        {
            get => new(x, y);
            set
            {
                x = value.X;
                y = value.Y;
            }
        }

        /// <summary>
        /// Gets the size of the rectangle.
        /// </summary>
        public Vector2 Size
        {
            get => new(width, height);
            set
            {
                width = value.X;
                height = value.Y;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this rectangle is empty (has zero area).
        /// </summary>
        public bool IsEmpty => width <= 0 || height <= 0;

        /// <summary>
        /// Determines whether the specified point is contained within this rectangle.
        /// </summary>
        public bool Contains(float x, float y)
        {
            return this.x <= x && x < this.x + width &&
                    this.y <= y && y < this.y + height;
        }

        /// <summary>
        /// Determines whether the specified point is contained within this rectangle.
        /// </summary>
        public bool Contains(Vector2 point)
        {
            return Contains(point.X, point.Y);
        }

        /// <summary>
        /// Determines whether the specified rectangle is entirely contained within this rectangle.
        /// </summary>
        public bool Contains(RectangleF rect)
        {
            return (x <= rect.x) && (rect.x + rect.width <= x + width) &&
                    (y <= rect.y) && (rect.y + rect.height <= y + height);
        }

        /// <summary>
        /// Determines whether this rectangle intersects with the specified rectangle.
        /// </summary>
        public bool IntersectsWith(RectangleF rect)
        {
            return (rect.x < x + width) && (x < rect.x + rect.width) &&
                    (rect.y < y + height) && (y < rect.y + rect.height);
        }

        /// <summary>
        /// Returns the intersection of two rectangles.
        /// </summary>
        public static RectangleF Intersect(RectangleF a, RectangleF b)
        {
            float x1 = Math.Max(a.x, b.x);
            float x2 = Math.Min(a.x + a.width, b.x + b.width);
            float y1 = Math.Max(a.y, b.y);
            float y2 = Math.Min(a.y + a.height, b.y + b.height);

            if (x2 >= x1 && y2 >= y1)
            {
                return new RectangleF(x1, y1, x2 - x1, y2 - y1);
            }
            return Empty;
        }

        /// <summary>
        /// Replaces this rectangle with the intersection of itself and the specified rectangle.
        /// </summary>
        public void Intersect(RectangleF rect)
        {
            RectangleF result = Intersect(rect, this);
            this.x = result.x;
            this.y = result.y;
            this.width = result.width;
            this.height = result.height;
        }

        /// <summary>
        /// Creates the smallest rectangle that can contain both of two rectangles.
        /// </summary>
        public static RectangleF Union(RectangleF a, RectangleF b)
        {
            float x1 = Math.Min(a.x, b.x);
            float x2 = Math.Max(a.x + a.width, b.x + b.width);
            float y1 = Math.Min(a.y, b.y);
            float y2 = Math.Max(a.y + a.height, b.y + b.height);
            return new RectangleF(x1, y1, x2 - x1, y2 - y1);
        }

        /// <summary>
        /// Adjusts the location of this rectangle by the specified amounts.
        /// </summary>
        public void Offset(float dx, float dy)
        {
            x += dx;
            y += dy;
        }

        /// <summary>
        /// Adjusts the location of this rectangle by the specified point.
        /// </summary>
        public void Offset(Vector2 pos)
        {
            Offset(pos.X, pos.Y);
        }

        /// <summary>
        /// Inflates this rectangle by the specified amount.
        /// </summary>
        public void Inflate(float dx, float dy)
        {
            x -= dx;
            y -= dy;
            width += 2 * dx;
            height += 2 * dy;
        }

        /// <summary>
        /// Inflates this rectangle by the specified size.
        /// </summary>
        public void Inflate(Vector2 size)
        {
            Inflate(size.X, size.Y);
        }

        /// <summary>
        /// Creates a rectangle that is inflated by the specified amount.
        /// </summary>
        public static RectangleF Inflate(RectangleF rect, float x, float y)
        {
            RectangleF result = rect;
            result.Inflate(x, y);
            return result;
        }

        public override bool Equals(object obj)
        {
            return obj is RectangleF other && Equals(other);
        }

        public bool Equals(RectangleF other)
        {
            return x.Equals(other.x) && y.Equals(other.y) &&
                    width.Equals(other.width) && height.Equals(other.height);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y, width, height);
        }

        public override string ToString()
        {
            return $"{{X={x}, Y={y}, Width={width}, Height={height}}}";
        }

        public static bool operator ==(RectangleF left, RectangleF right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RectangleF left, RectangleF right)
        {
            return !left.Equals(right);
        }
    }
}
