using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls.Views
{
    public sealed partial class SelectRadioView : TLContentDialog
    {
        public SelectRadioView(params SelectRadioItem[] options)
        {
            this.InitializeComponent();

            for (int i = 0; i < options.Length; i++)
            {
                /*                    <controls:PrivacyRadioButton
                        Content="{CustomResource LastSeenEverybody}"
                        Value="{x:Bind ViewModel.SelectedItem, Mode=TwoWay}"
                        Type="AllowAll"
                        Margin="12,6,0,0"/>
                    <Rectangle Fill="{ThemeResource TelegramSeparatorMediumBrush}" Height="1" Margin="12,6,0,6"/>
*/
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

        public object SelectedIndex { get; private set;}

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
