//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Popups
{
    public sealed partial class QrCodePopup : ContentPopup
    {
        private readonly IClientService _clientService;

        private readonly ChatThemeViewModel _selectedTheme;

        private readonly DispatcherTimer _expiresTimer;
        private int _expiresIn;

        private readonly Dictionary<string, BackgroundFillFreeformGradient> _lightBackgrounds = new()
        {
            { "\uD83C\uDFE0", new BackgroundFillFreeformGradient( new int[]{ 0x71B654, 0x2C9077, 0x9ABB3E, 0x68B55E }) },
            { "\uD83D\uDC25", new BackgroundFillFreeformGradient( new int[]{ 0x43A371, 0x8ABD4C, 0x9DB139, 0x85B950 }) },
            { "\u26C4", new BackgroundFillFreeformGradient( new int[]{ 0x66A1FF, 0x59B5EE, 0x41BAD2, 0x8A97FF }) },
            { "\uD83D\uDC8E", new BackgroundFillFreeformGradient( new int[]{ 0x5198F5, 0x4BB7D2, 0xAD79FB, 0xDF86C7 }) },
            { "\uD83D\uDC68\u200D\uD83C\uDFEB", new BackgroundFillFreeformGradient( new int[]{ 0x9AB955, 0x48A896, 0x369ADD, 0x5DC67B }) },
            { "\uD83C\uDF37", new BackgroundFillFreeformGradient( new int[]{ 0xEE8044, 0xE19B23, 0xE55D93, 0xCB75D7 }) },
            { "\uD83D\uDC9C", new BackgroundFillFreeformGradient( new int[]{ 0xEE597E, 0xE35FB2, 0xAD69F2, 0xFF9257 }) },
            { "\uD83C\uDF84", new BackgroundFillFreeformGradient( new int[]{ 0xEC7046, 0xF79626, 0xE3761C, 0xF4AA2A }) },
            { "\uD83C\uDFAE", new BackgroundFillFreeformGradient( new int[]{ 0x19B3D2, 0xDC62F4, 0xE64C73, 0xECA222 }) },
        };

        private readonly Dictionary<string, BackgroundFillFreeformGradient> _darkBackgrounds = new()
        {
            { "\uD83C\uDFE0", new BackgroundFillFreeformGradient( new int[]{ 0x157FD1, 0x4A6CF2, 0x1876CD, 0x2CA6CE }) },
            { "\uD83D\uDC25", new BackgroundFillFreeformGradient( new int[]{ 0x57A518, 0x1E7650, 0x6D9B17, 0x3FAB55 }) },
            { "\u26C4", new BackgroundFillFreeformGradient( new int[]{ 0x2B6EDA, 0x2F7CB6, 0x1DA6C9, 0x6B7CFF }) },
            { "\uD83D\uDC8E", new BackgroundFillFreeformGradient( new int[]{ 0xB256B8, 0x6F52FF, 0x249AC2, 0x347AD5 }) },
            { "\uD83D\uDC68\u200D\uD83C\uDFEB", new BackgroundFillFreeformGradient( new int[]{ 0x238B68, 0x73A163, 0x15AC7F, 0x0E8C95 }) },
            { "\uD83C\uDF37", new BackgroundFillFreeformGradient( new int[]{ 0xD95454, 0xD2770F, 0xCE4661, 0xAC5FC8 }) },
            { "\uD83D\uDC9C", new BackgroundFillFreeformGradient( new int[]{ 0xD058AA, 0xE0743E, 0xD85568, 0xA369D3 }) },
            { "\uD83C\uDF84", new BackgroundFillFreeformGradient( new int[]{ 0xD6681F, 0xCE8625, 0xCE6D30, 0xC98A1D }) },
            { "\uD83C\uDFAE", new BackgroundFillFreeformGradient( new int[]{ 0xC74343, 0xEC7F36, 0x06B0F9, 0xA347FF }) },
        };

        public QrCodePopup(IClientService clientService, INavigationService navigationService, ISettingsService settingsService, User user)
            : this(clientService, navigationService, settingsService)
        {
            Photo.Source = ProfilePictureSource.User(clientService, user);

            if (user.HasActiveUsername(out string username))
            {
                Username.Text = string.Format("@{0}", username.ToUpper());
                InitializeCode(username);
            }
            else if (user.Id == clientService.Options.MyId)
            {
                InitializeToken();
            }
        }

        public QrCodePopup(IClientService clientService, INavigationService navigationService, ISettingsService settingsService, Chat chat)
            : this(clientService, navigationService, settingsService)
        {
            if (_clientService.TryGetSupergroup(chat, out Supergroup supergroup) && supergroup.HasActiveUsername(out string username))
            {
                Photo.Source = ProfilePictureSource.Chat(clientService, chat);

                Username.Text = string.Format("@{0}", username.ToUpper());
                InitializeCode(username);
            }
            else if (_clientService.TryGetUser(chat, out User user))
            {
                Photo.Source = ProfilePictureSource.User(clientService, user);

                if (user.HasActiveUsername(out username))
                {
                    Username.Text = string.Format("@{0}", username.ToUpper());
                    InitializeCode(username);
                }
                else if (user.Id == clientService.Options.MyId)
                {
                    InitializeToken();
                }
            }
        }

        public QrCodePopup(IClientService clientService, INavigationService navigationService, ISettingsService settingsService)
        {
            InitializeComponent();

            _clientService = clientService;

            _expiresTimer = new DispatcherTimer();
            _expiresTimer.Interval = TimeSpan.FromSeconds(1);
            _expiresTimer.Tick += OnTick;

            static Background GetDefaultBackground(bool dark)
            {
                var freeform = dark ? new[] { 0x6C7FA6, 0x2E344B, 0x7874A7, 0x333258 } : new[] { 0xDBDDBB, 0x6BA587, 0xD5D88D, 0x88B884 };
                return new Background(0, true, dark, string.Empty,
                    new Document(string.Empty, "application/x-tgwallpattern", null, null, TdExtensions.GetLocalFile("Assets\\Background.tgv", "Background")),
                    new BackgroundTypePattern(new BackgroundFillFreeformGradient(freeform), dark ? 100 : 50, dark, false));
            }

            var defaultLight = new ThemeSettings
            {
                AccentColor = 0x158DCD,
                OutgoingMessageAccentColor = 0xF0FDDF,
                OutgoingMessageFill = new BackgroundFillSolid(0xF0FDDF),
                Background = GetDefaultBackground(false)
            };

            var defaultDark = new ThemeSettings
            {
                AccentColor = 0x71BAFA,
                OutgoingMessageAccentColor = 0x2B5278,
                OutgoingMessageFill = new BackgroundFillSolid(0x2B5278),
                Background = GetDefaultBackground(true)
            };

            var defaultTheme = new ChatThemeViewModel(clientService, "\U0001F3E0", defaultLight, defaultDark, false);
            var themes = clientService.ChatThemes.Select(x => new ChatThemeViewModel(clientService, x, false));

            var items = new[] { defaultTheme }.Union(themes).ToList();

            _selectedTheme = themes.FirstOrDefault(x => x.AreTheSame(settingsService.Appearance.ChatTheme)) ?? defaultTheme;

            ScrollingHost.ItemsSource = items;
            ScrollingHost.SelectedItem = _selectedTheme;
            ScrollingHost.SelectionChanged += OnSelectionChanged;
        }

        private async void InitializeCode(string username)
        {
            var response = await _clientService.SendAsync(new GetInternalLink(new InternalLinkTypePublicChat(username, string.Empty, false), true));
            if (response is HttpUrl httpUrl)
            {
                var geometry = QrCode.CreateGeometry(httpUrl.Url, 3, 4, true);
                var visual = ElementComposition.GetElementVisual(Code);
                visual.Clip = visual.Compositor.CreateGeometricClip(visual.Compositor.CreatePathGeometry(geometry.Data));
            }
        }

        private async void InitializeToken()
        {
            var response = await _clientService.SendAsync(new GetUserLink());
            if (response is UserLink userLink)
            {
                var geometry = QrCode.CreateGeometry(userLink.Url, 3, 4, true);
                var visual = ElementComposition.GetElementVisual(Code);
                visual.Clip = visual.Compositor.CreateGeometricClip(visual.Compositor.CreatePathGeometry(geometry.Data));

                if (userLink.ExpiresIn != 0)
                {
                    _expiresIn = userLink.ExpiresIn;
                    _expiresTimer.Start();
                }
                else
                {
                    _expiresIn = 0;
                    _expiresTimer.Stop();
                }

                Username.Text = _expiresIn.ToDuration();
            }
        }

        private void OnTick(object sender, object e)
        {
            _expiresIn--;

            if (_expiresIn == 0)
            {
                _expiresTimer.Stop();
                InitializeToken();
            }

            Username.Text = _expiresIn.ToDuration();
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            Theme.IsChecked = sender.ActualTheme == ElementTheme.Dark;

            OnSelectionChanged(null, null);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.ItemContainer.ContentTemplateRoot is not ChatThemeCell content)
            {
                return;
            }

            if (args.InRecycleQueue)
            {
                content.Recycle();
                return;
            }
            else if (args.Item is ChatThemeViewModel theme)
            {
                content.Update(args.ItemContainer, theme);
                args.Handled = true;

                if (Code.Background == null)
                {
                    OnSelectionChanged(null, null);
                }
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScrollingHost.SelectedItem is ChatThemeViewModel theme)
            {
                var settings = LayoutRoot.ActualTheme == ElementTheme.Light
                    ? theme.LightSettings
                    : theme.DarkSettings;

                var backgrounds = LayoutRoot.ActualTheme == ElementTheme.Light
                    ? _lightBackgrounds
                    : _darkBackgrounds;

                Preview.UpdateSource(_clientService, settings.Background, false);

                if (theme.Type is ChatThemeEmoji emoji)
                {
                    Code.Background = backgrounds[emoji.Name].ToBrush();
                    Username.Foreground = backgrounds[emoji.Name].ToBrush();
                }
            }
        }

        private async void Theme_Click(object sender, RoutedEventArgs e)
        {
            if (PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                Transition.Visibility = Visibility.Collapsed;
                Theme.Visibility = Visibility.Collapsed;

                var visual = BootStrapper.Current.Compositor.CreateRedirectVisual(LayoutRoot, Vector2.Zero, LayoutRoot.ActualSize, true);
                await VisualUtilities.WaitForCompositionRenderedAsync();

                ElementCompositionPreview.SetElementChildVisual(Transition, visual);

                Transition.Visibility = Visibility.Visible;
                Theme.Visibility = Visibility.Visible;
                Theme.Foreground = new SolidColorBrush(LayoutRoot.ActualTheme != ElementTheme.Dark ? Windows.UI.Colors.White : Windows.UI.Colors.Black);

                var actualWidth = (float)ActualWidth;
                var actualHeight = (float)ActualHeight;

                var transform = Theme.TransformToVisual(LayoutRoot);
                var point = transform.TransformVector2();
                var diagonal = MathFEx.DistanceToFarthestCorner(point + Theme.ActualSize / 2, LayoutRoot.ActualSize);

                var expand = false; // ActualTheme == ElementTheme.Dark;

                var rect1 = CanvasGeometry.CreateRectangle(null, 0, 0, expand ? 0 : actualWidth, expand ? 0 : actualHeight);

                var elli1 = CanvasGeometry.CreateCircle(null, point.X + 24, point.Y + 24, expand ? 0 : diagonal);
                var group1 = CanvasGeometry.CreateGroup(null, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);

                var elli2 = CanvasGeometry.CreateCircle(null, point.X + 24, point.Y + 24, expand ? diagonal : 0);
                var group2 = CanvasGeometry.CreateGroup(null, new[] { elli2, rect1 }, CanvasFilledRegionDetermination.Alternate);

                var ellipse = visual.Compositor.CreatePathGeometry(new CompositionPath(group2));
                var clip = visual.Compositor.CreateGeometricClip(ellipse);

                visual.Clip = clip;

                var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (s, args) =>
                {
                    visual.Clip = null;
                    visual.Brush = visual.Compositor.CreateColorBrush(Windows.UI.Colors.Transparent);

                    ElementCompositionPreview.SetElementChildVisual(Transition, visual.Compositor.CreateSpriteVisual());

                    Transition.Visibility = Visibility.Collapsed;
                    Theme.Foreground = new SolidColorBrush(LayoutRoot.ActualTheme == ElementTheme.Dark ? Windows.UI.Colors.White : Windows.UI.Colors.Black);
                };

                CompositionEasingFunction ease;
                if (expand)
                {
                    ease = visual.Compositor.CreateCubicBezierEasingFunction(new Vector2(.42f, 0), new Vector2(1, 1));
                }
                else
                {
                    ease = visual.Compositor.CreateCubicBezierEasingFunction(new Vector2(0, 0), new Vector2(.58f, 1));
                }

                var anim = visual.Compositor.CreatePathKeyFrameAnimation();
                anim.InsertKeyFrame(0, new CompositionPath(group2), ease);
                anim.InsertKeyFrame(1, new CompositionPath(group1), ease);
                anim.Duration = TimeSpan.FromMilliseconds(500);

                ellipse.StartAnimation("Path", anim);
                batch.End();
            }

            LayoutRoot.RequestedTheme = LayoutRoot.ActualTheme == ElementTheme.Light
                ? ElementTheme.Dark
                : ElementTheme.Light;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private async void Share_Click(object sender, RoutedEventArgs e)
        {
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(RootGrid);
            var pixels = await bitmap.GetPixelsAsync();

            var width = (uint)bitmap.PixelWidth;
            var height = (uint)bitmap.PixelHeight;

            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, width, height, 96, 96, pixels.ToArray());
            await encoder.FlushAsync();

            var dataPackage = new DataPackage();
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
            ClipboardEx.TrySetContent(dataPackage);

            ToastPopup.Show(XamlRoot, Strings.ImageCopied, ToastPopupIcon.Copied);
        }
    }
}
