//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Xaml.Media;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Point = Windows.Foundation.Point;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseProfileColorView : UserControl
    {
        private IClientService _clientService;
        private MessageSender _sender;

        public ChooseProfileColorView()
        {
            InitializeComponent();
        }

        public string PrimaryButtonText { get; private set; }

        public void Initialize(IClientService clientService, MessageSender sender)
        { 
            //Title = Strings.UserColorTitle;

            _clientService = clientService;
            _sender = sender;

            var colors = clientService.GetAvailableProfileColors();
            List.ItemsSource = colors;

            var preview = ElementComposition.GetElementVisual(HeaderRoot);
            preview.Clip = preview.Compositor.CreateInsetClip();

            var customEmojiId = 0L;
            var accentColorId = 0;

            if (clientService.TryGetUser(sender, out User user))
            {
                customEmojiId = user.ProfileBackgroundCustomEmojiId;
                accentColorId = user.ProfileAccentColorId;

                Segments.UpdateSegments(140, false, true);
                Photo.SetUser(clientService, user, 140);

                Title.Text = user.FullName();
                Subtitle.Text = Strings.Online;

                Identity.SetStatus(clientService, user);

                BadgeText.Text = Strings.UserProfileIcon;
                Reset.Content = Strings.UserProfileColorReset;
                PrimaryButtonText = Strings.UserColorApplyIcon;
                ProfileColor.Footer = Strings.UserProfileHint;
            }
            else if (clientService.TryGetChat(sender, out Chat chat))
            {
                customEmojiId = chat.ProfileBackgroundCustomEmojiId;
                accentColorId = chat.ProfileAccentColorId;

                Segments.UpdateSegments(140, false, true);
                Photo.SetChat(clientService, chat, 140);

                Title.Text = chat.Title;

                var channel = false;
                if (clientService.TryGetSupergroup(chat, out Supergroup supergroup))
                {
                    channel = supergroup.IsChannel;
                    if (clientService.TryGetSupergroupFull(chat, out SupergroupFullInfo supergroupFull))
                    {
                        Subtitle.Text = Locale.Declension(supergroup.IsChannel ? Strings.R.Subscribers : Strings.R.Members, supergroupFull.MemberCount);
                    }
                    else
                    {
                        Subtitle.Text = Locale.Declension(supergroup.IsChannel ? Strings.R.Subscribers : Strings.R.Members, supergroup.MemberCount);
                    }
                }

                Identity.SetStatus(clientService, chat);

                BadgeText.Text = Strings.ChannelProfileLogo;
                Reset.Content = Strings.UserProfileColorReset;
                PrimaryButtonText = Strings.UserColorApplyIcon;
                ProfileColor.Footer = channel
                    ? Strings.ChannelProfileInfo
                    : Strings.GroupProfileInfo;
            }

            var accent = colors.FirstOrDefault(x => x.Id == accentColorId);

            if (customEmojiId != 0)
            {
                Badge.Badge = null;
                Badge.Glyph = string.Empty;

                Animated.Source = new CustomEmojiFileSource(clientService, customEmojiId);
                //Animated.ReplacementColor = new SolidColorBrush(accent.LightThemeColors[0]);

                var temp = accent?.ForTheme(_actualTheme);
                if (temp != null)
                {
                    Animated.ReplacementColor = new SolidColorBrush(temp.PaletteColors[0]);
                }
                else
                {
                    Animated.ReplacementColor = null;
                }
            }
            else
            {
                Badge.Badge = Strings.UserReplyIconOff;
                Badge.Glyph = Icons.Color;

                Animated.Source = null;
                Animated.ReplacementColor = null;
            }

            SelectedCustomEmojiId = customEmojiId;
            SelectedAccentColor = accent;

            List.SelectedItem = accent;
            UpdateProfileAccentColor(null, accent?.Id ?? -1, customEmojiId);

            Reset.Visibility = SelectedAccentColor == null
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.SizeChanged += NameColor_SizeChanged;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content
                && content.Children[0] is Polygon polygon
                && content.Children[1] is Rectangle rectangle
                && args.Item is ProfileColor colors)
            {
                content.Background = new SolidColorBrush(colors.LightThemeColors.PaletteColors[0]);
                polygon.Fill = colors.LightThemeColors.PaletteColors.Count > 1
                    ? new SolidColorBrush(colors.LightThemeColors.PaletteColors[1])
                    : null;

                rectangle.Fill = null;

                args.Handled = true;
            }
        }

        #endregion

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (List.SelectedItem is ProfileColor accent)
            {
                SelectedAccentColor = accent;

                var colors = accent.ForTheme(_actualTheme);
                Animated.ReplacementColor = new SolidColorBrush(colors.PaletteColors[0]);
                UpdateProfileAccentColor(null, accent.Id, SelectedCustomEmojiId);
            }
            else
            {
                SelectedAccentColor = null;
                UpdateProfileAccentColor(null, -1, SelectedCustomEmojiId);
            }

            Reset.Visibility = SelectedAccentColor == null
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void Badge_Click(object sender, RoutedEventArgs e)
        {
            var flyout = EmojiMenuFlyout.ShowAt(_clientService, EmojiDrawerMode.Background, Animated, EmojiFlyoutAlignment.TopRight);
            flyout.EmojiSelected += Flyout_EmojiSelected;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            SelectedCustomEmojiId = 0;
            List.SelectedItem = null;

            UpdateProfileAccentColor(null, SelectedAccentColor?.Id ?? -1, SelectedCustomEmojiId);

            Animated.Source = null;
            Badge.Badge = Strings.UserReplyIconOff;
        }

        private void Flyout_EmojiSelected(object sender, EmojiSelectedEventArgs e)
        {
            SelectedCustomEmojiId = e.CustomEmojiId;

            UpdateProfileAccentColor(null, SelectedAccentColor?.Id ?? -1, SelectedCustomEmojiId);

            if (e.CustomEmojiId != 0)
            {
                Animated.Source = new CustomEmojiFileSource(_clientService, SelectedCustomEmojiId);
                Badge.Badge = string.Empty;
            }
            else
            {
                Animated.Source = null;
                Badge.Badge = Strings.UserReplyIconOff;
            }
        }

        private void NameColor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is GridViewItem selector)
            {
                var width = e.NewSize.Width;
                var content = selector.ContentTemplateRoot as Grid;
                var polygon = content.Children[0] as Polygon;

                //content.Width = width;
                content.Height = width;

                content.CornerRadius = new CornerRadius(width / 2);
                polygon.Points = new PointCollection
                {
                    new Point(width, 0),
                    new Point(width, width),
                    new Point(0, width)
                };
            }
        }

        private ElementTheme _actualTheme;

        private void UpdateProfileAccentColor(Chat chat, int colorId, long customEmojiId)
        {
            _actualTheme = WindowContext.Current.ActualTheme;

            if (_clientService.TryGetProfileColor(colorId, out ProfileColor color))
            {
                var colors = color.ForTheme(_actualTheme);

                Identity.Foreground = new SolidColorBrush(Colors.White);

                HeaderRoot.RequestedTheme = ElementTheme.Dark;

                if (colors.BackgroundColors.Count > 1)
                {
                    var gradient = new LinearGradientBrush();
                    gradient.StartPoint = new Point(0, 0);
                    gradient.EndPoint = new Point(0, 1);
                    gradient.GradientStops.Add(new GradientStop
                    {
                        Color = colors.BackgroundColors[1],
                        Offset = 0
                    });

                    gradient.GradientStops.Add(new GradientStop
                    {
                        Color = colors.BackgroundColors[0],
                        Offset = 1
                    });

                    HeaderRoot.Background = gradient;
                }
                else
                {
                    HeaderRoot.Background = new SolidColorBrush(colors.BackgroundColors[0]);
                }

                Segments.TopColor = colors.StoryColors[0];
                Segments.BottomColor = colors.StoryColors[1];
                Segments.UpdateSegments(140, false, true);

                UpdateProfileBackgroundCustomEmoji(colors);
            }
            else
            {
                Identity.ClearValue(ForegroundProperty);

                HeaderRoot.ClearValue(Panel.BackgroundProperty);
                HeaderRoot.RequestedTheme = ElementTheme.Default;

                Segments.TopColor = null;
                Segments.BottomColor = null;
                Segments.UpdateSegments(140, false, true);

                UpdateProfileBackgroundCustomEmoji(null);
            }

            if (customEmojiId != 0)
            {
                Pattern.Source = new CustomEmojiFileSource(_clientService, customEmojiId);
            }
            else
            {
                Pattern.Source = null;
            }
        }

        private void UpdateProfileBackgroundCustomEmoji(ProfileColors color)
        {
            var compositor = Window.Current.Compositor;

            // Create a VisualSurface positioned at the same location as this control and feed that
            // through the color effect.
            var surfaceBrush = compositor.CreateSurfaceBrush();
            surfaceBrush.Stretch = CompositionStretch.None;
            var surface = compositor.CreateVisualSurface();

            // Select the source visual and the offset/size of this control in that element's space.
            surface.SourceVisual = ElementComposition.GetElementVisual(Pattern);
            surface.SourceOffset = new Vector2(0, 0);
            surface.SourceSize = new Vector2(1000, 320);
            surfaceBrush.Surface = surface;
            surfaceBrush.Stretch = CompositionStretch.None;

            CompositionBrush brush;
            if (color == null)
            {
                brush = compositor.CreateColorBrush(_actualTheme == ElementTheme.Light
                    ? Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)
                    : Color.FromArgb(0x09, 0xFF, 0xFF, 0xFF));
            }
            else if (color.BackgroundColors.Count > 1)
            {
                var linear = compositor.CreateLinearGradientBrush();
                linear.StartPoint = new Vector2();
                linear.EndPoint = new Vector2(0, 1);
                linear.ColorStops.Add(compositor.CreateColorGradientStop(0, color.BackgroundColors[1]));
                linear.ColorStops.Add(compositor.CreateColorGradientStop(1, color.BackgroundColors[0]));

                brush = linear;
            }
            else
            {
                brush = compositor.CreateColorBrush(color.BackgroundColors[0]);
            }

            var radial = compositor.CreateRadialGradientBrush();
            //radial.CenterPoint = new Vector2(0.5f, 0.0f);
            radial.EllipseCenter = new Vector2(0.5f, 0.3f);
            radial.EllipseRadius = new Vector2(0.4f, 0.6f);
            radial.ColorStops.Add(compositor.CreateColorGradientStop(0, Color.FromArgb(200, 0, 0, 0)));
            radial.ColorStops.Add(compositor.CreateColorGradientStop(1, Color.FromArgb(0, 0, 0, 0)));

            var blend = new BlendEffect
            {
                Background = new CompositionEffectSourceParameter("Background"),
                Foreground = new CompositionEffectSourceParameter("Foreground"),
                Mode = BlendEffectMode.SoftLight
            };

            var borderEffectFactory = Window.Current.Compositor.CreateEffectFactory(blend);
            var borderEffectBrush = borderEffectFactory.CreateBrush();
            borderEffectBrush.SetSourceParameter("Foreground", brush);
            borderEffectBrush.SetSourceParameter("Background", radial); // compositor.CreateColorBrush(Color.FromArgb(80, 0x00, 0x00, 0x00)));

            CompositionMaskBrush maskBrush = compositor.CreateMaskBrush();
            maskBrush.Source = borderEffectBrush; // Set source to content that is to be masked 
            maskBrush.Mask = surfaceBrush; // Set mask to content that is the opacity mask 

            var visual = compositor.CreateSpriteVisual();
            visual.Size = new Vector2(1000, 320);
            visual.Offset = new Vector3(0, 0, 0);
            visual.Brush = maskBrush;

            ElementCompositionPreview.SetElementChildVisual(HeaderGlow, visual);

            var radial2 = new RadialGradientBrush();
            //radial.CenterPoint = new Vector2(0.5f, 0.0f);
            radial2.Center = new Point(0.5f, 0.3f);
            radial2.RadiusX = 0.4;
            radial2.RadiusY = 0.6;
            radial2.GradientStops.Add(new GradientStop { Color = Color.FromArgb(50, 255, 255, 255) });
            radial2.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, 255, 255, 255), Offset = 1 });

            HeaderGlow.Background = radial2;
        }

        public int RequiredLevel
        {
            set
            {
                if (value > 0)
                {
                    BadgeInfo.Text = Icons.LockClosedFilled12 + Icons.Spacing + string.Format(Strings.BoostLevel, value);
                    BadgeInfo.Visibility = Visibility.Visible;
                }
                else
                {
                    BadgeInfo.Visibility = Visibility.Collapsed;
                }
            }
        }

        #region SelectedAccentColor

        public ProfileColor SelectedAccentColor
        {
            get { return (ProfileColor)GetValue(SelectedAccentColorProperty); }
            set { SetValue(SelectedAccentColorProperty, value); }
        }

        public static readonly DependencyProperty SelectedAccentColorProperty =
            DependencyProperty.Register("SelectedAccentColor", typeof(ProfileColor), typeof(ChooseProfileColorView), new PropertyMetadata(null));

        #endregion

        #region SelectedCustomEmojiId

        public long SelectedCustomEmojiId
        {
            get { return (long)GetValue(SelectedCustomEmojiIdProperty); }
            set { SetValue(SelectedCustomEmojiIdProperty, value); }
        }

        public static readonly DependencyProperty SelectedCustomEmojiIdProperty =
            DependencyProperty.Register("SelectedCustomEmojiId", typeof(long), typeof(ChooseProfileColorView), new PropertyMetadata(0L));

        #endregion
    }
}
