//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Messages
{
    public partial class ReplyMarkupInlinePanel : Panel
    {
        private CompositionGeometricClip _clip;

        private bool _empty = true;

        public List<int> Rows { get; } = new();

        public Vector2 CornerRadius { get; set; }

        public ReplyMarkupInlinePanel()
        {
            TabFocusNavigation = KeyboardNavigationMode.Once;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ReplyMarkupInlinePanelAutomationPeer(this);
        }

        public void Update(MessageViewModel message)
        {
            if (_empty && message?.ReplyMarkup == null)
            {
                return;
            }

            _empty = message?.ReplyMarkup == null;

            Children.ClearIfNotEmpty();
            Rows.ClearIfNotEmpty();

            if (message.ReplyMarkup is ReplyMarkupInlineKeyboard inlineMarkup)
            {
                Update(message, inlineMarkup);
            }
        }

        public void Update(MessageViewModel message, ReplyMarkupInlineKeyboard inlineMarkup)
        {
            var rows = inlineMarkup.Rows;

            Tag = message;

            var receipt = false;
            if (message != null && message.Content is MessageInvoice invoice)
            {
                receipt = invoice.ReceiptMessageId != 0;

                if (invoice.PaidMedia is not PaidMediaUnsupported and not null)
                {
                    rows = null;
                }
            }

            if (rows == null)
            {
                return;
            }

            foreach (var row in rows)
            {
                foreach (var item in row)
                {
                    var button = new ReplyMarkupInlineButton(this, item);
                    button.HorizontalAlignment = HorizontalAlignment.Stretch;
                    button.VerticalAlignment = VerticalAlignment.Stretch;
                    button.Click += Button_Click;
                    button.SetButton(message.ClientService, item.Text, item.IconCustomEmojiId, item.Style, item.Type, receipt);

                    Children.Add(button);
                }

                Rows.Add(row.Count);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ReplyMarkupInlineButton button)
            {
                InlineButtonClick?.Invoke(this, new ReplyMarkupInlineButtonClickEventArgs(button.Button));
            }
        }

        public event EventHandler<ReplyMarkupInlineButtonClickEventArgs> InlineButtonClick;

        protected override Size MeasureOverride(Size availableSize)
        {
            var j = 0;
            var w = 0d;
            var h = 0d;

            var spacing = 2;

            foreach (var row in Rows)
            {
                var column = new Size(Math.Max(0, (availableSize.Width - spacing * (row - 1)) / row), availableSize.Height / Rows.Count);
                var width = 0d;
                var height = 0d;

                for (int i = 0; i < row; i++)
                {
                    var child = Children[j + i];
                    child.Measure(column);
                    width = Math.Max(width, child.DesiredSize.Width);
                    height = Math.Max(height, child.DesiredSize.Height);
                }

                var final = (width * row) + (spacing * (row - 1));
                if (final > availableSize.Width)
                {
                    w = availableSize.Width;
                }
                else
                {
                    w = Math.Max(w, final);
                }

                h += height + spacing;
                j += row;
            }

            return new Size(w, h);
        }

        // Reused across passes. These carry the button rectangles to the native clip
        // builder, and arrange runs on every layout of every inline keyboard, so
        // rebuilding them cost the outer list plus one inner list per row each time.
        private readonly List<IList<Rect>> _clipRows = new();
        private bool _clipValid;

        protected override Size ArrangeOverride(Size finalSize)
        {
            var j = 0;
            var y = 0d;

            var spacing = 2;

            if (_clip == null)
            {
                var visual = ElementComposition.GetElementVisual(this);
                visual.Clip = _clip = visual.Compositor.CreateGeometricClip();
            }

            // The clip costs a call across the ABI and a fresh path geometry, so it is
            // rebuilt only when a rectangle moves. Rectangles are compared in place as
            // they are written, which keeps the buffers reusable.
            var changed = !_clipValid || _clipRows.Count != Rows.Count;

            while (_clipRows.Count < Rows.Count)
            {
                _clipRows.Add(new List<Rect>());
            }

            while (_clipRows.Count > Rows.Count)
            {
                _clipRows.RemoveAt(_clipRows.Count - 1);
            }

            for (int r = 0; r < Rows.Count; r++)
            {
                var row = Rows[r];
                var buffer = _clipRows[r];

                var column = (finalSize.Width - spacing * (row - 1)) / row;
                var height = 0d;

                var x = 0d;

                // Cleared rather than trimmed afterwards, so the loop below only appends
                // past the end.
                if (buffer.Count != row)
                {
                    buffer.Clear();
                    changed = true;
                }

                y += spacing;

                for (int i = 0; i < row; i++)
                {
                    var child = Children[j + i];
                    var rect = new Rect(x, y, column, child.DesiredSize.Height);

                    child.Arrange(rect);

                    if (i < buffer.Count)
                    {
                        if (buffer[i] != rect)
                        {
                            buffer[i] = rect;
                            changed = true;
                        }
                    }
                    else
                    {
                        buffer.Add(rect);
                    }

                    height = Math.Max(height, child.DesiredSize.Height);
                    x += column + spacing;
                }

                y += height;
                j += row;
            }

            if (changed)
            {
                _clip.Geometry = _clip.Compositor.CreatePathGeometry(PlaceholderHelper.Foreground.GetReplyMarkupClip(_clipRows, CornerRadius.X, CornerRadius.Y));
                _clipValid = true;
            }

            return finalSize;
        }
    }

    public partial class ReplyMarkupInlinePanelAutomationPeer : FrameworkElementAutomationPeer
    {
        public ReplyMarkupInlinePanelAutomationPeer(ReplyMarkupInlinePanel owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.List;
        }
    }
}
