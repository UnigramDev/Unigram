using System;
using System.Collections.Generic;
using Telegram.Td.Api;
using Template10.Services.NavigationService;
using Template10.Services.ViewService;
using Unigram.Services;
using Unigram.Views;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Unigram.Common
{
    public class TLNavigationService : NavigationService
    {
        private readonly IProtoService _protoService;

        private ViewLifetimeControl _instantLifetime;

        public TLNavigationService(IProtoService protoService, Frame frame, int session, string id)
            : base(frame, session, id)
        {
            _protoService = protoService;
        }

        public int SessionId => _protoService.SessionId;

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

        public async void NavigateToChat(Chat chat, long? message = null, string accessToken = null, IDictionary<string, object> state = null)
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
                else
                {
                    await page.ViewModel.LoadMessageSliceAsync(null, chat.LastMessage?.Id ?? long.MaxValue, SnapPointsAlignment.Far, 8);
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

                if (message != null || accessToken != null)
                {
                    state = state ?? new Dictionary<string, object>();

                    if (message != null)
                    {
                        state["message_id"] = message.Value;
                    }

                    if (accessToken != null)
                    {
                        state["access_token"] = accessToken;
                    }
                }

                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
                if (shift)
                {
                    await OpenAsync(typeof(ChatPage), chat.Id);
                }
                else
                {
                    await NavigateAsync(typeof(ChatPage), chat.Id, state);
                }
            }
        }

        public async void NavigateToChat(long chatId, long? message = null, string accessToken = null, IDictionary<string, object> state = null)
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
