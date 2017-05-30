using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Views;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using LinqToVisualTree;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Composition;
using System.Diagnostics;
using Windows.UI.ViewManagement;
using Windows.Foundation.Metadata;
using Windows.UI;
using Template10.Utils;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace Unigram.Controls.Views
{
    public sealed partial class ShareView : ContentDialogBase
    {
        public ShareViewModel ViewModel => DataContext as ShareViewModel;

        private ShareView()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<ShareViewModel>();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ApiInformation.IsEventPresent("Windows.ApplicationModel.DataTransfer.DataTransferManager", "ShareProvidersRequested"))
            {
                DataTransferManager.GetForCurrentView().ShareProvidersRequested -= OnShareProvidersRequested;
                DataTransferManager.GetForCurrentView().ShareProvidersRequested += OnShareProvidersRequested;
            }

            DataTransferManager.GetForCurrentView().DataRequested -= OnDataRequested;
            DataTransferManager.GetForCurrentView().DataRequested += OnDataRequested;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (ApiInformation.IsEventPresent("Windows.ApplicationModel.DataTransfer.DataTransferManager", "ShareProvidersRequested"))
            {
                DataTransferManager.GetForCurrentView().ShareProvidersRequested -= OnShareProvidersRequested;
            }

            DataTransferManager.GetForCurrentView().DataRequested -= OnDataRequested;

            List.SelectedItems.Clear();
        }

        private void OnShareProvidersRequested(DataTransferManager sender, ShareProvidersRequestedEventArgs args)
        {
            if (args.Data.Contains(StandardDataFormats.WebLink))
            {
                var icon = RandomAccessStreamReference.CreateFromUri(new Uri(@"ms-appx:///Assets/Images/ShareProvider_CopyLink24x24.png"));
                var provider = new ShareProvider("Copy link", icon, (Color)App.Current.Resources["SystemAccentColor"], OnShareToClipboard);
                args.Providers.Add(provider);
            }

            Hide();
        }

        private async void OnShareToClipboard(ShareProviderOperation operation)
        {
            var webLink = await operation.Data.GetWebLinkAsync();
            var package = new DataPackage();
            package.SetWebLink(webLink);

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                Clipboard.SetContent(package);
                operation.ReportCompleted();
            });
        }

        private void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            var package = args.Request.Data;
            package.Properties.Title = ViewModel.ShareTitle;
            package.SetWebLink(ViewModel.ShareLink);
        }

        private static ShareView _current;
        public static ShareView Current
        {
            get
            {
                if (_current == null)
                    _current = new ShareView();

                return _current;
            }
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(TLMessage message, bool withMyScore = false)
        {
            ViewModel.ShareLink = null;
            ViewModel.ShareTitle = null;
            ViewModel.Message = message;
            ViewModel.InputMedia = null;
            ViewModel.IsWithMyScore = withMyScore;

            var channel = message.Parent as TLChannel;
            if (channel != null)
            {
                var link = $"{channel.Username}/{message.Id}";

                if (message.IsRoundVideo())
                {
                    link = $"https://telesco.pe/{link}";
                }
                else
                {
                    var config = ViewModel.CacheService.GetConfig();
                    if (config != null)
                    {
                        link = $"{config.MeUrlPrefix}{link}";
                    }
                    else
                    {
                        link = $"https://t.me/{link}";
                    }
                }

                string title = null;

                var media = message.Media as ITLMessageMediaCaption;
                if (media != null && !string.IsNullOrWhiteSpace(media.Caption))
                {
                    title = media.Caption;
                }
                else if (!string.IsNullOrWhiteSpace(message.Message))
                {
                    title = message.Message;
                }

                ViewModel.ShareLink = new Uri(link);
                ViewModel.ShareTitle = title ?? channel.DisplayName;
            }

            return ShowAsync();
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(Uri link, string title)
        {
            ViewModel.ShareLink = link;
            ViewModel.ShareTitle = title;
            ViewModel.Message = null;
            ViewModel.InputMedia = null;
            ViewModel.IsWithMyScore = false;

            return ShowAsync();
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(TLInputMediaBase inputMedia)
        {
            ViewModel.ShareLink = null;
            ViewModel.ShareTitle = null;
            ViewModel.Message = null;
            ViewModel.InputMedia = inputMedia;
            ViewModel.IsWithMyScore = false;

            return ShowAsync();
        }

        private Border LineTop;
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
            if (groupHeader != null)
            {
                LineTop = groupHeader.FindName("LineTop") as Border;
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
            var top = 1;
            var accent = 0;
            var bottom = 1;

            if (scroll.VerticalOffset <= BackgroundPanel.Margin.Top)
            {
                top = 0;
            }
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

            if (LineTop != null)
            {
                LineTop.BorderThickness = new Thickness(0, 0, 0, top);
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

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.SelectedItems = new List<TLDialog>(List.SelectedItems.Cast<TLDialog>());
        }
    }
}
