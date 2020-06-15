using Unigram.Controls;
using Windows.UI;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class SelectColorPopup : ContentPopup
    {
        public SelectColorPopup()
        {
            InitializeComponent();

            Title = Strings.Resources.ColorPickerMainColor;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        private Color _color;
        public Color Color
        {
            get => _color;
            set => Picker.Color = _color = value;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void TextField_ColorChanged(ColorTextBox sender, Controls.ColorChangedEventArgs args)
        {
            _color = args.NewColor;
            Picker.Color = args.NewColor;
        }

        private void Picker_ColorChanged(Controls.ColorPicker sender, Controls.ColorChangedEventArgs args)
        {
            _color = args.NewColor;
            TextField.Color = args.NewColor;
        }

        private void System_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            _color = default;
            Hide(ContentDialogResult.Primary);
        }
    }
}
