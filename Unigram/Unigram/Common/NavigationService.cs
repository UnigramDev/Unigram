using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Template10.Common;
using Template10.Services.LoggingService;
using Template10.Services.NavigationService;
using Template10.Services.SerializationService;
using Template10.Services.ViewService;
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
    public static class UnigramNavigationService 
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
            return await dialog.ShowAsync();
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

        public static void SelectUsers<T>(this INavigationService service, object parameter = null, NavigationTransitionInfo infoOverride = null)
        {
            ViewModels.Enqueue(typeof(T));
            service.Navigate(typeof(UsersSelectionPage), parameter, infoOverride);
        }

        public static Queue<Type> ViewModels { get; } = new Queue<Type>();





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







        public static void NavigateToPaymentFormStep1(this INavigationService service, TLMessage message, TLPaymentsPaymentForm paymentForm)
        {
            service.Navigate(typeof(PaymentFormStep1Page), TLTuple.Create(message, paymentForm));
        }

        public static void NavigateToPaymentFormStep2(this INavigationService service, TLMessage message, TLPaymentsPaymentForm paymentForm, TLPaymentRequestedInfo info, TLPaymentsValidatedRequestedInfo validatedInfo)
        {
            service.Navigate(typeof(PaymentFormStep2Page), TLTuple.Create(message, paymentForm, info, validatedInfo));
        }

        public static void NavigateToPaymentFormStep3(this INavigationService service, TLMessage message, TLPaymentsPaymentForm paymentForm, TLPaymentRequestedInfo info, TLPaymentsValidatedRequestedInfo validatedInfo, TLShippingOption shipping)
        {
            service.Navigate(typeof(PaymentFormStep3Page), TLTuple.Create(message, paymentForm, info, validatedInfo, shipping));
        }

        public static void NavigateToPaymentFormStep4(this INavigationService service, TLMessage message, TLPaymentsPaymentForm paymentForm, TLPaymentRequestedInfo info, TLPaymentsValidatedRequestedInfo validatedInfo, TLShippingOption shipping)
        {
            service.Navigate(typeof(PaymentFormStep4Page), TLTuple.Create(message, paymentForm, info, validatedInfo, shipping));
        }

        public static void NavigateToPaymentFormStep5(this INavigationService service, TLMessage message, TLPaymentsPaymentForm paymentForm, TLPaymentRequestedInfo info, TLPaymentsValidatedRequestedInfo validatedInfo, TLShippingOption shipping, string title, string credentials)
        {
            service.Navigate(typeof(PaymentFormStep5Page), TLTuple.Create(message, paymentForm, info, validatedInfo, shipping, title ?? string.Empty, credentials ?? string.Empty));
        }
    }
}
