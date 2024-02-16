//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsAppearancePage : HostedPage
    {
        public SettingsAppearanceViewModel ViewModel => DataContext as SettingsAppearanceViewModel;

        public SettingsAppearancePage()
        {
            InitializeComponent();
            Title = Strings.Appearance;

            var preview = ElementComposition.GetElementVisual(Preview);
            preview.Clip = preview.Compositor.CreateInsetClip();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            BackgroundControl.Update(ViewModel.ClientService, ViewModel.Aggregator);

            ViewModel.PropertyChanged += OnPropertyChanged;

            if (ViewModel.ClientService.TryGetUser(ViewModel.ClientService.Options.MyId, out User user))
            {
                if (ViewModel.IsPremiumAvailable)
                {
                    ProfileColor.SetUser(ViewModel.ClientService, user);
                }
                else
                {
                    NameColor.Visibility = Visibility.Collapsed;
                }

                Message1.Mockup(ViewModel.ClientService, Strings.FontSizePreviewLine1, user, Strings.FontSizePreviewReply, false, DateTime.Now.AddSeconds(-25));
                Message2.Mockup(Strings.FontSizePreviewLine2, true, DateTime.Now);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        #region Binding

        private string ConvertNightMode(NightMode mode)
        {
            return mode == NightMode.Scheduled
                ? Strings.AutoNightScheduled
                : mode == NightMode.Automatic
                ? Strings.AutoNightAutomatic
                : mode == NightMode.System
                ? Strings.AutoNightSystemDefault
                : Strings.AutoNightDisabled;
        }

        #endregion

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("FontSize"))
            {
                Message1.UpdateMockup();
                Message2.UpdateMockup();
            }
            else if (e.PropertyName.Equals("BubbleRadius"))
            {
                Message1.UpdateMockup();
                Message2.UpdateMockup();
            }
        }

        #region Context menu

        private void Theme_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var theme = List.ItemFromContainer(sender) as ChatThemeViewModel;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.CreateTheme, theme, Strings.CreateNewThemeMenu, Icons.Color);

            //if (!theme.IsOfficial)
            //{
            //    flyout.CreateFlyoutSeparator();
            //    flyout.CreateFlyoutItem(ViewModel.ThemeShareCommand, theme, Strings.ShareFile, Icons.Share);
            //    flyout.CreateFlyoutItem(ViewModel.ThemeEditCommand, theme, Strings.Edit, Icons.Edit);
            //    flyout.CreateFlyoutItem(ViewModel.ThemeDeleteCommand, theme, Strings.Delete, Icons.Delete);
            //}

            flyout.ShowAt(sender, args);
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Theme_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatThemeCell content && args.Item is ChatThemeViewModel theme)
            {
                content.Update(theme);
                args.Handled = true;
            }
        }

        #endregion
    }
}
