//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages
{
    public sealed partial class MessageSummary : HyperlinkButton
    {
        public MessageSummary()
        {
            DefaultStyleKey = typeof(MessageSummary);
        }

        #region HeaderBrush

        public Brush HeaderBrush
        {
            get { return (Brush)GetValue(HeaderBrushProperty); }
            set { SetValue(HeaderBrushProperty, value); }
        }

        public static readonly DependencyProperty HeaderBrushProperty =
            DependencyProperty.Register("HeaderBrush", typeof(Brush), typeof(MessageSummary), new PropertyMetadata(null));

        #endregion

        #region SubtleBrush

        public Brush SubtleBrush
        {
            get { return (Brush)GetValue(SubtleBrushProperty); }
            set { SetValue(SubtleBrushProperty, value); }
        }

        public static readonly DependencyProperty SubtleBrushProperty =
            DependencyProperty.Register("SubtleBrush", typeof(Brush), typeof(MessageSummary), new PropertyMetadata(null));

        #endregion

        #region InitializeComponent

        private Grid LayoutRoot;
        private Rectangle BackgroundOverlay;
        private TextBlock Label;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Grid;
            BackgroundOverlay = GetTemplateChild(nameof(BackgroundOverlay)) as Rectangle;
            Label = GetTemplateChild(nameof(Label)) as TextBlock;

            BackgroundOverlay.Margin = new Thickness(0, 0, -Padding.Right, 0);

            _templateApplied = true;
        }

        #endregion
    }
}
