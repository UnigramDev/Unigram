﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Services.Navigation;
using Unigram.Services.ViewService;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.Settings;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Common
{
    public class TLNavigationService : NavigationService
    {
        private readonly IPasscodeService _passcodeService;

        private Dictionary<string, AppWindow> _instantWindows = new Dictionary<string, AppWindow>();
        public TLNavigationService(IProtoService protoService, Frame frame, int session, string id)
            : base(frame, session, id)
        {
            ProtoService = protoService;
            _passcodeService = TLContainer.Current.Passcode;
        }

        public new int SessionId => ProtoService.SessionId;
        public IProtoService ProtoService { get; }

        public async void NavigateToInstant(string url)
        {
            //if (ApiInformation.IsTypePresent("Windows.UI.WindowManagement.AppWindow"))
            //{
            //    _instantWindows.TryGetValue(url, out AppWindow window);
            //    if (window == null)
            //    {
            //        var nav = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, BootStrapper.ExistingContent.Exclude, 0, "0", false);
            //        var frame = BootStrapper.Current.CreateRootElement(nav);
            //        nav.Navigate(typeof(InstantPage), url);

            //        window = await AppWindow.TryCreateAsync();
            //        window.PersistedStateId = "InstantView";
            //        window.TitleBar.ExtendsContentIntoTitleBar = true;
            //        window.Closed += (s, args) =>
            //        {
            //            _instantWindows.Remove(url);
            //            frame = null;
            //            window = null;
            //        };

            //        _instantWindows[url] = window;
            //        ElementCompositionPreview.SetAppWindowContent(window, frame);
            //    }

            //    await window.TryShowAsync();
            //    window.RequestMoveAdjacentToCurrentView();
            //}
            //else
            {
                Navigate(typeof(InstantPage), url);
            }
        }

        public async void NavigateToChat(Chat chat, long? message = null, string accessToken = null, IDictionary<string, object> state = null, bool scheduled = false)
        {
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                var user = ProtoService.GetUser(privata.UserId);
                if (user == null)
                {
                    return;
                }

                var reason = user.GetRestrictionReason();
                if (reason != null && reason.Length > 0)
                {
                    await TLMessageDialog.ShowAsync(reason, Strings.Resources.AppName, Strings.Resources.OK);
                    return;
                }
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup == null)
                {
                    return;
                }

                if (supergroup.Status is ChatMemberStatusLeft && string.IsNullOrEmpty(supergroup.Username) && !supergroup.HasLocation && !supergroup.HasLinkedChat)
                {
                    await TLMessageDialog.ShowAsync(Strings.Resources.ChannelCantOpenPrivate, Strings.Resources.AppName, Strings.Resources.OK);
                    return;
                }

                var reason = supergroup.GetRestrictionReason();
                if (reason != null && reason.Length > 0)
                {
                    await TLMessageDialog.ShowAsync(reason, Strings.Resources.AppName, Strings.Resources.OK);
                    return;
                }
            }

            if (Frame.Content is ChatPage page && chat.Id.Equals((long)CurrentPageParam) && !scheduled)
            {
                if (message != null)
                {
                    await page.ViewModel.LoadMessageSliceAsync(null, message.Value);
                }
                else
                {
                    await page.ViewModel.LoadMessageSliceAsync(null, chat.LastMessage?.Id ?? long.MaxValue, VerticalAlignment.Bottom, 8);
                }

                page.ViewModel.TextField?.Focus(FocusState.Programmatic);

                if (App.DataPackages.TryRemove(chat.Id, out DataPackageView package))
                {
                    await page.ViewModel.HandlePackageAsync(package);
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

                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
                if (shift && !ctrl)
                {
                    await OpenAsync(scheduled ? typeof(ChatScheduledPage) : typeof(ChatPage), chat.Id);
                }
                else
                {
                    await NavigateAsync(scheduled ? typeof(ChatScheduledPage) : typeof(ChatPage), chat.Id, state);
                }
            }
        }

        public async void NavigateToChat(long chatId, long? message = null, string accessToken = null, IDictionary<string, object> state = null, bool scheduled = false)
        {
            var chat = ProtoService.GetChat(chatId);
            if (chat == null)
            {
                var response = await ProtoService.SendAsync(new GetChat(chatId));
                if (response is Chat result)
                {
                    chat = result;
                }
            }

            if (chat == null)
            {
                return;
            }

            NavigateToChat(chat, message, accessToken, state, scheduled);
        }

        public async void NavigateToPasscode()
        {
            if (_passcodeService.IsEnabled)
            {
                var dialog = new SettingsPasscodeConfirmView(passcode => Task.FromResult(!_passcodeService.Check(passcode)), _passcodeService.IsSimple);
                dialog.IsSimple = _passcodeService.IsSimple;

                var confirm = await dialog.ShowAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    Navigate(typeof(SettingsPasscodePage));
                }
            }
            else
            {
                Navigate(typeof(SettingsPasscodePage));
            }
        }
    }
}
