using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.SignIn;
using Unigram.Views;
using Unigram.Views.SignIn;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Common
{
    public class TLRootNavigationService : NavigationService
    //, IHandle<UpdateAuthorizationState>
    {
        private readonly ILifetimeService _lifetimeService;

        public TLRootNavigationService(ISessionService sessionService, Frame frame, int session, string id)
            : base(frame, session, id)
        {
            _lifetimeService = TLContainer.Current.Lifetime;
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
                            await viewModel.NavigatedToAsync(null, NavigationMode.Refresh, null);
                        }
                        else
                        {
                            Navigate(typeof(SignInPage));
                        }

                        ClearBackStack();
                        AddToBackStack(typeof(BlankPage));
                    }
                    else
                    {
                        if (Frame.Content is SignInPage page && page.DataContext is SignInViewModel viewModel)
                        {
                            await viewModel.NavigatedToAsync(null, NavigationMode.Refresh, null);
                        }
                        else
                        {
                            Navigate(typeof(SignInPage));
                        }
                    }
                    break;
                case AuthorizationStateWaitCode:
                    Navigate(typeof(SignInSentCodePage));
                    break;
                case AuthorizationStateWaitEmailAddress:
                    Navigate(typeof(AuthorizationEmailAddressPage));
                    break;
                case AuthorizationStateWaitEmailCode:
                    Navigate(typeof(AuthorizationEmailCodePage));
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
