//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.ViewModels;
using Windows.Globalization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages
{
    public sealed class MessageFactCheck : Control
    {
        private MessageViewModel _message;

        public MessageFactCheck(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(MessageFactCheck);
        }

        #region HeaderBrush

        public Brush HeaderBrush
        {
            get { return (Brush)GetValue(HeaderBrushProperty); }
            set { SetValue(HeaderBrushProperty, value); }
        }

        public static readonly DependencyProperty HeaderBrushProperty =
            DependencyProperty.Register("HeaderBrush", typeof(Brush), typeof(MessageFactCheck), new PropertyMetadata(null));

        #endregion

        #region SubtleBrush

        public Brush SubtleBrush
        {
            get { return (Brush)GetValue(SubtleBrushProperty); }
            set { SetValue(SubtleBrushProperty, value); }
        }

        public static readonly DependencyProperty SubtleBrushProperty =
            DependencyProperty.Register("SubtleBrush", typeof(Brush), typeof(MessageFactCheck), new PropertyMetadata(null));

        #endregion

        #region InitializeComponent

        private Grid LayoutRoot;
        private Rectangle BackgroundOverlay;
        private Button Button;
        private TextBlock Label;
        private Border ExpandBackground;
        private CheckBox Expand;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Grid;
            BackgroundOverlay = GetTemplateChild(nameof(BackgroundOverlay)) as Rectangle;
            Button = GetTemplateChild(nameof(Button)) as Button;
            Label = GetTemplateChild(nameof(Label)) as TextBlock;
            ExpandBackground = GetTemplateChild(nameof(ExpandBackground)) as Border;
            Expand = GetTemplateChild(nameof(Expand)) as CheckBox;

            BackgroundOverlay.Margin = new Thickness(0, 0, -Padding.Right, 0);
            ExpandBackground.Visibility = Expand.Visibility = Label.IsTextTrimmed
                ? Visibility.Visible 
                : Visibility.Collapsed;

            Button.Click += Button_Click;
            Label.IsTextTrimmedChanged += Label_IsTextTrimmedChanged;
            Expand.Click += Expand_Click;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var factCheck = _message?.FactCheck;
            if (factCheck == null)
            {
                return;
            }

            string countryName;
            if (GeographicRegion.IsSupported(factCheck.CountryCode))
            {
                countryName = new GeographicRegion(factCheck.CountryCode).DisplayName;
            }
            else
            {
                countryName = factCheck.CountryCode;
            }

            await MessagePopup.ShowAsync(string.Format(Strings.FactCheckToast, countryName), Strings.FactCheckDialog, Strings.OK);
        }

        private void Label_IsTextTrimmedChanged(TextBlock sender, IsTextTrimmedChangedEventArgs args)
        {
            if (Expand.IsChecked is false)
            {
                ExpandBackground.Visibility = Expand.Visibility = sender.IsTextTrimmed
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void Expand_Click(object sender, RoutedEventArgs e)
        {
            Label.MaxLines = Expand.IsChecked is true ? 0 : 3;
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            if (!_templateApplied)
            {
                return;
            }

            Expand.IsChecked = false;
            TextBlockHelper.SetFormattedText(Label, message.FactCheck.Text);
        }
    }
}
