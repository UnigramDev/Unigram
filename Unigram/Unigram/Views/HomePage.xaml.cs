using Windows.UI.Xaml.Controls;

namespace Unigram.Views
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
           // UnigramContainer.Instance.ResolverType<LoginPhoneNumberViewModel>();

            this.InitializeComponent();
            frameDetailsDetails.Navigate(typeof(Views.ChatPage));
            // To see a toast on start just uncomment the line below
            //Core.Notifications.Toast.Create(long.MaxValue.ToString(), "Batman", "I'm on my way, commisioner!", "ms-appx:///Assets/Mockups/UserIcons/user_batman.png");
            //Core.Notifications.LiveTile.Update(5, "Batman", "I'm on my way, commisioner!");
        }

        //private void cbtnMasterAbout_Click(object sender, RoutedEventArgs e)
        //{
        //    Frame.Navigate(typeof(Views.About));
        //}
    }
}
