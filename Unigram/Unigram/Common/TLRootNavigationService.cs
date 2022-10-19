using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Authorization;
using Unigram.Views;
using Unigram.Views.Authorization;
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
                    if (Frame.Content is AuthorizationPage page && page.DataContext is AuthorizationViewModel viewModel)
                    {
                        await viewModel.NavigatedToAsync(null, NavigationMode.Refresh, null);
                    }
                    else
                    {
                        Navigate(typeof(AuthorizationPage));
                    }

                    if (_lifetimeService.Items.Count > 1)
                    {
                        ClearBackStack();
                        AddToBackStack(typeof(BlankPage));
                    }
                    break;
                case AuthorizationStateWaitCode:
                    Navigate(typeof(AuthorizationCodePage));
                    break;
                case AuthorizationStateWaitEmailAddress:
                    Navigate(typeof(AuthorizationEmailAddressPage));
                    break;
                case AuthorizationStateWaitEmailCode:
                    Navigate(typeof(AuthorizationEmailCodePage));
                    break;
                case AuthorizationStateWaitRegistration:
                    Navigate(typeof(AuthorizationRegistrationPage));
                    break;
                case AuthorizationStateWaitPassword waitPassword:
                    if (!string.IsNullOrEmpty(waitPassword.RecoveryEmailAddressPattern))
                    {
                        await MessagePopup.ShowAsync(string.Format(Strings.Resources.RestoreEmailSent, waitPassword.RecoveryEmailAddressPattern), Strings.Resources.AppName, Strings.Resources.OK);
                    }

                    Navigate(string.IsNullOrEmpty(waitPassword.RecoveryEmailAddressPattern) ? typeof(AuthorizationPasswordPage) : typeof(AuthorizationRecoveryPage));
                    break;
            }
        }
    }
}
