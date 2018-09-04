using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Core.Services;
using Unigram.Views.SignIn;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class BlankPage : Page
    {
        public BlankPage()
        {
            InitializeComponent();
            DataContext = new object();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back && Frame.ForwardStack.Any(x => x.SourcePageType == typeof(SignInPage)))
            {
                var lifecycle = TLContainer.Current.Lifecycle;
                var session = lifecycle.Remove(lifecycle.ActiveItem);

                var service = WindowWrapper.Current().NavigationServices.GetByFrameId(session.Id.ToString()) as NavigationService;
                if (service == null)
                {
                    service = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Attach, BootStrapper.ExistingContent.Exclude, new Frame(), session.Id) as NavigationService;
                    service.SerializationService = TLSerializationService.Current;
                    service.FrameFacade.FrameId = session.Id.ToString();
                    service.Navigate(typeof(MainPage));
                }

                Window.Current.Content = service.Frame;
            }
        }
    }
}
