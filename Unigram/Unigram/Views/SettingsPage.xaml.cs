using System;
using System.Diagnostics;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Views;
using Unigram.ViewModels;
using Unigram.Views.Settings;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Media.Animation;
using Telegram.Api.TL;

namespace Unigram.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel => DataContext as SettingsViewModel;

        public SettingsPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsViewModel>();

            NavigationCacheMode = NavigationCacheMode.Required;


            Loaded += OnLoaded;

#if DEBUG
            // THIS CODE WILL RUN ONLY IF FIRST CONFIGURED SERVER IP IS TEST SERVER
            if (Telegram.Api.Constants.FirstServerIpAddress.Equals("149.154.167.40"))
            {
                var optionDelete = new HyperButton();
                optionDelete.Style = App.Current.Resources["HyperButtonStyle"] as Style;
                optionDelete.Command = ViewModel.DeleteAccountCommand;
                optionDelete.Content = "!!! DELETE ACCOUNT !!!";

                OptionsGroup4.Children.Clear();
                OptionsGroup4.Children.Add(optionDelete);
            }

            var optionAccounts = new HyperButton();
            optionAccounts.Style = App.Current.Resources["HyperButtonStyle"] as Style;
            optionAccounts.Click += Accounts_Click;
            optionAccounts.Content = "Accounts management";

            OptionsGroup3.Children.Clear();
            OptionsGroup3.Children.Add(optionAccounts);
#endif
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {

            OnStateChanged(null, null);
        }

        private void OnStateChanged(object p1, object p2)
        {
            if (MasterDetail.CurrentState == MasterDetailState.Narrow)
            {
                Separator.BorderThickness = new Thickness(0);
            }
            else
            {
                Separator.BorderThickness = new Thickness(0, 0, 1, 0);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            //if (Frame.CanGoBack)
            //{
            //    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
            //        AppViewBackButtonVisibility.Visible;
            //}
            //else
            //{
            //    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
            //        AppViewBackButtonVisibility.Collapsed;
            //}

            if (MasterDetail.NavigationService == null)
            {
                MasterDetail.Initialize("Settings", Frame);
            }

            ViewModel.NavigationService = MasterDetail.NavigationService;
        }

        private void General_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsGeneralPage));
        }

        private void Username_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsUsernamePage));
        }

        private async void EditName_Click(object sender, RoutedEventArgs e)
        {
            await MasterDetail.NavigationService.NavigateModalAsync(typeof(EditYourNameView));
        }

        private void Privacy_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsPrivacyAndSecurityPage));
        }

        private void Stickers_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsStickersPage));
        }

        private void Data_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsDataAndStoragePage));
        }

        private void Notifications_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsNotificationsPage));
        }

        private void Accounts_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsAccountsPage));
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", Photo);

            var user = ViewModel.Self;
            if (user.HasPhoto && user.Photo is TLUserProfilePhoto photo)
            {
                var viewModel = new UserPhotosViewModel(user, ViewModel.ProtoService);
                await GalleryView.Current.ShowAsync(viewModel, (s, args) =>
                {
                    var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
                    if (animation != null)
                    {
                        animation.TryStart(Photo);
                    }
                });
            }
        }

        private async void EditPhoto_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.AddRange(Constants.PhotoTypes);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var dialog = new EditYourPhotoView(file);
                var dialogResult = await dialog.ShowAsync();
                if (dialogResult == ContentDialogBaseResult.OK)
                {
                    ViewModel.EditPhotoCommand.Execute(dialog.Result);
                }
            }
        }

        private async void Questions_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://telegram.org/faq"));
        }
    }

    // Experiment
    public class TableStackPanel : StackPanel
    {
        //protected override Size ArrangeOverride(Size finalSize)
        //{
        //    if (finalSize.Width >= 500)
        //    {
        //        //Margin = new Thickness(12, 0, 12, 0);
        //        //CornerRadius = new CornerRadius(8);
        //        //BorderThickness = new Thickness(0);

        //        HyperButton first = null;
        //        HyperButton last = null;

        //        foreach (var item in Children)
        //        {
        //            if (item.Visibility == Visibility.Visible)
        //            {
        //                if (first == null)
        //                {
        //                    first = item as HyperButton;
        //                }

        //                last = item as HyperButton;

        //                if (last != null)
        //                {
        //                    last.BorderBrush = Application.Current.Resources["SystemControlForegroundBaseLowBrush"] as SolidColorBrush;
        //                }
        //            }
        //        }

        //        var lastRadius = new CornerRadius(0, 0, 8, 8);

        //        if (first != null)
        //        {
        //            if (first == last)
        //            {
        //                last.CornerRadius = new CornerRadius(8, 8, 8, 8);
        //                last.BorderBrush = null;
        //            }
        //            else
        //            {
        //                first.CornerRadius = new CornerRadius(8, 8, 0, 0);

        //                if (last != null)
        //                {
        //                    last.CornerRadius = new CornerRadius(0, 0, 8, 8);
        //                    last.BorderBrush = null;
        //                }
        //            }
        //        }
        //    }

        //    return base.ArrangeOverride(finalSize);
        //}
    }
}
