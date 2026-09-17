//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    // The back button and the action that sit over a StickyHeader, and the box that places them.
    //
    // They live outside the scrolled content, so every page used to restate the same arrangement by
    // hand - and with it the assumption StickyHeader.Follow rests on, that the followed element's
    // parent starts where the header pins. A page that got the margin slightly wrong landed the
    // button a few pixels out rather than visibly breaking, so the arrangement belongs here.
    //
    // The action is the control's content:
    //
    //     <controls:StickyHeaderChrome Header="{x:Bind Header}">
    //         <controls:MoreButton Click="Menu_ContextRequested" />
    //     </controls:StickyHeaderChrome>
    public partial class StickyHeaderChrome : ContentControl
    {
        private BackButton BackPart;
        private ContentPresenter ActionPart;

        public StickyHeaderChrome()
        {
            DefaultStyleKey = typeof(StickyHeaderChrome);
        }

        #region Header

        /// <summary>
        /// The header both parts collapse with.
        /// </summary>
        public StickyHeader Header
        {
            get => (StickyHeader)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(StickyHeader), typeof(StickyHeaderChrome), new PropertyMetadata(null, OnHeaderChanged));

        private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // The header is usually set from XAML, before the template has been applied.
            (d as StickyHeaderChrome).UpdateHeader();
        }

        #endregion

        protected override void OnApplyTemplate()
        {
            BackPart = GetTemplateChild(nameof(BackPart)) as BackButton;
            ActionPart = GetTemplateChild(nameof(ActionPart)) as ContentPresenter;

            UpdateHeader();

            base.OnApplyTemplate();
        }

        /// <summary>
        /// Handed on through the attached property rather than by calling <see cref="StickyHeader.Follow"/>,
        /// so that there is one mechanism: a page can still attach the same behaviour to an element
        /// of its own, and clearing the header detaches both parts.
        /// </summary>
        private void UpdateHeader()
        {
            var header = Header;

            if (BackPart != null)
            {
                StickyHeader.SetHeader(BackPart, header);
            }

            if (ActionPart != null)
            {
                StickyHeader.SetHeader(ActionPart, header);
            }
        }
    }
}
