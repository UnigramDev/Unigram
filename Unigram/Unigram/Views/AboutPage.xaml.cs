namespace Unigram.Views
{
    using System;
    using Windows.ApplicationModel;
    using Windows.UI.Xaml.Media.Animation;
    using Windows.UI.Xaml.Navigation;

    public sealed partial class AboutPage
    {
        public AboutPage()
        {
            this.InitializeComponent();
        }

        // Stuff to make the back-button interaction possible
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var backStack = Frame.BackStack;
            var backStackCount = backStack.Count;

            if (backStackCount > 0)
            {
                var masterPageEntry = backStack[backStackCount - 1];
                backStack.RemoveAt(backStackCount - 1);

                // #TODO Restore the previous opened note when going back
                try
                {
                    var modifiedEntry = new PageStackEntry(masterPageEntry.SourcePageType, null, masterPageEntry.NavigationTransitionInfo);
                    backStack.Add(modifiedEntry);
                }
                catch // If stuff goes to the shitter, go back to Home
                {

                }
            }

            GetVersion();
        }

        public void GetVersion()
        {
            Package package = Package.Current;
            PackageId packageId = package.Id;
            PackageVersion version = packageId.Version;
            tblAppVersion.Text = string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
        }

        // Twitter
        private async void btnCoreRickTwitter_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriTwitter = new Uri(@"https://twitter.com/ikaragodev");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriTwitter);
        }

        private async void btnCoreSauravTwitter_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriTwitter = new Uri(@"https://twitter.com/gx_saurav");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriTwitter);
        }

        private async void btnCoreFelaTwitter_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriTwitter = new Uri(@"https://twitter.com/FrayxRulez");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriTwitter);
        }

        private async void btnCoreMateiTwitter_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriTwitter = new Uri(@"https://twitter.com/matei_dev");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriTwitter);
        }

        private async void btnCoreKesavaTwitter_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriTwitter = new Uri(@"https://twitter.com/kesavarul");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriTwitter);
        }

        private async void btnCoreAbdelTwitter_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriTwitter = new Uri(@"https://twitter.com/ADeltaXForce");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriTwitter);
        }

        private async void btnCoreLorenzoTwitter_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriTwitter = new Uri(@"https://twitter.com/LorenzRox");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriTwitter);
        }

        private async void btnThanksTelegramDesktopTwitter_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriTwitter = new Uri(@"https://twitter.com/telegramdesktop");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriTwitter);
        }

        // Websites
        private async void btnCoreRickWebsite_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var url = new Uri(@"http://www.ikarago.com");
            var success = await Windows.System.Launcher.LaunchUriAsync(url);
        }

        private async void btnCoreSauravWebsite_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var url = new Uri(@"http://about.me/gxsaurav");
            var success = await Windows.System.Launcher.LaunchUriAsync(url);
        }

        private void btnCoreFelaWebsite_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // No website... yet? :)
        }

        private void btnCoreMateiWebsite_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // No website... yet? :)
        }

        private async void btnCoreKesavaWebsite_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var url = new Uri(@"http://fb.com/kesavaprasadarul");
            var success = await Windows.System.Launcher.LaunchUriAsync(url);
        }

        private async void btnCoreAbdelWebsite_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var url = new Uri(@"https://adeltax.com/");
            var success = await Windows.System.Launcher.LaunchUriAsync(url);
        }

        private async void btnUnigramTwitter_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uriTwitter = new Uri(@"https://twitter.com/UnigramApp");
            var success = await Windows.System.Launcher.LaunchUriAsync(uriTwitter);            
        }
    }
}
