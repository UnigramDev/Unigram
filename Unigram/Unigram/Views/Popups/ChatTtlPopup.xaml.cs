using Microsoft.UI.Xaml.Controls;
using Unigram.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class ChatTtlPopup : ContentPopup
    {
        public ChatTtlPopup(bool secret)
        {
            InitializeComponent();

            Title = Strings.Resources.MessageLifetime;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

            if (secret)
            {
                var seconds = new int[21];
                for (int i = 0; i < seconds.Length; i++)
                {
                    seconds[i] = i;
                }

                FieldSeconds.ItemsSource = seconds;
            }
            else
            {
                FieldSeconds.ItemsSource = new int[] { 0, 19, 20 };
            }
        }

        public int Value { get; set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
