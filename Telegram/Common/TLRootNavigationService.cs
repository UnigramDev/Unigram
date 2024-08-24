//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Authorization;
using Telegram.Views;
using Telegram.Views.Authorization;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Telegram.Common
{
    public class TLRootNavigationService : NavigationService
    //, IHandle<UpdateAuthorizationState>
    {
        private readonly ILifetimeService _lifetimeService;

        public TLRootNavigationService(ISessionService sessionService, Frame frame, int session, string id)
            : base(frame, session, id)
        {
            _lifetimeService = TypeResolver.Current.Lifetime;
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
                    Navigate(typeof(AuthorizationCodePage), navigationStackEnabled: false);
                    break;
                case AuthorizationStateWaitEmailAddress:
                    Navigate(typeof(AuthorizationEmailAddressPage), navigationStackEnabled: false);
                    break;
                case AuthorizationStateWaitEmailCode:
                    Navigate(typeof(AuthorizationEmailCodePage), navigationStackEnabled: false);
                    break;
                case AuthorizationStateWaitRegistration:
                    Navigate(typeof(AuthorizationRegistrationPage), navigationStackEnabled: false);
                    break;
                case AuthorizationStateWaitPassword waitPassword:
                    if (!string.IsNullOrEmpty(waitPassword.RecoveryEmailAddressPattern))
                    {
                        await ShowPopupAsync(string.Format(Strings.RestoreEmailSent, waitPassword.RecoveryEmailAddressPattern), Strings.AppName, Strings.OK);
                    }

                    Navigate(string.IsNullOrEmpty(waitPassword.RecoveryEmailAddressPattern)
                        ? typeof(AuthorizationPasswordPage)
                        : typeof(AuthorizationRecoveryPage), navigationStackEnabled: false);
                    break;
            }
        }
    }
}
