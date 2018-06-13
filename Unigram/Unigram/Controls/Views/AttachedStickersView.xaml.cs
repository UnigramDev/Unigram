using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Template10.Utils;
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
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class AttachedStickersView : ContentDialogBase
    {
        public AttachedStickersViewModel ViewModel => DataContext as AttachedStickersViewModel;

        private AttachedStickersView()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.Resolve<AttachedStickersViewModel>();

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

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(long parameter)
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
            return ShowAsync();
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(IList<StickerSetInfo> parameter)
        {
            ViewModel.IsLoading = false;
            ViewModel.Items.ReplaceWith(parameter);

            return ShowAsync();
        }

        private Border LineAccent;

        private ScrollViewer _scrollingHost;

        private SpriteVisual _backgroundVisual;
        private ExpressionAnimation _expression;
        private ExpressionAnimation _expressionClip;

        private void GridView_Loaded(object sender, RoutedEventArgs e)
        {
            var scroll = List.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
            if (scroll != null)
            {
                _scrollingHost = scroll;
                _scrollingHost.ChangeView(null, 0, null, true);
                scroll.ViewChanged += Scroll_ViewChanged;
                Scroll_ViewChanged(scroll, null);

                var brush = App.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"] as SolidColorBrush;
                var props = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scroll);

                if (_backgroundVisual == null)
                {
                    _backgroundVisual = ElementCompositionPreview.GetElementVisual(BackgroundPanel).Compositor.CreateSpriteVisual();
                    ElementCompositionPreview.SetElementChildVisual(BackgroundPanel, _backgroundVisual);
                }

                _backgroundVisual.Brush = _backgroundVisual.Compositor.CreateColorBrush(brush.Color);
                _backgroundVisual.Size = new System.Numerics.Vector2((float)BackgroundPanel.ActualWidth, (float)BackgroundPanel.ActualHeight);
                _backgroundVisual.Clip = _backgroundVisual.Compositor.CreateInsetClip();

                _expression = _expression ?? _backgroundVisual.Compositor.CreateExpressionAnimation("Max(Maximum, Scrolling.Translation.Y)");
                _expression.SetReferenceParameter("Scrolling", props);
                _expression.SetScalarParameter("Maximum", -(float)BackgroundPanel.Margin.Top + 1);
                _backgroundVisual.StopAnimation("Offset.Y");
                _backgroundVisual.StartAnimation("Offset.Y", _expression);

                _expressionClip = _expressionClip ?? _backgroundVisual.Compositor.CreateExpressionAnimation("Min(0, Maximum - Scrolling.Translation.Y)");
                _expressionClip.SetReferenceParameter("Scrolling", props);
                _expressionClip.SetScalarParameter("Maximum", -(float)BackgroundPanel.Margin.Top + 1);
                _backgroundVisual.Clip.StopAnimation("Offset.Y");
                _backgroundVisual.Clip.StartAnimation("Offset.Y", _expressionClip);
            }

            var panel = List.ItemsPanelRoot as ItemsWrapGrid;
            if (panel != null)
            {
                panel.SizeChanged += (s, args) =>
                {
                    Scroll_ViewChanged(scroll, null);
                };
            }
        }

        private void GroupHeader_Loaded(object sender, RoutedEventArgs e)
        {
            var groupHeader = sender as Grid;
            if (groupHeader != null && groupHeader.DataContext is StickerSetInfo covered && covered == ViewModel.Items[0])
            {
                LineAccent = groupHeader.FindName("LineAccent") as Border;

                if (_scrollingHost != null)
                {
                    Scroll_ViewChanged(_scrollingHost, null);
                }
            }
        }

        private void Scroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var scroll = sender as ScrollViewer;
            var accent = 0;
            var bottom = 1;

            if (scroll.VerticalOffset < BackgroundPanel.Margin.Top)
            {
                accent = 1;
            }
            if (scroll.VerticalOffset == scroll.ScrollableHeight)
            {
                bottom = 0;
            }

            //if (LineTop.BorderThickness.Bottom != top)
            //{
            //    if (top == 0)
            //    {
            //        MaskTitleAndStatusBar();
            //    }
            //    else
            //    {
            //        SetupTitleAndStatusBar();
            //    }
            //}

            if (LineAccent != null)
            {
                LineAccent.BorderThickness = new Thickness(0, accent, 0, 0);
                LineBottom.BorderThickness = new Thickness(0, bottom, 0, 0);
            }
        }

        // SystemControlBackgroundChromeMediumLowBrush

        private void SetupTitleAndStatusBar()
        {
            var titlebar = ApplicationView.GetForCurrentView().TitleBar;
            var backgroundBrush = Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"] as SolidColorBrush;
            var foregroundBrush = Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as SolidColorBrush;

            titlebar.BackgroundColor = backgroundBrush.Color;
            titlebar.ForegroundColor = foregroundBrush.Color;
            titlebar.ButtonBackgroundColor = backgroundBrush.Color;
            titlebar.ButtonForegroundColor = foregroundBrush.Color;

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                statusBar.BackgroundColor = backgroundBrush.Color;
                statusBar.ForegroundColor = foregroundBrush.Color;
            }
        }

        private void List_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var itemWidth = (e.NewSize.Width - 24) / 5d;
            var minHeigth = itemWidth * 3d - 12 + 48;
            var top = Math.Max(0, e.NewSize.Height - minHeigth);

            if (!IsFullScreenMode())
            {
                top = 0;
            }

            if (top == 0)
            {
                Header.Visibility = Visibility.Collapsed;
            }
            else
            {
                Header.Visibility = Visibility.Visible;
            }

            Header.Height = top;

            BackgroundPanel.Height = e.NewSize.Height;
            BackgroundPanel.Margin = new Thickness(0, top, 0, -top);

            if (_backgroundVisual != null && _expression != null && _expressionClip != null)
            {
                var brush = App.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"] as SolidColorBrush;

                _backgroundVisual.Brush = _backgroundVisual.Compositor.CreateColorBrush(brush.Color);
                _backgroundVisual.Size = new System.Numerics.Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
                _backgroundVisual.Clip = _backgroundVisual.Compositor.CreateInsetClip();

                _expression.SetScalarParameter("Maximum", -(float)top + 1);
                _backgroundVisual.StopAnimation("Offset.Y");
                _backgroundVisual.StartAnimation("Offset.Y", _expression);

                _expressionClip.SetScalarParameter("Maximum", -(float)top + 1);
                _backgroundVisual.Clip.StopAnimation("Offset.Y");
                _backgroundVisual.Clip.StartAnimation("Offset.Y", _expressionClip);
            }
        }

        //protected override void UpdateView(Rect bounds)
        //{
        //    if (BackgroundElement == null) return;

        //    BackgroundElement.MinHeight = bounds.Height;
        //    BackgroundElement.BorderThickness = new Thickness(0);
        //}

        private void LightDismiss_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Hide(ContentDialogBaseResult.None);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogBaseResult.Cancel);
        }

        private async void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            Hide(ContentDialogBaseResult.OK);

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
                    var temp = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                    var buffer = await FileIO.ReadBufferAsync(temp);

                    content.Source = WebPImage.DecodeFromBuffer(buffer);
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    ViewModel.ProtoService.Send(new DownloadFile(1, file.Id));
                }
            }

            args.Handled = true;
        }
    }
}
