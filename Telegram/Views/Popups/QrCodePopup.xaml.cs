//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls;
using Telegram.Controls.Drawers;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Telegram.Views.Premium.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Popups
{
    public partial class QrShareColorOption
    {
        public QrShareColorOption(int id, Color top, Color bottom)
        {
            Id = id;
            Top = top;
            Bottom = bottom;

            var fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };

            fill.GradientStops.Add(new GradientStop { Color = top, Offset = 0 });
            fill.GradientStops.Add(new GradientStop { Color = bottom, Offset = 1 });

            Fill = fill;
        }

        // -1 for the default backdrop, which is not one of the server's.
        public int Id { get; }

        public Color Top { get; }

        public Color Bottom { get; }

        public Brush Fill { get; }
    }

    public sealed partial class QrCodePopup : ModalPopup, IIncrementalCollectionOwner
    {
        private const float CardWidth = 300;
        private const float NamedCardHeight = 336;
        private const float CodeSize = 222;

        // From the top of the card with a name; the square card centres the code instead.
        private const float CodeTop = 40;
        private const float ChipGap = 12;
        private const float ChipHeight = 24;

        // The highest the card goes while the sheet is open: right under the close button.
        private const float OpenCardTop = 56;

        // The card sits this much lower with no chip under it.
        private const float ChiplessShift = 16;

        // The card's size while the sheet is open, so the sheet gets the height it frees.
        private const float OpenCardScale = 0.8f;

        private const int MaxEmoji = 6;

        // The picks pill with its margins: 44 of buttons, 8 above and 8 below.
        private const double PicksHeight = 60;

        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly long _chatId;
        private readonly bool _self;

        private readonly DispatcherTimer _expiresTimer;
        private int _expiresIn;

        // The current user's own code with no username to show: a link that lasts a few minutes, on a square
        // card with no name, and no fee that can be lifted.
        private bool _temporary;
        private float _cardHeight = NamedCardHeight;

        // The plain link, and what the code shows now: the plain link, or one carrying a no-fee token.
        private string _url;
        private string _code;
        private bool _opened;
        private CompositionPath _logo;
        private readonly ThemeShadowEx _cardShadow;
        private readonly ThemeShadowEx[] _surfaceShadows;
        private bool _closed;

        // Hiding to open something else: the sheet goes down with the popup rather than in its place.
        private bool _leaving;

        private readonly DispatcherTimer _tokenTimer;
        private UserLink _token;
        private bool _canGetPermanentLink;
        private double _tokenExpires;
        private int _tokenLifetime;
        private bool _timer = true;
        private bool _requesting;

        // Moves on whenever the fee is restored, so a token request already on its way is dropped when it lands.
        private int _tokenGeneration;
        private long _starCount;
        private CompositionPropertySet _chipFill;

        // Under the emoji, and kept while a gift is on, so taking the gift off goes back to it.
        private QrShareColorOption _color = new(-1, QrShareColors.DefaultTop, QrShareColors.DefaultBottom);
        private UpgradedGift _gift;
        private string _receivedGiftId;
        private readonly List<QrShareEmoji> _emoji = new();

        // Changed since the last commit.
        private bool _dirty;

        private IncrementalCollection<ReceivedGift> _gifts;
        private string _giftsOffset = string.Empty;

        // How many gifts there are when it is three or fewer, -1 for more or not known yet.
        private int _fewGifts = -1;
        private bool _giftsEmptyLoaded;

        private float _restTop;
        private float _sheetHeight;
        private bool _sheetOpen;
        private int _tab;
        private ListViewBase _revealList;
        private CompositionRoundedRectangleGeometry _sheetGeometry;
        private CompositionColorSource _outlineBrush;
        private SpriteVisual _sheetShadow;
        private const float SheetShadowElevation = 16;
        private long _outlineToken;
        private CompositionScopedBatch _sheetBatch;

        private static readonly string[] BadgeNumbers = { "1", "2", "3", "4", "5", "6" };

        private readonly List<ContentControl> _badges = new();
        private ScrollViewer _emojiScroller;
        private bool _badgesQueued;
        private Rect _badgesViewport;

        public QrCodePopup(IClientService clientService, INavigationService navigationService, ISettingsService settingsService, User user)
            : this(clientService, navigationService, user.Id, user.Id == clientService.Options.MyId, clientService.TryGetUserFull(user.Id, out UserFullInfo fullInfo) ? fullInfo.ShareTheme : null)
        {
            if (user.HasActiveUsername(out string username))
            {
                Username.Text = string.Format("@{0}", username.ToUpper());
                InitializeCode(username);
                InitializeEditing();
            }
            else if (_self)
            {
                InitializeToken(user);
            }
        }

        public QrCodePopup(IClientService clientService, INavigationService navigationService, ISettingsService settingsService, Chat chat)
            : this(clientService, navigationService, chat.Id, clientService.TryGetUser(chat, out User self) && self.Id == clientService.Options.MyId, GetShareTheme(clientService, chat))
        {
            if (_clientService.TryGetSupergroup(chat, out Supergroup supergroup) && supergroup.HasActiveUsername(out string username))
            {
                Username.Text = string.Format("@{0}", username.ToUpper());
                InitializeCode(username);
            }
            else if (_clientService.TryGetUser(chat, out User user))
            {
                if (user.HasActiveUsername(out username))
                {
                    Username.Text = string.Format("@{0}", username.ToUpper());
                    InitializeCode(username);
                    InitializeEditing();
                }
                else if (_self)
                {
                    InitializeToken(user);
                }
            }
        }

        private QrCodePopup(IClientService clientService, INavigationService navigationService, long chatId, bool self, ProfileShareTheme theme)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _chatId = chatId;
            _self = self;

            _expiresTimer = new DispatcherTimer();
            _expiresTimer.Interval = TimeSpan.FromSeconds(1);
            _expiresTimer.Tick += OnTick;

            _tokenTimer = new DispatcherTimer();
            _tokenTimer.Interval = TimeSpan.FromSeconds(1);
            _tokenTimer.Tick += OnTokenTick;

            ElementCompositionPreview.SetIsTranslationEnabled(CardRoot, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ChipRoot, true);
            ElementCompositionPreview.SetIsTranslationEnabled(IslandIcons, true);

            _cardShadow = new ThemeShadowEx(CardShadow, 16);
            _surfaceShadows = new[]
            {
                new ThemeShadowEx(IslandSurface, 16),
                new ThemeShadowEx(PicksSurface, 16),
                new ThemeShadowEx(CloseSurface, 16),
                new ThemeShadowEx(ShareSurface, 16),
                new ThemeShadowEx(SaveSurface, 16)
            };

            // The open sheet shrinks the card towards its top, which stays where the sheet puts it.
            ElementComposition.GetElementVisual(CardScaler).CenterPoint = new Vector3(CardWidth / 2, 0, 0);

            SetCodeClip(_logo = new CompositionPath(CanvasGeometry.CreateCircle(null, CodeSize / 2, CodeSize / 2, 16)));
            UpdateCard();

            ApplyBackdrop(QrShareColors.DefaultTop, QrShareColors.DefaultBottom, false, QrShareColors.DefaultQr, false);
            InitializeTheme(theme);

            if (self)
            {
                InitializeFee();
            }
        }

        private static ProfileShareTheme GetShareTheme(IClientService clientService, Chat chat)
        {
            if (clientService.TryGetSupergroupFull(chat, out SupergroupFullInfo supergroupFullInfo))
            {
                return supergroupFullInfo.ShareTheme;
            }
            else if (clientService.TryGetUserFull(chat, out UserFullInfo userFullInfo))
            {
                return userFullInfo.ShareTheme;
            }

            return null;
        }

        // Only a chat with a username can have a theme set.
        private void InitializeEditing()
        {
            if (_self)
            {
                Island.Visibility = Visibility.Visible;
                IslandIcons.Visibility = Visibility.Visible;

                // Without Premium the gifts tab never opens.
                if (_clientService.IsPremium)
                {
                    InitializeGifts();
                }
            }
        }

        // Raised on the first layout pass, as the popup starts animating in.
        private void OnOpened(ModalPopup sender, ModalPopupOpenedEventArgs args)
        {
            _opened = true;

            if (_code != null && PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                CodeTransition.PlayIntro(_code);
            }
        }

        private void OnClosing(ModalPopup sender, ModalPopupClosingEventArgs args)
        {
            // Escape, back and a click away take the sheet down first.
            if (_sheetOpen && !_leaving)
            {
                args.Cancel = true;
                CloseSheet();
                return;
            }

            CommitTheme();
        }

        private void OnClosed(ModalPopup sender, ModalPopupClosedEventArgs args)
        {
            _closed = true;

            _expiresTimer.Stop();
            _expiresTimer.Tick -= OnTick;

            _tokenTimer.Stop();
            _tokenTimer.Tick -= OnTokenTick;

            _cardShadow.Detach();

            foreach (var shadow in _surfaceShadows)
            {
                shadow.Detach();
            }

            if (_sheetShadow != null)
            {
                SheetShadow.ActualThemeChanged -= OnSheetShadowThemeChanged;
            }

            if (_outlineBrush != null)
            {
                SheetRim.UnregisterPropertyChangedCallback(Border.BorderBrushProperty, _outlineToken);
                _outlineBrush.Unregister();
            }

            if (_emojiScroller != null)
            {
                EmojiPanel.ScrollingHost.ContainerContentChanging -= EmojiList_ContainerContentChanging;
                _emojiScroller.ViewChanged -= EmojiScroller_ViewChanged;
                _emojiScroller = null;
            }

            if (_badgesQueued)
            {
                _badgesQueued = false;
                EmojiRoot.LayoutUpdated -= OnBadgesLayoutUpdated;
            }

            QueueReveal(null);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ArrangeScene();
        }

        // The card and the chip are laid out where they sit with a chip under the card and the sheet closed;
        // everything else is their translation, which the sheet, the chip coming or going and the sheet's
        // height animate.
        private void ArrangeScene()
        {
            var width = LayoutRoot.ActualSize.X;
            var height = LayoutRoot.ActualSize.Y;

            if (height == 0)
            {
                return;
            }

            // Raised a little above the middle, so the chip under the card stays centred in what is left, and
            // kept on the 4 px grid the rest of the layout is on.
            _restTop = MathF.Round((height * 0.468f - _cardHeight / 2 - 16) / 4) * 4;

            CardRoot.Margin = new Thickness(0, _restTop, 0, 0);
            ChipRoot.Margin = new Thickness(0, _restTop + _cardHeight + ChipGap, 0, 0);

            Scene.Origin = new Vector2(width / 2, _restTop + _cardHeight / 2);

            // At its tallest the sheet ends 12 below the chip with the card at its highest.
            _sheetHeight = MathF.Max(160, height - 8 - (OpenCardTop + _cardHeight * OpenCardScale + ChipGap + ChipHeight + 12));
            Sheet.Height = SheetRim.Height = SheetShadow.Height = _sheetHeight;

            PlaceCard(false);
        }

        private bool HasChip => ChipRoot.Visibility == Visibility.Visible;

        // How tall the open sheet is for a tab: all of it, but just the row for three gifts or fewer, and
        // what the empty state needs for none.
        private float TabHeight(int tab)
        {
            if (tab == 2 && _fewGifts == 0)
            {
                return MathF.Min(_sheetHeight, 288);
            }
            else if (tab == 2 && _fewGifts > 0)
            {
                return MathF.Min(_sheetHeight, 144);
            }

            return _sheetHeight;
        }

        // The card's translation from where it is laid out. With the sheet open it rises only as far as the
        // sheet needs.
        private float CardOffset(bool open)
        {
            var rest = HasChip ? 0 : ChiplessShift;
            if (!open)
            {
                return rest;
            }

            var chip = HasChip ? ChipGap + ChipHeight : 0;
            var sheetTop = LayoutRoot.ActualSize.Y - 8 - TabHeight(_tab);
            var top = MathF.Max(OpenCardTop, MathF.Min(_restTop + rest, sheetTop - 12 - chip - _cardHeight * OpenCardScale));

            return Snap(top - _restTop);
        }

        // The chip stays under the card, which shrinks towards its top while the sheet is open.
        private float ChipOffset(bool open)
        {
            return Snap(CardOffset(open) - (open ? _cardHeight * (1 - OpenCardScale) : 0));
        }

        // A translation off the pixel grid resamples what it moves, and the chip's text goes soft.
        private float Snap(float value)
        {
            var scale = (float)(XamlRoot?.RasterizationScale ?? 1);
            return MathF.Round(value * scale) / scale;
        }

        // Moves the card and the chip to where they belong now, and an open sheet to its tab's height.
        private void PlaceCard(bool animated)
        {
            if (LayoutRoot.ActualSize.Y == 0)
            {
                return;
            }

            var card = ElementComposition.GetElementVisual(CardRoot);
            var chip = ElementComposition.GetElementVisual(ChipRoot);
            var compositor = card.Compositor;

            var offset = new Vector3(0, CardOffset(_sheetOpen), 0);
            var chipOffset = new Vector3(0, ChipOffset(_sheetOpen), 0);

            var width = LayoutRoot.ActualSize.X - 16;
            var tabHeight = TabHeight(_tab);
            var sheetOffset = new Vector2(0, _sheetHeight - tabHeight);
            var sheetSize = new Vector2(width, tabHeight);

            SheetContent.Margin = new Thickness(0, _sheetHeight - tabHeight, 0, 0);

            if (!animated || !PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                var scale = _sheetOpen ? OpenCardScale : 1;

                card.Properties.InsertVector3("Translation", offset);
                chip.Properties.InsertVector3("Translation", chipOffset);
                ElementComposition.GetElementVisual(CardScaler).Scale = new Vector3(scale, scale, 1);

                if (_sheetOpen && _sheetGeometry != null)
                {
                    _sheetGeometry.Offset = sheetOffset;
                    _sheetGeometry.Size = sheetSize;
                }

                return;
            }

            var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.65f, 0), new Vector2(0.35f, 1));
            var duration = TimeSpan.FromMilliseconds(320);

            var move = compositor.CreateVector3KeyFrameAnimation();
            move.InsertKeyFrame(1, offset, ease);
            move.Duration = duration;

            var chipMove = compositor.CreateVector3KeyFrameAnimation();
            chipMove.InsertKeyFrame(1, chipOffset, ease);
            chipMove.Duration = duration;

            card.StartAnimation("Translation", move);
            chip.StartAnimation("Translation", chipMove);

            if (_sheetOpen && _sheetGeometry != null)
            {
                var geometryOffset = compositor.CreateVector2KeyFrameAnimation();
                geometryOffset.InsertKeyFrame(1, sheetOffset, ease);
                geometryOffset.Duration = duration;

                var geometrySize = compositor.CreateVector2KeyFrameAnimation();
                geometrySize.InsertKeyFrame(1, sheetSize, ease);
                geometrySize.Duration = duration;

                _sheetGeometry.StartAnimation("Offset", geometryOffset);
                _sheetGeometry.StartAnimation("Size", geometrySize);
            }
        }

        #region Code

        private async void InitializeCode(string username)
        {
            var response = await _clientService.SendAsync(new GetInternalLink(new InternalLinkTypePublicChat(username, string.Empty, false, string.Empty), true));
            if (response is HttpUrl httpUrl && !_closed)
            {
                _url = httpUrl.Url;

                if (_token == null)
                {
                    SetCode(httpUrl.Url);
                }
            }
        }

        // With no username the link is the phone number's when the number is public, and otherwise one that
        // lasts a few minutes and is replaced as it runs out.
        private void InitializeToken(User user)
        {
            // Most numbers are not public: the card starts square rather than change shape as the link comes in.
            _temporary = true;
            Username.Text = user.FirstName.ToUpper();

            UpdateCard();
            RequestUserLink();
        }

        private async void RequestUserLink()
        {
            var response = await _clientService.SendAsync(new GetUserLink(false, false, false));
            if (_closed)
            {
                return;
            }
            else if (response is UserLink userLink)
            {
                _url = userLink.Url;
                _expiresIn = userLink.ExpiresIn;
                _canGetPermanentLink = userLink.CanGetPermanentLink;

                if (_expiresIn != 0)
                {
                    _expiresTimer.Start();
                }
                else
                {
                    _expiresTimer.Stop();
                }

                if (_temporary != (_expiresIn != 0))
                {
                    _temporary = _expiresIn != 0;
                    UpdateCard();
                }

                UpdateChip();

                if (_token == null)
                {
                    SetCode(userLink.Url);
                }
            }
        }

        private void OnTick(object sender, object e)
        {
            _expiresIn--;

            if (_expiresIn <= 0)
            {
                _expiresTimer.Stop();
                RequestUserLink();
            }
            else
            {
                UpdateChipText();
            }
        }

        // The card is square without a name under the code, the code then centred in it.
        private void UpdateCard()
        {
            _cardHeight = _temporary ? CardWidth : NamedCardHeight;

            var codeTop = _temporary ? (CardWidth - CodeSize) / 2 : CodeTop;

            CardRoot.Height = _cardHeight;
            Code.Margin = new Thickness(0, codeTop, 0, 0);
            Logo.Margin = new Thickness(0, codeTop + CodeSize / 2 - 16, 0, 0);
            UsernameRoot.Visibility = _temporary ? Visibility.Collapsed : Visibility.Visible;

            ElementComposition.GetElementVisual(CardRoot).CenterPoint = new Vector3(CardWidth / 2, _cardHeight / 2, 0);

            // In the transition's coordinates, which start 60 left of the card and 100 above it.
            CodeTransition.CardBounds = new Rect(60, 100, CardWidth, _cardHeight);
            CodeTransition.CodeBounds = new Rect(60 + (CardWidth - CodeSize) / 2, 100 + codeTop, CodeSize, CodeSize);

            ArrangeScene();
        }

        // The first code blooms in, every later one replaces the one before. While that runs the transition
        // draws the modules and the code itself keeps only the logo's circle.
        private void SetCode(string url)
        {
            if (url == null || url == _code)
            {
                return;
            }

            var previous = _code;
            _code = url;

            if (!PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                SetCodeClip(QrCode.CreateGeometry(url, 3, 4, true, CodeSize).Data);
                return;
            }

            SetCodeClip(_logo);

            if (!_opened)
            {
                return;
            }
            else if (previous == null)
            {
                CodeTransition.PlayIntro(url);
            }
            else if (CodeTransition.IsPlayingIntro)
            {
                CodeTransition.ReplaceIntro(url);
            }
            else
            {
                CodeTransition.PlaySwap(previous, url);
                Bounce(0.03f, 0.005f);
            }
        }

        private void CodeTransition_Completed(object sender, EventArgs e)
        {
            SetCodeClip(QrCode.CreateGeometry(_code, 3, 4, true, CodeSize).Data);
        }

        private void SetCodeClip(CompositionPath path)
        {
            var visual = ElementComposition.GetElementVisual(Code);
            visual.Clip = visual.Compositor.CreateGeometricClip(visual.Compositor.CreatePathGeometry(path));
        }

        private void Bounce(float up, float down)
        {
            if (!PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(CardRoot);
            var compositor = visual.Compositor;

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0.25f, new Vector3(1 + up, 1 + up, 1));
            scale.InsertKeyFrame(0.6f, new Vector3(1 - down, 1 - down, 1));
            scale.InsertKeyFrame(1, Vector3.One);
            scale.Duration = TimeSpan.FromMilliseconds(320);

            visual.StartAnimation("Scale", scale);
        }

        private void BounceChip(float up, float down)
        {
            if (!PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(ChipRoot);
            var compositor = visual.Compositor;

            visual.CenterPoint = new Vector3(ChipRoot.ActualSize / 2, 0);

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0.25f, new Vector3(1 + up, 1 + up, 1));
            scale.InsertKeyFrame(0.6f, new Vector3(1 - down, 1 - down, 1));
            scale.InsertKeyFrame(1, Vector3.One);
            scale.Duration = TimeSpan.FromMilliseconds(down > 0 ? 320 : 180);

            visual.StartAnimation("Scale", scale);
        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(CopyLink, Strings.CopyLink, Icons.Link);
            flyout.CreateFlyoutItem(CopyImage, Strings.CopyImage, Icons.Copy);
            flyout.ShowAt(ShareButton, FlyoutPlacementMode.TopEdgeAlignedLeft);
        }

        private void CopyLink()
        {
            if (_code != null)
            {
                MessageHelper.CopyLink(XamlRoot, _code);
            }
        }

        private async void CopyImage()
        {
            using var stream = new InMemoryRandomAccessStream();
            await RenderImageAsync(stream);

            var dataPackage = new DataPackage();
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
            ClipboardEx.TrySetContent(dataPackage);

            ToastPopup.Show(XamlRoot, Strings.ImageCopied, ToastPopupIcon.Copied);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                SuggestedFileName = "QR"
            };

            picker.FileTypeChoices.Add("PNG", new[] { ".png" });

            try
            {
                var file = await picker.PickSaveFileAsync(XamlRoot);
                if (file == null)
                {
                    return;
                }

                // Through the file itself: a picked file is reachable through the broker that granted it and
                // nothing else.
                using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                stream.Size = 0;

                await RenderImageAsync(stream);
            }
            catch
            {
                // The picker or the file can fail on their own; there is nothing to save then.
            }
        }

        // The scene and the card as they are on screen, as a PNG.
        private async Task RenderImageAsync(IRandomAccessStream stream)
        {
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(ShareRoot);
            var pixels = await bitmap.GetPixelsAsync();

            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
            await encoder.FlushAsync();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (_sheetOpen)
            {
                CloseSheet();
            }
            else
            {
                Hide();
            }
        }

        #endregion

        #region Message fee

        private async void InitializeFee()
        {
            // The current user's own fee is a privacy setting: UserFullInfo's count is what that user pays the
            // current one, which for the current user is always 0.
            var response = await _clientService.SendAsync(new GetNewChatPrivacySettings());
            if (response is NewChatPrivacySettings settings && settings.IncomingPaidMessageStarCount > 0 && !_closed)
            {
                _starCount = settings.IncomingPaidMessageStarCount;
                UpdateChip();
            }
        }

        private async void Chip_Click(object sender, RoutedEventArgs e)
        {
            if (_temporary)
            {
                return;
            }

            if (_token == null)
            {
                await RequestTokenAsync(false, true, Strings.QrNoPaidToastExpires, ToastPopupIcon.AutoRemoveOn);
            }
            else
            {
                ShowFeeFlyout();
            }
        }

        // Out of the chip's root, so the fill and its sparks go with the chip while the menu is open. The
        // root can't be rounded without clipping the sparks, so the chip's radius is passed instead.
        private void ShowFeeFlyout()
        {
            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ResetCode, Strings.QrNoPaidMenuReset, Icons.ArrowClockwise);

            // Some users can only have codes that expire.
            if (_canGetPermanentLink || !_timer)
            {
                flyout.Items.Add(new MenuFlyoutSubtitleItem
                {
                    Text = _timer ? Strings.QrNoPaidMenuDisableTimer : Strings.QrNoPaidMenuEnableTimer,
                    Subtitle = _timer ? Strings.QrNoPaidMenuDisableTimerInfo : Strings.QrNoPaidMenuEnableTimerInfo,
                    Icon = MenuFlyoutHelper.CreateIcon(_timer ? Icons.Timer : Icons.Clock),
                    Command = new RelayCommand(ToggleTimer)
                });
            }

            flyout.CreateFlyoutItem(RestoreFee, Strings.QrNoPaidMenuRestoreFee, Icons.Star);
            flyout.ShowMorphAt(ChipRoot, _sheetOpen ? FlyoutPlacementMode.Top : FlyoutPlacementMode.Bottom, cornerRadius: Chip.CornerRadius.TopLeft);
        }

        private async void ResetCode()
        {
            if (_timer)
            {
                await RequestTokenAsync(true, true, Strings.QrNoPaidToastExpires, ToastPopupIcon.AutoRemoveOn);
            }
            else
            {
                await RequestTokenAsync(true, false, Strings.QrNoPaidToastNewCode, ToastPopupIcon.Success);
            }
        }

        private async void ToggleTimer()
        {
            if (_timer)
            {
                await RequestTokenAsync(false, false, Strings.QrNoPaidToastNoTimeLimit, ToastPopupIcon.AutoRemoveOff);
            }
            else
            {
                await RequestTokenAsync(false, true, Strings.QrNoPaidToastExpires, ToastPopupIcon.AutoRemoveOn);
            }
        }

        private void RestoreFee()
        {
            _token = null;
            _tokenGeneration++;
            _tokenTimer.Stop();

            ChipSparks.Shatter();
            BounceChip(0.04f, 0);

            SetCode(_url);
            UpdateChip();

            // Revoking ends every temporary code, and the permanent one too when it is asked for, so nothing
            // handed out while the fee was off keeps working. The link that comes back is never shown.
            _clientService.Send(new GetUserLink(true, _canGetPermanentLink, true));

            ToastPopup.Show(XamlRoot, Strings.QrNoPaidToastFeeRestored, ToastPopupIcon.Success);
        }

        private async Task RequestTokenAsync(bool revoke, bool temporary, string toast, ToastPopupIcon icon)
        {
            if (_requesting)
            {
                return;
            }

            _requesting = true;
            var generation = _tokenGeneration;
            var response = await _clientService.SendAsync(new GetUserLink(true, !temporary, revoke));
            _requesting = false;

            if (_closed || generation != _tokenGeneration)
            {
                return;
            }
            else if (response is UserLink token)
            {
                var burst = _token == null;

                _token = token;
                _canGetPermanentLink = token.CanGetPermanentLink;
                _timer = token.ExpiresIn > 0;
                _tokenLifetime = token.ExpiresIn;
                _tokenExpires = _clientService.UnixTime + token.ExpiresIn;

                if (_timer)
                {
                    _tokenTimer.Start();
                }
                else
                {
                    _tokenTimer.Stop();
                }

                SetCode(token.Url);
                UpdateChip();

                ChipSparks.Burn((float)(_tokenExpires - _clientService.UnixTime), _tokenLifetime, _timer, burst);

                if (burst)
                {
                    BounceChip(0.06f, 0.02f);
                }

                ToastPopup.Show(XamlRoot, toast, icon);
            }
            else if (response is Error error)
            {
                ToastPopup.Show(XamlRoot, error.Message, ToastPopupIcon.Error);
            }
        }

        // The code is dead already, so nothing is revoked: the plain link comes back, and the fee with it.
        private void ExpireToken()
        {
            _token = null;
            _tokenGeneration++;
            _tokenTimer.Stop();

            SetCode(_url);
            UpdateChip();

            ToastPopup.Show(XamlRoot, Strings.QrNoPaidToastExpired, ToastPopupIcon.Info);
        }

        private void OnTokenTick(object sender, object e)
        {
            if (_token == null || !_timer)
            {
                _tokenTimer.Stop();
                return;
            }

            if (_tokenExpires - _clientService.UnixTime <= 0)
            {
                ExpireToken();
            }
            else
            {
                UpdateChipText();
            }
        }

        // A temporary link only says when it runs out: its fee can't be lifted. Otherwise the chip is there
        // when the user charges for messages, and offers to lift the fee or shows that it is lifted.
        private void UpdateChip()
        {
            var visible = _temporary || _starCount > 0;
            var changed = visible != HasChip;

            ChipRoot.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            Chip.IsHitTestVisible = !_temporary;
            Chip.IsTabStop = !_temporary;

            if (_temporary)
            {
                Chip.Padding = new Thickness(12, 0, 12, 0);
                ChipGlyph.Visibility = Visibility.Collapsed;
                ChipText.Margin = new Thickness(0);
                ChipFill.Visibility = Visibility.Collapsed;

                UpdateChipText();
            }
            else if (_token == null)
            {
                Chip.Padding = new Thickness(12, 0, 12, 0);
                ChipGlyph.Visibility = Visibility.Collapsed;
                ChipText.Margin = new Thickness(0);
                ChipText.Text = string.Format(Strings.QrNoPaidRemoveStarsFee.ReplaceStar(Icons.Star14), _starCount.ToString("N0"));
                ChipFill.Visibility = Visibility.Collapsed;
            }
            else
            {
                Chip.Padding = new Thickness(8, 0, 12, 0);
                ChipGlyph.Visibility = Visibility.Visible;
                ChipText.Margin = new Thickness(4, 0, 0, 0);
                ChipFill.Visibility = Visibility.Visible;

                UpdateChipText();
                UpdateChipFill();
            }

            if (changed)
            {
                PlaceCard(true);
            }

            if (changed && visible && PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                var visual = ElementComposition.GetElementVisual(ChipRoot);

                var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
                opacity.InsertKeyFrame(0, 0);
                opacity.InsertKeyFrame(1, 1);
                opacity.Duration = TimeSpan.FromMilliseconds(320);

                visual.StartAnimation("Opacity", opacity);
            }
        }

        private void UpdateChipText()
        {
            if (_temporary)
            {
                var seconds = Math.Max(0, _expiresIn);
                ChipText.Text = string.Format(Strings.QrNoPaidExpiresIn, string.Format("{0}:{1:D2}", seconds / 60, seconds % 60));
                return;
            }

            string text;
            if (_timer)
            {
                var seconds = (int)Math.Ceiling(Math.Max(0, _tokenExpires - _clientService.UnixTime));
                text = string.Format("{0} · {1}", Strings.QrNoPaidFree, string.Format(Strings.QrNoPaidExpiresIn, string.Format("{0}:{1:D2}", seconds / 60, seconds % 60)));
            }
            else
            {
                text = string.Format("{0} · {1}", Strings.QrNoPaidFree, Strings.QrNoPaidNoTimeLimit);
            }

            ChipText.Text = text;
            ChipFillText.Text = text;
        }

        // The fill sweeps in, then burns down from the right with the time the code has left, its end
        // torn. The clip reads the chip's own width, so it stays right as the text under it changes length,
        // and runs a few pixels past both ends, so the tear never shows on a fill that is all there or gone.
        private void UpdateChipFill()
        {
            var visual = ElementComposition.GetElementVisual(ChipFill);
            var compositor = visual.Compositor;

            if (_chipFill == null)
            {
                _chipFill = compositor.CreatePropertySet();
                _chipFill.InsertScalar("Fraction", 0);

                var edge = compositor.CreateExpressionAnimation("Vector2((host.Size.X + 6) * props.Fraction - 3, 0)");
                edge.SetReferenceParameter("host", visual);
                edge.SetReferenceParameter("props", _chipFill);

                var clip = compositor.CreateGeometricClip(compositor.CreatePathGeometry(QrChipSparks.CreateTornEdge(ChipHeight)));
                clip.StartAnimation("Offset", edge);

                visual.Clip = clip;
            }

            var sweep = compositor.CreateCubicBezierEasingFunction(new Vector2(0.33f, 1), new Vector2(0.68f, 1));
            var fill = compositor.CreateScalarKeyFrameAnimation();

            var remaining = _tokenExpires - _clientService.UnixTime;

            if (_timer && remaining > 0.34 && _tokenLifetime > 0)
            {
                var sweepEnd = (float)(0.34 / remaining);

                fill.InsertKeyFrame(sweepEnd, (float)((remaining - 0.34) / _tokenLifetime), sweep);
                fill.InsertKeyFrame(1, 0, compositor.CreateLinearEasingFunction());
                fill.Duration = TimeSpan.FromSeconds(remaining);
            }
            else
            {
                fill.InsertKeyFrame(1, _timer ? 0 : 1, sweep);
                fill.Duration = TimeSpan.FromMilliseconds(340);
            }

            _chipFill.StartAnimation("Fraction", fill);
        }

        #endregion

        private static double SpringStep(double t, double z, double period)
        {
            if (t <= 0)
            {
                return 0;
            }

            var w0 = 2 * Math.PI / period;
            var wd = w0 * Math.Sqrt(1 - z * z);

            return 1 - Math.Exp(-z * w0 * t) * (Math.Cos(wd * t) + z / Math.Sqrt(1 - z * z) * Math.Sin(wd * t));
        }

        #region Theme

        // Anyone's code is shown in their theme; only the current user can change their own here.
        private async void InitializeTheme(ProfileShareTheme theme)
        {
            int? backdropId = null;

            if (theme is ProfileShareThemeUpgradedGift themeGift)
            {
                SelectGift(themeGift.Gift, null, false);
            }
            else if (theme is ProfileShareThemeEmoji themeEmoji)
            {
                if (!themeEmoji.UseDefaultBackground)
                {
                    backdropId = themeEmoji.BackgroundBackdropId;
                }

                foreach (var type in themeEmoji.Emojis)
                {
                    if (_emoji.Count < MaxEmoji)
                    {
                        _emoji.Add(new QrShareEmoji(type, CreateSource(type)));
                    }
                }

                if (_emoji.Count > 0)
                {
                    UpdateEmoji(false);
                }
            }

            // The theme names its backdrop only by id.
            if (backdropId == null && !_self)
            {
                return;
            }

            var response = await _clientService.SendAsync(new GetProfileShareThemeBackdrops());
            if (_closed)
            {
                return;
            }

            var options = new List<QrShareColorOption>
            {
                _color
            };

            QrShareColorOption backdrop = null;

            if (response is ProfileShareThemeBackdrops backdrops)
            {
                foreach (var item in backdrops.Backdrops)
                {
                    var option = CreateColor(item);
                    options.Add(option);

                    if (item.Id == backdropId)
                    {
                        backdrop = option;
                    }
                }
            }

            if (_self)
            {
                ColorsList.ItemsSource = options;
                ColorsList.SelectedItem = _gift == null ? _color : null;
            }

            // Emoji added meanwhile still sit on the theme's backdrop, but a colour or a gift picked meanwhile wins.
            // Once the default backdrop has been seen, the theme's comes in as if it had just been picked.
            if (backdrop != null && _color.Id == -1 && _gift == null)
            {
                SelectColor(backdrop, _opened);
            }
        }

        // Laid top to bottom rather than around the card.
        private static QrShareColorOption CreateColor(ProfileShareThemeBackdrop backdrop)
        {
            return new QrShareColorOption(backdrop.Id, backdrop.Colors.CenterColor.ToColor(), QrShareColors.Deepen(backdrop.Colors.EdgeColor.ToColor()));
        }

        private void CommitTheme()
        {
            if (!_dirty)
            {
                return;
            }

            _dirty = false;

            InputProfileShareTheme theme;
            if (_receivedGiftId != null)
            {
                theme = new InputProfileShareThemeUpgradedGift(_receivedGiftId);
            }
            else if (_emoji.Count > 0 || _color.Id != -1)
            {
                var emojis = new EmojiType[_emoji.Count];

                for (int i = 0; i < emojis.Length; i++)
                {
                    emojis[i] = _emoji[i].Type;
                }

                theme = new InputProfileShareThemeEmoji(emojis, _color.Id == -1, Math.Max(0, _color.Id));
            }
            else
            {
                theme = null;
            }

            _clientService.Send(new SetProfileShareTheme(_chatId, theme));
        }

        private Vector2 CardCenter()
        {
            return new Vector2(LayoutRoot.ActualSize.X / 2, _restTop + CardOffset(_sheetOpen) + _cardHeight * (_sheetOpen ? OpenCardScale : 1) / 2);
        }

        // The controls take the theme whose text reads over the backdrop: light on a pale one, dark otherwise.
        private void ApplyBackdrop(Color center, Color edge, bool radial, Color ink, bool animated)
        {
            Scene.SetBackdrop(center, edge, radial, animated ? CardCenter() : null);
            SetInk(ink);

            var light = ColorEx.UseBlackText(new[] { ColorEx.ToHex(center), ColorEx.ToHex(edge) });
            LayoutRoot.RequestedTheme = light ? ElementTheme.Light : ElementTheme.Dark;

            if (animated)
            {
                Bounce(0.03f, 0.005f);
            }
        }

        private void SetInk(Color ink)
        {
            InkBrush.Color = ink;
            CodeTransition.Ink = ink;

            ChipFillForegroundBrush.Color = ink;
        }

        // The emoji stay where they are while the colour under them changes.
        private void SelectColor(QrShareColorOption option, bool animated)
        {
            var hadGift = _gift != null;

            ApplyColor(option, animated);

            // The gift's pattern leaves with the wave. Picking the gift dropped the emoji, so none come back.
            if (hadGift)
            {
                Scene.ClearPattern(animated, animated);
            }
        }

        private void ApplyColor(QrShareColorOption option, bool animated)
        {
            _color = option;
            _gift = null;
            _receivedGiftId = null;

            ColorsList.SelectedItem = option;
            GiftsList.SelectedItem = null;

            var ink = option.Id == -1
                ? QrShareColors.DefaultQr
                : QrShareColors.ContrastMatchedQr(option.Top, option.Bottom);

            ApplyBackdrop(option.Top, option.Bottom, false, ink, animated);
        }

        // A gift brings its own backdrop and pattern, so it replaces the emoji, and covers the colour until it
        // is taken off.
        private void SelectGift(UpgradedGift gift, string receivedGiftId, bool animated)
        {
            _gift = gift;
            _receivedGiftId = receivedGiftId;

            ColorsList.SelectedItem = null;

            if (_emoji.Count > 0)
            {
                _emoji.Clear();
                UpdatePicks();
            }

            var center = gift.Backdrop.Colors.CenterColor.ToColor();
            var edge = QrShareColors.Deepen(gift.Backdrop.Colors.EdgeColor.ToColor());
            var ink = QrShareColors.SymbolInk(gift.Backdrop.Colors.SymbolColor.ToColor(), center);

            ApplyBackdrop(center, edge, true, QrShareColors.ContrastMatchedQr(center, edge), animated);
            Scene.SetGiftPattern(_clientService, gift, ink, animated, animated);
        }

        private void ColorsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not QrShareColorOption option || (option.Id == _color.Id && _gift == null))
            {
                return;
            }

            _dirty = true;
            SelectColor(option, true);
        }

        private void GiftsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not ReceivedGift { Gift: SentGiftUpgraded upgraded } receivedGift)
            {
                return;
            }

            _dirty = true;

            // A second tap takes the gift off, back to the colour it covered.
            if (_gift?.Id == upgraded.Gift.Id)
            {
                SelectColor(_color, true);
            }
            else
            {
                GiftsList.SelectedItem = receivedGift;
                SelectGift(upgraded.Gift, receivedGift.ReceivedGiftId, true);
            }
        }

        private void GiftsList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            // The panel only exists once the list has been shown.
            if (sender.ItemsPanelRoot is ItemsWrapGrid panel && panel.ItemWidth != GiftWidth())
            {
                panel.ItemWidth = GiftWidth();
            }

            if (args.InRecycleQueue || args.ItemContainer.ContentTemplateRoot is not Grid content)
            {
                return;
            }

            if (args.Item is ReceivedGift { Gift: SentGiftUpgraded upgraded })
            {
                var pattern = content.Children[0] as PatternBackground;
                var animated = content.Children[1] as AnimatedImage;

                pattern.Update(_clientService, upgraded.Gift);
                animated.Source = new DelayedFileSource(_clientService, upgraded.Gift.Model.Sticker);

                if (_gift?.Id == upgraded.Gift.Id && sender.SelectedItem != args.Item)
                {
                    sender.SelectedItem = args.Item;
                }
            }

            args.Handled = true;
        }

        public async Task<IncrementalLoadResult> LoadMoreItemsAsync(uint count)
        {
            var response = await _clientService.SendAsync(new GetReceivedGifts(_clientService.MyId, 0, false, false, true, true, true, false, false, true, false, _giftsOffset, 50));
            if (response is ReceivedGifts gifts)
            {
                foreach (var gift in gifts.Gifts)
                {
                    if (gift.Gift is SentGiftUpgraded)
                    {
                        _gifts.Add(gift);
                    }
                }

                _giftsOffset = gifts.NextOffset;
                UpdateGifts(gifts.NextOffset.Length > 0);

                return new IncrementalLoadResult((uint)gifts.Gifts.Count, gifts.NextOffset.Length > 0);
            }

            // Not known to be empty: the sheet keeps its full height rather than show the empty state.
            return new IncrementalLoadResult(0, false);
        }

        // Fetched with the popup rather than with the tab, so the sheet opens at its height the first time.
        private void InitializeGifts()
        {
            _gifts = new IncrementalCollection<ReceivedGift>(this);
            GiftsList.ItemsSource = _gifts;

            _ = _gifts.LoadMoreItemsAsync(50);
        }

        private void UpdateGifts(bool more)
        {
            var few = more || _gifts.Count > 3 ? -1 : _gifts.Count;
            if (few == _fewGifts)
            {
                return;
            }

            _fewGifts = few;

            GiftsList.Visibility = few == 0 ? Visibility.Collapsed : Visibility.Visible;
            GiftsEmpty.Visibility = few == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (GiftsList.ItemsPanelRoot is ItemsWrapGrid panel)
            {
                panel.ItemWidth = GiftWidth();
            }

            if (few == 0)
            {
                LoadGiftsEmpty();
            }

            if (_tab == 2)
            {
                PlaceCard(true);
            }
        }

        // Three to a row, or a row shared by the gifts there are.
        private double GiftWidth()
        {
            return ColumnWidth(_fewGifts > 0 ? _fewGifts : 3);
        }

        // From the width the popup really has: its frame's border takes a pixel off each side, so cells sized
        // for 400 wrap a column early.
        private double ColumnWidth(int columns)
        {
            return (LayoutRoot.ActualSize.X - 16 - 24) / columns;
        }

        private void ColorsList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            // The panel only exists once the list has been shown.
            if (sender.ItemsPanelRoot is ItemsWrapGrid panel && panel.ItemWidth != ColumnWidth(6))
            {
                panel.ItemWidth = ColumnWidth(6);
            }
        }

        private async void LoadGiftsEmpty()
        {
            if (_giftsEmptyLoaded)
            {
                return;
            }

            _giftsEmptyLoaded = true;

            var response = await _clientService.SendAsync(new SearchStickerSet("Redo_edc21keMa", false));
            if (response is StickerSet set && set.Stickers.Count > 0)
            {
                GiftsEmptyAnimation.Source = new DelayedFileSource(_clientService, set.Stickers[0]);
            }
        }

        private async void BrowseGifts_Click(object sender, RoutedEventArgs e)
        {
            if (!_clientService.TryGetUser(_clientService.Options.MyId, out User user))
            {
                return;
            }

            _clientService.TryGetUserFull(user.Id, out UserFullInfo fullInfo);
            fullInfo ??= await _clientService.SendAsync(new GetUserFullInfo(user.Id)) as UserFullInfo;

            if (fullInfo != null)
            {
                // A popup only opens once this one has closed.
                _leaving = true;
                Hide();
                _navigationService.ShowPopup(new GiftPopup(_clientService, _navigationService, user, fullInfo, GiftGroupType.Collectibles));
            }
        }

        #endregion

        #region Emoji

        // Plain emoji are drawn from their animated versions.
        private AnimatedImageSource CreateSource(EmojiType type)
        {
            return type is EmojiTypeCustomEmoji custom
                ? new CustomEmojiFileSource(_clientService, custom.CustomEmojiId)
                : new AnimatedEmojiFileSource(_clientService, ((EmojiTypeRegular)type).Emoji);
        }

        private void Emoji_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerViewModel { FullType: StickerFullTypeCustomEmoji customEmoji })
            {
                // Without Premium only the free set can go in; one already picked can always come out.
                if (!_clientService.IsPremium && IndexOfEmoji(customEmoji.CustomEmojiId) < 0 && !EmojiPanel.ViewModel.IsDefaultEmoji(customEmoji.CustomEmojiId))
                {
                    ShowPremiumPromo(Strings.QrPremiumEmojiToast, new PremiumFeatureCustomEmoji());
                    return;
                }

                ToggleEmoji(new EmojiTypeCustomEmoji(customEmoji.CustomEmojiId));
            }
        }

        // The Premium screen would only open once this popup has closed, so subscribing closes it.
        private async void ShowPremiumPromo(string text, PremiumFeature feature)
        {
            var confirm = await ToastPopup.ShowActionAsync(XamlRoot, text, Strings.QrPremiumSubscribe, ToastPopupIcon.Premium);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            _leaving = true;
            Hide();

            if (feature != null)
            {
                _navigationService.ShowPromo(feature);
            }
            else
            {
                _navigationService.ShowPromo();
            }
        }

        // A toggle: a picked emoji comes out, a new one goes in, and a full set drops the oldest. The same
        // emoji never goes in twice.
        private void ToggleEmoji(EmojiTypeCustomEmoji type)
        {
            var index = IndexOfEmoji(type.CustomEmojiId);
            if (index >= 0)
            {
                _emoji.RemoveAt(index);
            }
            else
            {
                if (_emoji.Count >= MaxEmoji)
                {
                    _emoji.RemoveAt(0);
                }

                _emoji.Add(new QrShareEmoji(type, CreateSource(type)));
            }

            _dirty = true;
            UpdateEmoji(true);
        }

        private int IndexOfEmoji(long customEmojiId)
        {
            for (int i = 0; i < _emoji.Count; i++)
            {
                if (_emoji[i].Type is EmojiTypeCustomEmoji custom && custom.CustomEmojiId == customEmojiId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void Pick_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: QrShareEmoji emoji } && _emoji.IndexOf(emoji) is int index and >= 0)
            {
                _emoji.RemoveAt(index);
                _dirty = true;

                UpdateEmoji(true);
            }
        }

        // The emoji float on the colour, which is the default backdrop until one is picked. Emoji picked over
        // a gift take it off, back to the colour it covered, and come in with that colour's wave.
        private void UpdateEmoji(bool animated)
        {
            var hadGift = _emoji.Count > 0 && _gift != null;
            if (hadGift)
            {
                ApplyColor(_color, animated);
            }
            else if (animated && PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                var visual = ElementComposition.GetElementVisual(CardRoot);

                var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
                scale.InsertKeyFrame(0.45f, new Vector3(0.973f, 0.973f, 1));
                scale.InsertKeyFrame(1, Vector3.One);
                scale.Duration = TimeSpan.FromMilliseconds(360);

                visual.StartAnimation("Scale", scale);
            }

            UpdatePicks();

            if (_emoji.Count > 0)
            {
                Scene.SetEmojiPattern(_emoji.ToArray(), animated, hadGift && animated);
            }
            else
            {
                Scene.ClearPattern(animated, false);
            }
        }

        private void UpdatePicks()
        {
            PicksList.ItemsSource = _emoji.ToArray();
            Picks.Visibility = _emoji.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            // The pill floats over the list, which scrolls its last row out from under it.
            var padding = EmojiPanel.ScrollingHostPadding;
            EmojiPanel.ScrollingHostPadding = new Thickness(padding.Left, padding.Top, padding.Right, _emoji.Count > 0 ? PicksHeight : 0);

            QueueBadges();
        }

        private void EmojiList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            QueueBadges();
        }

        private void EmojiScroller_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (!e.IsIntermediate)
            {
                QueueBadges();
            }
        }

        // Once per layout pass at most, after it, when the cells have their final places.
        private void QueueBadges()
        {
            if (_badgesQueued || EmojiRoot.Visibility != Visibility.Visible)
            {
                return;
            }

            _badgesQueued = true;
            EmojiRoot.LayoutUpdated += OnBadgesLayoutUpdated;
        }

        private void OnBadgesLayoutUpdated(object sender, object e)
        {
            _badgesQueued = false;
            EmojiRoot.LayoutUpdated -= OnBadgesLayoutUpdated;

            UpdateBadges();
        }

        // Each picked emoji the list has a cell for gets its number in the top-right corner of the cell. Cells
        // move with the scrolling on the compositor; only realizing new ones or a new set of picks lands here.
        private void UpdateBadges()
        {
            var list = EmojiPanel.ScrollingHost;
            var panel = list?.ItemsPanelRoot;

            if (_emojiScroller == null && list != null)
            {
                _emojiScroller = list.GetScrollViewer();

                if (_emojiScroller != null)
                {
                    list.ContainerContentChanging += EmojiList_ContainerContentChanging;
                    _emojiScroller.ViewChanged += EmojiScroller_ViewChanged;

                    var follow = BootStrapper.Current.Compositor.CreateExpressionAnimation("Vector3(scroll.Translation.X, scroll.Translation.Y, 0)");
                    follow.SetReferenceParameter("scroll", ElementCompositionPreview.GetScrollViewerManipulationPropertySet(_emojiScroller));

                    ElementCompositionPreview.SetIsTranslationEnabled(PickBadgesContent, true);
                    ElementComposition.GetElementVisual(PickBadgesContent).StartAnimation("Translation", follow);
                }
            }

            var used = 0;

            if (_emojiScroller != null && panel != null && _emoji.Count > 0)
            {
                var origin = _emojiScroller.TransformToVisual(EmojiRoot).TransformPoint(new Point(0, 0));

                var viewport = new Rect(origin.X, origin.Y, _emojiScroller.ActualWidth, _emojiScroller.ActualHeight);
                if (viewport != _badgesViewport)
                {
                    _badgesViewport = viewport;

                    PickBadges.Margin = new Thickness(viewport.X, viewport.Y, 0, 0);
                    PickBadges.Width = viewport.Width;
                    PickBadges.Height = viewport.Height;
                    PickBadges.Clip = new RectangleGeometry { Rect = new Rect(0, 0, viewport.Width, viewport.Height) };
                }

                foreach (var child in panel.Children)
                {
                    if (child is not SelectorItem container || container.Visibility != Visibility.Visible)
                    {
                        continue;
                    }

                    // Not Content: the drawer fills its custom emoji containers itself, and those never get one.
                    var index = IndexOfPick(list.ItemFromContainer(container));
                    if (index < 0)
                    {
                        continue;
                    }

                    // In the list's content coordinates: where the cell is now, plus how far the list has scrolled.
                    var corner = container.TransformToVisual(_emojiScroller).TransformPoint(new Point(container.ActualWidth - 12, 12));
                    var x = corner.X + _emojiScroller.HorizontalOffset;
                    var y = corner.Y + _emojiScroller.VerticalOffset;

                    var badge = Badge(used++);
                    var number = BadgeNumbers[index];
                    var appearing = badge.Visibility != Visibility.Visible || !Equals(badge.Content, number);

                    badge.Content = number;
                    Canvas.SetLeft(badge, x - 8);
                    Canvas.SetTop(badge, y - 8);

                    if (appearing)
                    {
                        badge.Visibility = Visibility.Visible;
                        PopBadge(badge);
                    }
                }
            }

            for (int i = used; i < _badges.Count; i++)
            {
                _badges[i].Visibility = Visibility.Collapsed;
            }
        }

        private int IndexOfPick(object item)
        {
            return item is StickerViewModel { FullType: StickerFullTypeCustomEmoji customEmoji }
                ? IndexOfEmoji(customEmoji.CustomEmojiId)
                : -1;
        }

        private ContentControl Badge(int index)
        {
            while (_badges.Count <= index)
            {
                var badge = new ContentControl
                {
                    ContentTemplate = PickBadgeTemplate,
                    IsTabStop = false,
                    Visibility = Visibility.Collapsed
                };

                PickBadgesContent.Children.Add(badge);
                _badges.Add(badge);
            }

            return _badges[index];
        }

        private static void PopBadge(UIElement badge)
        {
            if (!PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(badge);
            var compositor = visual.Compositor;
            var linear = compositor.CreateLinearEasingFunction();

            visual.CenterPoint = new Vector3(8, 8, 0);

            var scale = compositor.CreateVector3KeyFrameAnimation();

            for (int i = 0; i <= 16; i++)
            {
                var t = i / 16f * 0.6f;
                var s = 0.6f + 0.4f * (float)SpringStep(t, 0.55, 0.3 * Math.Sqrt(1 - 0.55 * 0.55));

                scale.InsertKeyFrame(i / 16f, new Vector3(s, s, 1), linear);
            }

            scale.Duration = TimeSpan.FromSeconds(0.6);
            visual.StartAnimation("Scale", scale);
        }

        #endregion

        #region Sheet

        private void Colors_Click(object sender, RoutedEventArgs e)
        {
            ShowTab(0);
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            ShowTab(1);
        }

        private void Gifts_Click(object sender, RoutedEventArgs e)
        {
            if (_clientService.IsPremium)
            {
                ShowTab(2);
            }
            else
            {
                ShowPremiumPromo(Strings.QrPremiumGiftsToast, null);
            }
        }

        private void SheetDismiss_Tapped(object sender, TappedRoutedEventArgs e)
        {
            CloseSheet();
        }

        private void ShowTab(int tab)
        {
            _tab = tab;

            ColorsList.Visibility = tab == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmojiRoot.Visibility = tab == 1 ? Visibility.Visible : Visibility.Collapsed;
            GiftsRoot.Visibility = tab == 2 ? Visibility.Visible : Visibility.Collapsed;

            if (tab == 1)
            {
                if (EmojiPanel.DataContext == null)
                {
                    EmojiPanel.DataContext = EmojiDrawerViewModel.Create(_clientService.Session, EmojiDrawerMode.ShareTheme);
                }

                EmojiPanel.ViewModel.Update();
                QueueBadges();
            }

            if (!_sheetOpen)
            {
                _sheetOpen = true;

                Sheet.Visibility = Visibility.Visible;
                SheetRim.Visibility = Visibility.Visible;
                SheetShadow.Visibility = Visibility.Visible;
                SheetDismiss.Visibility = Visibility.Visible;

                IslandIcons.IsHitTestVisible = false;
                ShareButton.IsHitTestVisible = false;
                SaveButton.IsHitTestVisible = false;

                AnimateSheet(true);
            }
        }

        private void CloseSheet()
        {
            if (!_sheetOpen)
            {
                return;
            }

            _sheetOpen = false;

            SheetDismiss.Visibility = Visibility.Collapsed;

            IslandIcons.IsHitTestVisible = true;
            ShareButton.IsHitTestVisible = true;
            SaveButton.IsHitTestVisible = true;

            AnimateSheet(false);
            CommitTheme();
        }

        // The island grows into the sheet: one surface whose clip morphs between the two. What follows the
        // morph is sampled from the same curve, so it all moves as one.
        private void AnimateSheet(bool open)
        {
            var visual = ElementComposition.GetElementVisual(Sheet);
            var compositor = visual.Compositor;

            var width = LayoutRoot.ActualSize.X - 16;
            var height = _sheetHeight;

            if (_sheetGeometry == null)
            {
                // The island and the open sheet share one corner radius, so the morph only moves and sizes it.
                _sheetGeometry = compositor.CreateRoundedRectangleGeometry();
                _sheetGeometry.CornerRadius = new Vector2(20);
                visual.Clip = compositor.CreateGeometricClip(_sheetGeometry);

                ElementCompositionPreview.SetElementChildVisual(SheetRim, QrGlassRim.CreateOutline(compositor, _sheetGeometry, OutlineBrush(), 1));
                ElementCompositionPreview.SetElementChildVisual(SheetShadow, CreateSheetShadow(compositor));
            }

            // The island, in the sheet's coordinates: 120 x 40, centred, on the sheet's bottom edge.
            var islandOffset = new Vector2((width - 120) / 2, height - 40);
            var islandSize = new Vector2(120, 40);

            var tabHeight = TabHeight(_tab);
            var sheetOffset = new Vector2(0, height - tabHeight);
            var sheetSize = new Vector2(width, tabHeight);
            var translation = CardOffset(open);
            var chipTranslation = ChipOffset(open);
            var cardScale = open ? OpenCardScale : 1;

            SheetContent.Margin = new Thickness(0, height - tabHeight, 0, 0);

            var content = ElementComposition.GetElementVisual(SheetContent);
            var card = ElementComposition.GetElementVisual(CardRoot);
            var scaler = ElementComposition.GetElementVisual(CardScaler);
            var chip = ElementComposition.GetElementVisual(ChipRoot);
            var icons = ElementComposition.GetElementVisual(IslandIcons);
            var share = ElementComposition.GetElementVisual(ShareSurface);
            var save = ElementComposition.GetElementVisual(SaveSurface);

            share.CenterPoint = new Vector3(20, 20, 0);
            save.CenterPoint = new Vector3(20, 20, 0);

            // The sheet takes the island's place on the same frame, in the same shape; the island
            // comes back the same way once the sheet has shrunk onto it, in OnSheetCompleted.
            if (open)
            {
                Island.Opacity = 0;
            }

            if (!PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                _sheetGeometry.Offset = open ? sheetOffset : islandOffset;
                _sheetGeometry.Size = open ? sheetSize : islandSize;

                content.Opacity = open ? 1 : 0;
                content.Scale = Vector3.One;
                card.Properties.InsertVector3("Translation", new Vector3(0, translation, 0));
                chip.Properties.InsertVector3("Translation", new Vector3(0, chipTranslation, 0));
                scaler.Scale = new Vector3(cardScale, cardScale, 1);
                icons.Opacity = open ? 0 : 1;
                share.Opacity = save.Opacity = open ? 0 : 1;

                Sheet.Visibility = SheetRim.Visibility = SheetShadow.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
                Island.Opacity = open ? 0 : 1;
                return;
            }

            var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));
            var linear = compositor.CreateLinearEasingFunction();
            var duration = TimeSpan.FromMilliseconds(open ? 420 : 360);

            var offset = compositor.CreateVector2KeyFrameAnimation();
            var size = compositor.CreateVector2KeyFrameAnimation();

            if (open)
            {
                offset.InsertKeyFrame(0, islandOffset);
                size.InsertKeyFrame(0, islandSize);
            }

            offset.InsertKeyFrame(1, open ? sheetOffset : islandOffset, ease);
            size.InsertKeyFrame(1, open ? sheetSize : islandSize, ease);
            offset.Duration = size.Duration = duration;

            var move = compositor.CreateVector3KeyFrameAnimation();
            move.InsertKeyFrame(1, new Vector3(0, translation, 0), ease);
            move.Duration = duration;

            var chipMove = compositor.CreateVector3KeyFrameAnimation();
            chipMove.InsertKeyFrame(1, new Vector3(0, chipTranslation, 0), ease);
            chipMove.Duration = duration;

            // The card shrinks towards its top on the morph's curve, so the chip under it and the sheet's
            // edge move as one with it.
            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(1, new Vector3(cardScale, cardScale, 1), ease);
            scale.Duration = duration;

            // Laid out at its final size and revealed by the morph, on a clock of its own so the grow is felt.
            // On the way out it fades first and fast, with no long faint tail.
            var contentOpacity = compositor.CreateScalarKeyFrameAnimation();
            var contentScale = compositor.CreateVector3KeyFrameAnimation();

            content.CenterPoint = new Vector3(width / 2, tabHeight, 0);

            if (open)
            {
                var grow = _tab == 0 ? 0.8f : 0.75f;
                var reveal = compositor.CreateCubicBezierEasingFunction(new Vector2(0.33f, 1), new Vector2(0.68f, 1));

                contentOpacity.InsertKeyFrame(0, 0);
                contentOpacity.InsertKeyFrame(1, 1, ease);
                contentOpacity.Duration = TimeSpan.FromMilliseconds(340);

                contentScale.InsertKeyFrame(0, new Vector3(grow, grow, 1));
                contentScale.InsertKeyFrame(1, Vector3.One, reveal);
                contentScale.Duration = TimeSpan.FromMilliseconds(420);

                QueueReveal(_tab == 0 ? ColorsList : _tab == 2 ? GiftsList : null);
            }
            else
            {
                for (int i = 1; i <= 8; i++)
                {
                    var u = i / 8f;
                    var fade = MathF.Pow(1 - u, 1.7f);
                    var shrink = 0.96f + 0.04f * fade;

                    contentOpacity.InsertKeyFrame(u, fade, linear);
                    contentScale.InsertKeyFrame(u, new Vector3(shrink, shrink, 1), linear);
                }

                contentOpacity.Duration = contentScale.Duration = TimeSpan.FromMilliseconds(170);
            }

            // Sampled against m, how far open the sheet is.
            var iconsOpacity = compositor.CreateScalarKeyFrameAnimation();
            var iconsMove = compositor.CreateVector3KeyFrameAnimation();
            var sideOpacity = compositor.CreateScalarKeyFrameAnimation();
            var sideScale = compositor.CreateVector3KeyFrameAnimation();

            for (int i = 0; i <= 20; i++)
            {
                var t = i / 20f;
                var m = open ? SheetEase(t) : 1 - SheetEase(t);

                var iconsAlpha = 1 - Math.Clamp(m / 0.35f, 0, 1);
                var sideAlpha = 1 - Math.Clamp(m * 2.2f, 0, 1);
                var sideSize = 0.6f + 0.4f * sideAlpha;

                iconsOpacity.InsertKeyFrame(t, iconsAlpha, linear);
                iconsMove.InsertKeyFrame(t, new Vector3(0, -10 * m, 0), linear);
                sideOpacity.InsertKeyFrame(t, sideAlpha, linear);
                sideScale.InsertKeyFrame(t, new Vector3(sideSize, sideSize, 1), linear);
            }

            iconsOpacity.Duration = iconsMove.Duration = sideOpacity.Duration = sideScale.Duration = duration;

            _sheetBatch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _sheetBatch.Completed += OnSheetCompleted;

            _sheetGeometry.StartAnimation("Offset", offset);
            _sheetGeometry.StartAnimation("Size", size);

            card.StartAnimation("Translation", move);
            chip.StartAnimation("Translation", chipMove);
            scaler.StartAnimation("Scale", scale);

            content.StartAnimation("Opacity", contentOpacity);
            content.StartAnimation("Scale", contentScale);

            icons.StartAnimation("Opacity", iconsOpacity);
            icons.StartAnimation("Translation", iconsMove);
            share.StartAnimation("Opacity", sideOpacity);
            save.StartAnimation("Opacity", sideOpacity);
            share.StartAnimation("Scale", sideScale);
            save.StartAnimation("Scale", sideScale);

            _sheetBatch.End();
        }

        // On the layout pass after the sheet starts opening, when a list shown for the first time has its cells.
        private void QueueReveal(ListViewBase list)
        {
            if (_revealList != null)
            {
                _revealList.LayoutUpdated -= OnRevealLayoutUpdated;
            }

            _revealList = list;

            if (list != null)
            {
                list.LayoutUpdated += OnRevealLayoutUpdated;
            }
        }

        private void OnRevealLayoutUpdated(object sender, object e)
        {
            var list = _revealList;
            QueueReveal(null);

            RevealItems(list);
        }

        // Each swatch or gift in sight grows on its own too, row by row.
        private static void RevealItems(ListViewBase list)
        {
            if (list?.ItemsPanelRoot is not Panel panel)
            {
                return;
            }

            var compositor = ElementComposition.GetElementVisual(list).Compositor;

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector3(0.82f, 0.82f, 1));
            scale.InsertKeyFrame(1, Vector3.One, compositor.CreateCubicBezierEasingFunction(new Vector2(0.33f, 1), new Vector2(0.68f, 1)));
            scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            scale.Duration = TimeSpan.FromMilliseconds(300);

            foreach (var child in panel.Children)
            {
                if (child is not SelectorItem container || container.ActualHeight == 0)
                {
                    continue;
                }

                var top = container.TransformToVisual(list).TransformPoint(new Point()).Y;
                if (top > list.ActualHeight || top + container.ActualHeight < 0)
                {
                    continue;
                }

                var visual = ElementComposition.GetElementVisual(container);
                visual.CenterPoint = new Vector3(container.ActualSize / 2, 0);

                scale.DelayTime = TimeSpan.FromMilliseconds(12 * Math.Clamp(Math.Floor(top / container.ActualHeight), 0, 10));
                visual.StartAnimation("Scale", scale);
            }
        }

        // The drop shadow ThemeShadowEx draws, as a sprite kept on the sheet's shape grown by the shadow's
        // insets: the shape morphs, but its radius doesn't, so the same nine grid fits it all the way. The
        // elevation is the island's, so the shadow doesn't jump as the island hands over to the sheet.
        private SpriteVisual CreateSheetShadow(Compositor compositor)
        {
            var insets = ThemeShadowEx.GetInsets(SheetShadowElevation);

            _sheetShadow = compositor.CreateSpriteVisual();
            _sheetShadow.Brush = ThemeShadowEx.CreateBrush(compositor, SheetShadowElevation, SheetShadow.ActualTheme, 20, 20);

            var offset = compositor.CreateExpressionAnimation("Vector3(g.Offset.X - l, g.Offset.Y - t, 0)");
            offset.SetReferenceParameter("g", _sheetGeometry);
            offset.SetScalarParameter("l", (float)insets.Left);
            offset.SetScalarParameter("t", (float)insets.Top);

            var size = compositor.CreateExpressionAnimation("g.Size + Vector2(l + r, t + b)");
            size.SetReferenceParameter("g", _sheetGeometry);
            size.SetScalarParameter("l", (float)insets.Left);
            size.SetScalarParameter("t", (float)insets.Top);
            size.SetScalarParameter("r", (float)insets.Right);
            size.SetScalarParameter("b", (float)insets.Bottom);

            _sheetShadow.StartAnimation("Offset", offset);
            _sheetShadow.StartAnimation("Size", size);

            SheetShadow.ActualThemeChanged += OnSheetShadowThemeChanged;
            return _sheetShadow;
        }

        // The recipe differs between the themes, so a flip takes a new brush.
        private void OnSheetShadowThemeChanged(FrameworkElement sender, object args)
        {
            _sheetShadow.Brush = ThemeShadowEx.CreateBrush(_sheetShadow.Compositor, SheetShadowElevation, sender.ActualTheme, 20, 20);
        }

        // The sheet and the menu morph, so their borders are drawn along the shapes from code, in the brush the
        // markup gives SheetRim. A theme change swaps the brush rather than its colour, so the brush is watched.
        private CompositionColorBrush OutlineBrush()
        {
            if (_outlineBrush == null)
            {
                _outlineBrush = new CompositionColorSource(SheetRim.BorderBrush, true);
                _outlineToken = SheetRim.RegisterPropertyChangedCallback(Border.BorderBrushProperty, OnOutlineBrushChanged);
            }

            return _outlineBrush;
        }

        private void OnOutlineBrushChanged(DependencyObject sender, DependencyProperty dp)
        {
            _outlineBrush.PropertyChanged(SheetRim.BorderBrush as SolidColorBrush, true);
        }

        // cubic-bezier(0.16, 1, 0.3, 1), the curve the sheet's own animations run on.
        private static float SheetEase(float x)
        {
            float lo = 0;
            float hi = 1;
            float u = x;

            for (int i = 0; i < 16; i++)
            {
                u = (lo + hi) / 2;

                var bx = 3 * (1 - u) * (1 - u) * u * 0.16f + 3 * (1 - u) * u * u * 0.3f + u * u * u;
                if (bx < x)
                {
                    lo = u;
                }
                else
                {
                    hi = u;
                }
            }

            return 3 * (1 - u) * (1 - u) * u + 3 * (1 - u) * u * u + u * u * u;
        }

        private void OnSheetCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (sender is CompositionScopedBatch batch)
            {
                batch.Completed -= OnSheetCompleted;
            }

            if (sender == _sheetBatch && !_sheetOpen)
            {
                Sheet.Visibility = Visibility.Collapsed;
                SheetRim.Visibility = Visibility.Collapsed;
                SheetShadow.Visibility = Visibility.Collapsed;
                Island.Opacity = 1;
            }
        }

        #endregion
    }
}
