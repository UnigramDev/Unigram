using LinqToVisualTree;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Imaging;
using Point = Windows.Foundation.Point;

namespace Unigram.Controls.Messages
{
    public sealed partial class MenuFlyoutReactions : UserControl
    {
        private readonly IList<Reaction> _reactions;
        private readonly bool _canUnlockMore;

        private readonly MessageViewModel _message;
        private readonly MessageBubble _bubble;
        private readonly MenuFlyout _flyout;

        private readonly MenuFlyoutPresenter _presenter;
        private readonly Popup _popup;

        private bool _expanded;

        public static MenuFlyoutReactions ShowAt(IList<Reaction> reactions, MessageViewModel message, MessageBubble bubble, MenuFlyout flyout)
        {
            return new MenuFlyoutReactions(reactions, message, bubble, flyout);
        }

        private MenuFlyoutReactions(IList<Reaction> reactions, MessageViewModel message, MessageBubble bubble, MenuFlyout flyout)
        {
            _reactions = message.ProtoService.IsPremium ? reactions : reactions.Where(x => !x.IsPremium).ToList();
            _canUnlockMore = message.ProtoService.IsPremiumAvailable && !message.ProtoService.IsPremium && reactions.Any(x => x.IsPremium);
            _message = message;
            _bubble = bubble;
            _flyout = flyout;

            InitializeComponent();

            var last = flyout.Items.LastOrDefault();
            var presenter = last.Ancestors<MenuFlyoutPresenter>().FirstOrDefault();

            _presenter = presenter;
            _popup = new Popup();

            var transform = presenter.TransformToVisual(Window.Current.Content);
            var position = transform.TransformPoint(new Point());

            //var relativeFirst = Math.Abs(absolute.Y - position.Y);
            //var relativeLast = Math.Abs(absolute.Y - (position.Y + presenter.ActualHeight));
            var upsideDown = false; //relativeLast < relativeFirst;

            var count = Math.Min(_reactions.Count, 6);

            var actualWidth = presenter.ActualSize.X + 18 + 12 + 18;
            var width = 8 + count * 34 - (_reactions.Count > 6 ? 6 : 2);

            var padding = actualWidth - width;

            Shadow.Width = width;
            Pill.Width = width;
            ScrollingHost.Width = width;

            Expand.Visibility = _reactions.Count > 6 ? Visibility.Visible : Visibility.Collapsed;

            BubbleMedium.VerticalAlignment = upsideDown ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            BubbleMedium.Margin = new Thickness(0, upsideDown ? -6 : 0, 18, upsideDown ? 0 : -6);

            BubbleOverlay.VerticalAlignment = upsideDown ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            BubbleOverlay.Margin = new Thickness(0, upsideDown ? -6 : 0, 18, upsideDown ? 0 : -6);

            LayoutRoot.Padding = new Thickness(16, upsideDown ? 32 : 16, 16, upsideDown ? 16 : 32);

            var offset = 0;
            var visible = _reactions.Count > 6 ? 5 : 6; //Math.Ceiling((width - 8) / 34);

            static void DownloadFile(MessageViewModel message, File file)
            {
                if (file != null && file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
                {
                    message.ProtoService.DownloadFile(file.Id, 31);
                }
            }

            foreach (var item in _reactions)
            {
                // Pre-download additional assets
                DownloadFile(message, item.CenterAnimation?.StickerValue);
                DownloadFile(message, item.AroundAnimation?.StickerValue);

                if (offset >= visible)
                {
                    DownloadFile(message, item.AppearAnimation.StickerValue);
                    break;
                }
                
                var view = new LottieView();
                view.AutoPlay = offset < visible;
                view.IsLoopingEnabled = false;
                view.FrameSize = new Size(24, 24);
                view.DecodeFrameType = DecodePixelType.Logical;
                view.Width = 24;
                view.Height = 24;
                view.Tag = offset < visible ? null : new object();

                var file = item.AppearAnimation.StickerValue;
                if (file.Local.IsDownloadingCompleted)
                {
                    view.Source = UriEx.ToLocal(file.Local.Path);
                }
                else
                {
                    view.Source = null;

                    UpdateManager.Subscribe(view, message, file, /*UpdateReaction*/UpdateFile, true);

                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        message.ProtoService.DownloadFile(file.Id, 32);
                    }
                }

                var button = new HyperlinkButton
                {
                    Tag = item,
                    Content = view,
                    Margin = new Thickness(0, 0, 10, 0),
                    Style = BootStrapper.Current.Resources["EmptyHyperlinkButtonStyle"] as Style
                };

                button.Click += Reaction_Click;

                Grid.SetColumn(button, offset);

                Presenter.Children.Add(button);
                Presenter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

                if (offset < visible)
                {
                    var visual = ElementCompositionPreview.GetElementVisual(view);
                    visual.CenterPoint = new Vector3(12, 12, 0);
                    visual.Scale = Vector3.Zero;

                    var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
                    scale.InsertKeyFrame(0, Vector3.Zero);
                    scale.InsertKeyFrame(1, Vector3.One);
                    scale.DelayTime = TimeSpan.FromMilliseconds(50 * (visible - Presenter.Children.Count));

                    visual.StartAnimation("Scale", scale);
                }

                offset++;
            }

            var device = CanvasDevice.GetSharedDevice();
            var rect1 = CanvasGeometry.CreateRectangle(device, Math.Min(width - actualWidth, 0), 0, Math.Max(width + 16 + 16 + Math.Max(0, padding), actualWidth), 86);
            var elli1 = CanvasGeometry.CreateRoundedRectangle(device, width - actualWidth + 18 + 16, upsideDown ? -86 - 4 : 16 + 36 + 4, presenter.ActualSize.X, 86, 8, 8);
            var group1 = CanvasGeometry.CreateGroup(device, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);

            var rootVisual = ElementCompositionPreview.GetElementVisual(LayoutRoot);
            rootVisual.Clip = rootVisual.Compositor.CreateGeometricClip(rootVisual.Compositor.CreatePathGeometry(new CompositionPath(group1)));

            var pillShadow = Window.Current.Compositor.CreateDropShadow();
            pillShadow.BlurRadius = 16;
            pillShadow.Opacity = 0.14f;
            pillShadow.Color = Colors.Black;
            pillShadow.Mask = Pill.GetAlphaMask();

            var pillReceiver = Window.Current.Compositor.CreateSpriteVisual();
            pillReceiver.Shadow = pillShadow;
            pillReceiver.Size = new Vector2(width, 36);
            pillReceiver.Offset = new Vector3(0, 8, 0);

            var mediumShadow = Window.Current.Compositor.CreateDropShadow();
            mediumShadow.BlurRadius = 16;
            mediumShadow.Opacity = 0.14f;
            mediumShadow.Color = Colors.Black;
            mediumShadow.Mask = BubbleMedium.GetAlphaMask();

            var mediumReceiver = Window.Current.Compositor.CreateSpriteVisual();
            mediumReceiver.Shadow = mediumShadow;
            mediumReceiver.Size = new Vector2(12, 12);
            mediumReceiver.Offset = new Vector3(width - 18 - 12, upsideDown ? -8 : 36 - 8, 0);

            var receivers = Window.Current.Compositor.CreateContainerVisual();
            receivers.Children.InsertAtBottom(pillReceiver);
            receivers.Children.InsertAtBottom(mediumReceiver);
            receivers.Size = new Vector2(width, 54);

            ElementCompositionPreview.SetElementChildVisual(Shadow, receivers);

            var x = position.X - 18 + padding;
            var y = position.Y - (36 + 4);

            if (upsideDown)
            {
                y = position.Y + presenter.ActualHeight + 4;
                y -= 16;
            }

            _popup.Child = this;
            _popup.Margin = new Thickness(x - 16, y - 16, 0, 0);
            _popup.ShouldConstrainToRootBounds = false;
            _popup.RequestedTheme = presenter.ActualTheme;
            _popup.IsOpen = true;

            var visualMedium = ElementCompositionPreview.GetElementVisual(BubbleMedium);
            var visualOverlay = ElementCompositionPreview.GetElementVisual(BubbleOverlay);
            visualMedium.CenterPoint = new Vector3(6, 6, 0);
            visualOverlay.CenterPoint = new Vector3(6, 6, 0);

            var visualPill = ElementCompositionPreview.GetElementVisual(Pill);
            visualPill.CenterPoint = new Vector3(36 / 2, 36 / 2, 0);
            visualPill.CenterPoint = new Vector3(width - 36 / 2, 36 / 2, 0);

            var visualExpand = ElementCompositionPreview.GetElementVisual(Expand);
            visualExpand.CenterPoint = new Vector3(32 / 2f, 24 / 2f, 0);

            var clip = visualPill.Compositor.CreateRoundedRectangleGeometry();
            clip.CornerRadius = new Vector2(36 / 2);

            var batch = visualPill.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            var scaleMedium = visualMedium.Compositor.CreateVector3KeyFrameAnimation();
            scaleMedium.InsertKeyFrame(0, Vector3.Zero);
            scaleMedium.InsertKeyFrame(1, Vector3.One);
            scaleMedium.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            scaleMedium.DelayTime = TimeSpan.FromMilliseconds(100);
            scaleMedium.Duration = TimeSpan.FromMilliseconds(150);

            var scalePill = visualMedium.Compositor.CreateSpringVector3Animation();
            scalePill.InitialValue = Vector3.Zero;
            scalePill.FinalValue = Vector3.One;
            scalePill.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            scalePill.DampingRatio = 0.6f;

            var translation = visualMedium.Compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, 0);
            translation.InsertKeyFrame(1, 16);

            var opacity = visualMedium.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 0.14f);

            visualMedium.StartAnimation("Scale", scaleMedium);
            visualOverlay.StartAnimation("Scale", scaleMedium);
            visualPill.StartAnimation("Scale", scalePill);
            visualExpand.StartAnimation("Scale", scalePill);

            mediumShadow.StartAnimation("BlurRadius", translation);
            mediumShadow.StartAnimation("Opacity", opacity);

            translation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            translation.DelayTime = scaleMedium.Duration + TimeSpan.FromMilliseconds(100);
            opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            opacity.DelayTime = scaleMedium.Duration + TimeSpan.FromMilliseconds(100);

            pillShadow.StartAnimation("BlurRadius", translation);
            pillShadow.StartAnimation("Opacity", opacity);

            var resize = visualPill.Compositor.CreateVector2KeyFrameAnimation();
            resize.InsertKeyFrame(0, new Vector2(36, 36));
            resize.InsertKeyFrame(1, new Vector2(width, 36));
            resize.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            resize.DelayTime = TimeSpan.FromMilliseconds(100);
            resize.Duration = TimeSpan.FromMilliseconds(150);

            var move = visualPill.Compositor.CreateVector2KeyFrameAnimation();
            move.InsertKeyFrame(0, new Vector2(width - 36, 0));
            move.InsertKeyFrame(1, new Vector2());
            move.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            move.DelayTime = TimeSpan.FromMilliseconds(100);
            move.Duration = TimeSpan.FromMilliseconds(150);

            visualPill.Clip = visualPill.Compositor.CreateGeometricClip(clip);
            clip.StartAnimation("Size", resize);
            clip.StartAnimation("Offset", move);

            batch.End();

            presenter.Unloaded += Presenter_Unloaded;
        }

        private void Presenter_Unloaded(object sender, RoutedEventArgs e)
        {
            _popup.IsOpen = false;
        }

        private async void Reaction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is HyperlinkButton button && button.Tag is Reaction reaction)
            {
                _flyout.Hide();
                await _message.ProtoService.SendAsync(new SetMessageReaction(_message.ChatId, _message.Id, reaction.ReactionValue, false));

                if (_bubble != null)
                {
                    _bubble.UpdateMessageReactions(_message, true);
                }
            }
        }

        private void UpdateFile(object target, File file)
        {
            if (target is LottieView lottie)
            {
                lottie.Source = UriEx.ToLocal(file.Local.Path);
            }
        }

        private void OnViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            var j = (int)Math.Floor((e.NextView.HorizontalOffset - 8) / 34);
            var k = (int)Math.Ceiling((e.NextView.HorizontalOffset - 8 + Shadow.ActualWidth) / 34);

            for (int i = 0; i < Presenter.Children.Count; i++)
            {
                var button = Presenter.Children[i] as HyperlinkButton;
                if (button.Content is LottieView view)
                {
                    if (view.Tag != null && i >= j && i < k)
                    {
                        view.Play();
                        view.Tag = null;
                    }
                    else if (view.Tag == null && (i < j || i >= k))
                    {
                        view.Tag = new object();
                    }
                }
            }
        }

        private async void Expand_Click(object sender, RoutedEventArgs e)
        {
            if (_expanded)
            {
                _expanded = false;

                var cols = 5;
                var rows = (int)Math.Ceiling((double)_reactions.Count / cols);

                var width = 8 + 5 * 34 - 2;
                var viewport = 8 + 6 * 34 - 6;
                var height = (rows + 1) * 34;

                var actualWidth = _presenter.ActualSize.X + 18 + 12 + 18;
                var width2 = 8 + 6 * 34 - 6;

                var padding = actualWidth - width2;

                ScrollingHost.VerticalScrollMode = ScrollMode.Disabled;
                ScrollingHost.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

                ScrollingHost.HorizontalAlignment = HorizontalAlignment.Stretch;
                ScrollingHost.VerticalAlignment = VerticalAlignment.Stretch;

                Shadow.Height = 36;
                Pill.Height = 36;
                ScrollingHost.Height = 36;

                BubbleMedium.Visibility = Visibility.Visible;
                BubbleOverlay.Visibility = Visibility.Visible;

                var pillShadow = Window.Current.Compositor.CreateDropShadow();
                pillShadow.BlurRadius = 16;
                pillShadow.Opacity = 0.14f;
                pillShadow.Color = Colors.Black;
                pillShadow.Mask = Pill.GetAlphaMask();

                var pillReceiver = Window.Current.Compositor.CreateSpriteVisual();
                pillReceiver.Shadow = pillShadow;
                pillReceiver.Size = new Vector2(width, 36);
                pillReceiver.Offset = new Vector3(0, 8, 0);

                var mediumShadow = Window.Current.Compositor.CreateDropShadow();
                mediumShadow.BlurRadius = 16;
                mediumShadow.Opacity = 0.14f;
                mediumShadow.Color = Colors.Black;
                mediumShadow.Mask = BubbleMedium.GetAlphaMask();

                var mediumReceiver = Window.Current.Compositor.CreateSpriteVisual();
                mediumReceiver.Shadow = mediumShadow;
                mediumReceiver.Size = new Vector2(12, 12);
                mediumReceiver.Offset = new Vector3(width - 18 - 12, 36 - 8, 0);

                var receivers = Window.Current.Compositor.CreateContainerVisual();
                receivers.Children.InsertAtBottom(pillReceiver);
                receivers.Children.InsertAtBottom(mediumReceiver);
                receivers.Size = new Vector2(width, 54);

                ElementCompositionPreview.SetElementChildVisual(Shadow, receivers);

                Presenter.ColumnDefinitions.Clear();
                Presenter.RowDefinitions.Clear();

                for (int i = Presenter.Children.Count - 1; i >= 0; i--)
                {
                    if (i >= 5)
                    {
                        Presenter.Children.RemoveAt(i);
                    }
                    else
                    {
                        Presenter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

                        Grid.SetColumn(Presenter.Children[i] as FrameworkElement, i);
                        Grid.SetRow(Presenter.Children[i] as FrameworkElement, 0);
                    }
                }

                var device = CanvasDevice.GetSharedDevice();
                var rect1 = CanvasGeometry.CreateRectangle(device, Math.Min(width2 - actualWidth, 0), 0, width2 + 16 + 16 + Math.Max(0, padding), 86);
                var elli1 = CanvasGeometry.CreateRoundedRectangle(device, width2 - actualWidth + 18 + 16, 16 + 36 + 4, _presenter.ActualSize.X, 86, 8, 8);
                var group1 = CanvasGeometry.CreateGroup(device, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);

                var rootVisual = ElementCompositionPreview.GetElementVisual(LayoutRoot);
                rootVisual.Clip = rootVisual.Compositor.CreateGeometricClip(rootVisual.Compositor.CreatePathGeometry(new CompositionPath(group1)));

                var visualPill = ElementCompositionPreview.GetElementVisual(Pill);
                var clip = visualPill.Clip as CompositionGeometricClip;
                var geometry = clip.Geometry as CompositionRoundedRectangleGeometry;

                var resize = visualPill.Compositor.CreateVector2KeyFrameAnimation();
                resize.InsertKeyFrame(0, geometry.Size);
                resize.InsertKeyFrame(1, new Vector2(geometry.Size.X, 36));
                resize.Duration = TimeSpan.FromMilliseconds(150);

                geometry.StartAnimation("Size", resize);

                var offset = visualPill.Compositor.CreateVector3KeyFrameAnimation();
                offset.InsertKeyFrame(0, new Vector3((viewport - width) / 2f - 2, 30, 0));
                offset.InsertKeyFrame(1, Vector3.Zero);

                ElementCompositionPreview.SetIsTranslationEnabled(ScrollingHost, true);
                var scrollingVisual = ElementCompositionPreview.GetElementVisual(ScrollingHost);
                scrollingVisual.StartAnimation("Translation", offset);

                var opacity = visualPill.Compositor.CreateScalarKeyFrameAnimation();
                opacity.InsertKeyFrame(0, 0);
                opacity.InsertKeyFrame(1, 1);

                var presenterVisual = ElementCompositionPreview.GetElementVisual(_presenter);
                presenterVisual.StartAnimation("Opacity", opacity);

                if (InfoText != null)
                {
                    var infoVisual = ElementCompositionPreview.GetElementVisual(InfoText);
                    //var editVisual = ElementCompositionPreview.GetElementVisual(EditButton);

                    infoVisual.CenterPoint = new Vector3(InfoText.ActualSize / 2, 0);
                    //editVisual.CenterPoint = new Vector3(EditButton.ActualSize / 2, 0);

                    var show = visualPill.Compositor.CreateScalarKeyFrameAnimation();
                    show.InsertKeyFrame(0, 1);
                    show.InsertKeyFrame(1, 0);

                    var scale = visualPill.Compositor.CreateVector3KeyFrameAnimation();
                    scale.InsertKeyFrame(0, Vector3.One);
                    scale.InsertKeyFrame(1, Vector3.Zero);

                    infoVisual.StartAnimation("Opacity", show);
                    infoVisual.StartAnimation("Scale", scale);
                }

                //show.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                //show.DelayTime = TimeSpan.FromMilliseconds(150);

                //scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                //scale.DelayTime = TimeSpan.FromMilliseconds(150);

                //editVisual.StartAnimation("Opacity", show);
                //editVisual.StartAnimation("Scale", scale);
            }
            else
            {
                _expanded = true;

                var cols = 5;
                var rows = (int)Math.Ceiling((double)_reactions.Count / cols);

                var width = 8 + 5 * 34 - 2;
                var viewport = 8 + 6 * 34 - 6;
                var height = (rows + 1) * 34;

                ScrollingHost.VerticalScrollMode = ScrollMode.Auto;
                ScrollingHost.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

                ScrollingHost.HorizontalScrollMode = ScrollMode.Disabled;
                ScrollingHost.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

                ScrollingHost.HorizontalAlignment = HorizontalAlignment.Left;
                ScrollingHost.VerticalAlignment = VerticalAlignment.Top;

                Shadow.Height = height;
                Pill.Height = height;
                ScrollingHost.Height = rows * 34 - 4;

                BubbleMedium.Visibility = Visibility.Collapsed;
                BubbleOverlay.Visibility = Visibility.Collapsed;

                var pillShadow = Window.Current.Compositor.CreateDropShadow();
                pillShadow.BlurRadius = 16;
                pillShadow.Opacity = 0.14f;
                pillShadow.Color = Colors.Black;
                pillShadow.Mask = Pill.GetAlphaMask();

                var pillReceiver = Window.Current.Compositor.CreateSpriteVisual();
                pillReceiver.Shadow = pillShadow;
                pillReceiver.Size = new Vector2(viewport, height);
                pillReceiver.Offset = new Vector3(0, 8, 0);

                var receivers = Window.Current.Compositor.CreateContainerVisual();
                receivers.Children.InsertAtBottom(pillReceiver);
                receivers.Size = new Vector2(width, height + 18);

                ElementCompositionPreview.SetElementChildVisual(Shadow, receivers);

                Presenter.ColumnDefinitions.Clear();
                Presenter.RowDefinitions.Clear();

                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        var i = x + y * cols;

                        var button = Presenter.Children[i] as HyperlinkButton;
                        if (button == null)
                        {
                            if (i < _reactions.Count)
                            {
                                var item = _reactions[i];

                                var view2 = new LottieView();
                                view2.AutoPlay = false;
                                view2.IsLoopingEnabled = false;
                                view2.FrameSize = new Size(24, 24);
                                view2.DecodeFrameType = DecodePixelType.Logical;
                                view2.Width = 24;
                                view2.Height = 24;
                                view2.Tag = new object();

                                var file = item.AppearAnimation.StickerValue;
                                if (file.Local.IsDownloadingCompleted)
                                {
                                    view2.Source = UriEx.ToLocal(file.Local.Path);
                                }
                                else
                                {
                                    view2.Source = null;

                                    UpdateManager.Subscribe(view2, _message, file, /*UpdateReaction*/UpdateFile, true);

                                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                                    {
                                        _message.ProtoService.DownloadFile(file.Id, 32);
                                    }
                                }

                                button = new HyperlinkButton
                                {
                                    Tag = _reactions[i],
                                    Content = view2,
                                    Margin = new Thickness(0, 0, 10, 0),
                                    Style = BootStrapper.Current.Resources["EmptyHyperlinkButtonStyle"] as Style
                                };

                                button.Click += Reaction_Click;
                                Presenter.Children.Add(button);
                            }
                            else
                            {
                                continue;
                            }
                        }

                        if (button.Content is LottieView view && view.Tag != null)
                        {
                            view.Play();
                            view.Tag = null;
                        }

                        if (y > 0)
                        {
                            button.Margin = new Thickness(0, 10, 10, 0);
                        }

                        Grid.SetColumn(button, x);
                        Grid.SetRow(button, y);

                        if (y == 0)
                        {
                            Presenter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                        }
                    }

                    Presenter.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                }

                var rootVisual = ElementCompositionPreview.GetElementVisual(LayoutRoot);
                rootVisual.Clip = null;

                var visualPill = ElementCompositionPreview.GetElementVisual(Pill);
                var clip = visualPill.Clip as CompositionGeometricClip;
                var geometry = clip.Geometry as CompositionRoundedRectangleGeometry;

                var resize = visualPill.Compositor.CreateVector2KeyFrameAnimation();
                resize.InsertKeyFrame(0, geometry.Size);
                resize.InsertKeyFrame(1, new Vector2(geometry.Size.X, height));
                resize.Duration = TimeSpan.FromMilliseconds(150);

                geometry.StartAnimation("Size", resize);

                var offset = visualPill.Compositor.CreateVector3KeyFrameAnimation();
                offset.InsertKeyFrame(0, Vector3.Zero);
                offset.InsertKeyFrame(1, new Vector3((viewport - width) / 2f - 2, 30, 0));

                ElementCompositionPreview.SetIsTranslationEnabled(ScrollingHost, true);
                var scrollingVisual = ElementCompositionPreview.GetElementVisual(ScrollingHost);
                scrollingVisual.StartAnimation("Translation", offset);

                var opacity = visualPill.Compositor.CreateScalarKeyFrameAnimation();
                opacity.InsertKeyFrame(0, 1);
                opacity.InsertKeyFrame(1, 0);

                var presenterVisual = ElementCompositionPreview.GetElementVisual(_presenter);
                presenterVisual.StartAnimation("Opacity", opacity);

                FindName(nameof(InfoText));
                //FindName(nameof(EditButton));

                await this.UpdateLayoutAsync();

                var infoVisual = ElementCompositionPreview.GetElementVisual(InfoText);
                //var editVisual = ElementCompositionPreview.GetElementVisual(EditButton);

                infoVisual.CenterPoint = new Vector3(InfoText.ActualSize / 2, 0);
                //editVisual.CenterPoint = new Vector3(EditButton.ActualSize / 2, 0);

                var show = visualPill.Compositor.CreateScalarKeyFrameAnimation();
                show.InsertKeyFrame(0, 0);
                show.InsertKeyFrame(1, 1);

                var scale = visualPill.Compositor.CreateVector3KeyFrameAnimation();
                scale.InsertKeyFrame(0, Vector3.Zero);
                scale.InsertKeyFrame(1, Vector3.One);

                infoVisual.StartAnimation("Opacity", show);
                infoVisual.StartAnimation("Scale", scale);

                //show.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                //show.DelayTime = TimeSpan.FromMilliseconds(150);

                //scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                //scale.DelayTime = TimeSpan.FromMilliseconds(150);

                //editVisual.StartAnimation("Opacity", show);
                //editVisual.StartAnimation("Scale", scale);
            }
        }
    }
}
