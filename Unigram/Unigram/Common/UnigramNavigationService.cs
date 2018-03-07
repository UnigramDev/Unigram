using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Template10.Services.NavigationService;
using Template10.Services.ViewService;
using Unigram.Services;
using Unigram.Views;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Common
{
    public class UnigramNavigationService : NavigationService
    {
        private readonly IProtoService _protoService;

        private ViewLifetimeControl _instantLifetime;

        public UnigramNavigationService(IProtoService protoService, Frame frame)
            : base(frame)
        {
            _protoService = protoService;
        }

        public async void NavigateToInstant(string url)
        {
            if (_instantLifetime == null)
            {
                _instantLifetime = await OpenAsync(typeof(InstantPage), url);
            }
            else
            {
                await _instantLifetime.CoreDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    _instantLifetime.NavigationService.Navigate(typeof(InstantPage), url);
                });
                await ApplicationViewSwitcher.TryShowAsStandaloneAsync(_instantLifetime.Id, ViewSizePreference.Default, ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow), ViewSizePreference.UseHalf);
            }
        }

        public async void NavigateToChat(Chat chat, long? message = null, string accessToken = null)
        {
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                var user = _protoService.GetUser(privata.UserId);
                if (user == null)
                {
                    return;
                }

                //user.RestrictionReason
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = _protoService.GetSupergroup(super.SupergroupId);
                if (supergroup == null)
                {
                    return;
                }
            }

            if (Frame.Content is ChatPage page && chat.Id.Equals((long)CurrentPageParam))
            {
                if (message != null)
                {
                    await page.ViewModel.LoadMessageSliceAsync(null, message.Value);
                }
            }
            else
            {
                //NavigatedEventHandler handler = null;
                //handler = async (s, args) =>
                //{
                //    Frame.Navigated -= handler;

                //    if (args.Content is DialogPage page1 /*&& chat.Id.Equals((long)args.Parameter)*/)
                //    {
                //        if (message.HasValue)
                //        {
                //            await page1.ViewModel.LoadMessageSliceAsync(null, message.Value);
                //        }
                //    }
                //};

                //Frame.Navigated += handler;
                if (message != null)
                {
                    App.Current.SessionState["message_id"] = message.Value;
                }
                else
                {
                    App.Current.SessionState.Remove("message_id");
                }


                Navigate(typeof(ChatPage), chat.Id);
            }
        }

        public async void NavigateToChat(long chatId, long? message = null, string accessToken = null)
        {
            var chat = _protoService.GetChat(chatId);
            if (chat == null)
            {
                var response = await _protoService.SendAsync(new GetChat(chatId));
                if (response is Chat result)
                {
                    chat = result;
                }
            }

            if (chat == null)
            {
                return;
            }

            NavigateToChat(chat, message, accessToken);
        }
    }
}
