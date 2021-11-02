using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.SignIn;
using Unigram.Views;
using Unigram.Views.SignIn;

namespace Unigram.Common
{
    public class TLRootNavigationService : NavigationService, IHandle<UpdateAuthorizationState>
    {
        private readonly ILifetimeService _lifetimeService;
        private readonly ISessionService _sessionService;

        public TLRootNavigationService(ISessionService sessionService, Frame frame, int session, string id)
            : base(frame, session, id)
        {
            _lifetimeService = TLContainer.Current.Lifetime;
            _sessionService = sessionService;
        }

        public async void Handle(UpdateAuthorizationState update)
        {
            switch (update.AuthorizationState)
            {
                case AuthorizationStateReady:
                    Navigate(typeof(MainPage));
                    break;
                case AuthorizationStateWaitPhoneNumber:
                case AuthorizationStateWaitOtherDeviceConfirmation:
                    if (_lifetimeService.Items.Count > 1)
                    {
                        if (Frame.Content is SignInPage page && page.DataContext is SignInViewModel viewModel)
                        {
                            await viewModel.OnNavigatedToAsync(null, NavigationMode.Refresh, null);
                        }
                        else
                        {
                            Navigate(typeof(SignInPage));
                        }

                        Frame.BackStack.Clear();
                        Frame.BackStack.Add(new PageStackEntry(typeof(BlankPage), null, null));
                    }
                    else
                    {
                        if (Frame.Content is SignInPage page && page.DataContext is SignInViewModel viewModel)
                        {
                            await viewModel.OnNavigatedToAsync(null, NavigationMode.Refresh, null);
                        }
                        else
                        {
                            Navigate(typeof(IntroPage));
                        }
                    }
                    break;
                case AuthorizationStateWaitCode:
                    Navigate(typeof(SignInSentCodePage));
                    break;
                case AuthorizationStateWaitRegistration:
                    Navigate(typeof(SignUpPage));
                    break;
                case AuthorizationStateWaitPassword waitPassword:
                    if (!string.IsNullOrEmpty(waitPassword.RecoveryEmailAddressPattern))
                    {
                        await MessagePopup.ShowAsync(string.Format(Strings.Resources.RestoreEmailSent, waitPassword.RecoveryEmailAddressPattern), Strings.Resources.AppName, Strings.Resources.OK);
                    }

                    Navigate(string.IsNullOrEmpty(waitPassword.RecoveryEmailAddressPattern) ? typeof(SignInPasswordPage) : typeof(SignInRecoveryPage));
                    break;
            }
        }
    }
}
