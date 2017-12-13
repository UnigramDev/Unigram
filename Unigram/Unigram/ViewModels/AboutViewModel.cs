using System;
using System.Windows.Input;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Common;
using Windows.ApplicationModel;
using Windows.System;

namespace Unigram.ViewModels
{
    /// <summary>
    /// ViewModel for the <see cref="Unigram.Views.AboutPage"/> View.
    /// </summary>
    public class AboutViewModel : UnigramViewModelBase
    {
        public AboutViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            UsefulPrivacyCommand = new RelayCommand(UsefulPrivacyExecute);
            UsefulFaqCommand = new RelayCommand(UsefulFaqExecute);
            UsefulWebsiteCommand = new RelayCommand(UsefulWebsiteExecute);
            UsefulChangelogCommand = new RelayCommand(UsefulChangelogExecute);
            SupportEmailCommand = new RelayCommand(SupportEmailExecute);
            SupportGitHubCommand = new RelayCommand(SupportGitHubExecute);
            SocialTwitterCommand = new RelayCommand(SocialTwitterExecute);
            SocialFacebookCommand = new RelayCommand(SocialFacebookExecute);
            ThanksTelegramCommand = new RelayCommand(ThanksTelegramExecute);
            ThanksGregoryCommand = new RelayCommand(ThanksGregoryExecute);
        }

        /// <summary>
        /// Returns the app version as a formated string. 
        /// </summary>
        /// <returns>App version as a string.</returns>
        private static string GetVersion()
        {
            Package package = Package.Current;
            PackageId packageId = package.Id;
            PackageVersion version = packageId.Version;
            return string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build, version.Revision);
        }

        public string Version { get; } = GetVersion();

        public string AppName { get; } = Strings.Branding.ApplicationName;

        #region Useful

        public RelayCommand UsefulPrivacyCommand { get; }
        private async void UsefulPrivacyExecute()
        {
            await Launcher.LaunchUriAsync(new Uri("http://unigram.me/privacy.html"));
        }

        public RelayCommand UsefulFaqCommand { get; }
        private async void UsefulFaqExecute()
        {
            await Launcher.LaunchUriAsync(new Uri("http://unigram.me/faq.html"));
        }

        public RelayCommand UsefulWebsiteCommand { get; }
        private async void UsefulWebsiteExecute()
        {
            await Launcher.LaunchUriAsync(new Uri("http://unigram.me"));
        }

        public RelayCommand UsefulChangelogCommand { get; }
        private async void UsefulChangelogExecute()
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/UnigramDev/Unigram/releases"));
        }

        #endregion

        #region Support

        public RelayCommand SupportEmailCommand { get; }
        private async void SupportEmailExecute()
        {
            await Launcher.LaunchUriAsync(new Uri("mailto:team@unigram.me"));
        }

        public RelayCommand SupportGitHubCommand { get; }
        private async void SupportGitHubExecute()
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.github.com/UnigramDev/Unigram/issues"));
        }

        #endregion

        #region Social

        public RelayCommand SocialTwitterCommand { get; }
        private async void SocialTwitterExecute()
        {
            await Launcher.LaunchUriAsync(new Uri("https://twitter.com/UnigramApp"));
        }

        public RelayCommand SocialFacebookCommand { get; }
        private async void SocialFacebookExecute()
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.facebook.com/UnigramApp"));
        }

        #endregion

        #region Thanks

        public RelayCommand ThanksTelegramCommand { get; }
        private async void ThanksTelegramExecute()
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/grishka"));
        }

        public RelayCommand ThanksGregoryCommand { get; }
        private async void ThanksGregoryExecute()
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/grishka"));
        }

        #endregion
    }
}
