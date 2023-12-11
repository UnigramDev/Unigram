//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Gallery;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Point = Windows.Foundation.Point;

namespace Telegram.Controls
{
    public class ProfileHeaderPattern : Control
    {
        public ProfileHeaderPattern()
        {
            DefaultStyleKey = typeof(ProfileHeaderPattern);
        }

        protected override void OnApplyTemplate()
        {
            var animated = GetTemplateChild("Animated") as AnimatedImage;
            var layoutRoot = GetTemplateChild("LayoutRoot") as Border;

            animated.Ready += OnReady;

            var visual = ElementCompositionPreview.GetElementVisual(animated);
            var compositor = visual.Compositor;

            // Create a VisualSurface positioned at the same location as this control and feed that
            // through the color effect.
            var surfaceBrush = compositor.CreateSurfaceBrush();
            var surface = compositor.CreateVisualSurface();

            // Select the source visual and the offset/size of this control in that element's space.
            surface.SourceVisual = visual;
            surface.SourceOffset = new Vector2(0, 0);
            surface.SourceSize = new Vector2(37, 37);
            surfaceBrush.HorizontalAlignmentRatio = 0.5f;
            surfaceBrush.VerticalAlignmentRatio = 0.5f;
            surfaceBrush.Surface = surface;
            surfaceBrush.Stretch = CompositionStretch.Fill;
            surfaceBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;
            surfaceBrush.SnapToPixels = true;

            var container = compositor.CreateContainerVisual();
            container.Size = new Vector2(1000, 320);

            var clones = Generate(0);

            for (int i = 1; i < clones.Count; i++)
            {
                Vector4 clone = clones[i];

                var redirect = compositor.CreateSpriteVisual();
                redirect.Size = new Vector2(clone.Z);
                redirect.Offset = new Vector3(clone.X, clone.Y, 0);
                redirect.CenterPoint = new Vector3(clone.Z / 2);
                redirect.Opacity = clone.W;
                redirect.Brush = surfaceBrush;

                container.Children.InsertAtTop(redirect);
            }

            ElementCompositionPreview.SetElementChildVisual(layoutRoot, container);
        }

        private void OnReady(object sender, EventArgs e)
        {
            var layoutRoot = GetTemplateChild("LayoutRoot") as Border;
            var container = ElementCompositionPreview.GetElementChildVisual(layoutRoot) as ContainerVisual;

            var scale = container.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, Vector3.Zero);
            scale.InsertKeyFrame(1, Vector3.One);

            var batch = container.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            foreach (var redirect in container.Children)
            {
                redirect.StartAnimation("Scale", scale);
            }

            batch.End();
        }

        public void Update(float avatarTransitionFraction)
        {
            var layoutRoot = GetTemplateChild("LayoutRoot") as Border;
            var container = ElementCompositionPreview.GetElementChildVisual(layoutRoot) as ContainerVisual;

            var clones = Generate(avatarTransitionFraction);
            var i = 0;

            foreach (var redirect in container.Children)
            {
                Vector4 clone = clones[i++];

                redirect.Size = new Vector2(clone.Z);
                redirect.Offset = new Vector3(clone.X, clone.Y, 0);
                redirect.Opacity = clone.W;
            }
        }

        private float windowFunction(float t)
        {
            return BezierPoint.Calculate(0.6f, 0.0f, 0.4f, 1.0f, t);
        }

        private float patternScaleValueAt(float fraction, float t, bool reverse)
        {
            float windowSize = 0.8f;

            float effectiveT;
            float windowStartOffset;
            float windowEndOffset;
            if (reverse)
            {
                effectiveT = 1.0f - t;
                windowStartOffset = 1.0f;
                windowEndOffset = -windowSize;
            }
            else
            {
                effectiveT = t;
                windowStartOffset = -0.3f;
                windowEndOffset = 1.0f;
            }

            float windowPosition = (1.0f - fraction) * windowStartOffset + fraction * windowEndOffset;
            float windowT = MathF.Max(0.0f, MathF.Min(windowSize, effectiveT - windowPosition)) / windowSize;
            float localT = 1.0f - windowFunction(t: windowT);

            return localT;
        }

        private IList<Vector4> Generate(float avatarTransitionFraction)
        {
            var results = new List<Vector4>();

            var avatarPatternFrame = new Vector2(1000 - 36, 86 + 36 * 2);
            //var avatarPatternFrame = new Vector2(500, 500);

            var lokiRng = new LokiRng(seed0: 123, seed1: 0, seed2: 0);
            var numRows = 5;

            for (int row = 0; row < numRows; row++)
            {
                int avatarPatternCount = 7;
                float avatarPatternAngleSpan = MathF.PI * 2.0f / (avatarPatternCount - 1f);

                for (int i = 0; i < avatarPatternCount - 1; i++)
                {
                    //float baseItemDistance = 72.0f + row * 28.0f;
                    float baseItemDistance = 100.0f + row * 40.0f;

                    //float itemDistanceFraction = MathF.Max(0.0f, MathF.Min(1.0f, baseItemDistance / 140.0f));
                    float itemDistanceFraction = MathF.Max(0.0f, MathF.Min(1.0f, baseItemDistance / 196.0f));
                    float itemScaleFraction = patternScaleValueAt(fraction: avatarTransitionFraction, t: itemDistanceFraction, reverse: false);
                    //float itemDistance = baseItemDistance * (1.0f - itemScaleFraction) + 20.0f * itemScaleFraction;
                    float itemDistance = baseItemDistance * (1.0f - itemScaleFraction) + 28.0f * itemScaleFraction;

                    float itemAngle = -MathF.PI * 0.5f + i * avatarPatternAngleSpan;

                    if (row % 2 != 0)
                    {
                        itemAngle += avatarPatternAngleSpan * 0.5f;
                    }

                    Vector2 itemPosition = new Vector2(avatarPatternFrame.X * 0.5f + MathF.Cos(itemAngle) * itemDistance, avatarPatternFrame.Y * 0.5f + MathF.Sin(itemAngle) * itemDistance);

                    float itemScale = 0.7f + lokiRng.Next() * (1.0f - 0.7f);
                    float itemSize = MathF.Floor(36.0f * itemScale);

                    results.Add(new Vector4(itemPosition.X, itemPosition.Y, itemSize, 1.0f - itemScaleFraction));
                }
            }

            return results;
        }

        #region Source

        public AnimatedImageSource Source
        {
            get { return (AnimatedImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(AnimatedImageSource), typeof(ProfileHeaderPattern), new PropertyMetadata(null));

        #endregion
    }

    public sealed partial class ProfileHeader : UserControl
    {
        public ProfileViewModel ViewModel => DataContext as ProfileViewModel;

        public ProfileHeader()
        {
            InitializeComponent();
            DescriptionLabel.AddHandler(ContextRequestedEvent, new TypedEventHandler<UIElement, ContextRequestedEventArgs>(About_ContextRequested), true);

            ActualThemeChanged += OnActualThemeChanged;
        }

        public ElementTheme HeaderTheme => HeaderRoot.RequestedTheme;

        private CompositionPropertySet _properties;

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            if (_actualTheme == sender.ActualTheme || !ViewModel.ClientService.TryGetUser(ViewModel.Chat, out User user))
            {
                return;
            }

            UpdateProfileAccentColor(ViewModel.Chat, user.ProfileAccentColorId, user.ProfileBackgroundCustomEmojiId);
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            await GalleryWindow.ShowAsync(ViewModel, ViewModel.StorageService, chat, () => Photo);
        }

        private void Segments_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null || sender is not ActiveStoriesSegments segments)
            {
                return;
            }

            if (segments.HasActiveStories)
            {
                segments.Open(ViewModel.NavigationService, ViewModel.ClientService, chat, 140, story =>
                {
                    var transform = Segments.TransformToVisual(Window.Current.Content);
                    var point = transform.TransformPoint(new Point());

                    return new Rect(point.X + 4, point.Y + 4, 132, 132);
                });
            }
            else
            {
                GalleryWindow.ShowAsync(ViewModel, ViewModel.StorageService, chat, () => Photo);
            }
        }

        public void ViewChanged(double verticalOffset)
        {
            Pattern.Update((float)(verticalOffset / HeaderRoot.ActualHeight));

            var visual = ElementCompositionPreview.GetElementVisual(Segments);
            visual.CenterPoint = new Vector3(70, 70, 0);
            visual.Scale = new Vector3(1 - (float)(verticalOffset / HeaderRoot.ActualHeight));

            var title = ElementCompositionPreview.GetElementVisual(LabelTitle);
            var subtitle = ElementCompositionPreview.GetElementVisual(Subtitle);

            title.CenterPoint = new Vector3(LabelTitle.ActualSize.X / 2, LabelTitle.ActualSize.Y, 0);
            subtitle.CenterPoint = new Vector3(Subtitle.ActualSize.X / 2, 0, 0);
        }

        public void InitializeScrolling(CompositionPropertySet properties)
        {
            _properties = properties.Compositor.CreatePropertySet();
            _properties.InsertScalar("targetY", HeaderRoot.ActualSize.Y);

            var target = ElementCompositionPreview.GetElementVisual(this);
            var controls = ElementCompositionPreview.GetElementVisual(ControlsRoot);
            var title = ElementCompositionPreview.GetElementVisual(LabelTitle);
            var subtitle = ElementCompositionPreview.GetElementVisual(Subtitle);
            var buttons = ElementCompositionPreview.GetElementVisual(Buttons);
            var root = ElementCompositionPreview.GetElementVisual(HeaderRoot);

            ElementCompositionPreview.SetIsTranslationEnabled(Buttons, true);
            ElementCompositionPreview.SetIsTranslationEnabled(HeaderRoot, true);

            //var rootExp = "clamp(-scrollViewer.Translation.Y - (this.Target.Size.Y - 48 + 0), 0, target.Size.Y - this.Target.Size.Y)";
            var rootExp = "clamp(-scrollViewer.Translation.Y - (this.Target.Size.Y - 48 + 16), 0, target.Size.Y - this.Target.Size.Y)";
            var rootTranslation = root.Compositor.CreateExpressionAnimation(rootExp);
            rootTranslation.SetReferenceParameter("scrollViewer", properties);
            rootTranslation.SetReferenceParameter("target", target);

            //var rootExp = "clamp(-scrollViewer.Translation.Y - (this.Target.Size.Y - 48 + 0), 0, target.Size.Y - this.Target.Size.Y)";
            var controlsExp = "clamp(-scrollViewer.Translation.Y - (target.Size.Y - 40), 0, target.Size.Y)";
            var controlsClip = root.Compositor.CreateExpressionAnimation(controlsExp);
            controlsClip.SetReferenceParameter("scrollViewer", properties);
            controlsClip.SetReferenceParameter("target", root);

            //var buttonsExp = "clamp(-scrollViewer.Translation.Y - (target.Size.Y - this.Target.Size.Y - 56), 0, 72)";
            var buttonsExp = "clamp(-scrollViewer.Translation.Y - (target.Size.Y - this.Target.Size.Y - 48), 0, 72)";
            var buttonsTranslation = root.Compositor.CreateExpressionAnimation(buttonsExp);
            buttonsTranslation.SetReferenceParameter("scrollViewer", properties);
            buttonsTranslation.SetReferenceParameter("target", root);

            var buttonsOpacity = root.Compositor.CreateExpressionAnimation($"clamp(1 - {buttonsExp} / this.Target.Size.Y, 0, 1)");
            buttonsOpacity.SetReferenceParameter("scrollViewer", properties);
            buttonsOpacity.SetReferenceParameter("target", root);

            //var titleExp = "clamp(-scrollViewer.Translation.Y - 168 - 8, 0, 86)";
            var titleExp = "clamp(-scrollViewer.Translation.Y - 184 - 8, 0, 86)";
            var titleTranslation = root.Compositor.CreateExpressionAnimation(titleExp);
            titleTranslation.SetReferenceParameter("scrollViewer", properties);

            //var titleScaleExp = "max(diff, 1 - clamp((-scrollViewer.Translation.Y - 184) / 32, 0, 1) * diff)";
            var titleScaleExp = "clamp(1 - ((-scrollViewer.Translation.Y - 140) / 68) * 0.3, 0.7, 1)";
            var titleScale = root.Compositor.CreateExpressionAnimation($"vector3({titleScaleExp}, {titleScaleExp}, 1)");
            titleScale.SetReferenceParameter("scrollViewer", properties);

            //var subtitleScaleExp = "max(diff, 1 - clamp((-scrollViewer.Translation.Y - 184) / 32, 0, 1) * diff)";
            var subtitleScaleExp = "clamp(1 - ((-scrollViewer.Translation.Y - 140) / 68) * 0.143, 0.857, 1)";
            var subtitleScale = root.Compositor.CreateExpressionAnimation($"vector3({subtitleScaleExp}, {subtitleScaleExp}, 1)");
            subtitleScale.SetReferenceParameter("scrollViewer", properties);

            controls.Clip = properties.Compositor.CreateInsetClip();
            controls.Clip.StartAnimation("TopInset", controlsClip);
            root.StartAnimation("Translation.Y", rootTranslation);
            buttons.StartAnimation("Translation.Y", buttonsTranslation);
            buttons.StartAnimation("Opacity", buttonsOpacity);
            title.StartAnimation("Offset.Y", titleTranslation);
            title.StartAnimation("Scale", titleScale);
            subtitle.StartAnimation("Offset.Y", titleTranslation);
            subtitle.StartAnimation("Scale", subtitleScale);
        }

        #region Delegate

        private void UpdateProfileAccentColor(Chat chat, int colorId, long customEmojiId)
        {
            _actualTheme = WindowContext.Current.ActualTheme;

            if (ViewModel.ClientService.TryGetProfileColor(colorId, out ProfileColor color))
            {
                var colors = color.ForTheme(_actualTheme);

                Identity.Foreground = new SolidColorBrush(Colors.White);

                //HeaderRoot.BorderThickness = new Thickness(0, 0, 0, 1);
                //HeaderRoot.CornerRadius = new CornerRadius(8, 0, 0, 0);
                //HeaderRoot.Margin = new Thickness(0, 0, 0, -8);
                //HeaderRoot.Padding = new Thickness(24, 16, 24, 8);
                HeaderRoot.BorderThickness = new Thickness(1);
                HeaderRoot.CornerRadius = new CornerRadius(4);
                HeaderRoot.Margin = new Thickness(24, 16, 24, -8);
                HeaderRoot.Padding = new Thickness(8, 16, 8, 8);
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

                UpdateProfileBackgroundCustomEmoji(colors);
                UpdateIcons(chat, true);
            }
            else
            {
                Identity.ClearValue(ForegroundProperty);

                //HeaderRoot.Background = null;
                //HeaderRoot.BorderThickness = new Thickness(0);
                //HeaderRoot.CornerRadius = new CornerRadius(0);
                //HeaderRoot.Margin = new Thickness(24, 0, 24, -8);
                //HeaderRoot.Padding = new Thickness(0, 32, 0, 0);
                //HeaderRoot.RequestedTheme = ElementTheme.Default;
                HeaderRoot.ClearValue(Panel.BackgroundProperty);
                HeaderRoot.BorderThickness = new Thickness(1);
                HeaderRoot.CornerRadius = new CornerRadius(4);
                HeaderRoot.Margin = new Thickness(24, 16, 24, -8);
                HeaderRoot.Padding = new Thickness(8, 16, 8, 8);
                HeaderRoot.RequestedTheme = ElementTheme.Default;

                UpdateProfileBackgroundCustomEmoji(null);
                UpdateIcons(chat, false);
            }

            if (customEmojiId != 0)
            {
                Pattern.Source = new CustomEmojiFileSource(ViewModel.ClientService, customEmojiId);
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
            surface.SourceVisual = ElementCompositionPreview.GetElementVisual(Pattern);
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

        private ElementTheme _actualTheme;
        private bool _filledIcons = true;

        private void UpdateIcons(Chat chat, bool filled)
        {
            if (_filledIcons == filled)
            {
                return;
            }

            _filledIcons = filled;

            if (filled)
            {
                OpenChat.Glyph = Icons.ChatEmptyFilled;
                Call.Glyph = Icons.CallFilled;
                VideoChat.Glyph = Icons.VideoChatFilled;
                VideoCall.Glyph = Icons.VideoFilled;
                Search.Glyph = Icons.SearchFilled;
                Edit.Glyph = Icons.EditFilled;
                Join.Glyph = Icons.ArrowEnterFilled;
                Leave.Glyph = Icons.ArrowExitFilled;
                Menu.Glyph = Icons.MoreHorizontalFilled;
            }
            else
            {
                OpenChat.Glyph = Icons.ChatEmpty;
                Call.Glyph = Icons.Call;
                VideoChat.Glyph = Icons.VideoChat;
                VideoCall.Glyph = Icons.Video;
                Search.Glyph = Icons.Search;
                Edit.Glyph = Icons.Edit;
                Join.Glyph = Icons.ArrowEnter;
                Leave.Glyph = Icons.ArrowExit;
                Menu.Glyph = Icons.MoreHorizontal;
            }

            UpdateChatNotificationSettings(chat);
        }

        public void UpdateChat(Chat chat)
        {
            if (ViewModel.ClientService.IsSavedMessages(chat))
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);

            UpdateChatActiveStories(chat);

            UpdateChatNotificationSettings(chat);

            if (SettingsService.Current.Diagnostics.ShowIds)
            {
                ChatId.Visibility = Visibility.Visible;

                if (chat.Type is ChatTypePrivate privata)
                {
                    ChatId.Content = privata.UserId;
                }
                else
                {
                    ChatId.Content = chat.Id;
                }
            }
            else
            {
                ChatId.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateChatTitle(Chat chat)
        {
            if (ViewModel.Topic != null)
            {
                Title.Text = ViewModel.Topic.Name;
            }
            else
            {
                Title.Text = ViewModel.ClientService.GetTitle(chat);
            }
        }

        public void UpdateChatPhoto(Chat chat)
        {
            if (ViewModel.Topic != null)
            {
                FindName(nameof(Icon));
                Icon.Source = new CustomEmojiFileSource(ViewModel.ClientService, ViewModel.Topic.Icon.CustomEmojiId);
                Photo.Clear();
            }
            else
            {
                UnloadObject(Icon);
                Photo.SetChat(ViewModel.ClientService, chat, 140);
            }
        }

        public void UpdateChatActiveStories(Chat chat)
        {
            Segments.SetChat(ViewModel.ClientService, chat, 140);
        }

        public void UpdateChatNotificationSettings(Chat chat)
        {
            var unmuted = ViewModel.ClientService.Notifications.GetMutedFor(chat) == 0;
            Notifications.Content = unmuted ? Strings.ChatsMute : Strings.ChatsUnmute;
            Notifications.Glyph = unmuted
                ? (_filledIcons ? Icons.AlertFilled : Icons.Alert)
                : (_filledIcons ? Icons.AlertOffFilled : Icons.AlertOff);
        }

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            UpdateProfileAccentColor(chat, user.ProfileAccentColorId, user.ProfileBackgroundCustomEmojiId);

            Subtitle.Text = LastSeenConverter.GetLabel(user, true);

            Identity.SetStatus(ViewModel.ClientService, user);

            UserPhone.Badge = PhoneNumber.Format(user.PhoneNumber);
            UserPhone.Visibility = string.IsNullOrEmpty(user.PhoneNumber) ? Visibility.Collapsed : Visibility.Visible;

            if (user.HasActiveUsername(out string username))
            {
                Username.Badge = username;
                Username.Visibility = Visibility.Visible;
            }
            else
            {
                Username.Visibility = Visibility.Collapsed;
            }

            UpdateUsernames(user.Usernames);

            Description.Content = user.Type is UserTypeBot ? Strings.DescriptionPlaceholder : Strings.UserBio;

            if (secret is false)
            {
                MiscPanel.Visibility = Visibility.Collapsed;
                SecretLifetime.Visibility = Visibility.Collapsed;
                SecretHashKey.Visibility = Visibility.Collapsed;
            }

            if (user.PhoneNumber.Length > 0)
            {
                var info = Client.Execute(new GetPhoneNumberInfoSync("en", user.PhoneNumber)) as PhoneNumberInfo;
                if (info != null)
                {
                    AnonymousNumber.Visibility = info.IsAnonymous ? Visibility.Visible : Visibility.Collapsed;
                    AnonymousNumberSeparator.Visibility = info.IsAnonymous ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            OpenChat.Content = Strings.VoipGroupOpenChat;


            if (user.Type is UserTypeBot userTypeBot && userTypeBot.CanBeEdited)
            {
                Call.Visibility = Visibility.Collapsed;
                VideoCall.Visibility = Visibility.Collapsed;

                Edit.Visibility = Visibility.Visible;
                Search.Visibility = Visibility.Visible;
                Grid.SetColumn(Search, 2);
                Grid.SetColumn(Edit, 1);
            }
            else
            {
                Edit.Visibility = Visibility.Collapsed;
            }

            // Unused:
            Location.Visibility = Visibility.Collapsed;

            VideoChat.Visibility = Visibility.Collapsed;
            Join.Visibility = Visibility.Collapsed;
            Leave.Visibility = Visibility.Collapsed;

            ChannelMembersPanel.Visibility = Visibility.Collapsed;
            MembersPanel.Visibility = Visibility.Collapsed;
            //Admins.Visibility = Visibility.Collapsed;
            //Banned.Visibility = Visibility.Collapsed;
            //Restricted.Visibility = Visibility.Collapsed;
            //Members.Visibility = Visibility.Collapsed;
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            if (user.Type is UserTypeBot && fullInfo.BotInfo != null)
            {
                GetEntities(fullInfo.BotInfo.ShortDescription);
                Description.Visibility = string.IsNullOrEmpty(fullInfo.BotInfo.ShortDescription) ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                ReplaceEntities(fullInfo.Bio);
                Description.Visibility = string.IsNullOrEmpty(fullInfo.Bio.Text) ? Visibility.Collapsed : Visibility.Visible;
            }

            if (user.Type is UserTypeBot userTypeBot && userTypeBot.CanBeEdited)
            {
            }
            else
            {
                Call.Visibility = Visibility.Visible;
                Call.Content = Strings.Call;
                VideoCall.Visibility = fullInfo.CanBeCalled && fullInfo.SupportsVideoCalls ? Visibility.Visible : Visibility.Collapsed;
                Search.Visibility = fullInfo.CanBeCalled && fullInfo.SupportsVideoCalls ? Visibility.Collapsed : Visibility.Visible;
                Grid.SetColumn(Search, 2);
            }
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            Subtitle.Text = LastSeenConverter.GetLabel(user, true);
        }



        public void UpdateSecretChat(Chat chat, SecretChat secretChat)
        {
            if (secretChat.State is SecretChatStateReady)
            {
                SecretLifetime.Badge = chat.MessageAutoDeleteTime > 0 ? Locale.FormatTtl(chat.MessageAutoDeleteTime) : Strings.ShortMessageLifetimeForever;
                //SecretIdenticon.Source = PlaceholderHelper.GetIdenticon(secretChat.KeyHash, 24);

                MiscPanel.Visibility = Visibility.Visible;
                SecretLifetime.Visibility = Visibility.Visible;
                SecretHashKey.Visibility = Visibility.Visible;
            }
            else
            {
                MiscPanel.Visibility = Visibility.Collapsed;
                SecretLifetime.Visibility = Visibility.Collapsed;
                SecretHashKey.Visibility = Visibility.Collapsed;
            }
        }



        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            UpdateProfileAccentColor(chat, -1, 0);

            Subtitle.Text = Locale.Declension(Strings.R.Members, group.MemberCount);

            Description.Content = Strings.DescriptionPlaceholder;

            Identity.ClearStatus();

            UserPhone.Visibility = Visibility.Collapsed;
            Location.Visibility = Visibility.Collapsed;
            Username.Visibility = Visibility.Collapsed;

            Description.Visibility = Visibility.Collapsed;

            //UserCommonChats.Visibility = Visibility.Collapsed;
            MiscPanel.Visibility = Visibility.Collapsed;

            SecretLifetime.Visibility = Visibility.Collapsed;
            SecretHashKey.Visibility = Visibility.Collapsed;

            ChannelMembersPanel.Visibility = Visibility.Collapsed;
            MembersPanel.Visibility = Visibility.Collapsed;
            //Admins.Visibility = Visibility.Collapsed;
            //Banned.Visibility = Visibility.Collapsed;
            //Restricted.Visibility = Visibility.Collapsed;
            //Members.Visibility = Visibility.Collapsed;

            if (chat.Permissions.CanChangeInfo || group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator)
            {
                Edit.Visibility = Visibility.Visible;
                Join.Visibility = Visibility.Collapsed;
                Leave.Visibility = Visibility.Collapsed;
            }
            else
            {
                Edit.Visibility = Visibility.Collapsed;
                Join.Visibility = Visibility.Collapsed;
                Leave.Visibility = Visibility.Visible;
            }

            OpenChat.Content = Strings.VoipGroupOpenGroup;

            if (chat.VideoChat.GroupCallId != 0 || group.CanManageVideoChats())
            {
                VideoChat.Visibility = Visibility.Visible;
                Search.Visibility = Visibility.Collapsed;
            }
            else
            {
                VideoChat.Visibility = Visibility.Collapsed;
                Search.Visibility = Visibility.Visible;

                Grid.SetColumn(Search, 1);
            }

            // Unused:
            Call.Visibility = Visibility.Collapsed;
            VideoCall.Visibility = Visibility.Collapsed;

            AnonymousNumber.Visibility = Visibility.Collapsed;
            AnonymousNumberSeparator.Visibility = Visibility.Collapsed;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            GetEntities(fullInfo.Description);

            Description.Visibility = string.IsNullOrEmpty(fullInfo.Description)
                ? Visibility.Collapsed
                : Visibility.Visible;

            InfoPanel.Visibility = Description.Visibility == Visibility.Visible || ChatId.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }



        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            UpdateProfileAccentColor(chat, -1, 0);

            if (ViewModel.Topic != null)
            {
                Subtitle.Text = string.Format(Strings.TopicProfileStatus, chat.Title);
            }
            else
            {
                Subtitle.Text = Locale.Declension(group.IsChannel ? Strings.R.Subscribers : Strings.R.Members, group.MemberCount);
            }

            Description.Content = Strings.DescriptionPlaceholder;

            Identity.SetStatus(group);

            if (group.HasActiveUsername(out string username))
            {
                Username.Badge = username;
                Username.Visibility = Visibility.Visible;
            }
            else
            {
                Username.Visibility = Visibility.Collapsed;
            }

            UpdateUsernames(group.Usernames);

            Location.Visibility = group.HasLocation ? Visibility.Visible : Visibility.Collapsed;

            ChannelMembersPanel.Visibility = group.IsChannel && (group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator) ? Visibility.Visible : Visibility.Collapsed;
            MembersPanel.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Collapsed;
            //Admins.Visibility = Visibility.Collapsed;
            //Banned.Visibility = Visibility.Collapsed;
            //Restricted.Visibility = Visibility.Collapsed;
            //Members.Visibility = Visibility.Collapsed;

            if (chat.VideoChat.GroupCallId != 0 || group.CanManageVideoChats())
            {
                VideoChat.Visibility = Visibility.Visible;
                Search.Visibility = Visibility.Collapsed;
            }
            else
            {
                VideoChat.Visibility = Visibility.Collapsed;
                Search.Visibility = Visibility.Visible;

                Grid.SetColumn(Search, 1);
            }

            Call.Visibility = Visibility.Collapsed;
            VideoCall.Visibility = Visibility.Collapsed;

            if (group.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
            {
                Edit.Visibility = Visibility.Visible;
                Join.Visibility = Visibility.Collapsed;
                Leave.Visibility = Visibility.Collapsed;
            }
            else
            {
                Edit.Visibility = Visibility.Collapsed;

                if (group.CanJoin())
                {
                    Join.Visibility = Visibility.Visible;
                    Leave.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Join.Visibility = Visibility.Collapsed;
                    Leave.Visibility = Visibility.Visible;
                }
            }

            OpenChat.Content = group.IsChannel
                ? Strings.VoipGroupOpenChannel
                : Strings.VoipGroupOpenGroup;

            // Unused:
            MiscPanel.Visibility = Visibility.Collapsed;
            UserPhone.Visibility = Visibility.Collapsed;
            //UserCommonChats.Visibility = Visibility.Collapsed;
            SecretLifetime.Visibility = Visibility.Collapsed;
            SecretHashKey.Visibility = Visibility.Collapsed;

            AnonymousNumber.Visibility = Visibility.Collapsed;
            AnonymousNumberSeparator.Visibility = Visibility.Collapsed;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            if (ViewModel.Topic != null)
            {
                Subtitle.Text = string.Format(Strings.TopicProfileStatus, chat.Title);
            }
            else
            {
                Subtitle.Text = Locale.Declension(group.IsChannel ? Strings.R.Subscribers : Strings.R.Members, fullInfo.MemberCount);
            }

            GetEntities(fullInfo.Description);
            Description.Visibility = string.IsNullOrEmpty(fullInfo.Description) ? Visibility.Collapsed : Visibility.Visible;

            Location.Visibility = fullInfo.Location != null ? Visibility.Visible : Visibility.Collapsed;
            Location.Badge = fullInfo.Location?.Address;

            Admins.Badge = fullInfo.AdministratorCount;
            //Admins.Visibility = fullInfo.AdministratorCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            Banned.Badge = fullInfo.BannedCount;
            //Banned.Visibility = fullInfo.BannedCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            //Restricted.Badge = fullInfo.RestrictedCount;
            //Restricted.Visibility = fullInfo.RestrictedCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            Members.Badge = fullInfo.MemberCount;
            //Members.Visibility = fullInfo.CanGetMembers && group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateUsernames(Usernames usernames)
        {
            if (usernames?.ActiveUsernames.Count > 1)
            {
                ActiveUsernames.Inlines.Clear();
                ActiveUsernames.Inlines.Add(new Run { Text = string.Format(Strings.UsernameAlso, string.Empty) });

                for (int i = 1; i < usernames.ActiveUsernames.Count; i++)
                {
                    if (i > 1)
                    {
                        ActiveUsernames.Inlines.Add(new Run { Text = ", " });
                    }

                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(new Run { Text = $"@{usernames.ActiveUsernames[i]}" });

                    ActiveUsernames.Inlines.Add(hyperlink);
                }
            }
            else
            {
                ActiveUsernames.Inlines.Clear();
                ActiveUsernames.Inlines.Add(new Run { Text = Strings.Username });
            }
        }

        #endregion

        #region Context menu

        private void About_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            MessageHelper.Hyperlink_ContextRequested(ViewModel.TranslateService, sender, args);
        }

        private void About_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
        }

        private void Description_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var description = ViewModel.CopyDescription();
            if (description != null)
            {
                MessageHelper.Hyperlink_ContextRequested(ViewModel.TranslateService, sender, description, args);
            }
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            var user = chat.Type is ChatTypePrivate or ChatTypeSecret ? ViewModel.ClientService.GetUser(chat) : null;
            var basicGroup = chat.Type is ChatTypeBasicGroup basicGroupType ? ViewModel.ClientService.GetBasicGroup(basicGroupType.BasicGroupId) : null;
            var supergroup = chat.Type is ChatTypeSupergroup supergroupType ? ViewModel.ClientService.GetSupergroup(supergroupType.SupergroupId) : null;

            if ((user != null && user.Type is not UserTypeBot) || (basicGroup != null && basicGroup.CanChangeInfo()) || (supergroup != null && supergroup.CanChangeInfo()))
            {
                var icon = chat.MessageAutoDeleteTime switch
                {
                    60 * 60 * 24 => Icons.AutoDeleteDay,
                    60 * 60 * 24 * 7 => Icons.AutoDeleteWeek,
                    60 * 60 * 24 * 31 => Icons.AutoDeleteMonth,
                    _ => Icons.Timer
                };

                var autodelete = new MenuFlyoutSubItem();
                autodelete.Text = Strings.AutoDeletePopupTitle;
                autodelete.Icon = MenuFlyoutHelper.CreateIcon(icon);

                void AddToggle(int value, int? parameter, string text, string icon)
                {
                    var item = new ToggleMenuFlyoutItem();
                    item.Text = text;
                    item.IsChecked = parameter != null && value == parameter;
                    item.CommandParameter = parameter;
                    item.Command = ViewModel.SetTimerCommand;
                    item.Icon = MenuFlyoutHelper.CreateIcon(icon);

                    autodelete.Items.Add(item);
                }

                AddToggle(chat.MessageAutoDeleteTime, 0, Strings.ShortMessageLifetimeForever, Icons.AutoDeleteOff);

                autodelete.CreateFlyoutSeparator();

                AddToggle(chat.MessageAutoDeleteTime, 60 * 60 * 24, Locale.FormatTtl(60 * 60 * 24), Icons.AutoDeleteDay);
                AddToggle(chat.MessageAutoDeleteTime, 60 * 60 * 24 * 7, Locale.FormatTtl(60 * 60 * 24 * 7), Icons.AutoDeleteWeek);
                AddToggle(chat.MessageAutoDeleteTime, 60 * 60 * 24 * 31, Locale.FormatTtl(60 * 60 * 24 * 31), Icons.AutoDeleteMonth);
                AddToggle(chat.MessageAutoDeleteTime, null, Strings.AutoDownloadCustom, Icons.Options);

                flyout.Items.Add(autodelete);
                flyout.CreateFlyoutSeparator();
            }

            if (chat.Type is ChatTypePrivate or ChatTypeSecret && user != null)
            {
                var userId = chat.Type is ChatTypePrivate privata ? privata.UserId : chat.Type is ChatTypeSecret secret ? secret.UserId : 0;
                if (userId != ViewModel.ClientService.Options.MyId)
                {
                    var fullInfo = ViewModel.ClientService.GetUserFull(userId);
                    if (fullInfo == null)
                    {
                        return;
                    }

                    //if (fullInfo.CanBeCalled)
                    //{
                    //    callItem = menu.addItem(call_item, R.drawable.ic_call_white_24dp);
                    //}
                    if (user.IsContact)
                    {
                        flyout.CreateFlyoutItem(ViewModel.Share, Strings.ShareContact, Icons.Share);
                        flyout.CreateFlyoutItem(chat.BlockList is BlockListMain ? ViewModel.Unblock : ViewModel.Block, chat.BlockList is BlockListMain ? Strings.Unblock : Strings.BlockContact, chat.BlockList is BlockListMain ? Icons.Block : Icons.Block);
                        flyout.CreateFlyoutItem(ViewModel.Edit, Strings.EditContact, Icons.Edit);
                        flyout.CreateFlyoutItem(ViewModel.Delete, Strings.DeleteContact, Icons.Delete, destructive: true);
                    }
                    else
                    {
                        if (user.Type is UserTypeBot bot)
                        {
                            if (bot.CanJoinGroups)
                            {
                                flyout.CreateFlyoutItem(ViewModel.Invite, Strings.BotInvite, Icons.PersonAdd);
                            }

                            flyout.CreateFlyoutItem(() => { }, Strings.BotShare, Icons.Share);
                        }
                        else
                        {
                            flyout.CreateFlyoutItem(ViewModel.AddToContacts, Strings.AddContact, Icons.PersonAdd);
                        }

                        if (user.PhoneNumber.Length > 0)
                        {
                            flyout.CreateFlyoutItem(ViewModel.Share, Strings.ShareContact, Icons.Share);
                            flyout.CreateFlyoutItem(chat.BlockList is BlockListMain ? ViewModel.Unblock : ViewModel.Block, chat.BlockList is BlockListMain ? Strings.Unblock : Strings.BlockContact, chat.BlockList is BlockListMain ? Icons.Block : Icons.Block);
                        }
                        else
                        {
                            if (user.Type is UserTypeBot)
                            {
                                flyout.CreateFlyoutItem(chat.BlockList is BlockListMain ? ViewModel.Unblock : ViewModel.Block, chat.BlockList is BlockListMain ? Strings.BotRestart : Strings.BotStop, chat.BlockList is BlockListMain ? Icons.Block : Icons.Block);
                            }
                            else
                            {
                                flyout.CreateFlyoutItem(chat.BlockList is BlockListMain ? ViewModel.Unblock : ViewModel.Block, chat.BlockList is BlockListMain ? Strings.Unblock : Strings.BlockContact, chat.BlockList is BlockListMain ? Icons.Block : Icons.Block);
                            }
                        }
                    }

                    if (ViewModel.IsPremium && fullInfo.PremiumGiftOptions.Count > 0)
                    {
                        flyout.CreateFlyoutItem(ViewModel.GiftPremium, Strings.GiftPremium, Icons.GiftPremium);
                    }

                    if (user.Type is UserTypeRegular
                        && !LastSeenConverter.IsServiceUser(user)
                        && !LastSeenConverter.IsSupportUser(user))
                    {
                        flyout.CreateFlyoutItem(ViewModel.CreateSecretChat, Strings.StartEncryptedChat, Icons.LockClosed);
                    }
                }
                else
                {
                    flyout.CreateFlyoutItem(ViewModel.Share, Strings.ShareContact, Icons.Share);
                }
            }
            //if (writeButton != null)
            //{
            //    boolean isChannel = ChatObject.isChannel(currentChat);
            //    if (isChannel && !ChatObject.canChangeChatInfo(currentChat) || !isChannel && !currentChat.admin && !currentChat.creator && currentChat.admins_enabled)
            //    {
            //        writeButton.setImageResource(R.drawable.floating_message);
            //        writeButton.setPadding(0, AndroidUtilities.dp(3), 0, 0);
            //    }
            //    else
            //    {
            //        writeButton.setImageResource(R.drawable.floating_camera);
            //        writeButton.setPadding(0, 0, 0, 0);
            //    }
            //}
            if (chat.Type is ChatTypeSupergroup super && supergroup != null)
            {
                var fullInfo = ViewModel.ClientService.GetSupergroupFull(super.SupergroupId);

                if (supergroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
                {
                    if (supergroup.IsChannel)
                    {
                        //flyout.CreateFlyoutItem(ViewModel.EditCommand, Strings.ManageChannelMenu, Icons.Edit);
                    }
                    else if (chat.Permissions.CanInviteUsers || supergroup.CanInviteUsers())
                    {
                        flyout.CreateFlyoutItem(ViewModel.Invite, Strings.AddMember, Icons.PersonAdd);
                    }
                }

                if (fullInfo != null && fullInfo.CanGetStatistics)
                {
                    flyout.CreateFlyoutItem(ViewModel.OpenStatistics, Strings.Statistics, Icons.DataUsage);

                    if (super.IsChannel)
                    {
                        flyout.CreateFlyoutItem(ViewModel.OpenBoosts, Strings.Boosts, Icons.Boosts);
                    }
                }

                if (super.IsChannel && supergroup.CanEditStories())
                {
                    flyout.CreateFlyoutItem(ViewModel.OpenArchivedStories, Strings.ArchivedStories, Icons.Archive);
                }

                if (super.IsChannel && supergroup.HasLinkedChat)
                {
                    flyout.CreateFlyoutItem(ViewModel.Discuss, Strings.ViewDiscussion, Icons.ChatEmpty);
                }
            }
            else if (chat.Type is ChatTypeBasicGroup basic && basicGroup != null)
            {
                if (basicGroup.Status is ChatMemberStatusCreator || (basicGroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanInviteUsers) || chat.Permissions.CanInviteUsers)
                {
                    flyout.CreateFlyoutItem(ViewModel.Invite, Strings.AddMember, Icons.PersonAdd);
                }

                flyout.CreateFlyoutItem(ViewModel.OpenMembers, Strings.SearchMembers, Icons.Search);
            }

            //flyout.CreateFlyoutItem(null, Strings.AddShortcut, Icons.Pin);

            MenuTarget.RequestedTheme = ActualTheme;

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(MenuTarget, FlyoutPlacementMode.BottomEdgeAlignedRight);
            }
        }

        #endregion

        #region Entities

        private void GetEntities(string text)
        {
            DescriptionSpan.Inlines.Clear();
            Description.BadgeLabel = text;

            var response = Client.Execute(new GetTextEntities(text));
            if (response is TextEntities entities)
            {
                ReplaceEntities(DescriptionSpan, text, entities.Entities);
            }
            else
            {
                DescriptionSpan.Inlines.Add(new Run { Text = text });
            }
        }

        private void ReplaceEntities(FormattedText text)
        {
            DescriptionSpan.Inlines.Clear();
            Description.BadgeLabel = text.Text;

            ReplaceEntities(DescriptionSpan, text.Text, text.Entities);
        }

        private void ReplaceEntities(Span span, string text, IList<TextEntity> entities)
        {
            var previous = 0;

            foreach (var entity in entities.OrderBy(x => x.Offset))
            {
                if (entity.Offset > previous)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(previous, entity.Offset - previous) });
                }

                if (entity.Length + entity.Offset > text.Length)
                {
                    previous = entity.Offset + entity.Length;
                    continue;
                }

                if (entity.Type is TextEntityTypeBold)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontWeight = FontWeights.SemiBold });
                }
                else if (entity.Type is TextEntityTypeItalic)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontStyle = FontStyle.Italic });
                }
                else if (entity.Type is TextEntityTypeCode)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else if (entity.Type is TextEntityTypePre or TextEntityTypePreCode)
                {
                    // TODO any additional
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else if (entity.Type is TextEntityTypeUrl or TextEntityTypeEmailAddress or TextEntityTypePhoneNumber or TextEntityTypeMention or TextEntityTypeHashtag or TextEntityTypeCashtag or TextEntityTypeBotCommand)
                {
                    var hyperlink = new Hyperlink();
                    var data = text.Substring(entity.Offset, entity.Length);

                    hyperlink.Click += (s, args) => Entity_Click(entity.Type, data);
                    hyperlink.Inlines.Add(new Run { Text = data });
                    //hyperlink.Foreground = foreground;
                    span.Inlines.Add(hyperlink);

                    if (entity.Type is TextEntityTypeUrl)
                    {
                        MessageHelper.SetEntityData(hyperlink, data);
                    }
                }
                else if (entity.Type is TextEntityTypeTextUrl or TextEntityTypeMentionName)
                {
                    var hyperlink = new Hyperlink();
                    object data;
                    if (entity.Type is TextEntityTypeTextUrl textUrl)
                    {
                        data = textUrl.Url;
                        MessageHelper.SetEntityData(hyperlink, textUrl.Url);
                        ToolTipService.SetToolTip(hyperlink, textUrl.Url);
                    }
                    else if (entity.Type is TextEntityTypeMentionName mentionName)
                    {
                        data = mentionName.UserId;
                    }

                    hyperlink.Click += (s, args) => Entity_Click(entity.Type, null);
                    hyperlink.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length) });
                    //hyperlink.Foreground = foreground;
                    span.Inlines.Add(hyperlink);
                }

                previous = entity.Offset + entity.Length;
            }

            if (text.Length > previous)
            {
                span.Inlines.Add(new Run { Text = text.Substring(previous) });
            }
        }

        private void Entity_Click(TextEntityType type, string data)
        {
            if (type is TextEntityTypeBotCommand)
            {

            }
            else if (type is TextEntityTypeEmailAddress)
            {
                ViewModel.OpenUrl("mailto:" + data, false);
            }
            else if (type is TextEntityTypePhoneNumber)
            {
                ViewModel.OpenUrl("tel:" + data, false);
            }
            else if (type is TextEntityTypeHashtag or TextEntityTypeCashtag)
            {
                ViewModel.OpenSearch(data);
            }
            else if (type is TextEntityTypeMention)
            {
                ViewModel.OpenUsername(data);
            }
            else if (type is TextEntityTypeMentionName mentionName)
            {
                ViewModel.OpenUser(mentionName.UserId);
            }
            else if (type is TextEntityTypeTextUrl textUrl)
            {
                ViewModel.OpenUrl(textUrl.Url, true);
            }
            else if (type is TextEntityTypeUrl)
            {
                ViewModel.OpenUrl(data, false);
            }
        }

        #endregion

        private void Notifications_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            var muted = ViewModel.ClientService.Notifications.GetMutedFor(chat) > 0;
            if (muted)
            {
                ViewModel.Unmute();
            }
            else
            {
                var silent = chat.DefaultDisableNotification;

                var flyout = new MenuFlyout();

                if (muted is false)
                {
                    flyout.CreateFlyoutItem(true, () => { },
                        silent ? Strings.SoundOn : Strings.SoundOff,
                        silent ? Icons.MusicNote2 : Icons.MusicNoteOff2);
                }

                flyout.CreateFlyoutItem<int?>(ViewModel.MuteFor, 60 * 60, Strings.MuteFor1h, Icons.ClockAlarmHour);
                flyout.CreateFlyoutItem<int?>(ViewModel.MuteFor, null, Strings.MuteForPopup, Icons.AlertSnooze);

                var toggle = flyout.CreateFlyoutItem(
                    muted ? ViewModel.Unmute : ViewModel.Mute,
                    muted ? Strings.UnmuteNotifications : Strings.MuteNotifications,
                    muted ? Icons.Speaker3 : Icons.SpeakerOff);

                if (muted is false)
                {
                    toggle.Foreground = BootStrapper.Current.Resources["DangerButtonBackground"] as Brush;
                }

                NotificationsTarget.RequestedTheme = ActualTheme;

                flyout.ShowAt(NotificationsTarget, FlyoutPlacementMode.Bottom);
            }
        }
    }
}
