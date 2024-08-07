using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Tabbed
{
    public record NavigateToHistoryEntryParameters
    {
        public NavigateToHistoryEntryParameters(int entryId)
        {
            EntryId = entryId;
        }

        [JsonProperty("entryId")]
        public int EntryId { get; init; }
    }

    public record HistoryEntry
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("title")]
        public string Title { get; init; }

        [JsonProperty("url")]
        public string Url { get; init; }

        [JsonIgnore]
        public string DocumentTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title))
                {
                    return Title;
                }

                return Url;
            }
        }

        [JsonIgnore]
        public string FaviconUri { get; init; }

        [JsonIgnore]
        public int Index { get; set; }
    }

    public record NavigationHistory
    {
        [JsonProperty("currentIndex")]
        public int CurrentIndex { get; init; }

        [JsonProperty("entries")]
        public IReadOnlyList<HistoryEntry> Entries { get; init; }
    }

    public sealed partial class WebBrowserPage : UserControl
    {
        public static TabViewItem Create(IClientService clientService, string url)
        {
            var tabViewItem = new TabViewItem
            {
                IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource
                {
                    Symbol = Symbol.World
                }
            };

            tabViewItem.Content = new WebBrowserPage(tabViewItem, clientService, url);
            return tabViewItem;
        }

        private readonly IClientService _clientService;

        private readonly TabViewItem _owner;
        private readonly string _startUrl;

        private WebBrowserPage(TabViewItem owner, IClientService clientService, string startUrl)
        {
            InitializeComponent();

            _owner = owner;
            _clientService = clientService;
            _startUrl = startUrl;
            _ = Navigation.EnsureCoreWebView2Async();

            if (TonSite.TryUnmask(clientService, startUrl, out Uri navigation))
            {
                _owner.Header = navigation.Host + navigation.PathAndQuery + navigation.Fragment;
                Header.Source = navigation;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Navigation.CoreWebView2?.GoBack();
        }

        private void BackButton_ContextRequested(UIElement sender, object args)
        {
            CreateHistoryFlyout(sender, true);
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            Navigation.CoreWebView2?.GoForward();
        }

        private void ForwardButton_ContextRequested(UIElement sender, object args)
        {
            CreateHistoryFlyout(sender, false);
        }

        private static readonly Dictionary<Uri, string> _sourceToFavicon = new();

        private async void CreateHistoryFlyout(object sender, bool back)
        {
            var flyout = new MenuFlyout();

            var history = await GetHistoryAsync();
            var entries = history.Entries
                .Where(x => back ? x.Index < history.CurrentIndex : x.Index > history.CurrentIndex)
                .Take(10);

            if (back)
            {
                entries = entries.Reverse();
            }

            foreach (var entry in entries)
            {
                var item = new MenuFlyoutItem
                {
                    Text = GetDocumentTitle(entry.DocumentTitle),
                    Icon = GetFaviconSource2(entry.FaviconUri),
                    Tag = entry
                };

                item.Click += HistoryEntry_Click;

                flyout.Items.Add(item);
            }

            flyout.ShowAt(sender as DependencyObject, FlyoutPlacementMode.BottomEdgeAlignedLeft);
        }

        private async void HistoryEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is HistoryEntry entry)
            {
                try
                {
                    await Navigation.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.navigateToHistoryEntry", JsonConvert.SerializeObject(new NavigateToHistoryEntryParameters(entry.Id)));
                }
                catch
                {
                    // All...
                }
            }
        }

        private async Task<NavigationHistory> GetHistoryAsync()
        {
            try
            {
                var response = await Navigation.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.getNavigationHistory", "{}");
                var history = JsonConvert.DeserializeObject<NavigationHistory>(response);
                var entries = new List<HistoryEntry>(history.Entries);

                static string GetFaviconForUri(Uri uri)
                {
                    if (_sourceToFavicon.TryGetValue(uri, out var favicon))
                    {
                        return favicon;
                    }

                    return null;
                }

                for (int i = 0; i < history.Entries.Count; i++)
                {
                    entries[i] = history.Entries[i] with
                    {
                        FaviconUri = GetFaviconForUri(new Uri(history.Entries[i].Url)),
                        Index = i
                    };
                }

                return new NavigationHistory
                {
                    CurrentIndex = history.CurrentIndex,
                    Entries = entries
                };
            }
            catch
            {
                return null;
            }
        }

        private void OnInitialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            sender.CoreWebView2.Profile.PreferredColorScheme = ActualTheme switch
            {
                ElementTheme.Dark => CoreWebView2PreferredColorScheme.Dark,
                ElementTheme.Light => CoreWebView2PreferredColorScheme.Light,
                _ => CoreWebView2PreferredColorScheme.Auto
            };

            sender.CoreWebView2.Settings.IsStatusBarEnabled = false;
            sender.CoreWebView2.Settings.AreDevToolsEnabled = SettingsService.Current.Diagnostics.EnableWebViewDevTools;

            sender.CoreWebView2.Navigate(_startUrl);
            sender.CoreWebView2.SourceChanged += OnSourceChanged;
            sender.CoreWebView2.HistoryChanged += OnHistoryChanged;
            sender.CoreWebView2.DocumentTitleChanged += OnDocumentTitleChanged;
            sender.CoreWebView2.FaviconChanged += OnFaviconChanged;

            //sender.CoreWebView2.ContextMenuRequested += OnContextMenuRequested;
            sender.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
        }

        private void OnContextMenuRequested(CoreWebView2 sender, CoreWebView2ContextMenuRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            foreach (var item in args.MenuItems)
            {
                if (item.Kind == CoreWebView2ContextMenuItemKind.Command)
                {
                    var element = new MenuFlyoutItem();
                    element.Text = item.Label;
                    element.KeyboardAcceleratorTextOverride = item.ShortcutKeyDescription;
                    element.IsEnabled = item.IsEnabled;

                    flyout.Items.Add(element);
                }
                else if (item.Kind == CoreWebView2ContextMenuItemKind.Separator)
                {
                    flyout.CreateFlyoutSeparator();
                }
            }

            flyout.ShowAt(Navigation, args.Location);

            args.Handled = true;
        }

        private async void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            if (Uri.TryCreate(args.Uri, UriKind.Absolute, out Uri navigation))
            {
                args.Handled = true;
                await Launcher.LaunchUriAsync(navigation);
            }
        }

        private void OnSourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs args)
        {
            if (TonSite.TryUnmask(_clientService, sender.Source, out Uri navigation))
            {
                Header.Source = navigation;
            }

            if (Uri.TryCreate(sender.Source, UriKind.Absolute, out Uri source))
            {
                _sourceToFavicon[source] = sender.FaviconUri;
            }
        }

        private void OnHistoryChanged(CoreWebView2 sender, object args)
        {
            Header.CanGoBack = sender.CanGoBack;
            Header.CanGoForward = sender.CanGoForward;
        }

        private void OnDocumentTitleChanged(CoreWebView2 sender, object args)
        {
            _owner.Header = GetDocumentTitle(sender.DocumentTitle);
        }

        private void OnFaviconChanged(CoreWebView2 sender, object args)
        {
            if (Uri.TryCreate(sender.Source, UriKind.Absolute, out Uri source))
            {
                _sourceToFavicon[source] = sender.FaviconUri;
            }

            _owner.IconSource = GetFaviconSource1(sender.FaviconUri);
        }

        private string GetDocumentTitle(string title)
        {
            if (TonSite.TryUnmask(_clientService, title, out Uri navigation))
            {
                return navigation.Host + navigation.PathAndQuery + navigation.Fragment;
            }

            return title;
        }

        private Microsoft.UI.Xaml.Controls.IconSource GetFaviconSource1(string faviconUri)
        {
            if (Uri.TryCreate(faviconUri, UriKind.Absolute, out Uri favicon))
            {
                return new Microsoft.UI.Xaml.Controls.BitmapIconSource
                {
                    UriSource = favicon,
                    ShowAsMonochrome = false
                };
            }

            return new Microsoft.UI.Xaml.Controls.SymbolIconSource
            {
                Symbol = Symbol.World
            };
        }

        private Windows.UI.Xaml.Controls.IconElement GetFaviconSource2(string faviconUri)
        {
            if (Uri.TryCreate(faviconUri, UriKind.Absolute, out Uri favicon))
            {
                return new Windows.UI.Xaml.Controls.BitmapIcon
                {
                    UriSource = favicon,
                    ShowAsMonochrome = false
                };
            }

            return new Windows.UI.Xaml.Controls.FontIcon
            {
                Glyph = "\uE774",
                FontFamily = BootStrapper.Current.Resources["SymbolThemeFontFamily"] as FontFamily
            };
        }
    }
}
