//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Authorization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views
{
    public sealed partial class BlankPage : Page
    {
        private IClientService _clientService;
        private INavigationService _navigationService;
        private IEventAggregator _aggregator;

        public BlankPage()
        {
            InitializeComponent();
            DataContext = new object();

            NavigationCacheMode = NavigationCacheMode.Required;
        }

        public void Activate(INavigationService navigationService)
        {
            _navigationService = navigationService;
            _clientService ??= navigationService.Session.Resolve<IClientService>();
            _aggregator ??= navigationService.Session.Resolve<IEventAggregator>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back && Frame.ForwardStack.Count > 0 && Frame.ForwardStack[^1].SourcePageType == typeof(AuthorizationPage))
            {
                _clientService.Send(new Destroy());
            }
            else if (_navigationService.Window.UpdateChatTheme(ActualTheme, null, null, null, null))
            {
                if (_clientService == null)
                {
                    return;
                }

                var forDarkTheme = Frame.ActualTheme == ElementTheme.Dark;
                var background = _clientService.GetDefaultBackground(forDarkTheme);
                _aggregator.Publish(new UpdateDefaultBackground(forDarkTheme, background));
            }
        }
    }
}
