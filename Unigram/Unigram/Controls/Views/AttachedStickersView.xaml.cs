using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Utils;
using Unigram.Common;
using Unigram.Native;
using Unigram.ViewModels;
using Unigram.Views;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class AttachedStickersView : ContentDialog
    {
        public AttachedStickersViewModel ViewModel => DataContext as AttachedStickersViewModel;

        private AttachedStickersView()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<AttachedStickersViewModel>();

            SecondaryButtonText = Strings.Resources.Close;

            //Loaded += async (s, args) =>
            //{
            //    await ViewModel.OnNavigatedToAsync(parameter, NavigationMode.New, null);
            //};
        }

        private static Dictionary<int, WeakReference<AttachedStickersView>> _windowContext = new Dictionary<int, WeakReference<AttachedStickersView>>();
        public static AttachedStickersView GetForCurrentView()
        {
            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out WeakReference<AttachedStickersView> reference) && reference.TryGetTarget(out AttachedStickersView value))
            {
                return value;
            }

            var context = new AttachedStickersView();
            _windowContext[id] = new WeakReference<AttachedStickersView>(context);

            return context;
        }

        public Task<ContentDialogResult> ShowAsync(IList<StickerSetInfo> parameter)
        {
            ViewModel.IsLoading = false;
            ViewModel.Items.ReplaceWith(parameter);

            return this.ShowQueuedAsync();
        }

        public Task<ContentDialogResult> ShowAsync(long parameter)
        {
            ViewModel.IsLoading = true;
            ViewModel.Items.Clear();

            RoutedEventHandler handler = null;
            handler = new RoutedEventHandler(async (s, args) =>
            {
                Loaded -= handler;
                await ViewModel.OnNavigatedToAsync(parameter, NavigationMode.New, null);
            });

            Loaded += handler;
            return this.ShowQueuedAsync();
        }

        // SystemControlBackgroundChromeMediumLowBrush

        private void SetupTitleAndStatusBar()
        {
            var titlebar = ApplicationView.GetForCurrentView().TitleBar;
            var backgroundBrush = Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"] as SolidColorBrush;
            var foregroundBrush = Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as SolidColorBrush;

            //titlebar.BackgroundColor = backgroundBrush.Color;
            titlebar.ForegroundColor = foregroundBrush.Color;
            //titlebar.ButtonBackgroundColor = backgroundBrush.Color;
            titlebar.ButtonForegroundColor = foregroundBrush.Color;

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                statusBar.BackgroundColor = backgroundBrush.Color;
                statusBar.ForegroundColor = foregroundBrush.Color;
            }
        }

        private async void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            Hide();

            var item = e.ClickedItem as Sticker;
            if (item != null)
            {
                await StickerSetView.GetForCurrentView().ShowAsync(item.SetId);
            }
        }

        private async void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Image;
            var sticker = args.Item as Sticker;

            if (sticker == null || sticker.Thumbnail == null)
            {
                content.Source = null;
                return;
            }

            if (args.Phase < 2)
            {
                content.Source = null;
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }
            else
            {
                var file = sticker.Thumbnail.Photo;
                if (file.Local.IsDownloadingCompleted)
                {
                    content.Source = await PlaceholderHelper.GetWebpAsync(file.Local.Path);
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    ViewModel.ProtoService.DownloadFile(file.Id, 1);
                }
            }

            args.Handled = true;
        }
    }
}
