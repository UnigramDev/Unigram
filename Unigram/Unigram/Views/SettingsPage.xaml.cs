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
using Unigram.ViewModels.Users;
using Windows.UI.Xaml.Markup;
using System.Linq;
using Windows.UI.Xaml.Media;
using Telegram.Api.Helpers;

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

#if DEBUG
            var optionAccounts = new HyperButton();
            optionAccounts.Style = App.Current.Resources["HyperButtonStyle"] as Style;
            optionAccounts.Click += Accounts_Click;
            optionAccounts.Content = "Accounts management";

            OptionsGroup3.Children.Clear();
            OptionsGroup3.Children.Add(optionAccounts);
#endif
        }

        public MasterDetailView MasterDetail { get; set; }


        private void General_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsGeneralPage));
        }

        private void Phone_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsPhoneIntroPage));
        }

        private void Username_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsUsernamePage));
        }

        public async void EditName_Click(object sender, RoutedEventArgs e)
        {
            await MasterDetail.NavigationService.NavigateModalAsync(typeof(EditYourNameView));
        }

        private async void About_Click(object sender, RoutedEventArgs e)
        {
            var full = ViewModel.Full as TLUserFull;
            if (full == null)
            {
                return;
            }

            var dialog = new EditYourAboutView();
            dialog.About = full.About;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var about = dialog.About;

                var response = await ViewModel.ProtoService.UpdateProfileAsync(null, null, about);
                if (response.IsSucceeded)
                {
                    ViewModel.Full.About = about;
                    ViewModel.Full.RaisePropertyChanged(() => ViewModel.Full.About);
                }
            }
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

        private void Appearance_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsAppearancePage));
        }

        private void Language_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsLanguagePage));
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var user = ViewModel.Self;
            var userFull = ViewModel.Full as TLUserFull;
            if (userFull != null && userFull.ProfilePhoto is TLPhoto && user != null)
            {
                var viewModel = new UserPhotosViewModel(ViewModel.ProtoService, userFull, user);
                await GalleryView.Current.ShowAsync(viewModel, () => Photo);
            }
        }

        public async void EditPhoto_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.AddRange(Constants.PhotoTypes);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var dialog = new EditYourPhotoView(file)
                {
                    CroppingProportions = ImageCroppingProportions.Square,
                    IsCropEnabled = false
                };
                var dialogResult = await dialog.ShowAsync();
                if (dialogResult == ContentDialogBaseResult.OK)
                {
                    ViewModel.EditPhotoCommand.Execute(dialog.Result);
                }
            }
        }

        private async void Questions_Click(object sender, RoutedEventArgs e)
        {
            var response = await ViewModel.ProtoService.GetWebPageAsync("https://telegram.org/faq", 0);
            if (response.IsSucceeded && response.Result is TLWebPage webPage && webPage.HasCachedPage)
            {
                MasterDetail.NavigationService.Navigate(typeof(InstantPage), response.Result);
            }
            else
            {
                await Launcher.LaunchUriAsync(new Uri("https://telegram.org/faq"));
            }
        }

        private async void Theme_Click(object sender, RoutedEventArgs e)
        {
            if (Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
            {
                var remove = await FileUtils.TryGetTempItemAsync("theme.xaml");
                if (remove != null)
                {
                    await remove.DeleteAsync();

                    Theme.Current.Update();
                    App.NotifyThemeChanged();
                }

                return;
            }

            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".xaml");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var result = await FileUtils.CreateTempFileAsync("theme.xaml");
                await file.CopyAndReplaceAsync(result);

                Theme.Current.Update();
                App.NotifyThemeChanged();

                //var text = await FileIO.ReadTextAsync(file);

                //var dictionary = XamlReader.Load(text) as ResourceDictionary;
                //if (dictionary == null)
                //{
                //    return;
                //}

                //var accent = App.Current.Resources.MergedDictionaries.FirstOrDefault(x => x.Source.AbsoluteUri.EndsWith("Accent.xaml"));
                //if (accent == null)
                //{
                //    return;
                //}

                //foreach (var theme in dictionary.ThemeDictionaries)
                //{
                //    var item = theme.Value as ResourceDictionary;
                //    if (accent.ThemeDictionaries.TryGetValue(theme.Key, out object value))
                //    {
                //        var pair = value as ResourceDictionary;
                //        if (pair == null)
                //        {
                //            continue;
                //        }

                //        foreach (var key in item)
                //        {
                //            if (pair.ContainsKey(key.Key))
                //            {
                //                try
                //                {
                //                    pair[key.Key] = key.Value;
                //                }
                //                catch
                //                {
                //                    Debug.WriteLine("Theme: unable to apply " + key.Key);
                //                }
                //            }
                //        }
                //    }
                //}

                //App.RaiseThemeChanged();
            }
        }

        #region Binding

        private string ConvertUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return Strings.Android.UsernameEmpty;
            }

            return "@" + username;
        }

        private string ConvertAbout(string about)
        {
            if (string.IsNullOrEmpty(about))
            {
                return Strings.Android.UserBioEmpty;
            }

            return about;
        }

        #endregion
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
