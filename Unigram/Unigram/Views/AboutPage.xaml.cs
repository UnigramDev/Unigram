using System;
using Windows.ApplicationModel;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class AboutPage
    {
        public AboutPage()
        {
            InitializeComponent();

            Package package = Package.Current;
            PackageId packageId = package.Id;
            PackageVersion version = packageId.Version;
            tblAppVersion.Text = string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
        }


        // Useful links
        private async void btnPrivacy_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriPrivacy = new Uri(@"http://unigram.me/privacy.html");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriPrivacy);
        }

        private async void btnFAQ_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriFAQ = new Uri(@"http://unigram.me/faq.html");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriFAQ);
        }

        private async void btnWebsite_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriWebsite = new Uri(@"http://unigram.me");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriWebsite);
        }

        // Support
        private async void btnSupportEmail_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriEmail = new Uri(@"mailto:team@unigram.me");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriEmail);
        }

        private async void btnSupportGitHub_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriGitHub = new Uri(@"https://www.github.com/UnigramDev/Unigram/issues");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriGitHub);
        }

        // Social media
        private async void btnSocialTwitter_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriTwitter = new Uri(@"https://twitter.com/UnigramApp");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriTwitter);
        }

        private async void btnSocialFacebook_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriFacebook = new Uri(@"https://www.facebook.com/UnigramApp");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriFacebook);
        }

        // Special thanks
        private async void btnThanksTelegramDesktop_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriTwitter = new Uri(@"https://twitter.com/telegramdesktop");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriTwitter);
        }

        private async void btnThanksGregory_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriGitHub = new Uri(@"https://github.com/grishka");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriGitHub);
        }
    }
}
