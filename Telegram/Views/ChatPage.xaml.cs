//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Windows.UI.Composition;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views
{
    public sealed partial class ChatPage : HostedPage, IChatPage
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public ChatPage()
        {
            InitializeComponent();
            NavigationCacheMode = ApiInfo.NavigationCacheMode;
        }

        public override string GetTitle()
        {
            return View.ChatTitle;
        }

        public override HostedPagePositionBase GetPosition()
        {
            return null;
        }

        public void OnBackRequested(BackRequestedRoutedEventArgs args)
        {
            View.OnBackRequested(args);
        }

        public void Search()
        {
            View.Search();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (PowerSavingPolicy.AreSmoothTransitionsEnabled && SettingsService.Current.Diagnostics.ConnectedAnimationsDebug)
            {
                View.AnimateEntrance();
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (PowerSavingPolicy.AreSmoothTransitionsEnabled && SettingsService.Current.Diagnostics.ConnectedAnimationsDebug && e.SourcePageType == typeof(ProfilePage) && ViewModel.NavigationService.TryGetChatFromParameter(e.Parameter, out ChatMessageTopic nextTopic))
            {
                if (ViewModel.TopicId.IsDirectMessagesChat(nextTopic.ChatId))
                {
                    View.PrepareExit();
                }
                else if (ViewModel.ChatId == nextTopic.ChatId && ViewModel.ChatId != ViewModel.ClientService.Options.MyId && ViewModel.TopicId.AreTheSame(nextTopic.MessageTopic))
                {
                    View.PrepareExit();
                }
            }
        }

        public void Deactivate(bool navigation)
        {
            View.Deactivate(navigation);

            if (navigation)
            {
                return;
            }

            DataContext = new object();
        }

        public void Activate(INavigationService navigationService)
        {
            var viewModel = navigationService.Session.Resolve<DialogViewModel, IDialogDelegate>(View);
            viewModel.NavigationService = navigationService;
            DataContext = viewModel;
            View.Activate(viewModel);
        }

        public void PopupOpened()
        {
            View.PopupOpened();
        }

        public void PopupClosed()
        {
            View.PopupClosed();
        }

        public void StartBannerAnimation(ScalarKeyFrameAnimation translate)
        {
            View.StartBannerAnimation(translate);
        }

        public void CompleteBannerAnimation()
        {
            View.CompleteBannerAnimation();
        }
    }
}
