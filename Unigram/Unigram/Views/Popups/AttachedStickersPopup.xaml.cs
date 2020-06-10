using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Popups
{
    public sealed partial class AttachedStickersPopup : ContentPopup
    {
        public AttachedStickersViewModel ViewModel => DataContext as AttachedStickersViewModel;

        private AttachedStickersPopup()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<AttachedStickersViewModel>();

            SecondaryButtonText = Strings.Resources.Close;

            //Loaded += async (s, args) =>
            //{
            //    await ViewModel.OnNavigatedToAsync(parameter, NavigationMode.New, null);
            //};
        }

        private static Dictionary<int, WeakReference<AttachedStickersPopup>> _windowContext = new Dictionary<int, WeakReference<AttachedStickersPopup>>();
        public static AttachedStickersPopup GetForCurrentView()
        {
            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out WeakReference<AttachedStickersPopup> reference) && reference.TryGetTarget(out AttachedStickersPopup value))
            {
                return value;
            }

            var context = new AttachedStickersPopup();
            _windowContext[id] = new WeakReference<AttachedStickersPopup>(context);

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
        }

        private async void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            Hide();

            var item = e.ClickedItem as Sticker;
            if (item != null)
            {
                await StickerSetPopup.GetForCurrentView().ShowAsync(item.SetId);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
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
                var file = sticker.Thumbnail.File;
                if (file.Local.IsDownloadingCompleted)
                {
                    content.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
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
