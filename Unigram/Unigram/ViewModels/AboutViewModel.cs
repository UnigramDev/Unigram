using System;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Common;
using Windows.ApplicationModel;

namespace Unigram.ViewModels
{
    /// <summary>
    /// ViewModel for the <see cref="Unigram.Views.AboutPage"/> View.
    /// </summary>
    public class AboutViewModel : UnigramViewModelBase
    {
        // App version.
        public string Version { get; private set; }

        // Useful links.
        public string UsefulPrivacy = "http://unigram.me/privacy.html";
        public string UsefulFaq = "http://unigram.me/faq.html";
        public string UsefulWebsite = "http://unigram.me";

        // Support.
        public string SupportEmail = "mailto:team@unigram.me";
        public string SupportGitHub = "https://www.github.com/UnigramDev/Unigram/issues";

        // Social media.
        public string SocialTwitter = "https://twitter.com/UnigramApp";
        public string SocialFacebook = "https://www.facebook.com/UnigramApp";

        // Thanks.
        public string ThanksTelegram = "https://twitter.com/telegramdesktop";
        public string ThanksGregory = "https://github.com/grishka";

        public AboutViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) : base(protoService, cacheService, aggregator)
        {
            Version = GetVersion();
        }

        /// <summary>
        /// Returns the app version as a formated string. 
        /// </summary>
        /// <returns>App version as a string.</returns>
        private string GetVersion()
        {
            Package package = Package.Current;
            PackageId packageId = package.Id;
            PackageVersion version = packageId.Version;
            return string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
        }

        #region USEFUL

        public RelayCommand UsefulPrivacyCommand => new RelayCommand(UsefulPrivacyExecute);
        private async void UsefulPrivacyExecute()
        {
            var uri = new Uri(@UsefulPrivacy);
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        public RelayCommand UsefulFaqCommand => new RelayCommand(UsefulFaqExecute);
        private async void UsefulFaqExecute()
        {
            var uri = new Uri(@UsefulFaq);
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        public RelayCommand UsefulWebsiteCommand => new RelayCommand(UsefulWebsiteExecute);
        private async void UsefulWebsiteExecute()
        {
            var uri = new Uri(@UsefulWebsite);
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        #endregion

        #region SUPPORT

        public RelayCommand SupportEmailCommand => new RelayCommand(SupportEmailExecute);
        private async void SupportEmailExecute()
        {
            var uri = new Uri(@SupportEmail);
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        public RelayCommand SupportGitHubCommand => new RelayCommand(SupportGitHubExecute);
        private async void SupportGitHubExecute()
        {
            var uri = new Uri(@SupportGitHub);
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        #endregion

        #region SOCIAL

        public RelayCommand SocialTwitterCommand => new RelayCommand(SocialTwitterExecute);
        private async void SocialTwitterExecute()
        {
            var uri = new Uri(@SocialTwitter);
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        public RelayCommand SocialFacebookCommand => new RelayCommand(SocialFacebookExecute);
        private async void SocialFacebookExecute()
        {
            var uri = new Uri(@SocialFacebook);
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        #endregion

        #region THANKS

        public RelayCommand ThanksTelegramCommand => new RelayCommand(ThanksTelegramExecute);
        private async void ThanksTelegramExecute()
        {
            var uri = new Uri(@ThanksTelegram);
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        public RelayCommand ThanksGregoryCommand => new RelayCommand(ThanksGregoryExecute);
        private async void ThanksGregoryExecute()
        {
            var uri = new Uri(@ThanksGregory);
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        #endregion
    }
}
