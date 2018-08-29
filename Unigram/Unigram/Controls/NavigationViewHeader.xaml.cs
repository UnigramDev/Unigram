using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Common;
using Unigram.Core.Services;
using Unigram.ViewModels;
using Unigram.Views;
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

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls
{
    public sealed partial class NavigationViewHeader : UserControl
    {
        public MainViewModel ViewModel => DataContext as MainViewModel;

        public NavigationViewHeader()
        {
            this.InitializeComponent();
        }

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var session = args.Item as SessionViewModel;

            var user = session.ProtoService.GetUser(session.UserId);
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                var title = content.Children[2] as TextBlock;
                title.Text = user.GetFullName();

                if (session.IsSelected)
                {
                    FullName.Text = user.GetFullName();
                    PhoneNumber.Text = Telegram.Helpers.PhoneNumber.Format(user.PhoneNumber);
                }
            }
            //else if (args.Phase == 1)
            //{
            //    var subtitle = content.Children[2] as TextBlock;
            //    subtitle.Text = ChannelParticipantToTypeConverter.Convert(ViewModel.ProtoService, session);
            //}
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetUser(session.ProtoService, user, 28, 28);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            //args.Handled = true;
        }

        #endregion

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ItemClick?.Invoke(this, EventArgs.Empty);

            var session = e.ClickedItem as SessionViewModel;
            if (session.IsSelected)
            {
                return;
            }

            ViewModel.Lifecycle.SelectedItem = session;

            var service = WindowWrapper.Current().NavigationServices.GetByFrameId(session.Id.ToString()) as NavigationService;
            if (service == null)
            {
                service = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Attach, BootStrapper.ExistingContent.Exclude, new Frame(), session.Id) as NavigationService;
                service.SerializationService = TLSerializationService.Current;
                service.FrameFacade.FrameId = session.Id.ToString();
                service.Navigate(typeof(MainPage));
                //WindowContext.GetForCurrentView().Handle(new UpdateAuthorizationState(session.ProtoService.GetAuthorizationState()));
            }

            Window.Current.Content = service.Frame;
        }

        private void Add_Click(object sender, TappedRoutedEventArgs e)
        {
            ItemClick?.Invoke(this, EventArgs.Empty);

            var session = ViewModel.Lifecycle.CreateNewSession();

            var service = WindowWrapper.Current().NavigationServices.GetByFrameId(session.Id.ToString()) as NavigationService;
            if (service == null)
            {
                service = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Attach, BootStrapper.ExistingContent.Exclude, new Frame(), session.Id) as NavigationService;
                service.SerializationService = TLSerializationService.Current;
                service.FrameFacade.FrameId = session.Id.ToString();
                service.Navigate(typeof(SignInPage));
                service.Frame.BackStack.Add(new PageStackEntry(typeof(BlankPage), null, null));
            }

            Window.Current.Content = service.Frame;
        }

        public event EventHandler ItemClick;
    }
}
