//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Common
{
    public static class UnigramNavigationServiceEx
    {
        public static void RemoveSkip(this INavigationService service, int count)
        {
            while (service.Frame.BackStackDepth > count)
            {
                service.RemoveFromBackStack(count);
            }
        }

        public static void RemoveLast(this INavigationService service)
        {
            if (service.CanGoBack)
            {
                service.RemoveFromBackStack(service.Frame.BackStackDepth - 1);
            }
        }

        public static void RemoveLastIf(this INavigationService service, Type type)
        {
            if (service.CanGoBack && service.Frame.BackStack[service.Frame.BackStackDepth - 1].SourcePageType == type)
            {
                service.RemoveFromBackStack(service.Frame.BackStackDepth - 1);
            }
        }

        public static void NavigateToWebApp(this INavigationService service, User botUser, WebAppUrl url, long launchId = 0, AttachmentMenuBot menuBot = null, WebAppOpenMode openMode = null, OpenUrlSource source = null, InternalLinkType sourceLink = null, string buttonText = null)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToWebApp(botUser, url, launchId, menuBot, openMode, source, sourceLink, buttonText);
            }
        }

        public static void NavigateToWebApp(this INavigationService service, User botUser, string url, string title, long gameChatId = 0, long gameMessageId = 0)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToWebApp(botUser, url, title, gameChatId, gameMessageId);
            }
        }

        public static void NavigateToInstant(this INavigationService service, string url, string fallbackUrl = null)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToInstant(url, fallbackUrl);
            }
        }

        public static void NavigateToInstant(this INavigationService service, WebPageInstantView instantView, string url)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToInstant(instantView, url);
            }
        }

        public static void NavigateToInvoice(this INavigationService service, MessageViewModel message)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToInvoice(message);
            }
        }

        public static void NavigateToReceipt(this INavigationService service, MessageViewModel message)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToReceipt(message);
            }
        }

        public static void NavigateToInvoice(this INavigationService service, InputInvoice inputInvoice, MessageContent content = null)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToInvoice(inputInvoice, content);
            }
        }

        public static void NavigateToSender(this INavigationService service, MessageSender sender, NavigationState state = null, NavigationTransitionInfo infoOverride = null)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToSender(sender, state, infoOverride);
            }
        }

        public static void NavigateToUser(this INavigationService service, long userId, bool toChat = false, NavigationState state = null)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToUser(userId, toChat, state);
            }
        }

        public static void NavigateToForum(this INavigationService service, long chatId)
        {
            if (service is TLNavigationService serviceEx && serviceEx.ClientService.TryGetChat(chatId, out Chat chat))
            {
                NavigateToForum(service, chat);
            }
        }

        public static void NavigateToForum(this INavigationService service, Chat chat)
        {
            if (service.Content is UIElement element)
            {
                var mainPage = element.GetParent<MainPage>();
                mainPage?.ShowTopicList(chat);
            }
        }

        public static void NavigateToChat(this INavigationService service, Chat chat, long? message = null, MessageTopic topic = null, string accessToken = null, NavigationState state = null, bool scheduled = false, bool force = true, bool createNewWindow = false, bool clearBackStack = false)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToChat(chat, message, topic, accessToken, state, scheduled, force, createNewWindow, clearBackStack);
            }
        }

        public static void NavigateToChat(this INavigationService service, long chatId, long? message = null, MessageTopic topic = null, string accessToken = null, NavigationState state = null, bool scheduled = false, bool force = true, bool createNewWindow = false)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToChat(chatId, message, topic, accessToken, state, scheduled, force, createNewWindow);
            }
        }

        public static void NavigateToMain(this INavigationService service, string parameter)
        {
            void handler(object s, NavigationEventArgs args)
            {
                service.Frame.Navigated -= handler;

                if (args.Content is MainPage page)
                {
                    page.Activate(parameter);
                }
            }

            service.Frame.Navigated += handler;
            service.Navigate(typeof(MainPage));
        }

        public static void NavigateToPasscode(this INavigationService service)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.NavigateToPasscode();
            }
        }

        public static Task<PasswordState> NavigateToPasswordSetupAsync(this INavigationService service)
        {
            if (service is TLNavigationService serviceEx)
            {
                return serviceEx.NavigateToPasswordSetupAsync();
            }

            return Task.FromResult<PasswordState>(null);
        }

        public static void NavigateToPasswordSetup(this INavigationService service)
        {
            if (service is TLNavigationService serviceEx)
            {
                _ = serviceEx.NavigateToPasswordSetupAsync();
            }
        }

        public static void NavigateToPassword(this INavigationService service)
        {
            if (service is TLNavigationService serviceEx)
            {
                _ = serviceEx.NavigateToPasswordAsync();
            }
        }

        public static Task<PasswordState> NavigateToPasswordAsync(this INavigationService service)
        {
            if (service is TLNavigationService serviceEx)
            {
                return serviceEx.NavigateToPasswordAsync();
            }

            return Task.FromResult<PasswordState>(null);
        }

        public static void ShowLimitReached(this INavigationService service, PremiumLimitType limit)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.ShowLimitReached(limit);
            }
        }

        public static void ShowPromo(this INavigationService service, PremiumSource source = null)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.ShowPromo(source);
            }
        }

        public static Task ShowPromoAsync(this INavigationService service, PremiumSource source = null, ElementTheme requestedTheme = ElementTheme.Default)
        {
            if (service is TLNavigationService serviceEx)
            {
                return serviceEx.ShowPromoAsync(source, requestedTheme);
            }

            return Task.CompletedTask;
        }

        public static void ShowPromo(this INavigationService service, PremiumFeature feature, PremiumSource source = null)
        {
            if (service is TLNavigationService serviceEx)
            {
                serviceEx.ShowPromo(feature, source);
            }
        }

        public static void RemoveChatFromStack(this INavigationService service, long target)
        {
            ChatMessageTopic peer;
            bool found = false;

            for (int i = 0; i < service.Frame.BackStackDepth; i++)
            {
                var entry = service.Frame.BackStack[i];
                if (TryGetChatFromParameter(service, entry.Parameter, out peer))
                {
                    found = peer.ChatId == target;
                }

                if (found)
                {
                    service.RemoveFromBackStack(i);
                    i--;
                }
            }

            if (TryGetChatFromParameter(service, service.CurrentPageParam, out peer))
            {
                if (peer.ChatId == target)
                {
                    service.GoBack();
                    service.Frame.ForwardStack.Clear();
                }
            }
        }

        public static bool IsChatOpen(this INavigationService service, long chatId, bool currentPageOnly = false)
        {
            return chatId == GetChatFromBackStack(service, currentPageOnly).ChatId;
        }

        public static bool IsChatOpen(this INavigationService service, long chatId, MessageTopic topic, bool currentPageOnly = false)
        {
            var currentChat = GetChatFromBackStack(service, currentPageOnly);
            return currentChat.ChatId == chatId && currentChat.MessageTopic.AreTheSame(topic);
        }

        public static ChatMessageTopic GetChatFromBackStack(this INavigationService service, bool currentPageOnly = false, params Type[] currentPageType)
        {
            if (service.CurrentPageType == typeof(ChatPage) || Array.IndexOf(currentPageType, service.CurrentPageType) != -1)
            {
                if (TryGetChatFromParameter(service, service.CurrentPageParam, out ChatMessageTopic chatId))
                {
                    return chatId;
                }
            }

            if (currentPageOnly)
            {
                return new ChatMessageTopic(0, null);
            }

            //else if (service.CurrentPageType == typeof(ChatSavedPage))
            //{
            //    if (service.CurrentPageParam is SavedMessagesTopicSavedFromChat savedFromChat)
            //    {
            //        return savedFromChat.ChatId;
            //    }
            //}

            for (int i = service.Frame.BackStackDepth - 1; i >= 0; i--)
            {
                var entry = service.Frame.BackStack[i];
                if (entry.SourcePageType == typeof(ChatPage))
                {
                    if (TryGetChatFromParameter(service, entry.Parameter, out ChatMessageTopic chatId))
                    {
                        return chatId;
                    }
                }
                //else if (entry.SourcePageType == typeof(ChatSavedPage))
                //{
                //    if (entry.Parameter is SavedMessagesTopicSavedFromChat savedFromChat)
                //    {
                //        return savedFromChat.ChatId;
                //    }
                //}
            }

            return new ChatMessageTopic(0, null);
        }

        public static bool TryGetChatFromParameter(this INavigationService service, object parameter, out ChatMessageTopic chatId)
        {
            if (parameter is string cacheKey && service.CacheKeyToParameter.TryGetValue(cacheKey, out object value))
            {
                parameter = value;
            }

            if (parameter is long)
            {
                chatId = new ChatMessageTopic((long)parameter, null);
                return true;
            }
            else if (parameter is ChatMessageTopic args)
            {
                chatId = args;
                return true;
            }

            chatId = default;
            return false;
        }

        public static void ReplaceChatInBackStack(this INavigationService service, long oldChatId, long newChatId)
        {
            for (int i = service.Frame.BackStackDepth - 1; i >= 0; i--)
            {
                var item = service.Frame.BackStack[i];

                if (service.TryGetChatFromParameter(item.Parameter, out ChatMessageTopic chatId))
                {
                    if (chatId.ChatId == oldChatId)
                    {
                        if (item.Parameter is string cacheKey && service.CacheKeyToParameter.ContainsKey(cacheKey))
                        {
                            service.CacheKeyToParameter[cacheKey] = newChatId;
                        }
                        else
                        {
                            service.Frame.BackStack[i] = new PageStackEntry(item.SourcePageType, newChatId, item.NavigationTransitionInfo);
                        }
                    }
                }
            }
        }

        public static void ReplaceChatInBackStack(this INavigationService service, long oldChatId, ChatMessageTopic newChatId)
        {
            for (int i = service.Frame.BackStackDepth - 1; i >= 0; i--)
            {
                var item = service.Frame.BackStack[i];

                if (service.TryGetChatFromParameter(item.Parameter, out ChatMessageTopic chatId))
                {
                    if (chatId.ChatId == oldChatId)
                    {
                        if (item.Parameter is string cacheKey && service.CacheKeyToParameter.ContainsKey(cacheKey))
                        {
                            service.CacheKeyToParameter[cacheKey] = newChatId;
                        }
                        else
                        {
                            service.Frame.BackStack[i] = new PageStackEntry(item.SourcePageType, newChatId, item.NavigationTransitionInfo);
                        }
                    }
                }
            }
        }

        public static Task<T> NavigateWithResult<T>(this INavigationService service, Type type, object parameter = null)
        {
            var tsc = new TaskCompletionSource<T>();
            void handler(object s, NavigationEventArgs args)
            {
                service.Frame.Navigated -= handler;

                if (args.Content is Page page)
                {
                    if (page.DataContext is INavigable navigable)
                    {
                        navigable.Dispatcher = service.Dispatcher;
                    }

                    if (page.DataContext is INavigableWithResult<T> withResult)
                    {
                        withResult.SetAwaiter(tsc, parameter);
                    }
                }
            }

            service.Frame.Navigated += handler;
            service.Navigate(type);
            return tsc.Task;
        }
    }

    public interface INavigableWithResult<T>
    {
        void SetAwaiter(TaskCompletionSource<T> tsc, object parameter);
    }
}
