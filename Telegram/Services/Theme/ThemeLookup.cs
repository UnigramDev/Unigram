//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Services.Settings;
using Windows.UI;

namespace Telegram.Services
{
    public enum ThemeValueKind : byte
    {
        None,
        Color,
        Shade,
        AcrylicColor,
        AcrylicShade
    }

    /// <summary>
    /// One default of one theme: a colour, an accent shade, or an acrylic recipe of either.
    /// </summary>
    /// <remarks>
    /// Eight bytes - the kind in the high word, and an ARGB colour, an <see cref="AccentShade"/> or
    /// an index into <see cref="ThemeDefaults.AcrylicColors"/> in the low one. There are ~1600 of
    /// these per theme and they were <c>object</c>, so each was a box that lived for the session.
    /// </remarks>
    public readonly struct ThemeValue
    {
        private readonly ulong _packed;

        internal ThemeValue(ulong packed)
        {
            _packed = packed;
        }

        internal ulong Packed => _packed;

        public ThemeValueKind Kind => (ThemeValueKind)(byte)(_packed >> 32);

        public Color Color
        {
            get
            {
                var value = (uint)_packed;
                return Windows.UI.Color.FromArgb((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
            }
        }

        public AccentShade Shade => (AccentShade)(uint)_packed;

        public Acrylic<Windows.UI.Color> AcrylicColor => ThemeDefaults.AcrylicColors[(int)(uint)_packed];

        public Acrylic<AccentShade> AcrylicShade => ThemeDefaults.AcrylicShades[(int)(uint)_packed];

        public static implicit operator ThemeValue(Color color)
        {
            var value = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
            return new ThemeValue(((ulong)ThemeValueKind.Color << 32) | value);
        }

        public static implicit operator ThemeValue(AccentShade shade)
        {
            return new ThemeValue(((ulong)ThemeValueKind.Shade << 32) | (uint)shade);
        }
    }

    public readonly struct ThemeEntry
    {
        public string Key { get; }

        public ThemeValue Value { get; }

        internal ThemeEntry(string key, ThemeValue value)
        {
            Key = key;
            Value = value;
        }
    }

    /// <summary>
    /// The defaults of one theme, over the key table both themes share.
    /// </summary>
    /// <remarks>
    /// A struct over two arrays owned by <see cref="ThemeDefaults"/>: the packed values, indexed by
    /// slot, and the slots this theme actually defines, in the order it defined them. The order is
    /// carried explicitly because the two themes disagree about it for twenty of their keys, and
    /// the theme editor lists them in that order.
    /// </remarks>
    public readonly struct ThemeLookup
    {
        private readonly ulong[] _values;
        private readonly int[] _order;

        internal ThemeLookup(ulong[] values, int[] order)
        {
            _values = values;
            _order = order;
        }

        public int Count => _order.Length;

        public bool TryGetValue(string key, out ThemeValue value)
        {
            if (ThemeDefaults.Slots.TryGetValue(key, out int slot))
            {
                // Zero is None with an empty payload, which no real value packs to: a key the
                // other theme defines and this one does not.
                var packed = _values[slot];
                if (packed != 0)
                {
                    value = new ThemeValue(packed);
                    return true;
                }
            }

            value = default;
            return false;
        }

        public bool TryGetColor(string key, out Color color)
        {
            if (TryGetValue(key, out ThemeValue value) && value.Kind == ThemeValueKind.Color)
            {
                color = value.Color;
                return true;
            }

            color = default;
            return false;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_values, _order);
        }

        public struct Enumerator
        {
            private readonly ulong[] _values;
            private readonly int[] _order;
            private int _index;

            internal Enumerator(ulong[] values, int[] order)
            {
                _values = values;
                _order = order;
                _index = -1;
            }

            public ThemeEntry Current
            {
                get
                {
                    var slot = _order[_index];
                    return new ThemeEntry(ThemeDefaults.Keys[slot], new ThemeValue(_values[slot]));
                }
            }

            public bool MoveNext()
            {
                return ++_index < _order.Length;
            }
        }
    }
}
