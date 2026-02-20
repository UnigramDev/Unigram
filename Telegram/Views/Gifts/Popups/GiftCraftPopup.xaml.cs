//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Gifts;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Stars.Popups;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Gifts.Popups
{
    public sealed partial class GiftCraftPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly ReceivedGift[] _items = [null, null, null, null];

        private readonly ReceivedGift _reference;

        private GiftsForCrafting _crafting;

        private TaskCompletionSource<Object> _crafted;

        public GiftCraftPopup(IClientService clientService, INavigationService navigationService, ReceivedGift gift)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _items[0] = gift;
            _reference = gift;

            _cube = ElementCompositionPreview.GetElementVisual(Cube);
            _cube.CenterPoint = new Vector3(FACE_CENTER, FACE_CENTER, -FACE_CENTER);

            for (int i = 0; i < _items.Length; i++)
            {
                UpdateContent(Gifts.Children[i] as Button, i, _items[i]);
            }

            UpdateSelection();

            InitializeGiftsForCrafting();
            InitializeGiftVariants();
            InitializeCube();
        }

        private record CraftingColors(Color Center, Color Edge, Color Pattern, Color Button1, Color Button2)
        {
            public CraftingColors(int center, int edge, int pattern, int button1, int button2)
                : this(center.ToColor(), edge.ToColor(), pattern.ToColor(), button1.ToColor(), button2.ToColor())
            {

            }
        }

        private readonly CraftingColors[] _colors = new[]
        {
            new CraftingColors(0x2C4359, 0x232E3F, 0x040C1A, 0x10A5DF, 0x2091E9),
            new CraftingColors(0x2C4359, 0x232E3F, 0x040C1A, 0x10A5DF, 0x2091E9),
            new CraftingColors(0x2C4359, 0x232E3F, 0x040C1A, 0x10A5DF, 0x2091E9),
            new CraftingColors(0x1C4843, 0x1A2E37, 0x040C1A, 0x3ACA49, 0x007D9E),
            new CraftingColors(0x5D2E16, 0x371B1A, 0x040C1A, 0xE27519, 0xDD4819),
        };

        private void InitializeCube()
        {
            Pattern.Center = new RectangleF(new Vector2(0, 0), new Vector2(140));

            CreateFace(Face2, Quaternion.Identity);
            CreateFace(Face3, CreateRotationY(-90));
            CreateFace(Face4, CreateRotationY(90));
            CreateFace(Face5, CreateRotationY(180));
            CreateFace(Face6, CreateRotationX(90));
            CreateFace(Face1, CreateRotationX(-90));

            CreateGift(Gift1, new Vector3(-104, -40, 0));
            CreateGift(Gift2, new Vector3(-104, 40, 0));
            CreateGift(Gift3, new Vector3(104, -40, 0));
            CreateGift(Gift4, new Vector3(104, 40, 0));
        }

        private async void InitializeGiftsForCrafting()
        {
            if (_reference.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var response = await _clientService.SendAsync(new GetGiftsForCrafting(upgraded.Gift.RegularGiftId, string.Empty, 100));
            if (response is GiftsForCrafting crafting)
            {
                _crafting = crafting;
                UpdateSelection();
            }
        }

        private async void InitializeGiftVariants()
        {
            if (_reference.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var response = await _clientService.SendAsync(new GetUpgradedGiftVariants(upgraded.Gift.RegularGiftId, false, true));
            if (response is GiftUpgradeVariants variants)
            {
                var count = Math.Min(variants.Models.Count, 3);

                for (int i = count - 1; i >= 0; i--)
                {
                    var player = new CustomEmojiIcon();
                    player.LoopCount = 0;
                    player.Source = DelayedFileSource.FromSticker(_clientService, variants.Models[i].Sticker);
                    player.HorizontalAlignment = HorizontalAlignment.Left;
                    player.FlowDirection = FlowDirection.LeftToRight;
                    player.IsHitTestVisible = false;
                    player.Margin = new Thickness(0, -2, 0, -2);
                    player.Width = 16;
                    player.Height = 16;
                    player.FrameSize = new Windows.Foundation.Size(16, 16);

                    Variants.Children.Insert(0, player);
                }
            }
        }

        private void UpdateSelection()
        {
            UpdateText();
            UpdateProbability();
            UpdateColors();
        }

        private class BackdropComparer : IEqualityComparer<UpgradedGiftBackdrop>
        {
            public bool Equals(UpgradedGiftBackdrop x, UpgradedGiftBackdrop y)
            {
                return x.Id == y.Id;
            }

            public int GetHashCode(UpgradedGiftBackdrop obj)
            {
                return obj.Id.GetHashCode();
            }
        }

        private class SymbolComparer : IEqualityComparer<UpgradedGiftSymbol>
        {
            public bool Equals(UpgradedGiftSymbol x, UpgradedGiftSymbol y)
            {
                return x.Sticker.Id == y.Sticker.Id;
            }

            public int GetHashCode(UpgradedGiftSymbol obj)
            {
                return obj.Sticker.Id.GetHashCode();
            }
        }

        private void UpdateColors()
        {
            var count = _items.Count(x => x != null);
            var colors = _colors[Math.Max(0, count - 1)];

            var radial = new RadialGradientBrush();
            radial.Center = new Point(0.5f, 0.3f);
            radial.GradientOrigin = new Point(0.5f, 0.3f);
            radial.RadiusX = 0.5;
            radial.RadiusY = 0.5;
            radial.GradientStops.Add(new GradientStop { Color = colors.Center });
            radial.GradientStops.Add(new GradientStop { Color = colors.Edge, Offset = 1 });

            BackgroundRoot.Background = radial;
            Pattern.Foreground = new SolidColorBrush(colors.Pattern);

            if (count > 0)
            {
                var linear = new LinearGradientBrush();
                linear.GradientStops.Add(new GradientStop { Color = colors.Button1 });
                linear.GradientStops.Add(new GradientStop { Color = colors.Button2, Offset = 1 });

                CraftButtonBackground.Background = linear;
                CraftButtonBackground.Visibility = Visibility.Visible;
            }
            else
            {
                CraftButtonBackground.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateProbability()
        {
            Attributes1.Children.Clear();
            Attributes2.Children.Clear();

            var count = _items.Count(x => x != null);
            if (count > 0)
            {
                var probability = _crafting?.AttributePersistenceProbabilities[_items.Length - 1];
                var sum = 0f;

                var backdrops = new Dictionary<UpgradedGiftBackdrop, int>(new BackdropComparer());
                var symbols = new Dictionary<UpgradedGiftSymbol, int>(new SymbolComparer());

                foreach (var gift in _items)
                {
                    if (gift?.Gift is not SentGiftUpgraded upgraded)
                    {
                        continue;
                    }

                    backdrops.TryGetValue(upgraded.Gift.Backdrop, out int backdropCount);
                    backdrops[upgraded.Gift.Backdrop] = ++backdropCount;

                    symbols.TryGetValue(upgraded.Gift.Symbol, out int symbolCount);
                    symbols[upgraded.Gift.Symbol] = ++symbolCount;

                    sum += upgraded.Gift.CraftProbabilityPerMille / 1000f;
                }

                var backdropTarget = Attributes1;
                var symbolTarget = backdrops.Count + symbols.Count > 4
                    ? Attributes2
                    : Attributes1;

                if (probability != null)
                {
                    foreach (var backdrop in backdrops)
                    {
                        backdropTarget.Children.Add(new GiftAttributeGauge(_clientService, backdrop.Key, probability.PersistenceChancePerMille[backdrop.Value - 1] / 1000f));
                    }

                    foreach (var symbol in symbols)
                    {
                        symbolTarget.Children.Add(new GiftAttributeGauge(_clientService, symbol.Key, probability.PersistenceChancePerMille[symbol.Value - 1] / 1000f));
                    }
                }

                VariantsRoot.Visibility = backdrops.Count + symbols.Count > 4
                    ? Visibility.Collapsed
                    : Visibility.Visible;

                InitializeProbability(sum);
            }
            else
            {
                Attributes1.Children.Add(new GiftAttributeGauge(_clientService, null as UpgradedGiftBackdrop, 0));
                Attributes1.Children.Add(new GiftAttributeGauge(_clientService, null as UpgradedGiftSymbol, 0));

                VariantsRoot.Visibility = Visibility.Visible;

                InitializeProbability(0);
            }
        }

        private void InitializeProbability(float probability)
        {
            Probability.Text = (probability * 100).ToString("0.##") + "%";
            CraftingProbability.Text = string.Format(Strings.GiftCraftProgressSuccessChance, (probability * 100).ToString("0.##") + "%");

            CraftButtonText.Text = Strings.GiftCraftButton;

            if (probability > 0)
            {
                TextBlockHelper.SetMarkdown(CraftButtonInfo, string.Format(Strings.GiftCraftSuccessChance, (probability * 100).ToString("0.##") + "%"));
            }
            else
            {
                TextBlockHelper.SetMarkdown(CraftButtonInfo, Strings.GiftCraftButtonEmpty);
            }

            var compositor = BootStrapper.Current.Compositor;
            var visual = compositor.CreateShapeVisual();

            var background = compositor.CreateEllipseGeometry();
            background.Radius = new Vector2(34);
            background.Center = new Vector2(36);
            background.TrimStart = 0.25f;

            var backgroundShape = compositor.CreateSpriteShape(background);
            backgroundShape.StrokeBrush = compositor.CreateColorBrush(Color.FromArgb(0x55, 255, 255, 255));
            backgroundShape.StrokeThickness = 4;
            backgroundShape.StrokeStartCap = CompositionStrokeCap.Round;
            backgroundShape.StrokeEndCap = CompositionStrokeCap.Round;
            backgroundShape.RotationAngleInDegrees = 45 + 90;
            backgroundShape.CenterPoint = new Vector2(36);

            var foreground = compositor.CreateEllipseGeometry();
            foreground.Radius = new Vector2(34);
            foreground.Center = new Vector2(36);
            foreground.TrimStart = 0.25f;
            foreground.TrimEnd = 0.25f + (probability * 0.75f);

            var foregroundShape = compositor.CreateSpriteShape(foreground);
            foregroundShape.StrokeBrush = compositor.CreateColorBrush(Color.FromArgb(255, 255, 255, 255));
            foregroundShape.StrokeThickness = 4;
            foregroundShape.StrokeStartCap = CompositionStrokeCap.Round;
            foregroundShape.StrokeEndCap = CompositionStrokeCap.Round;
            foregroundShape.RotationAngleInDegrees = 45 + 90;
            foregroundShape.CenterPoint = new Vector2(36);

            visual.Shapes.Add(backgroundShape);
            visual.Shapes.Add(foregroundShape);
            visual.Size = new Vector2(72);

            ElementCompositionPreview.SetElementChildVisual(Gauge, visual);
        }

        private void UpdateText()
        {
            if (_reference.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var any = _items.Any(x => x != null);

            TextBlockHelper.SetMarkdown(Text1, any ? Strings.GiftCraftText1 : Strings.GiftCraftTextEmpty1);

            var markdown = ClientEx.ParseMarkdown(any ? Strings.GiftCraftText2 : Strings.GiftCraftTextEmpty2);
            var previous = 0;

            var paragraph = new Paragraph();

            foreach (var entity in markdown.Entities)
            {
                if (entity.Offset > previous)
                {
                    paragraph.Inlines.Add(markdown.Text.Substring(previous, entity.Offset - previous));
                }

                var text = markdown.Text.Substring(entity.Offset, entity.Length);
                if (text == "{0}" || text == "{0} #{1}")
                {
                    var player = new CustomEmojiIcon();
                    player.LoopCount = 0;
                    player.Source = DelayedFileSource.FromSticker(_clientService, upgraded.Gift.Model.Sticker);
                    player.HorizontalAlignment = HorizontalAlignment.Left;
                    player.FlowDirection = FlowDirection.LeftToRight;
                    player.IsHitTestVisible = false;
                    player.Margin = new Thickness(0, -2, 0, -6);

                    var inline = new InlineUIContainer();
                    inline.Child = player;

                    // If the Span starts with a InlineUIContainer the RichTextBlock bugs and shows ellipsis
                    if (previous == 0)
                    {
                        paragraph.Inlines.Add(Icons.ZWNJ);
                    }

                    paragraph.Inlines.Add(inline);
                    paragraph.Inlines.Add(Icons.ZWNJ);

                    paragraph.Inlines.Add(Icons.Space);

                    if (any)
                    {
                        paragraph.Inlines.Add(string.Format(text, upgraded.Gift.Title, upgraded.Gift.Number.ToString("N0")), FontWeights.SemiBold);
                    }
                    else
                    {
                        paragraph.Inlines.Add(string.Format(text, upgraded.Gift.Title), FontWeights.SemiBold);
                    }
                }
                else
                {
                    paragraph.Inlines.Add(text, FontWeights.SemiBold);
                }

                previous = entity.Offset + entity.Length;
            }

            if (markdown.Text.Length > previous)
            {
                paragraph.Inlines.Add(markdown.Text.Substring(previous, markdown.Text.Length - previous));
            }

            Text2.Blocks.Clear();
            Text2.Blocks.Add(paragraph);
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                SelectGift(button, Gifts.Children.IndexOf(button));
            }
        }

        private async void SelectGift(Button target, int index)
        {
            if (_items[index] != null)
            {
                UpdateContent(target, index, null);
                UpdateSelection();
                return;
            }

            Hide();

            var popup = new GiftCraftChoosePopup(_clientService, _navigationService, _reference, _crafting);

            var confirm = await _navigationService.ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary && popup.SelectedGift != null)
            {
                UpdateContent(target, index, popup.SelectedGift);
                UpdateSelection();
            }

            await this.ShowQueuedAsync(XamlRoot);
        }

        private void UpdateContent(Button target, int index, ReceivedGift gift)
        {
            _items[index] = gift;

            var overlay = Overlays.Children[index] as Grid;
            var probability = overlay.Children[0] as BadgeControl;
            var dismiss = overlay.Children[1] as Border;

            if (gift == null)
            {
                overlay.Visibility = Visibility.Collapsed;
                target.Content = new TextBlock
                {
                    Text = Icons.AddCircleFilled,
                    FontFamily = BootStrapper.Current.Resources["SymbolThemeFontFamily"] as FontFamily,
                    FontSize = 48,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
            }
            else if (gift.Gift is SentGiftUpgraded upgraded)
            {
                var background = new PatternBackground
                {
                    Margin = new Thickness(-1),
                    CornerRadius = new CornerRadius(16),
                    Content = new AnimatedImage
                    {
                        AutoPlay = true,
                        Width = 64,
                        Height = 64,
                        FrameSize = new Windows.Foundation.Size(64, 64),
                        DecodeFrameType = Windows.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
                        Source = DelayedFileSource.FromSticker(_clientService, upgraded.Gift.Model.Sticker)
                    }
                };

                background.Update(_clientService, upgraded.Gift);

                overlay.Visibility = Visibility.Visible;
                target.Content = background;

                probability.Text = ((upgraded.Gift.CraftProbabilityPerMille / 1000d) * 100).ToString("0.##") + "%";

                var badge = new SolidColorBrush(upgraded.Gift.Backdrop.Colors.EdgeColor.ToColor().Darken());
                probability.Background = badge;
                dismiss.Background = badge;
            }
        }

        #region Cube

        private readonly Visual _cube;

        private const float FACE_SIZE = 96;
        private const float FACE_CENTER = 48;
        private const float FACE_SCALE = 64f / 96f;

        private void CreateFace(Grid element, Quaternion rotation)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            visual.CenterPoint = new Vector3(FACE_CENTER, FACE_CENTER, -FACE_CENTER);
            visual.Orientation = rotation;
            visual.BackfaceVisibility = CompositionBackfaceVisibility.Hidden;
            visual.RotationAngle = 0;

            if (rotation.IsIdentity)
            {
                return;
            }

            for (int i = 0; i < element.Children.Count; i++)
            {
                if (i == 1)
                {
                    element.Children.RemoveAt(1);
                    i++;

                    continue;
                }

                element.Children[i].Visibility = Visibility.Visible;
            }
        }

        private static Quaternion CreateRotationY(float angle)
        {
            return Quaternion.CreateFromAxisAngle(Vector3.UnitY, DegreesToRadians(angle));
        }

        private static Quaternion CreateRotationX(float angle)
        {
            return Quaternion.CreateFromAxisAngle(Vector3.UnitX, DegreesToRadians(angle));
        }

        private static float DegreesToRadians(float degrees)
        {
            return degrees * (MathF.PI / 180f);
        }

        private void CreateGift(UIElement element, Vector3 offset)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(element, true);

            var visual = ElementCompositionPreview.GetElementVisual(element);
            visual.Properties.InsertVector3("Translation", offset);
            visual.Scale = new Vector3(FACE_SCALE);
            visual.CenterPoint = new Vector3(FACE_CENTER, FACE_CENTER, -FACE_CENTER);
            visual.Orientation = Quaternion.Identity;
            visual.BackfaceVisibility = CompositionBackfaceVisibility.Hidden;
            visual.RotationAngleInDegrees = 0;
            visual.Opacity = 1;
        }

        private bool _spinning;

        private record Face(Visual Visual, float Duration, float Rotations, float Force, Vector3 Axis)
        {
            public Quaternion Orientation { get; set; } = Quaternion.Identity;

            public bool Snapped { get; set; }

            public float? LastHiddenTime { get; set; }

            public int LastFaceIndex { get; set; }

            public float LastFaceRotationAngle { get; set; }
        }

        private record Spin(float Duration, float Rotations, float Force);

        private Spin[][] _spins = new[]
        {
            new[]
            {
                new Spin(2244, 3.7f, 1.5f),
            },
            new[]
            {
                //new Spin(1620, 1.2f, 1.5f),
                //new Spin(2244, 7.0f, 1.5f),
                new Spin(1620, 1.2f, 3.0f),
                new Spin(1620, 4.9f, 1.5f),
            },
            new[]
            {
                new Spin(1620, 1.2f, 3.0f),
                new Spin(1620, 1.6f, 2.0f),
                //new Spin(1620, 2.3f, 2.0f), // 4.3
                new Spin(1620, 5.1f, 1.5f),
                //new Spin(1620, 1.2f, 1.8f),
                //new Spin(2244, 3.7f, 1.5f),
                //new Spin(2244, 7.1f, 1.5f),
            },
            new[]
            {
                new Spin(1333, 1.2f, 3.0f),
                new Spin(1333, 1.6f, 2.0f),
                new Spin(1333, 2.3f, 2.0f), // 4.3
                new Spin(1333, 5.1f, 1.5f),
            },
        };

        private void TransitionToCrafting()
        {
            var reference = _items.FirstOrDefault(x => x != null);
            if (reference.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var visual1 = ElementComposition.GetElementVisual(PrepareRoot);
            var visual2 = ElementComposition.GetElementVisual(CraftingRoot);
            var visual3 = ElementComposition.GetElementVisual(Overlays);

            var show = visual1.Compositor.CreateScalarKeyFrameAnimation();
            show.InsertKeyFrame(0, 0);
            show.InsertKeyFrame(1, 1);

            var hide = visual1.Compositor.CreateScalarKeyFrameAnimation();
            hide.InsertKeyFrame(0, 1);
            hide.InsertKeyFrame(1, 0);

            visual1.StartAnimation("Opacity", hide);
            visual2.StartAnimation("Opacity", show);
            visual3.StartAnimation("Opacity", hide);

            CraftingName.Text = upgraded.Gift.ToName();
            CraftingRoot.Visibility = Visibility.Visible;
        }

        private ulong _tickCount;
        private float _accumulated;

        private readonly HashSet<int> _excludedFaces = new();

        private Face[] _faces;
        private int _faceIndex;

        private Quaternion _orientation = Quaternion.Identity;

        private void OnRendering(object sender, object e)
        {
            var elapsed = Logger.TickCount - _tickCount;

            _tickCount = Logger.TickCount;
            _accumulated += elapsed;

            var face = _faces[_faceIndex];
            var duration = face.Duration;
            var rotations = face.Rotations;
            var axis = face.Axis;

            var decay = CalculateSpinDecay(_accumulated, duration, face.Force);

            _cube.Orientation = SlerpOrientation(face, _orientation, decay, _faceIndex == _faces.Length - 1, _excludedFaces);

            var facesCount = _faceIndex;
            var finalDecay = CalculateSpinDecay(face.Duration, face.Duration, face.Force);
            var finalDuration = duration * finalDecay;

            if (_accumulated >= duration - Constants.FastAnimation.TotalMilliseconds * 1 && _faceIndex != _faces.Length - 1)
            {
                facesCount++;

                if (_faces[facesCount].Orientation.IsIdentity)
                {
                    var finalOrientation = SlerpOrientation(face, _orientation, finalDecay, false, _excludedFaces);

                    var childFace = _faces[facesCount];
                    var child = childFace.Visual;

                    childFace.Orientation = GetRotationToFront(finalOrientation, _excludedFaces);

                    var compositor = _cube.Compositor;

                    var linear = compositor.CreateLinearEasingFunction();

                    var orientation = compositor.CreateQuaternionKeyFrameAnimation();
                    orientation.InsertKeyFrame(0, Quaternion.Identity);
                    orientation.InsertKeyFrame(1, childFace.Orientation, linear);
                    orientation.Duration = Constants.FastAnimation;

                    var offset = compositor.CreateVector3KeyFrameAnimation();
                    offset.InsertKeyFrame(1, Vector3.Zero, linear);
                    offset.Duration = Constants.FastAnimation;

                    var scale = compositor.CreateVector3KeyFrameAnimation();
                    scale.InsertKeyFrame(1, Vector3.One, linear);
                    scale.Duration = Constants.FastAnimation;

                    child.StartAnimation("Orientation", orientation);
                    child.StartAnimation("Translation", offset);
                    child.StartAnimation("Scale", scale);
                }
            }

            for (int i = 0; i <= _faceIndex; i++)
            {
                _faces[i].Visual.Orientation = SlerpOrientation(face, _faces[i].Orientation, decay, _faceIndex == _faces.Length - 1, null);
            }

            if (_accumulated >= duration)
            {
                float overflow = _accumulated - duration;

                for (int i = 0; i <= _faceIndex; i++)
                {
                    var childFace = _faces[i];
                    var child = childFace.Visual;

                    childFace.Orientation = child.Orientation;
                }

                _orientation = _cube.Orientation;
                _accumulated = overflow;

                _faceIndex++;

                if (_faceIndex == _faces.Length - 1)
                {
                    var finalFace = _faces[^1];
                    finalDecay = CalculateSpinDecay(finalFace.Duration, finalFace.Duration, finalFace.Force);

                    var finalSpinOrientation = Quaternion.Concatenate(_orientation, Quaternion.CreateFromAxisAngle(finalFace.Axis, MathF.PI * (finalFace.Rotations * finalDecay)));
                    var targetSnapOrientation = GetNearestCardinalOrientation(finalSpinOrientation, _excludedFaces, out int faceIndex, out float rotationAngle);

                    finalFace.LastHiddenTime = FindLastHiddenMoment(finalFace.Axis, finalFace.Rotations, finalFace.Duration, finalFace.Force, faceIndex, _orientation);
                    finalFace.LastFaceIndex = faceIndex;
                    finalFace.LastFaceRotationAngle = rotationAngle;
                }
                else if (_faceIndex >= _faces.Length)
                {
                    Windows.UI.Xaml.Media.CompositionTarget.Rendering -= OnRendering;
                    TransitionToCompleted();
                }
            }
        }

        private async void TransitionToCompleted(int faceIndex, float rotationAngle)
        {
            if (_crafted == null)
            {
                TransitionToFailed(faceIndex, rotationAngle);
                return;
            }
            else if (_crafted.Task.IsCompleted)
            {
                if (_crafted.Task.Result is ReceivedGift receivedGift)
                {
                    TransitionToSucceeded(receivedGift, faceIndex, rotationAngle);
                }
                else
                {
                    TransitionToFailed(faceIndex, rotationAngle);
                }
            }
            else
            {
                var response = await _crafted.Task;
                if (response is ReceivedGift receivedGift)
                {
                    TransitionToSucceeded(receivedGift, faceIndex, rotationAngle);
                }
                else
                {
                    TransitionToFailed(faceIndex, rotationAngle);
                }
            }
        }

        private void TransitionToFailed(int faceIndex, float rotationAngle)
        {
            var target = faceIndex switch
            {
                0 => Face1,
                1 => Face2,
                2 => Face4,
                3 => Face3,
                4 => Face5,
                5 => Face6,
                _ => null
            };

            if (target != null)
            {
                var visual = ElementComposition.GetElementVisual(target);
                visual.RotationAngle = -rotationAngle;

                var animated = new AnimatedImage
                {
                    Source = new LocalFileSource("ms-appx:///Assets/Animations/GiftCraftingFailed.tgs"),
                    AutoPlay = true,
                    Width = 48,
                    Height = 48,
                    FrameSize = new Windows.Foundation.Size(48, 48),
                    LoopCount = 1,
                    DecodeFrameType = Windows.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
                    IsCachingEnabled = false,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                for (int i = 0; i < target.Children.Count; i++)
                {
                    if (i == 1)
                    {
                        target.Children.RemoveAt(1);
                        i++;

                        continue;
                    }

                    target.Children[i].Visibility = Visibility.Collapsed;
                }

                target.Children.Add(animated);
            }
        }

        private void TransitionToSucceeded(ReceivedGift receivedGift, int faceIndex, float rotationAngle)
        {
            var target = faceIndex switch
            {
                0 => Face1,
                1 => Face2,
                2 => Face4,
                3 => Face3,
                4 => Face5,
                5 => Face6,
                _ => null
            };

            if (target != null && receivedGift.Gift is SentGiftUpgraded success)
            {
                var visual = ElementComposition.GetElementVisual(target);
                visual.RotationAngle = -rotationAngle;

                var pattern = new PatternBackground
                {
                    Margin = new Thickness(-1),
                    CornerRadius = new CornerRadius(16),
                    Content = new AnimatedImage
                    {
                        AutoPlay = true,
                        Width = 64,
                        Height = 64,
                        FrameSize = new Windows.Foundation.Size(64, 64),
                        DecodeFrameType = Windows.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
                        Source = DelayedFileSource.FromSticker(_clientService, success.Gift.Model.Sticker)
                    }
                };

                pattern.Update(_clientService, success.Gift);

                for (int i = 0; i < target.Children.Count; i++)
                {
                    if (i == 1)
                    {
                        target.Children.RemoveAt(1);
                        i++;

                        continue;
                    }

                    target.Children[i].Visibility = Visibility.Collapsed;
                }

                target.Children.Add(pattern);
            }
        }

        private async void TransitionToCompleted()
        {
            if (_crafted == null)
            {
                TransitionToFailed();
                return;
            }
            else if (_crafted.Task.IsCompleted)
            {
                if (_crafted.Task.Result is ReceivedGift receivedGift)
                {
                    TransitionToSucceeded(receivedGift);
                }
                else
                {
                    TransitionToFailed();
                }
            }
            else
            {
                var response = await _crafted.Task;
                if (response is ReceivedGift receivedGift)
                {
                    TransitionToSucceeded(receivedGift);
                }
                else
                {
                    TransitionToFailed();
                }
            }
        }

        private void TransitionToFailed()
        {
            var visual1 = ElementComposition.GetElementVisual(CraftingRoot);
            var visual2 = ElementComposition.GetElementVisual(FailedRoot);
            var visual3 = ElementComposition.GetElementVisual(FailedBackground);

            var show = visual1.Compositor.CreateScalarKeyFrameAnimation();
            show.InsertKeyFrame(0, 0);
            show.InsertKeyFrame(1, 1);

            var hide = visual1.Compositor.CreateScalarKeyFrameAnimation();
            hide.InsertKeyFrame(0, 1);
            hide.InsertKeyFrame(1, 0);

            visual1.StartAnimation("Opacity", hide);
            visual2.StartAnimation("Opacity", show);
            visual3.StartAnimation("Opacity", show);

            TextBlockHelper.SetMarkdown(FailedName, Locale.Declension(Strings.R.GiftCraftFailedText, _items.Count(x => x != null)));
            FailedRoot.Visibility = Visibility.Visible;
            FailedBackground.Visibility = Visibility.Visible;
            FailedList.Children.Clear();

            foreach (var item in _items)
            {
                if (item == null)
                {
                    continue;
                }

                var cell = new BurnedGiftCell();
                cell.UpdateGift(_clientService, item);

                FailedList.Children.Add(cell);
            }
        }

        private void TransitionToSucceeded(ReceivedGift receivedGift)
        {
            Hide();
            _navigationService.ShowPopup(new ReceivedGiftPopup(_clientService, _navigationService, receivedGift, _clientService.MyId, null));
            return;

            var size = 240;
            var center = 120;

            var compositor = Window.Current.Compositor;
            var shapeVisual = compositor.CreateShapeVisual();
            shapeVisual.Size = new Vector2(size);

            for (int i = 0; i < 6; i++)
            {
                CanvasGeometry geometry;
                using (var builder = new CanvasPathBuilder(null))
                {
                    // Calculate start and end angles (in radians)
                    float startAngle = -(float)(Math.PI * 2 / 24); // Starting at 0 degrees
                    float sweepAngle = (float)(Math.PI * 2 / 12); // 30 degrees in radians (2π/12)

                    // Calculate start and end points
                    Vector2 startPoint = new Vector2(
                        center + center * (float)Math.Cos(startAngle),
                        center + center * (float)Math.Sin(startAngle)
                    );

                    Vector2 endPoint = new Vector2(
                        center + center * (float)Math.Cos(startAngle + sweepAngle),
                        center + center * (float)Math.Sin(startAngle + sweepAngle)
                    );

                    // Begin the path at the start point
                    builder.BeginFigure(startPoint);

                    // Add the arc
                    builder.AddArc(
                        endPoint,           // End point of the arc
                        center,             // X radius
                        center,             // Y radius
                        0,                  // Rotation angle (0 for circular arc)
                        CanvasSweepDirection.Clockwise,
                        CanvasArcSize.Small // Small arc (less than 180 degrees)
                    );

                    builder.AddLine(new Vector2(center, center));

                    builder.EndFigure(CanvasFigureLoop.Closed);

                    geometry = CanvasGeometry.CreatePath(builder);
                }


                var ellipse = compositor.CreatePathGeometry(new CompositionPath(geometry));

                var fillGradient = compositor.CreateLinearGradientBrush();
                fillGradient.ColorStops.Add(compositor.CreateColorGradientStop(0, Color.FromArgb(0, 255, 0, 0)));
                fillGradient.ColorStops.Add(compositor.CreateColorGradientStop(0.15f, Color.FromArgb(0, 255, 0, 0)));
                fillGradient.ColorStops.Add(compositor.CreateColorGradientStop(0.32f, Color.FromArgb(0x55, 255, 0, 0)));
                fillGradient.ColorStops.Add(compositor.CreateColorGradientStop(0.55f, Color.FromArgb(0x55, 255, 0, 0)));
                fillGradient.ColorStops.Add(compositor.CreateColorGradientStop(1, Color.FromArgb(0, 255, 0, 0)));

                var strokeGradient = compositor.CreateLinearGradientBrush();
                strokeGradient.ColorStops.Add(compositor.CreateColorGradientStop(0, Color.FromArgb(0, 255, 0, 0)));
                strokeGradient.ColorStops.Add(compositor.CreateColorGradientStop(0.2f, Color.FromArgb(0, 255, 0, 0)));
                strokeGradient.ColorStops.Add(compositor.CreateColorGradientStop(0.40f, Color.FromArgb(0x55, 255, 0, 0)));
                strokeGradient.ColorStops.Add(compositor.CreateColorGradientStop(0.50f, Color.FromArgb(0x88, 255, 0, 0)));
                strokeGradient.ColorStops.Add(compositor.CreateColorGradientStop(0.80f, Color.FromArgb(0, 255, 0, 0)));
                strokeGradient.ColorStops.Add(compositor.CreateColorGradientStop(1, Color.FromArgb(0, 255, 0, 0)));

                var ellipseShape = compositor.CreateSpriteShape(ellipse);
                ellipseShape.FillBrush = fillGradient;
                ellipseShape.StrokeBrush = strokeGradient;
                ellipseShape.StrokeThickness = 2;
                ellipseShape.CenterPoint = new Vector2(center);
                ellipseShape.RotationAngleInDegrees = i * 2 * 30;

                shapeVisual.Shapes.Add(ellipseShape);
            }

            var easing = compositor.CreateLinearEasingFunction();

            var rotationAngle = compositor.CreateScalarKeyFrameAnimation();
            rotationAngle.InsertKeyFrame(0, 0);
            rotationAngle.InsertKeyFrame(1, 360, easing);
            rotationAngle.IterationBehavior = AnimationIterationBehavior.Forever;
            rotationAngle.Duration = TimeSpan.FromSeconds(10);

            shapeVisual.CenterPoint = new Vector3(center);
            shapeVisual.StartAnimation("RotationAngleInDegrees", rotationAngle);

            Success.Width = size;
            Success.Height = size;
            Success.Margin = new Thickness(0, -((size - 96) / 2), 0, -((size - 96) / 2));
            ElementCompositionPreview.SetElementChildVisual(Success, shapeVisual);
        }

        public static double RadiansToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }

        private Quaternion SlerpOrientation(Face face, Quaternion orientation, float decay, bool final, HashSet<int> excludedFaces)
        {
            var spinRotation = Quaternion.CreateFromAxisAngle(face.Axis, MathF.PI * (decay * face.Rotations));
            var currentOrientation = Quaternion.Concatenate(orientation, spinRotation);

            float snapThreshold = 0.7f;
            float normalizedTime = Math.Min(1.0f, _accumulated / face.Duration);

            if (final && _accumulated >= face.LastHiddenTime)
            {
                if (excludedFaces != null && !face.Snapped)
                {
                    face.Snapped = true;
                    TransitionToCompleted(face.LastFaceIndex, face.LastFaceRotationAngle);
                }
            }

            if (normalizedTime > snapThreshold && final)
            {
                float finalDecay = CalculateSpinDecay(face.Duration, face.Duration, face.Force);

                var finalSpinOrientation = Quaternion.Concatenate(_orientation, Quaternion.CreateFromAxisAngle(face.Axis, MathF.PI * (face.Rotations * finalDecay)));
                var targetSnapOrientation = GetNearestCardinalOrientation(finalSpinOrientation, _excludedFaces, out int faceIndex, out float rotationAngle);

                if (excludedFaces != null && !face.Snapped)
                {
                    face.Snapped = true;
                    TransitionToCompleted(faceIndex, rotationAngle);
                }

                float snapBlend = (normalizedTime - snapThreshold) / (1.0f - snapThreshold);
                snapBlend = snapBlend * snapBlend * (3.0f - 2.0f * snapBlend);

                if (_excludedFaces != excludedFaces)
                {
                    var straighteningRotation = Quaternion.Concatenate(
                        Quaternion.Inverse(finalSpinOrientation),
                        targetSnapOrientation
                    );

                    var frontFaceOrientation = Quaternion.Concatenate(orientation, Quaternion.CreateFromAxisAngle(face.Axis, MathF.PI * (face.Rotations * finalDecay)));
                    var frontFaceSnapped = Quaternion.Concatenate(
                        frontFaceOrientation,
                        straighteningRotation
                    );

                    targetSnapOrientation = frontFaceSnapped;
                }

                return Quaternion.Slerp(currentOrientation, targetSnapOrientation, snapBlend);
            }

            return currentOrientation;
        }

        private float? FindLastHiddenMoment(Vector3 axis, float totalRotations, float duration, float force, int targetFaceIndex, Quaternion startingOrientation)
        {
            Vector3 targetFaceNormal = _faceNormals[targetFaceIndex];
            Vector3 cameraDirection = new Vector3(0, 0, -1); // Camera looks at -Z

            int samples = 100;
            float? lastHiddenTime = null;

            for (int i = samples; i >= 0; i--)
            {
                float t = (i / (float)samples) * duration;
                float decay = CalculateSpinDecay(t, duration, force);

                var spinRotation = Quaternion.CreateFromAxisAngle(axis, MathF.PI * (decay * totalRotations));
                var orientation = Quaternion.Concatenate(startingOrientation, spinRotation);

                Vector3 worldNormal = Vector3.Transform(targetFaceNormal, orientation);

                float dot = Vector3.Dot(worldNormal, -cameraDirection);

                if (dot < 0) // Face is hidden
                {
                    lastHiddenTime = t;
                    break;
                }
            }

            return lastHiddenTime;
        }

        public float CalculateSpinDecay(float position, float duration, float force = 1.0f)
        {
            if (duration <= 0) return 0f;

            float normalizedTime = Math.Clamp(position / duration, 0, 1);

            float decayRate = 1.0f / MathF.Max(0.1f, force);
            float decay = MathF.Exp(-decayRate * normalizedTime * 5.0f);

            return 1 - Math.Clamp(decay, 0, 1);
        }

        private Vector3 CalculateAxis(UIElement element)
        {
            var transform1 = element.TransformToVisual(this);
            var point1 = transform1.TransformPoint(new Point()).ToVector2();

            var transform2 = Cube.TransformToVisual(this);
            var point2 = transform2.TransformPoint(new Point()).ToVector2();

            var x1 = point1.X + ((element.ActualSize.X * FACE_SCALE) / 2);
            var y1 = point1.Y + ((element.ActualSize.Y * FACE_SCALE) / 2);

            var x2 = point2.X + ((Cube.ActualSize.X * 1) / 2);
            var y2 = point2.Y + ((Cube.ActualSize.Y * 1) / 2);

            float dx = x1 - x2;
            float dy = y1 - y2;

            float angleRadians = MathF.Atan2(dy, dx);
            Vector2 dir2D = new Vector2(
                MathF.Cos(-angleRadians),
                MathF.Sin(-angleRadians)
            );

            dir2D = -dir2D;

            return Vector3.Normalize(new Vector3(
                dir2D.Y,
                dir2D.X,
                0f
            ));
        }

        private static readonly Vector3[] _faceNormals = new[]
        {
            new Vector3(0, 1, 0),   // [1] Top
            new Vector3(0, 0, 1),   // [2] Front (center)
            new Vector3(1, 0, 0),   // [3] Right
            new Vector3(-1, 0, 0),  // [4] Left
            new Vector3(0, 0, -1),  // [5] Back
            new Vector3(0, -1, 0)   // [6] Bottom
        };

        private Quaternion GetRotationToFront(Quaternion orientation, HashSet<int> excludedFaces)
        {
            int facingFaceIndex = GetNearestFaceIndex(orientation, excludedFaces);
            if (facingFaceIndex == -1)
            {
                return orientation;
            }

            if (excludedFaces != null)
            {
                excludedFaces.Add(facingFaceIndex);
            }

            Vector3 fromNormal = _faceNormals[1];
            Vector3 toNormal = _faceNormals[facingFaceIndex];

            Vector3 rotationAxis = Vector3.Cross(fromNormal, toNormal);
            float rotationAngle = MathF.Acos(Vector3.Dot(
                Vector3.Normalize(fromNormal),
                Vector3.Normalize(toNormal)
            ));

            Quaternion faceSwapRotation;
            if (rotationAxis.LengthSquared() < 0.0001f)
            {
                if (Vector3.Dot(fromNormal, toNormal) > 0)
                {
                    faceSwapRotation = Quaternion.Identity;
                }
                else
                {
                    // 180° flip - choose perpendicular axis
                    Vector3 perpAxis = Math.Abs(fromNormal.X) < 0.9f
                        ? new Vector3(1, 0, 0)
                        : new Vector3(0, 1, 0);
                    rotationAxis = Vector3.Cross(fromNormal, perpAxis);
                    faceSwapRotation = Quaternion.CreateFromAxisAngle(
                        Vector3.Normalize(rotationAxis),
                        MathF.PI
                    );
                }
            }
            else
            {
                faceSwapRotation = Quaternion.CreateFromAxisAngle(
                    Vector3.Normalize(rotationAxis),
                    rotationAngle
                );
            }

            Quaternion newOrientation = orientation * faceSwapRotation;
            return newOrientation;
        }

        private void Craft_Click(object sender, RoutedEventArgs e)
        {
            if (_spinning)
            {
                Clear_Click(null, null);
                return;
            }

            var count = _items.Count(x => x != null);
            if (count == 0)
            {
                VisualUtilities.ShakeView(CraftButton);
                return;
            }

            TransitionToCrafting();

            var diff = _items.Length - count;
            var spins = _spins[count - 1];

            _orientation = Quaternion.Identity;
            _accumulated = 0;
            _tickCount = 0;
            _excludedFaces.Clear();
            _excludedFaces.Add(1);

            _faces = new Face[count];
            _faceIndex = 0;

            int j = 0;

            for (int i = 0; i < _items.Length; i++)
            {
                var child = ElementComposition.GetElementVisual(Gifts.Children[i]);

                if (_items[i] == null)
                {
                    var opacity = child.Compositor.CreateScalarKeyFrameAnimation();
                    opacity.InsertKeyFrame(1, 0);

                    child.StartAnimation("Opacity", opacity);
                    continue;
                }

                if (j == 0)
                {
                    var offset = child.Compositor.CreateVector3KeyFrameAnimation();
                    offset.InsertKeyFrame(1, Vector3.Zero);
                    offset.Duration = Constants.SoftAnimation;

                    var scale = child.Compositor.CreateVector3KeyFrameAnimation();
                    scale.InsertKeyFrame(1, Vector3.One);
                    scale.Duration = Constants.SoftAnimation;

                    child.StartAnimation("Translation", offset);
                    child.StartAnimation("Scale", scale);
                }

                var spin = spins[j];

                _faces[j++] = new Face(child, spin.Duration, spin.Rotations, spin.Force, CalculateAxis(Gifts.Children[i]));
            }

            if (_faces.Length == 1)
            {
                var finalFace = _faces[^1];
                var finalDecay = CalculateSpinDecay(finalFace.Duration, finalFace.Duration, finalFace.Force); // decay at t=duration

                var finalSpinOrientation = Quaternion.Concatenate(Quaternion.Identity, Quaternion.CreateFromAxisAngle(finalFace.Axis, MathF.PI * (finalFace.Rotations * finalDecay)));
                var targetSnapOrientation = GetNearestCardinalOrientation(finalSpinOrientation, _excludedFaces, out int faceIndex, out float rotationAngle);

                finalFace.LastHiddenTime = FindLastHiddenMoment(finalFace.Axis, finalFace.Rotations, finalFace.Duration, finalFace.Force, faceIndex, _orientation);
                finalFace.LastFaceIndex = faceIndex;
                finalFace.LastFaceRotationAngle = rotationAngle;
            }

            _tickCount = Logger.TickCount;

            _spinning = true;
            Windows.UI.Xaml.Media.CompositionTarget.Rendering += OnRendering;

            var receivedGiftIds = _items.Where(x => x != null).Select(x => x.ReceivedGiftId).ToList();
            var craftGifts = new CraftGift(receivedGiftIds);

            _crafted = new TaskCompletionSource<Object>();
            _clientService.Send(craftGifts, SetResult);
        }

        private async void SetResult(Object result)
        {
            if (result is Error error)
            {
                _crafted.TrySetResult(result);

                this.BeginOnUIThread(() =>
                {
                    ToastPopup.ShowError(XamlRoot, error);
                    Clear_Click(null, null);
                });
            }
            else if (result is CraftGiftResultSuccess success)
            {
                _clientService.Send(new GetReceivedGift(success.ReceivedGiftId), SetResult);
            }
            else
            {
                _crafted.TrySetResult(result);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            _spinning = false;
            Windows.UI.Xaml.Media.CompositionTarget.Rendering -= OnRendering;

            ElementCompositionPreview.SetElementChildVisual(Success, null);

            _cube.Orientation = Quaternion.Identity;

            var visual1 = ElementComposition.GetElementVisual(PrepareRoot);
            var visual3 = ElementComposition.GetElementVisual(Overlays);
            visual1.Opacity = 1;
            visual3.Opacity = 1;

            CraftingRoot.Visibility = Visibility.Collapsed;
            FailedRoot.Visibility = Visibility.Collapsed;
            FailedBackground.Visibility = Visibility.Collapsed;

            CreateFace(Face2, Quaternion.Identity);
            CreateFace(Face3, CreateRotationY(-90));
            CreateFace(Face4, CreateRotationY(90));
            CreateFace(Face5, CreateRotationY(180));
            CreateFace(Face6, CreateRotationX(90));
            CreateFace(Face1, CreateRotationX(-90));

            CreateGift(Gift1, new Vector3(-104, -40, 0));
            CreateGift(Gift2, new Vector3(-104, 40, 0));
            CreateGift(Gift3, new Vector3(104, -40, 0));
            CreateGift(Gift4, new Vector3(104, 40, 0));

            for (int i = 0; i < _items.Length; i++)
            {
                _items[i] = null;
            }

            UpdateSelection();
        }

        private int GetNearestFaceIndex(Quaternion orientation, HashSet<int> excludedFaces)
        {
            Vector3 cameraDirection = new Vector3(0, 0, -1); // Camera looks at -Z

            var faceCandidates = new List<(int index, float dot)>();

            for (int i = 0; i < _faceNormals.Length; i++)
            {
                if (excludedFaces.Contains(i))
                    continue;

                Vector3 worldNormal = Vector3.Transform(_faceNormals[i], orientation);
                float dot = Vector3.Dot(worldNormal, -cameraDirection);

                faceCandidates.Add((i, dot));
            }

            if (faceCandidates.Count == 0)
                return -1;

            faceCandidates.Sort((a, b) => b.dot.CompareTo(a.dot));
            return faceCandidates[0].index;
        }

        private Quaternion GetNearestCardinalOrientation(Quaternion orientation, HashSet<int> excludedFaces, out int faceIndex, out float rotationAngle)
        {
            int bestFaceIndex = GetNearestFaceIndex(orientation, excludedFaces);
            if (bestFaceIndex == -1)
            {
                faceIndex = -1;
                rotationAngle = 0;
                return orientation;
            }

            // 0 => face 1
            // 1 => face 2
            // 2 => face 4
            // 3 => face 3
            // 4 => face 5
            // 5 => face 6

            Vector3 targetFaceNormal = _faceNormals[bestFaceIndex];
            Vector3 desiredForward = new Vector3(0, 0, 1); // We want this face to point forward (+Z)

            Quaternion faceAlignment;
            Vector3 rotationAxis = Vector3.Cross(targetFaceNormal, desiredForward);

            if (rotationAxis.LengthSquared() < 0.0001f)
            {
                if (Vector3.Dot(targetFaceNormal, desiredForward) > 0)
                {
                    faceAlignment = Quaternion.Identity;
                }
                else
                {
                    Vector3 perpAxis = Math.Abs(targetFaceNormal.X) < 0.9f
                        ? new Vector3(1, 0, 0)
                        : new Vector3(0, 1, 0);
                    faceAlignment = Quaternion.CreateFromAxisAngle(perpAxis, MathF.PI);
                }
            }
            else
            {
                float angle = MathF.Acos(Math.Clamp(Vector3.Dot(
                    Vector3.Normalize(targetFaceNormal),
                    Vector3.Normalize(desiredForward)
                ), -1f, 1f));
                faceAlignment = Quaternion.CreateFromAxisAngle(Vector3.Normalize(rotationAxis), angle);
            }

            Quaternion bestOrientation = faceAlignment;
            float bestDistance = float.MaxValue;
            float bestRotationAngle = 0;

            for (int i = 0; i < 4; i++)
            {
                float rollAngle = i * MathF.PI / 2; // 0°, 90°, 180°, 270°
                Quaternion rollRotation = Quaternion.CreateFromAxisAngle(desiredForward, rollAngle);
                Quaternion candidate = Quaternion.Concatenate(faceAlignment, rollRotation);

                float distance = QuaternionAngularDistance(orientation, candidate);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestOrientation = candidate;
                    bestRotationAngle = rollAngle;
                }
            }

            faceIndex = bestFaceIndex;
            rotationAngle = bestRotationAngle;
            return bestOrientation;
        }

        private float QuaternionAngularDistance(Quaternion a, Quaternion b)
        {
            float dot = Math.Abs(a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W);
            dot = Math.Clamp(dot, 0f, 1f);
            return 2.0f * MathF.Acos(dot);
        }

        #endregion
    }
}
