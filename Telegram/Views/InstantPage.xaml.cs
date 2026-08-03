//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views.Popups;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views
{
    public partial class InstantPageArgs
    {
        public InstantPageArgs(WebPageInstantView instantView, string url)
        {
            InstantView = instantView;
            Url = url;
        }

        public WebPageInstantView InstantView { get; }

        public string Url { get; set; }

        public override string ToString()
        {
            return Url;
        }
    }

    public sealed partial class InstantPage : HostedPage, IPageBlockContext
    {
        public InstantViewModel ViewModel => DataContext as InstantViewModel;

        public ISettingsService Settings => ViewModel.Settings;

        public IEventAggregator Aggregator => ViewModel.Aggregator;

        private TextSelectionManager _textSelectionManager;

        public InstantPage()
        {
            _renderer = new PageBlockRenderer(this);

            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var scroll = ScrollingHost.GetScrollViewer();
            if (scroll != null)
            {
                scroll.ViewChanged += OnViewChanged;
                scroll.PointerWheelChanged += OnPointerWheelChanged;
            }

            // Selection across the whole page, driven by the shared manager: every text
            // block the renderer produces is a FormattedTextBlock (an ISelectableControl),
            // so the manager finds them itself and the page needs no per-block wiring.
            // Rooted at the list rather than the page so the header and footer chrome
            // stay outside the selection.
            _textSelectionManager ??= new TextSelectionManager(this, ScrollingHost, handleContextMenu: true);

            Dispatcher.AcceleratorKeyActivated += OnAcceleratorKeyActivated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.AcceleratorKeyActivated -= OnAcceleratorKeyActivated;

            // Detach explicitly: the manager hooks the STATIC FocusManager events while a
            // selection is live, which would otherwise keep this page alive for the session.
            _textSelectionManager?.Detach();
            _textSelectionManager = null;
        }

        private void OnAcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Number0 && VirtualKeyModifiers.Control == WindowContext.KeyModifiers())
            {
                _zoomFactor = 7;
                ZoomingHost.ZoomFactor = 1.0;
                args.Handled = true;
            }
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ScrollingHost.Items.Clear();
            _renderer.ClearAnchors();

            var args = e.Parameter as InstantPageArgs;
            if (args?.InstantView == null || !Uri.TryCreate(args.Url, UriKind.Absolute, out Uri uri))
            {
                return;
            }

            if (args.Url.StartsWith("tg"))
            {
                Feedback.Visibility = Visibility.Collapsed;
            }

            ViewModel.ShareLink = uri;
            ViewModel.ShareTitle = args.Url;

            UpdateView(args.InstantView);

            Header.CanGoBack = Frame.CanGoBack;
            Header.CanGoForward = Frame.CanGoForward;
        }

        private WebPageInstantView _instantView;

        private void UpdateView(WebPageInstantView instantView)
        {
            _instantView = instantView;

            // The gallery is built from these when a medium is tapped, not collected here.
            ViewModel.Blocks = instantView.Blocks;

            ScrollingHost.FlowDirection = instantView.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            if (instantView.ViewCount > 0)
            {
                ViewsLabel.Text = Locale.Declension(Strings.R.Views, instantView.ViewCount);
            }
            else
            {
                ViewsLabel.Text = string.Empty;
            }

            var processed = 0;
            PageBlock previousBlock = null;
            FrameworkElement previousElement = null;
            FrameworkElement firstElement = null;
            foreach (var block in instantView.Blocks)
            {
                var element = _renderer.ProcessBlock(ViewModel.ClientService, block, null);
                var spacing = SpacingBetweenBlocks(previousBlock, block);
                var padding = PaddingForBlock(block);

                if (element != null)
                {
                    if (block is PageBlockChatLink && previousBlock is PageBlockCover)
                    {
                        if (previousElement is StackPanel stack && element is Button)
                        {
                            element.Style = Resources["CoverChannelBlockStyle"] as Style;
                            element.Margin = new Thickness(padding, -40, padding, 0);
                            stack.Children.Insert(1, element);
                        }
                    }
                    else
                    {
                        element.Margin = new Thickness(padding, spacing, padding, 0);

                        // How a tapped medium finds the block it came from — the delegate
                        // walks up from the tapped control until it hits a Tag like this.
                        element.Tag = block;

                        ScrollingHost.Items.Add(element);
                    }
                }

                firstElement ??= element;

                previousBlock = block;
                previousElement = element;
                processed++;
            }

            if (firstElement != null)
            {
                firstElement.Loaded += (s, args) =>
                {
                    if (ViewModel.ShareLink?.Fragment?.Length > 0)
                    {
                        Hyperlink_Click(new RichTextAnchorLink { AnchorName = ViewModel.ShareLink.Fragment.TrimStart('#') });
                    }
                };
            }

            if (previousElement != null)
            {
                previousElement.Margin = new Thickness(previousElement.Margin.Left, previousElement.Margin.Top, previousElement.Margin.Right, previousElement.Margin.Bottom + 24);
            }
        }


        // Every block becomes XAML through the shared renderer — the same one the
        // rich-message bubble uses. This page only decides how the results are hosted
        // (one virtualized list item per top-level block) and what a link does.
        private readonly PageBlockRenderer _renderer;

        #region IPageBlockContext

        ResourceDictionary IPageBlockContext.Resources => Resources;

        // The page is loaded before it renders, and never recycled block-by-block.
        bool IPageBlockContext.IsConnected => true;

        // Nothing streams into an instant view: it arrives whole.
        bool IPageBlockContext.IsSkeletonVisible => false;

        MessageViewModel IPageBlockContext.CreateMessage(long id, MessageContent content)
        {
            return ViewModel.CreateMessage(new Message { Id = id, Content = content });
        }

        void IPageBlockContext.TextEntityClick(FormattedTextBlock sender, TextEntityClickEventArgs args)
        {
            if (args.Type is TextEntityTypeTextUrl textUrl)
            {
                Hyperlink_Click(new RichTextUrl(null, textUrl.Url, false));
            }
            else if (args.Type is TextEntityTypeUrl && args.Text != null)
            {
                // An auto-detected url carries no payload — the covered text is the url.
                Hyperlink_Click(new RichTextUrl(null, args.Text, false));
            }
        }

        void IPageBlockContext.OpenUrl(string url)
        {
            // is_cached: a related article is part of this page's set, so prefer opening
            // it as an instant view rather than handing it to the browser.
            Hyperlink_Click(new RichTextUrl(null, url, true));
        }

        void IPageBlockContext.OpenInlineButton(InlineButton button)
        {
            // A page has no message behind it, so the types that answer *to a message*
            // (callback, login-url, switch-inline, buy, user) have nothing to answer
            // against. Better inert than firing a query that can't be attributed.
            switch (button.Type)
            {
                case InlineKeyboardButtonTypeUrl url:
                    if (MessageHelper.TryCreateUri(url.Url, out Uri uri))
                    {
                        OpenUrl(uri);
                    }
                    break;
                case InlineKeyboardButtonTypeWebApp webApp:
                    if (MessageHelper.TryCreateUri(webApp.Url, out Uri webAppUri))
                    {
                        OpenUrl(webAppUri);
                    }
                    break;
                case InlineKeyboardButtonTypeCopyText copyText:
                    MessageHelper.CopyText(XamlRoot, copyText.Text);
                    break;
            }
        }

        void IPageBlockContext.RegisterDebug(object element)
        {
        }

        #endregion

        private double SpacingBetweenBlocks(PageBlock upper, PageBlock lower)
        {
            if (lower is PageBlockCover or PageBlockChatLink)
            {
                return 0;
            }

            if (upper is PageBlockDetails && lower is PageBlockDetails)
            {
                return 0;
            }

            return 12;

            if (lower is PageBlockCover or PageBlockChatLink)
            {
                return 0;
            }
            else if (lower is PageBlockDivider || upper is PageBlockDivider)
            {
                return 15; // 25;
            }
            else if (lower is PageBlockBlockQuote || upper is PageBlockBlockQuote || lower is PageBlockPullQuote || upper is PageBlockPullQuote)
            {
                return 17; // 27;
            }
            else if (lower is PageBlockTitle)
            {
                return 12; // 20;
            }
            else if (lower is PageBlockAuthorDate)
            {
                if (upper is PageBlockTitle)
                {
                    return 16; // 26;
                }
                else
                {
                    return 12; // 20;
                }
            }
            else if (lower is PageBlockParagraph)
            {
                if (upper is PageBlockTitle or PageBlockAuthorDate)
                {
                    return 20; // 34;
                }
                else if (upper is PageBlockHeader or PageBlockSubheader)
                {
                    return 15; // 25;
                }
                else if (upper is PageBlockParagraph)
                {
                    return 15; // 25;
                }
                else if (upper is PageBlockList)
                {
                    return 19; // 31;
                }
                else if (upper is PageBlockPreformatted)
                {
                    return 11; // 19;
                }
                else
                {
                    return 12; // 20;
                }
            }
            else if (lower is PageBlockList)
            {
                if (upper is PageBlockTitle or PageBlockAuthorDate)
                {
                    return 20; // 34;
                }
                else if (upper is PageBlockHeader or PageBlockSubheader)
                {
                    return 19; // 31;
                }
                else if (upper is PageBlockParagraph or PageBlockList)
                {
                    return 19; // 31;
                }
                else if (upper is PageBlockPreformatted)
                {
                    return 11; // 19;
                }
                else
                {
                    return 12; // 20;
                }
            }
            else if (lower is PageBlockPreformatted)
            {
                if (upper is PageBlockParagraph)
                {
                    return 11; // 19;
                }
                else
                {
                    return 12; // 20;
                }
            }
            else if (lower is PageBlockHeader)
            {
                return 20; // 32;
            }
            else if (lower is PageBlockSubheader)
            {
                return 20; // 32;
            }
            else if (lower == null)
            {
                if (upper is PageBlockFooter)
                {
                    return 14; // 24;
                }
                else
                {
                    return 14; // 24;
                }
            }

            return 12; // 20;
        }

        private double PaddingForBlock(PageBlock block)
        {
            if (block is PageBlockCover or PageBlockPreformatted or
                PageBlockPhoto or PageBlockVideo or
                PageBlockSlideshow or PageBlockChatLink)
            {
                return 0.0;
            }

            return 12;
        }

        private async void Hyperlink_Click(RichTextAnchorLink anchorLinkText)
        {
            if (string.IsNullOrEmpty(anchorLinkText.AnchorName))
            {
                ScrollingHost.ScrollToTop();
            }
            else if (_renderer.TryGetAnchor(anchorLinkText.AnchorName, out Border anchor))
            {
                await ScrollingHost.ScrollToItem2(anchor, VerticalAlignment.Top);
            }
        }

        private async void Hyperlink_Click(RichTextUrl urlText)
        {
            ViewModel.IsLoading = true;

            var response = await ViewModel.ClientService.SendAsync(new GetWebPageInstantView(urlText.Url, false));
            if (response is WebPageInstantView instantView)
            {
                ViewModel.IsLoading = false;
                ViewModel.NavigationService.Navigate(typeof(InstantPage), new InstantPageArgs(instantView, urlText.Url));
            }
            else if (MessageHelper.TryCreateUri(urlText.Url, out Uri url))
            {
                ViewModel.IsLoading = false;
                OpenUrl(url);
            }
        }

        private async void OpenUrl(Uri url)
        {
            if (MessageHelper.IsTelegramUrl(url))
            {
                var clientService = ViewModel.ClientService;
                ByNavigation(navigation => MessageHelper.OpenTelegramUrl(clientService, navigation, url));
            }
            else
            {
                await Launcher.LaunchUriAsync(url);
            }
        }

        private async void ByNavigation(Action<INavigationService> action)
        {
            WindowContext.Main.Dispatcher.Dispatch(() => action(WindowContext.Main.GetNavigationService()));
            await ApplicationViewSwitcher.SwitchAsync(WindowContext.Main.Id);
        }

        private void Hyperlink_Click(RichTextPhoneNumber phoneNumber)
        {

        }

        private void Header_GoBackClicked(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        private void Header_GoForwardClicked(object sender, RoutedEventArgs e)
        {
            Frame.GoForward();
        }

        private void Feedback_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            ByNavigation(navigation => viewModel.Feedback(navigation));
        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            var link = ViewModel.ShareLink;
            if (link == null)
            {
                return;
            }

            this.ShowPopup(ViewModel.Session, new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(new HttpUrl(link.ToString())));
        }

        private void Browser_Click(object sender, RoutedEventArgs e)
        {
            var link = ViewModel.ShareLink;
            if (link == null)
            {
                return;
            }

            MessageHelper.OpenUrl(null, null, link.ToString());
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            var link = ViewModel.ShareLink;
            if (link == null)
            {
                return;
            }

            MessageHelper.CopyLink(XamlRoot, link.ToString());
        }

        private int _zoomFactor = 7;
        private readonly double[] _zoomFactors = new double[]
        {
            100d / 25,
            100d / 33,
            100d / 50,
            100d / 67,
            100d / 75,
            100d / 80,
            100d / 90,
            100d / 100,
            100d / 110,
            100d / 125,
            100d / 150,
            100d / 175,
            100d / 200,
            100d / 250,
            100d / 300,
            100d / 400,
            100d / 500
        };

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var modifiers = WindowContext.KeyModifiers();
            if (modifiers == VirtualKeyModifiers.Control)
            {
                var pointer = e.GetCurrentPoint(this);
                var zoom = ZoomingHost.ZoomFactor;
                var delta = pointer.Properties.MouseWheelDelta > 0 ? 1 : -1;

                var index = _zoomFactor + delta;
                if (index >= 0 && index < _zoomFactors.Length)
                {
                    _zoomFactor = index;
                    ZoomingHost.ZoomFactor = _zoomFactors[index];
                }

                e.Handled = true;
            }
        }
    }
}
