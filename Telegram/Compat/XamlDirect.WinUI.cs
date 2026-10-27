//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Telegram.Native;
using Windows.UI.Text;

namespace Telegram.Compat.Direct
{
    /// <summary>
    /// XamlDirect, reimplemented over ordinary XAML objects, because the Windows App SDK has none -
    /// no <c>XamlDirect</c>, no <c>IXamlDirectObject</c>, no <c>Microsoft.UI.Xaml.Core.Direct</c>
    /// anywhere in its metadata, re-verified against 2.4.0.
    ///
    /// The point of XamlDirect in <see cref="Telegram.Controls.FormattedTextBlock"/> is to mutate
    /// recycled Paragraph, Span and Run instances without paying for a DependencyObject and a CCW
    /// per inline, and **this shim gives all of that cost straight back** - it is ordinary SetValue
    /// on ordinary objects. What it buys is that the call sites compile and run unchanged.
    ///
    /// A handle is the object itself: <c>IXamlDirectObject</c> is a global alias of
    /// <see cref="object"/> in this flavour, so identity survives a round trip with no table.
    /// </summary>
    public sealed class XamlDirect
    {
        private static readonly XamlDirect _default = new();

        public static XamlDirect GetDefault() => _default;

        public IXamlDirectObject CreateInstance(XamlTypeIndex index)
        {
            return index switch
            {
                XamlTypeIndex.Paragraph => new Paragraph(),
                XamlTypeIndex.Run => new Run(),
                XamlTypeIndex.Span => new Span(),
                _ => null
            };
        }

        public IXamlDirectObject GetXamlDirectObject(object instance)
        {
            return instance;
        }

        public object GetObject(IXamlDirectObject handle)
        {
            return handle;
        }

        public IXamlDirectObject GetXamlDirectObjectProperty(IXamlDirectObject handle, XamlPropertyIndex property)
        {
            return property switch
            {
                XamlPropertyIndex.Paragraph_Inlines => (handle as Paragraph)?.Inlines,
                XamlPropertyIndex.Span_Inlines => (handle as Span)?.Inlines,
                XamlPropertyIndex.RichTextBlock_Blocks => (handle as RichTextBlock)?.Blocks,
                _ => handle is DependencyObject o && Property(property) is DependencyProperty p
                    ? o.GetValue(p)
                    : null
            };
        }

        public void AddToCollection(IXamlDirectObject collection, IXamlDirectObject item)
        {
            switch (collection)
            {
                case InlineCollection inlines when item is Inline inline:
                    inlines.Add(inline);
                    break;
                case BlockCollection blocks when item is Block block:
                    blocks.Add(block);
                    break;
            }
        }

        public void ClearCollection(IXamlDirectObject collection)
        {
            switch (collection)
            {
                case InlineCollection inlines:
                    inlines.Clear();
                    break;
                case BlockCollection blocks:
                    blocks.Clear();
                    break;
            }
        }

        public void SetObjectProperty(IXamlDirectObject handle, XamlPropertyIndex property, object value)
        {
            Set(handle, property, value);
        }

        public void SetStringProperty(IXamlDirectObject handle, XamlPropertyIndex property, string value)
        {
            // Run.Text is a plain property in WinUI 3, not a DependencyProperty - there is no
            // Run.TextProperty to SetValue against. It is also the single hottest write on this
            // path, so it is worth the special case rather than a general fallback.
            if (property == XamlPropertyIndex.Run_Text && handle is Run run)
            {
                run.Text = value;
                return;
            }

            Set(handle, property, value);
        }

        public void SetDoubleProperty(IXamlDirectObject handle, XamlPropertyIndex property, double value)
        {
            Set(handle, property, value);
        }

        /// <summary>
        /// The one method the framework's signature makes lossy: every enum arrives as a uint, and
        /// SetValue needs the boxed value to be the property's own enum type or the assignment is
        /// rejected at the ABI. So the index has to say which enum it was.
        /// </summary>
        public void SetEnumProperty(IXamlDirectObject handle, XamlPropertyIndex property, uint value)
        {
            object boxed = property switch
            {
                XamlPropertyIndex.Block_TextAlignment => (TextAlignment)value,
                XamlPropertyIndex.Run_FlowDirection => (FlowDirection)value,
                XamlPropertyIndex.Hyperlink_UnderlineStyle => (UnderlineStyle)value,
                XamlPropertyIndex.TextElement_FontStyle => (FontStyle)value,
                XamlPropertyIndex.TextElement_TextDecorations => (TextDecorations)value,
                _ => value
            };

            Set(handle, property, boxed);
        }

        public void ClearProperty(IXamlDirectObject handle, XamlPropertyIndex property)
        {
            if (handle is DependencyObject o && Property(property) is DependencyProperty p)
            {
                o.ClearValue(p);
            }
        }

        private static void Set(IXamlDirectObject handle, XamlPropertyIndex property, object value)
        {
            if (handle is DependencyObject o && Property(property) is DependencyProperty p)
            {
                o.SetValue(p, value);
            }
        }

        private static DependencyProperty Property(XamlPropertyIndex property)
        {
            return property switch
            {
                XamlPropertyIndex.Block_TextAlignment => Block.TextAlignmentProperty,
                XamlPropertyIndex.Hyperlink_UnderlineStyle => Hyperlink.UnderlineStyleProperty,
                XamlPropertyIndex.Run_FlowDirection => Run.FlowDirectionProperty,
                // Run_Text is deliberately absent - see SetStringProperty.
                XamlPropertyIndex.TextElement_FontFamily => TextElement.FontFamilyProperty,
                XamlPropertyIndex.TextElement_FontSize => TextElement.FontSizeProperty,
                XamlPropertyIndex.TextElement_FontStyle => TextElement.FontStyleProperty,
                XamlPropertyIndex.TextElement_FontWeight => TextElement.FontWeightProperty,
                XamlPropertyIndex.TextElement_Foreground => TextElement.ForegroundProperty,
                XamlPropertyIndex.TextElement_TextDecorations => TextElement.TextDecorationsProperty,
                _ => null
            };
        }
    }

    /// <summary>
    /// Static members the shared sources call that this flavour's types lack. An extension only
    /// answers when the type has no member of that name, so each of these lights up here and
    /// nowhere else, and the call sites stay as they are.
    /// </summary>
    public static class FlavorExtensions
    {
        /// <summary>
        /// The managed half of <c>NativeUtils.AddRunToCollection</c>, the native fast path for
        /// building a Run, whose IDL is written in <c>Windows.UI.Xaml.Core.Direct</c> types end to
        /// end and so cannot exist here. A line-for-line port of the C++, so the runs come out the
        /// same; it gives up the single ABI crossing and the substring-free text.
        /// </summary>
        extension(NativeUtils)
        {
            public static IXamlDirectObject AddRunToCollection(XamlDirect direct, IXamlDirectObject inlines, string text, FlowDirection direction, TextStyle style, FontFamily fontFamily, double fontSize)
            {
                return NativeUtils.AddRunToCollection(direct, inlines, text, 0, text?.Length ?? 0, direction, style, fontFamily, fontSize);
            }

            public static IXamlDirectObject AddRunToCollection(XamlDirect direct, IXamlDirectObject inlines, string text, int offset, int length, FlowDirection direction, TextStyle style, FontFamily fontFamily, double fontSize)
            {
                var run = direct.CreateInstance(XamlTypeIndex.Run);
                direct.SetStringProperty(run, XamlPropertyIndex.Run_Text, text?.Substring(offset, length));
                direct.SetEnumProperty(run, XamlPropertyIndex.Run_FlowDirection, (uint)direction);

                if ((style & TextStyle.Bold) != TextStyle.None)
                {
                    direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontWeight, FontWeights.SemiBold);
                }

                if ((style & TextStyle.Italic) != TextStyle.None)
                {
                    direct.SetEnumProperty(run, XamlPropertyIndex.TextElement_FontStyle, (uint)FontStyle.Italic);
                }

                var decorations = TextDecorations.None;

                if ((style & TextStyle.Underline) != TextStyle.None)
                {
                    decorations |= TextDecorations.Underline;
                }

                if ((style & TextStyle.Strikethrough) != TextStyle.None)
                {
                    decorations |= TextDecorations.Strikethrough;
                }

                if (decorations != TextDecorations.None)
                {
                    direct.SetEnumProperty(run, XamlPropertyIndex.TextElement_TextDecorations, (uint)decorations);
                }

                if (fontFamily != null)
                {
                    direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontFamily, fontFamily);
                }

                if (fontSize > 0)
                {
                    direct.SetDoubleProperty(run, XamlPropertyIndex.TextElement_FontSize, fontSize);
                }

                direct.AddToCollection(inlines, run);
                return run;
            }
        }

        /// <summary>
        /// The Windows App SDK compositor has no view to ask, so the capabilities are constructed.
        /// </summary>
        extension(CompositionCapabilities)
        {
            public static CompositionCapabilities GetForCurrentView()
            {
                return new CompositionCapabilities();
            }
        }
    }

    /// <summary>
    /// Only what the app uses, of the framework's several hundred. A member that is missing here is
    /// a compile error at its call site, which is the right way round.
    /// </summary>
    public enum XamlTypeIndex
    {
        Paragraph,
        Run,
        Span,
    }

    public enum XamlPropertyIndex
    {
        Block_TextAlignment,
        Hyperlink_UnderlineStyle,
        Paragraph_Inlines,
        RichTextBlock_Blocks,
        Run_FlowDirection,
        Run_Text,
        Span_Inlines,
        TextElement_FontFamily,
        TextElement_FontSize,
        TextElement_FontStyle,
        TextElement_FontWeight,
        TextElement_Foreground,
        TextElement_TextDecorations,
    }
}
