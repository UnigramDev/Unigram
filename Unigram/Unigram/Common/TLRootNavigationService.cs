using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Services.NavigationService;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.SignIn;
using Windows.UI.Xaml.Controls;

namespace Unigram.Common
{
    public class TLRootNavigationService : NavigationService, IHandle<UpdateAuthorizationState>
    {
        private readonly ISessionService _sessionService;

        public TLRootNavigationService(ISessionService sessionService, Frame frame, int session, string id)
            : base(frame, session, id)
        {
            _sessionService = sessionService;
        }

        public async void Handle(UpdateAuthorizationState update)
        {
            switch (update.AuthorizationState)
            {
                case AuthorizationStateReady ready:
                    //App.Current.NavigationService.Navigate(typeof(Views.MainPage));
                    Navigate(typeof(MainPage));
                    break;
                case AuthorizationStateWaitPhoneNumber waitPhoneNumber:
                    Execute.Initialize();
                    Navigate(CurrentPageType != null ? typeof(SignInPage) : typeof(IntroPage));
                    break;
                case AuthorizationStateWaitCode waitCode:
                    Navigate(waitCode.IsRegistered ? typeof(SignInSentCodePage) : typeof(SignUpPage));
                    break;
                case AuthorizationStateWaitPassword waitPassword:
                    if (!string.IsNullOrEmpty(waitPassword.RecoveryEmailAddressPattern))
                    {
                        await TLMessageDialog.ShowAsync(string.Format(Strings.Resources.RestoreEmailSent, waitPassword.RecoveryEmailAddressPattern), Strings.Resources.AppName, Strings.Resources.OK);
                    }

                    Navigate(typeof(SignInPasswordPage));
                    break;
            }
        }
    }
}
