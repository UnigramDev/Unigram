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
using Unigram.Common;
using Unigram.Converters;
using Windows.System;
using Windows.UI.Core;

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

        #region Share

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Bindings.Update();

            if (ApiInformation.IsEventPresent("Windows.ApplicationModel.DataTransfer.DataTransferManager", "ShareProvidersRequested") && !ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5))
            {
                DataTransferManager.GetForCurrentView().ShareProvidersRequested -= OnShareProvidersRequested;
                DataTransferManager.GetForCurrentView().ShareProvidersRequested += OnShareProvidersRequested;
            }

            DataTransferManager.GetForCurrentView().DataRequested -= OnDataRequested;
            DataTransferManager.GetForCurrentView().DataRequested += OnDataRequested;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (ApiInformation.IsEventPresent("Windows.ApplicationModel.DataTransfer.DataTransferManager", "ShareProvidersRequested") && !ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5))
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
            var dataPackage = new DataPackage();
            dataPackage.SetText(webLink.ToString());

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                ClipboardEx.TrySetContent(dataPackage);
                operation.ReportCompleted();
            });
        }

        private void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            var package = args.Request.Data;
            package.Properties.Title = ViewModel.ShareTitle;
            package.SetText(ViewModel.ShareLink.ToString());
            package.SetWebLink(ViewModel.ShareLink);
        }

        #endregion

        #region Show

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
            ViewModel.Comment = null;
            ViewModel.ShareLink = null;
            ViewModel.ShareTitle = null;
            ViewModel.Messages = new[] { message };
            ViewModel.InputMedia = null;
            ViewModel.IsWithMyScore = withMyScore;

            var channel = message.Parent as TLChannel;
            if (channel != null && channel.IsBroadcast && channel.HasUsername)
            {
                var link = $"{channel.Username}/{message.Id}";

                if (message.IsRoundVideo())
                {
                    link = $"https://telesco.pe/{link}";
                }
                else
                {
                    link = MeUrlPrefixConverter.Convert(link);
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
            else if (message.Media is TLMessageMediaGame gameMedia)
            {
                if (message.ViaBot != null && message.ViaBot.Username != null)
                {
                    ViewModel.ShareLink = new Uri(MeUrlPrefixConverter.Convert($"{message.From.Username}?game={gameMedia.Game.ShortName}"));
                    ViewModel.ShareTitle = gameMedia.Game.Title;
                }
            }

            return ShowAsync();
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(IEnumerable<TLMessage> messages, bool withMyScore = false)
        {
            ViewModel.Comment = null;
            ViewModel.ShareLink = null;
            ViewModel.ShareTitle = null;
            ViewModel.Messages = messages;
            ViewModel.InputMedia = null;
            ViewModel.IsWithMyScore = withMyScore;

            return ShowAsync();
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(Uri link, string title)
        {
            ViewModel.Comment = null;
            ViewModel.ShareLink = link;
            ViewModel.ShareTitle = title;
            ViewModel.Messages = null;
            ViewModel.InputMedia = null;
            ViewModel.IsWithMyScore = false;

            return ShowAsync();
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(TLInputMediaBase inputMedia)
        {
            ViewModel.Comment = null;
            ViewModel.ShareLink = null;
            ViewModel.ShareTitle = null;
            ViewModel.Messages = null;
            ViewModel.InputMedia = inputMedia;
            ViewModel.IsWithMyScore = false;

            if (inputMedia is TLInputMediaGame gameMedia && gameMedia.Id is TLInputGameShortName shortName)
            {
                // TODO: maybe?
            }

            return ShowAsync();
        }

        private new IAsyncOperation<ContentDialogBaseResult> ShowAsync()
        {
            ViewModel.Items.Clear();

            RoutedEventHandler handler = null;
            handler = new RoutedEventHandler(async (s, args) =>
            {
                Loaded -= handler;
                await ViewModel.OnNavigatedToAsync(null, NavigationMode.New, null);
            });

            Loaded += handler;
            return base.ShowAsync();
        }

        #endregion

        #region Header

        private ScrollViewer _scrollingHost;

        private Visual _groupHeader;
        private SpriteVisual _background;
        private ExpressionAnimation _expression;
        private ExpressionAnimation _expressionHeader;
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

                if (_background == null)
                {
                    _background = ElementCompositionPreview.GetElementVisual(BackgroundPanel).Compositor.CreateSpriteVisual();
                    ElementCompositionPreview.SetElementChildVisual(BackgroundPanel, _background);
                }

                _background.Brush = _background.Compositor.CreateColorBrush(brush.Color);
                _background.Size = new System.Numerics.Vector2((float)BackgroundPanel.ActualWidth, (float)BackgroundPanel.ActualHeight);
                _background.Clip = _background.Compositor.CreateInsetClip();

                _groupHeader = ElementCompositionPreview.GetElementVisual(GroupHeader);

                _expression = _expression ?? _background.Compositor.CreateExpressionAnimation("Max(Maximum, Scrolling.Translation.Y)");
                _expression.SetReferenceParameter("Scrolling", props);
                _expression.SetScalarParameter("Maximum", -(float)BackgroundPanel.Margin.Top + 1);
                _background.StopAnimation("Offset.Y");
                _background.StartAnimation("Offset.Y", _expression);

                _expressionHeader = _expressionHeader ?? _background.Compositor.CreateExpressionAnimation("Max(0, Maximum - Scrolling.Translation.Y)");
                _expressionHeader.SetReferenceParameter("Scrolling", props);
                _expressionHeader.SetScalarParameter("Maximum", -(float)BackgroundPanel.Margin.Top);
                _groupHeader.StopAnimation("Offset.Y");
                _groupHeader.StartAnimation("Offset.Y", _expressionHeader);

                _expressionClip = _expressionClip ?? _background.Compositor.CreateExpressionAnimation("Min(0, Maximum - Scrolling.Translation.Y)");
                _expressionClip.SetReferenceParameter("Scrolling", props);
                _expressionClip.SetScalarParameter("Maximum", -(float)BackgroundPanel.Margin.Top + 1);
                _background.Clip.StopAnimation("Offset.Y");
                _background.Clip.StartAnimation("Offset.Y", _expressionClip);
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

            LineTop.BorderThickness = new Thickness(0, 0, 0, top);
            LineAccent.BorderThickness = new Thickness(0, accent, 0, 0);
            LineBottom.BorderThickness = new Thickness(0, bottom, 0, 0);
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

            if (_background != null && _expression != null && _expressionClip != null)
            {
                var brush = App.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"] as SolidColorBrush;

                _background.Brush = _background.Compositor.CreateColorBrush(brush.Color);
                _background.Size = new System.Numerics.Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
                _background.Clip = _background.Compositor.CreateInsetClip();

                _expression.SetScalarParameter("Maximum", -(float)top + 1);
                _background.StopAnimation("Offset.Y");
                _background.StartAnimation("Offset.Y", _expression);

                _expressionHeader.SetScalarParameter("Maximum", -(float)top);
                _groupHeader.StopAnimation("Offset.Y");
                _groupHeader.StartAnimation("Offset.Y", _expressionHeader);

                _expressionClip.SetScalarParameter("Maximum", -(float)top + 1);
                _background.Clip.StopAnimation("Offset.Y");
                _background.Clip.StartAnimation("Offset.Y", _expressionClip);
            }
        }

        #endregion

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
            if (Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
            {
                foreach (var item in ViewModel.SelectedItems)
                {
                    if (item is ITLDialogWith with && List.Items.Contains(with) && !List.SelectedItems.Contains(with))
                    {
                        Debug.WriteLine("Adding \"{0}\" to ListView", (object)with.DisplayName);
                        List.SelectedItems.Add(with);
                    }
                }

                ViewModel.SelectionMode = ListViewSelectionMode.Multiple;
            }

            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                return;
            }

            if (e.AddedItems != null)
            {
                foreach (var item in e.AddedItems)
                {
                    if (item is ITLDialogWith with && !ViewModel.SelectedItems.Contains(with))
                    {
                        Debug.WriteLine("Adding \"{0}\" to ViewModel", (object)with.DisplayName);
                        ViewModel.SelectedItems.Add(with);
                        ViewModel.SendCommand.RaiseCanExecuteChanged();
                    }
                }
            }

            if (e.RemovedItems != null)
            {
                foreach (var item in e.RemovedItems)
                {
                    if (item is ITLDialogWith with && ViewModel.SelectedItems.Contains(with))
                    {
                        Debug.WriteLine("Removing \"{0}\" from ViewModel", (object)with.DisplayName);
                        ViewModel.SelectedItems.Remove(with);
                        ViewModel.SendCommand.RaiseCanExecuteChanged();
                    }
                }
            }
        }

        private void Query_Changed(object sender, TextChangedEventArgs e)
        {
            ViewModel.Search(((TextBox)sender).Text);
        }
    }
}
