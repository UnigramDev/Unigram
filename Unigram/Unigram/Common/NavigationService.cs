using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Template10.Common;
using Template10.Services.LoggingService;
using Template10.Services.NavigationService;
using Template10.Services.SerializationService;
using Template10.Services.ViewService;
using Unigram.Controls;
using Unigram.Core.Services;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.Views;
using Unigram.Views;
using Unigram.Views.Payments;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Common
{
    public static class UnigramNavigationServiceEx
    {
        private static ContentDialog _currentDialog;

        public static Task<ContentDialogResult> NavigateModalAsync(this INavigationService service, Type dialog)
        {
            return NavigateModalAsync(service, (ContentDialog)Activator.CreateInstance(dialog));
        }

        public static async Task<ContentDialogResult> NavigateModalAsync(this INavigationService service, ContentDialog dialog)
        {
            var viewModel = dialog.DataContext as INavigable;
            if (viewModel != null)
            {
                viewModel.NavigationService = service;
                dialog.Opened += async (s, args) =>
                {
                    await viewModel.OnNavigatedToAsync(null, NavigationMode.New, null);
                };
            }

            _currentDialog = dialog;
            return await dialog.ShowQueuedAsync();
        }

        public static void PopModal(this INavigationService service)
        {
            if (_currentDialog != null)
            {
                _currentDialog.Hide();
                _currentDialog = null;
            }
        }

        public static void Navigate<T>(this INavigationService service, Type page, object parameter = null, NavigationTransitionInfo infoOverride = null)
        {
            //NavigatedEventHandler handler = null;
            //handler = (s, args) =>
            //{
            //    service.Frame.Navigated -= handler;

            //    var navigated = args.Content as Page;
            //    if (navigated != null && args.SourcePageType == page)
            //    {
            //        navigated.DataContext = UnigramContainer.Instance.ResolveType<T>();
            //    }
            //};

            ViewModels.Enqueue(typeof(T));
            service.Navigate(page, parameter, infoOverride);
        }

        //public static void SelectUsers<T>(this INavigationService service, object parameter = null, NavigationTransitionInfo infoOverride = null)
        //{
        //    ViewModels.Enqueue(typeof(T));
        //    service.Navigate(typeof(UsersSelectionPage), parameter, infoOverride);
        //}

        public static Queue<Type> ViewModels { get; } = new Queue<Type>();



        public static void Reset(this INavigationService service)
        {
            var cacheSize = service.Frame.CacheSize;
            service.Frame.CacheSize = 0;
            service.Refresh();
            service.Frame.BackStack.Clear();
            service.Frame.CacheSize = cacheSize;
        }

        public static void GoBackAt(this INavigationService service, int index)
        {
            while (service.Frame.BackStackDepth > index + 1)
            {
                service.Frame.BackStack.RemoveAt(index + 1);
            }

            if (service.Frame.CanGoBack)
            {
                service.Frame.GoBack();
            }
        }

        public static void RemoveSkip(this INavigationService service, int count)
        {
            while (service.Frame.BackStackDepth > count)
            {
                service.Frame.BackStack.RemoveAt(count);
            }
        }

        public static void RemoveLast(this INavigationService service)
        {
            if (service.CanGoBack)
            {
                service.Frame.BackStack.RemoveAt(service.Frame.BackStackDepth - 1);
            }
        }

        public static void RemoveLastIf(this INavigationService service, Type type)
        {
            if (service.CanGoBack && service.Frame.BackStack[service.Frame.BackStackDepth - 1].SourcePageType == type)
            {
                service.Frame.BackStack.RemoveAt(service.Frame.BackStackDepth - 1);
            }
        }

        public static async void NavigateToChat(this INavigationService service, Chat chat, long? message = null, string accessToken = null)
        {
            if (service is UnigramNavigationService serviceEx)
            {
                serviceEx.NavigateToChat(chat, message, accessToken);
            }
        }

        public static void NavigateToChat(this INavigationService service, long chatId, long? message = null, string accessToken = null)
        {
            if (service is UnigramNavigationService serviceEx)
            {
                serviceEx.NavigateToChat(chatId, message, accessToken);
            }

            //if (with == null)
            //{
            //    return;
            //}

            //if (with is TLUser user && user.IsRestricted)
            //{
            //    var reason = user.ExtractRestrictionReason();
            //    if (reason != null)
            //    {
            //        await TLMessageDialog.ShowAsync(reason, "Sorry", "OK");
            //        return;
            //    }
            //}

            //if (with is TLChannel channel)
            //{
            //    if (channel.IsRestricted)
            //    {
            //        var reason = channel.ExtractRestrictionReason();
            //        if (reason != null)
            //        {
            //            await TLMessageDialog.ShowAsync(reason, "Sorry", "OK");
            //            return;
            //        }
            //    }
            //    else if ((channel.IsLeft) && !channel.HasUsername)
            //    {
            //        return;
            //    }
            //}

            //var peer = with.ToPeer();
            //if (service.CurrentPageType == typeof(DialogPage) && peer.Equals(service.CurrentPageParam))
            //{
            //    if (service.Frame.Content is DialogPage page && page.ViewModel != null)
            //    {
            //        if (message.HasValue)
            //        {
            //            await page.ViewModel.LoadMessageSliceAsync(null, message.Value);
            //        }

            //        if (accessToken != null)
            //        {
            //            page.ViewModel.AccessToken = accessToken;
            //        }

            //        if (App.InMemoryState.ForwardMessages != null)
            //        {
            //            //page.ViewModel.Reply = new TLMessagesContainter { FwdMessages = new TLVector<TLMessage>(App.InMemoryState.ForwardMessages) };
            //        }

            //        if (App.InMemoryState.SwitchInline != null)
            //        {
            //            var switchInlineButton = App.InMemoryState.SwitchInline;
            //            var bot = App.InMemoryState.SwitchInlineBot;

            //            page.ViewModel.SetText(string.Format("@{0} {1}", bot.Username, switchInlineButton.Query), focus: true);
            //            page.ViewModel.ResolveInlineBot(bot.Username, switchInlineButton.Query);

            //            App.InMemoryState.SwitchInline = null;
            //            App.InMemoryState.SwitchInlineBot = null;
            //        }
            //        else if (App.InMemoryState.SendMessage != null)
            //        {
            //            var text = App.InMemoryState.SendMessage;
            //            var hasUrl = App.InMemoryState.SendMessageUrl;

            //            page.ViewModel.SetText(text);

            //            if (hasUrl)
            //            {
            //                page.ViewModel.SetSelection(text.IndexOf('\n') + 1);
            //            }

            //            App.InMemoryState.SendMessage = null;
            //            App.InMemoryState.SendMessageUrl = false;
            //        }
            //    }
            //    else
            //    {
            //        service.Refresh(TLSerializationService.Current.Serialize(peer));
            //    }
            //}
            //else
            //{
            //    App.InMemoryState.NavigateToMessage = message;
            //    App.InMemoryState.NavigateToAccessToken = accessToken;
            //    service.Navigate(typeof(DialogPage), peer);
            //}
        }

        public static void NavigateToMain(this INavigationService service, string parameter)
        {
            NavigatedEventHandler handler = null;
            handler = (s, args) =>
            {
                service.Frame.Navigated -= handler;

                if (args.Content is MainPage page)
                {
                    page.Activate(parameter);
                }
            };

            service.Frame.Navigated += handler;
            service.Navigate(typeof(MainPage));
        }

        #region Payments

        public static void NavigateToPaymentFormStep1(this INavigationService service, MessageViewModel message, PaymentForm paymentForm)
        {
            service.Navigate(typeof(PaymentFormStep1Page), Tuple.Create(message, paymentForm));
        }

        public static void NavigateToPaymentFormStep2(this INavigationService service, MessageViewModel message, PaymentForm paymentForm, OrderInfo info, ValidatedOrderInfo validatedInfo)
        {
            service.Navigate(typeof(PaymentFormStep2Page), Tuple.Create(message, paymentForm, info, validatedInfo));
        }

        public static void NavigateToPaymentFormStep3(this INavigationService service, MessageViewModel message, PaymentForm paymentForm, OrderInfo info, ValidatedOrderInfo validatedInfo, ShippingOption shipping)
        {
            service.Navigate(typeof(PaymentFormStep3Page), Tuple.Create(message, paymentForm, info, validatedInfo, shipping));
        }

        public static void NavigateToPaymentFormStep4(this INavigationService service, MessageViewModel message, PaymentForm paymentForm, OrderInfo info, ValidatedOrderInfo validatedInfo, ShippingOption shipping)
        {
            service.Navigate(typeof(PaymentFormStep4Page), Tuple.Create(message, paymentForm, info, validatedInfo, shipping));
        }

        public static void NavigateToPaymentFormStep5(this INavigationService service, MessageViewModel message, PaymentForm paymentForm, OrderInfo info, ValidatedOrderInfo validatedInfo, ShippingOption shipping, string title, string credentials, bool save)
        {
            service.Navigate(typeof(PaymentFormStep5Page), Tuple.Create(message, paymentForm, info, validatedInfo, shipping, title ?? string.Empty, credentials ?? string.Empty, save));
        }

        #endregion

        public static void RemovePeerFromStack(this INavigationService service, long target)
        {
            long peer;
            bool found = false;

            for (int i = 0; i < service.Frame.BackStackDepth; i++)
            {
                var entry = service.Frame.BackStack[i];
                if (TryGetPeerFromParameter(service, entry.Parameter, out peer))
                {
                    found = peer.Equals(target);
                }

                if (found)
                {
                    service.Frame.BackStack.RemoveAt(i);
                    i--;
                }
            }

            if (TryGetPeerFromParameter(service, service.CurrentPageParam, out peer))
            {
                if (peer.Equals(target))
                {
                    service.GoBack();
                    service.Frame.ForwardStack.Clear();
                }
            }
        }

        public static bool IsPeerActive(this INavigationService service, long chat)
        {
            if (service.CurrentPageType == typeof(ChatPage))
            {
                if (TryGetPeerFromParameter(service, service.CurrentPageParam, out long chatId))
                {
                    return chat == chatId;
                }
            }

            return false;
        }

        public static long GetPeerFromBackStack(this INavigationService service)
        {
            if (service.CurrentPageType == typeof(ChatPage))
            {
                if (TryGetPeerFromParameter(service, service.CurrentPageParam, out long chatId))
                {
                    return chatId;
                }
            }

            for (int i = service.Frame.BackStackDepth - 1; i >= 0; i--)
            {
                var entry = service.Frame.BackStack[i];
                if (entry.SourcePageType == typeof(ChatPage))
                {
                    if (TryGetPeerFromParameter(service, entry.Parameter, out long chatId))
                    {
                        return chatId;
                    }
                }
            }

            return 0;
        }

        public static bool TryGetPeerFromParameter(this INavigationService service, object parameter, out long chatId)
        {
            if (parameter is string)
            {
                parameter = TLSerializationService.Current.Deserialize((string)parameter);
            }

            if (parameter is long)
            {
                chatId = (long)parameter;
                return true;
            }

            chatId = 0;
            return false;
        }



        public static Task<T> NavigateWithResult<T>(this INavigationService service, Type type, object parameter = null)
        {
            TaskCompletionSource<T> tsc = new TaskCompletionSource<T>();
            NavigatedEventHandler handler = null;
            handler = (s, args) =>
            {
                service.Frame.Navigated -= handler;

                if (args.Content is Page page && page.DataContext is INavigableWithResult<T> withResult)
                {
                    withResult.SetAwaiter(tsc, parameter);
                }
            };

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
