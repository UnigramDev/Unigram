//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Folders;
using Telegram.Views;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class ChatShareCell : GridEx, IMultipleElement
    {
        private bool _selected;

        public ChatShareCell()
        {
            InitializeComponent();

            Connected += OnLoaded;
            Disconnected += OnUnloaded;

            _selectionPhoto = ElementComposition.GetElementVisual(Photo);
            _selectionOutline = ElementComposition.GetElementVisual(SelectionOutline);
            _selectionPhoto.CenterPoint = new Vector3(18);
            _selectionOutline.CenterPoint = new Vector3(18);
            _selectionOutline.Opacity = 0;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_strokeToken == 0 && _ellipse != null)
            {
                Stroke?.RegisterColorChangedCallback(OnStrokeChanged, ref _strokeToken);
                OnStrokeChanged(Stroke, SolidColorBrush.ColorProperty);
            }

            if (_selectionStrokeToken == 0 && _stroke != null)
            {
                SelectionStroke?.RegisterColorChangedCallback(OnSelectionStrokeChanged, ref _selectionStrokeToken);
                OnSelectionStrokeChanged(SelectionStroke, SolidColorBrush.ColorProperty);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Stroke?.UnregisterColorChangedCallback(ref _strokeToken);
            SelectionStroke?.UnregisterColorChangedCallback(ref _selectionStrokeToken);
        }

        public void UpdateChat(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var chat = args.Item as Chat;
            if (chat == null)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;
            Tag = args.Item;

            if (args.Phase == 0)
            {
                if (chat.Type is ChatTypeSecret)
                {
                    TitleLabel.Foreground = BootStrapper.Current.Resources["TelegramSecretChatForegroundBrush"] as Brush;
                    TitleLabel.Text = Icons.LockClosedFilled14 + "\u00A0" + clientService.GetTitle(chat);
                }
                else
                {
                    TitleLabel.ClearValue(TextBlock.ForegroundProperty);
                    TitleLabel.Text = clientService.GetTitle(chat);
                }
            }
            else if (args.Phase == 2)
            {
                Photo.SetChat(clientService, chat, 36);
                Identity.SetStatus(clientService, chat);

                SelectionOutline.RadiusX = Photo.Shape == ProfilePictureShape.Ellipse ? 18 : 9;
                SelectionOutline.RadiusY = Photo.Shape == ProfilePictureShape.Ellipse ? 18 : 9;
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateUser(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var user = args.Item as User;
            if (user == null)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;
            Tag = args.Item;

            if (args.Phase == 0)
            {
                TitleLabel.Text = user.FullName();
            }
            else if (args.Phase == 2)
            {
                Photo.SetUser(clientService, user, 36);
                Identity.SetStatus(clientService, user);

                SelectionOutline.RadiusX = 18;
                SelectionOutline.RadiusY = 18;
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }


        public void UpdateMessageSender(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var messageSender = args.Item as MessageSender;
            if (messageSender == null)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;
            Tag = args.Item;

            if (args.Phase == 0)
            {
                if (clientService.TryGetUser(messageSender, out User user))
                {
                    TitleLabel.Text = user.FullName();
                }
                else if (clientService.TryGetChat(messageSender, out Chat chat))
                {
                    TitleLabel.Text = clientService.GetTitle(chat);
                }
            }
            else if (args.Phase == 2)
            {
                if (clientService.TryGetUser(messageSender, out User user))
                {
                    Photo.SetUser(clientService, user, 36);
                    Identity.SetStatus(clientService, user);
                }
                else if (clientService.TryGetChat(messageSender, out Chat chat))
                {
                    Photo.SetChat(clientService, chat, 36);
                    Identity.SetStatus(clientService, chat);
                }

                SelectionOutline.RadiusX = Photo.Shape == ProfilePictureShape.Ellipse ? 18 : 9;
                SelectionOutline.RadiusY = Photo.Shape == ProfilePictureShape.Ellipse ? 18 : 9;
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateSharedChat(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var chat = args.Item as Chat;
            if (chat == null)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;
            Tag = args.Item;

            if (args.Phase == 0)
            {
                if (chat.Type is ChatTypeSecret)
                {
                    TitleLabel.Foreground = BootStrapper.Current.Resources["TelegramSecretChatForegroundBrush"] as Brush;
                    TitleLabel.Text = Icons.LockClosedFilled14 + "\u00A0" + clientService.GetTitle(chat);
                }
                else
                {
                    TitleLabel.ClearValue(TextBlock.ForegroundProperty);
                    TitleLabel.Text = clientService.GetTitle(chat);
                }
            }
            else if (args.Phase == 2)
            {
                Photo.SetChat(clientService, chat, 36);
                Identity.SetStatus(clientService, chat);

                SelectionOutline.RadiusX = Photo.Shape == ProfilePictureShape.Ellipse ? 18 : 9;
                SelectionOutline.RadiusY = Photo.Shape == ProfilePictureShape.Ellipse ? 18 : 9;
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateChatFolder(FolderFlag folder)
        {
            Photo.Source = PlaceholderImage.GetGlyph(MainPage.GetFolderIcon(folder.Flag), (int)folder.Flag);

            SelectionOutline.RadiusX = 18;
            SelectionOutline.RadiusY = 18;

            switch (folder.Flag)
            {
                case ChatListFolderFlags.IncludeContacts:
                    TitleLabel.Text = Strings.FilterContacts;
                    break;
                case ChatListFolderFlags.IncludeNonContacts:
                    TitleLabel.Text = Strings.FilterNonContacts;
                    break;
                case ChatListFolderFlags.IncludeGroups:
                    TitleLabel.Text = Strings.FilterGroups;
                    break;
                case ChatListFolderFlags.IncludeChannels:
                    TitleLabel.Text = Strings.FilterChannels;
                    break;
                case ChatListFolderFlags.IncludeBots:
                    TitleLabel.Text = Strings.FilterBots;
                    break;

                case ChatListFolderFlags.ExcludeMuted:
                    TitleLabel.Text = Strings.FilterMuted;
                    break;
                case ChatListFolderFlags.ExcludeRead:
                    TitleLabel.Text = Strings.FilterRead;
                    break;
                case ChatListFolderFlags.ExcludeArchived:
                    TitleLabel.Text = Strings.FilterArchived;
                    break;
            }
        }

        #region Stroke

        private long _strokeToken;

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(ChatShareCell), new PropertyMetadata(null, OnStrokeChanged));

        private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatShareCell)d).OnStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnStrokeChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            oldValue?.UnregisterColorChangedCallback(ref _strokeToken);

            if (newValue == null || _ellipse == null)
            {
                return;
            }

            _ellipse.FillBrush = Window.Current.Compositor.CreateColorBrush(newValue.Color);

            if (IsConnected)
            {
                newValue.RegisterColorChangedCallback(OnStrokeChanged, ref _strokeToken);
            }
        }

        private void OnStrokeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var solid = sender as SolidColorBrush;
            if (solid == null || _ellipse == null)
            {
                return;
            }

            _ellipse.FillBrush = Window.Current.Compositor.CreateColorBrush(solid.Color);
        }

        #endregion

        #region SelectionStroke

        private long _selectionStrokeToken;

        public SolidColorBrush SelectionStroke
        {
            get => (SolidColorBrush)GetValue(SelectionStrokeProperty);
            set => SetValue(SelectionStrokeProperty, value);
        }

        public static readonly DependencyProperty SelectionStrokeProperty =
            DependencyProperty.Register("SelectionStroke", typeof(SolidColorBrush), typeof(ChatShareCell), new PropertyMetadata(null, OnSelectionStrokeChanged));

        private static void OnSelectionStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatShareCell)d).OnSelectionStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnSelectionStrokeChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            oldValue?.UnregisterColorChangedCallback(ref _selectionStrokeToken);

            if (newValue == null || _stroke == null)
            {
                return;
            }

            _stroke.FillBrush = Window.Current.Compositor.CreateColorBrush(newValue.Color);

            if (IsConnected)
            {
                newValue.RegisterColorChangedCallback(OnSelectionStrokeChanged, ref _selectionStrokeToken);
            }
        }

        private void OnSelectionStrokeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var solid = sender as SolidColorBrush;
            if (solid == null || _stroke == null)
            {
                return;
            }

            _stroke.FillBrush = Window.Current.Compositor.CreateColorBrush(solid.Color);
        }

        #endregion

        #region Selection Animation

        private readonly Visual _selectionOutline;
        private readonly Visual _selectionPhoto;

        private CompositionPathGeometry _polygon;
        private CompositionSpriteShape _ellipse;
        private CompositionSpriteShape _stroke;
        private ShapeVisual _visual;

        private CompositionBrush GetBrush(DependencyProperty dp, ref long token, DependencyPropertyChangedCallback callback)
        {
            var value = GetValue(dp);
            if (value is SolidColorBrush solid)
            {
                if (IsConnected)
                {
                    solid.RegisterColorChangedCallback(callback, ref token);
                }

                return Window.Current.Compositor.CreateColorBrush(solid.Color);
            }

            return Window.Current.Compositor.CreateColorBrush(Colors.Black);
        }

        private void InitializeSelection()
        {
            static CompositionPath GetCheckMark()
            {
                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    //builder.BeginFigure(new Vector2(3.821f, 7.819f));
                    //builder.AddLine(new Vector2(6.503f, 10.501f));
                    //builder.AddLine(new Vector2(12.153f, 4.832f));
                    builder.BeginFigure(new Vector2(5.821f, 9.819f));
                    builder.AddLine(new Vector2(7.503f, 12.501f));
                    builder.AddLine(new Vector2(14.153f, 6.832f));
                    builder.EndFigure(CanvasFigureLoop.Open);
                    result = CanvasGeometry.CreatePath(builder);
                }
                return new CompositionPath(result);
            }

            var compositor = Window.Current.Compositor;
            //12.711,5.352 11.648,4.289 6.5,9.438 4.352,7.289 3.289,8.352 6.5,11.563

            var polygon = compositor.CreatePathGeometry();
            polygon.Path = GetCheckMark();

            var shape1 = compositor.CreateSpriteShape();
            shape1.Geometry = polygon;
            shape1.StrokeThickness = 1.5f;
            shape1.StrokeBrush = compositor.CreateColorBrush(Colors.White);

            var ellipse = compositor.CreateEllipseGeometry();
            ellipse.Radius = new Vector2(8);
            ellipse.Center = new Vector2(10);

            var shape2 = compositor.CreateSpriteShape();
            shape2.Geometry = ellipse;
            shape2.FillBrush = GetBrush(StrokeProperty, ref _strokeToken, OnStrokeChanged);

            var outer = compositor.CreateEllipseGeometry();
            outer.Radius = new Vector2(10);
            outer.Center = new Vector2(10);

            var shape3 = compositor.CreateSpriteShape();
            shape3.Geometry = outer;
            shape3.FillBrush = GetBrush(SelectionStrokeProperty, ref _selectionStrokeToken, OnSelectionStrokeChanged);

            var visual = compositor.CreateShapeVisual();
            visual.Shapes.Add(shape3);
            visual.Shapes.Add(shape2);
            visual.Shapes.Add(shape1);
            visual.Size = new Vector2(20, 20);
            visual.Offset = new Vector3(36 - 17, 36 - 17, 0);
            visual.CenterPoint = new Vector3(8);
            visual.Scale = new Vector3(0);

            ElementCompositionPreview.SetElementChildVisual(PhotoPanel, visual);

            _polygon = polygon;
            _ellipse = shape2;
            _stroke = shape3;
            _visual = visual;
        }

        public void UpdateState(bool selected, bool animate, bool multiple)
        {
            if (_selected == selected)
            {
                return;
            }

            if (_visual == null)
            {
                InitializeSelection();
            }

            if (animate)
            {
                var compositor = Window.Current.Compositor;

                var anim3 = compositor.CreateScalarKeyFrameAnimation();
                anim3.InsertKeyFrame(selected ? 0 : 1, 0);
                anim3.InsertKeyFrame(selected ? 1 : 0, 1);

                var anim1 = compositor.CreateScalarKeyFrameAnimation();
                anim1.InsertKeyFrame(selected ? 0 : 1, 0);
                anim1.InsertKeyFrame(selected ? 1 : 0, 1);
                anim1.DelayTime = TimeSpan.FromMilliseconds(anim1.Duration.TotalMilliseconds / 2);
                anim1.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

                var anim2 = compositor.CreateVector3KeyFrameAnimation();
                anim2.InsertKeyFrame(selected ? 0 : 1, new Vector3(0));
                anim2.InsertKeyFrame(selected ? 1 : 0, new Vector3(1));

                _polygon.StartAnimation("TrimEnd", anim1);
                _visual.StartAnimation("Scale", anim2);
                _visual.StartAnimation("Opacity", anim3);

                var anim4 = compositor.CreateVector3KeyFrameAnimation();
                anim4.InsertKeyFrame(selected ? 0 : 1, new Vector3(1));
                anim4.InsertKeyFrame(selected ? 1 : 0, new Vector3(28f / 36f));

                var anim5 = compositor.CreateVector3KeyFrameAnimation();
                anim5.InsertKeyFrame(selected ? 1 : 0, new Vector3(1));
                anim5.InsertKeyFrame(selected ? 0 : 1, new Vector3(28f / 36f));

                _selectionPhoto.StartAnimation("Scale", anim4);
                _selectionOutline.StartAnimation("Scale", anim5);
                _selectionOutline.StartAnimation("Opacity", anim3);
            }
            else
            {
                _polygon.TrimEnd = selected ? 1 : 0;
                _visual.Scale = new Vector3(selected ? 1 : 0);
                _visual.Opacity = selected ? 1 : 0;

                _selectionPhoto.Scale = new Vector3(selected ? 28f / 36f : 1);
                _selectionOutline.Scale = new Vector3(selected ? 1 : 28f / 36f);
                _selectionOutline.Opacity = selected ? 1 : 0;
            }

            _selected = selected;
        }

        #endregion
    }
}
