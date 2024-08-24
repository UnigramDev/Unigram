﻿//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Telegram.Controls
{
    public class SettingsPanel : Panel
    {
        public SettingsPanel()
        {
            MaxWidth = 1048;

            //ChildrenTransitions = new TransitionCollection
            //{
            //    new RepositionThemeTransition()
            //};
        }

        public bool IsHeader { get; set; }
        public bool IsFooter { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            var accumulated = IsFooter ? 0 : 64d;

            foreach (UIElement child in Children)
            {
                if (child is SettingsFooter && accumulated > (IsFooter ? 16 : 80))
                {
                    accumulated -= 16;
                }

                child.Measure(availableSize);

                if (child.DesiredSize.Height > 0)
                {
                    accumulated += child.DesiredSize.Height;
                    accumulated += 16;
                }
            }

            if (IsHeader)
            {
                return new Size(availableSize.Width, accumulated - 16);
            }
            else
            {
                return new Size(availableSize.Width, accumulated + 16);
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var accumulated = IsFooter ? 0 : 64d;

            foreach (var child in Children)
            {
                if (child is SettingsFooter && accumulated > (IsFooter ? 16 : 80))
                {
                    accumulated -= 16;
                }

                child.Arrange(new Rect(0, accumulated, finalSize.Width, child.DesiredSize.Height));

                if (child.DesiredSize.Height > 0)
                {
                    accumulated += child.DesiredSize.Height;
                    accumulated += 16;
                }
            }

            return finalSize;
        }
    }
}
