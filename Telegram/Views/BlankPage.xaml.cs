//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
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
        private IEventAggregator _aggregator;

        public BlankPage()
        {
            InitializeComponent();
            DataContext = new object();

            NavigationCacheMode = NavigationCacheMode.Required;
        }

        public void Activate(int sessionId)
        {
            _clientService ??= TypeResolver.Current.Resolve<IClientService>(sessionId);
            _aggregator ??= TypeResolver.Current.Resolve<IEventAggregator>(sessionId);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back && Frame.ForwardStack.Count > 0 && Frame.ForwardStack[0].SourcePageType == typeof(AuthorizationPage))
            {
                _clientService.Send(new Destroy());
            }
            else if (Theme.Current.Update(ActualTheme, null, null))
            {
                var forDarkTheme = Frame.ActualTheme == ElementTheme.Dark;
                var background = _clientService.GetSelectedBackground(forDarkTheme);
                _aggregator.Publish(new UpdateSelectedBackground(forDarkTheme, background));
            }
        }
    }
}
