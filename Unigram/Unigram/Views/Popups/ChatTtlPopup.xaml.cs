using Unigram.Controls;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class ChatTtlPopup : ContentPopup
    {
        public ChatTtlPopup()
        {
            this.InitializeComponent();

            Title = Strings.Resources.MessageLifetime;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

            var seconds = new int[21];
            for (int i = 0; i < seconds.Length; i++)
            {
                seconds[i] = i;
            }

            FieldSeconds.ItemsSource = seconds;
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
