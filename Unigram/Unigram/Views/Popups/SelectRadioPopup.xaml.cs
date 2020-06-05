using System.Linq;
using Unigram.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Views.Popups
{
    public sealed partial class SelectRadioPopup : ContentPopup
    {
        public SelectRadioPopup(params SelectRadioItem[] options)
        {
            this.InitializeComponent();

            for (int i = 0; i < options.Length; i++)
            {
                var radio = new RadioButton();
                radio.Checked += Radio_Checked;
                radio.Content = options[i].Text;
                radio.Tag = options[i];
                radio.IsChecked = options[i].IsChecked;
                radio.Margin = new Thickness(12, i == 0 ? 6 : 0, 0, 0);

                var rect = new Rectangle();
                rect.Style = Resources["RectangleStyle"] as Style;
                rect.Margin = new Thickness(12, 6, 0, i == options.Length - 1 ? 0 : 6);

                LayoutRoot.Items.Add(radio);
                LayoutRoot.Items.Add(rect);
            }
        }

        private void Radio_Checked(object sender, RoutedEventArgs e)
        {
            var radio = sender as RadioButton;
            var item = radio.Tag as SelectRadioItem;

            LayoutRoot.Footer = item.Footer ?? string.Empty;
        }

        public object SelectedIndex { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SelectedIndex = ((SelectRadioItem)LayoutRoot.Items.OfType<RadioButton>().FirstOrDefault(x => x.IsChecked == true)?.Tag)?.Value;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }

    public class SelectRadioItem
    {
        public SelectRadioItem(object value, string text, bool check)
        {
            Value = value;
            Text = text;
            IsChecked = check;
        }

        public object Value { get; set; }
        public string Text { get; set; }
        public bool IsChecked { get; set; }

        public string Footer { get; set; }
    }
}
