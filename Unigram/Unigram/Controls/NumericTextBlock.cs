//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Unigram.Controls
{
    public class NumericTextBlock : Control
    {
        private Grid _layoutRoot;
        private TextBlock _beforePart;
        private TextBlock _afterPart;

        public NumericTextBlock()
        {
            DefaultStyleKey = typeof(NumericTextBlock);
        }

        protected override void OnApplyTemplate()
        {
            _layoutRoot = GetTemplateChild("LayoutRoot") as Grid;
            _beforePart = GetTemplateChild("BeforePart") as TextBlock;
            _afterPart = GetTemplateChild("AfterPart") as TextBlock;

            OnTextChanged(Text, null);

            base.OnApplyTemplate();
        }

        private void OnTextChanged(string newValue, string oldValue)
        {
            if (_layoutRoot == null)
            {
                return;
            }

            _beforePart.Style = TextStyle;
            _afterPart.Style = TextStyle;

            _layoutRoot.Children.Clear();
            _layoutRoot.ColumnDefinitions.Clear();

            var next = ParseText(newValue, out var nextValue, out var before, out var after);
            var prev = ParseText(oldValue, out var prevValue, out _, out _);

            if (next != null && prev != null)
            {
                _beforePart.Text = before;
                _afterPart.Text = after;

                UpdateView(next, prev, nextValue, prevValue);
            }
            else
            {
                _beforePart.Text = newValue ?? string.Empty;
                _afterPart.Text = string.Empty;
            }
        }

        private void UpdateView(string next, string prev, int nextValue, int prevValue)
        {
            var direction = nextValue - prevValue;

            var nextArr = new TextBlock[Math.Max(next.Length, prev.Length)];
            var prevArr = new TextBlock[Math.Max(next.Length, prev.Length)];
            var prevFor = new bool[Math.Max(next.Length, prev.Length)];

            for (int i = 0; i < Math.Max(next.Length, prev.Length); i++)
            {
                _layoutRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

                if (next.Length > i && prev.Length > i)
                {
                    prevArr[i] = GetPart(prev[i], i);

                    if (next[i] != prev[i])
                    {
                        nextArr[i] = GetPart(next[i], i);
                    }
                }
                else if (prev.Length > i)
                {
                    prevArr[i] = GetPart(prev[i], i);
                    prevFor[i] = true;
                }
                else if (next.Length > i)
                {
                    nextArr[i] = GetPart(next[i], i);
                }
            }

            for (int i = 0; i < nextArr.Length; i++)
            {
                if (prevArr[i] != null)
                {
                    _layoutRoot.Children.Add(prevArr[i]);

                    if (nextArr[i] != null || prevFor[i])
                    {
                        var visual = ElementCompositionPreview.GetElementVisual(prevArr[i]);

                        var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
                        offset.InsertKeyFrame(0, new Vector3(0, 0, 0));
                        offset.InsertKeyFrame(1, new Vector3(0, direction > 0 ? -8 : 8, 0));

                        visual.StartAnimation("Translation", offset);

                        var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
                        opacity.InsertKeyFrame(0, 1);
                        opacity.InsertKeyFrame(1, 0);

                        visual.StartAnimation("Opacity", opacity);
                    }
                }

                if (nextArr[i] != null)
                {
                    _layoutRoot.Children.Add(nextArr[i]);

                    var visual = ElementCompositionPreview.GetElementVisual(nextArr[i]);

                    var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
                    offset.InsertKeyFrame(0, new Vector3(0, direction > 0 ? 8 : -8, 0));
                    offset.InsertKeyFrame(1, new Vector3(0, 0, 0));

                    visual.StartAnimation("Translation", offset);

                    var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
                    opacity.InsertKeyFrame(0, 0);
                    opacity.InsertKeyFrame(1, 1);

                    visual.StartAnimation("Opacity", opacity);
                }
            }
        }

        private TextBlock GetPart(char part, int index)
        {
            var textBlock = new TextBlock
            {
                Text = $"{part}",
                Style = TextStyle
            };

            var binding = new Binding
            {
                Path = new PropertyPath("Foreground"),
                Source = this
            };

            textBlock.SetBinding(ForegroundProperty, binding);

            Grid.SetColumn(textBlock, index);
            ElementCompositionPreview.SetIsTranslationEnabled(textBlock, true);
            return textBlock;
        }

        private string ParseText(string text, out int value, out string before, out string after)
        {
            value = -1;
            before = string.Empty;
            after = string.Empty;

            if (text == null)
            {
                return null;
            }

            var match = Regex.Match(text, "[0-9]+");
            if (match.Success && match.Groups.Count > 0)
            {
                var group = match.Groups[0];

                if (int.TryParse(group.Value, out value))
                {
                    if (group.Index > 0)
                    {
                        before = text.Substring(0, group.Index);
                    }

                    if (group.Index + group.Length < text.Length)
                    {
                        after = text.Substring(group.Index + group.Length);
                    }

                    return group.Value;
                }
            }

            return null;
        }

        #region Text

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(NumericTextBlock), new PropertyMetadata(null, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((NumericTextBlock)d).OnTextChanged((string)e.NewValue, (string)e.OldValue);
        }

        #endregion

        #region TextStyle

        public Style TextStyle
        {
            get => (Style)GetValue(TextStyleProperty);
            set => SetValue(TextStyleProperty, value);
        }

        public static readonly DependencyProperty TextStyleProperty =
            DependencyProperty.Register("TextStyle", typeof(NumericTextBlock), typeof(NumericTextBlock), new PropertyMetadata(null));

        #endregion

        #region OverflowVisibility

        public Visibility OverflowVisibility
        {
            get => (Visibility)GetValue(OverflowVisibilityProperty);
            set => SetValue(OverflowVisibilityProperty, value);
        }

        public static readonly DependencyProperty OverflowVisibilityProperty =
            DependencyProperty.Register("OverflowVisibility", typeof(Visibility), typeof(NumericTextBlock), new PropertyMetadata(Visibility.Visible));

        #endregion

    }
}
