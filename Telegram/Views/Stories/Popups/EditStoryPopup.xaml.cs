//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Entities;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Factories;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Stories.Popups
{
    /// <summary>
    /// The story editor: the photo or video in a 9:16 frame, with what is drawn and placed laid
    /// out in the frame rather than on the media.
    /// </summary>
    public sealed partial class EditStoryPopup : OverlayWindow
    {
        // The frame strokes and objects are laid out in, at the size a photo story is posted.
        private static readonly Size FrameSize = StoryFactory.PhotoSize;

        // The longest stretch of video a story holds.
        private static readonly TimeSpan MaximumLength = TimeSpan.FromSeconds(59);

        private readonly INavigationService _navigationService;
        private readonly IClientService _clientService;
        private readonly StorageMedia _media;

        // Kept, so that coming back to it after more editing finds what was chosen.
        private PostStoryPopup _postOptions;

        private bool _accepting;

        public EditStoryPopup(INavigationService navigationService, IClientService clientService, StorageMedia media)
            : base(navigationService.XamlRoot)
        {
            InitializeComponent();

            _navigationService = navigationService;
            _clientService = clientService;
            _media = media;

            Surface.Initialize(clientService, media);
            Surface.InitializeFrame(FrameSize);
            Surface.Cropper.Changed += Cropper_Changed;

            CropToolbar.Attach(Surface.Cropper);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            if (Surface.IsVideo)
            {
                FindName(nameof(TrimToolbar));
                TrimToolbar.Visibility = Visibility.Visible;

                Surface.InitializePlayer(false);

                TrimToolbar.Initialize(XamlRoot, Surface.Player, media, MaximumLength, false);

                if (media is StorageVideo video)
                {
                    TrimToolbar.IsMuted = video.IsMuted;
                }
                else
                {
                    TrimToolbar.CanMute = false;
                }

                Surface.Play();
            }
        }

        /// <summary>
        /// The caption and settings the story is posted with, once the editor closed with Primary.
        /// </summary>
        public PostStoryPopup PostOptions => _postOptions;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await Surface.Cropper.SetSourceAsync(_media.File, proportions: BitmapProportions.NineOverSixteen);
        }

        // The toolbars let go of the player before the surface closes it.
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            PaintTabs?.Deactivate();
            PaintToolbar?.Detach();
            TrimToolbar?.Close();

            CropToolbar.Detach();

            Surface.Cropper.Changed -= Cropper_Changed;
            Surface.Close();
        }

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (TrimToolbar?.Visibility == Visibility.Visible && args.Key == VirtualKey.Space)
            {
                Surface.Player?.Toggle();
                args.Handled = true;
            }
        }

        private void OnProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs args)
        {
            // Keys typed into a text object belong to the text.
            if (Surface.Entities.IsEditorFocused)
            {
                return;
            }

            if (args.Key == VirtualKey.Enter && args.Modifiers == VirtualKeyModifiers.None)
            {
                Accept_Click(null, null);
                args.Handled = true;
            }
            // Draw mode, which the tabs mark: the toolbar itself hides while a sticker is selected.
            else if (PaintTabs?.Visibility == Visibility.Visible)
            {
                args.Handled = PaintToolbar.ProcessKeyboardAccelerator(args.Key, args.Modifiers);
            }
            else if (TrimToolbar?.Visibility == Visibility.Visible && TrimToolbar.ProcessKeyboardAccelerator(args.Key, args.Modifiers))
            {
                args.Handled = true;
            }
            else if (args.Key == VirtualKey.R && args.Modifiers == VirtualKeyModifiers.Control)
            {
                Rotate_Click(null, null);
                args.Handled = true;
            }
            else if (args.Key == VirtualKey.D && args.Modifiers == VirtualKeyModifiers.Control)
            {
                Draw_Click(null, null);
                args.Handled = true;
            }
        }

        protected override void OnBackRequestedOverride(object sender, BackRequestedRoutedEventArgs e)
        {
            if (!Surface.HandleBack())
            {
                Cancel_Click(null, null);
            }

            e.Handled = true;
        }

        private async void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (Surface.Cropper.IsCropEnabled)
            {
                // Waiting for the files below leaves room for a second press.
                if (_accepting)
                {
                    return;
                }

                _accepting = true;

                var entities = Surface.Entities.Items;

                if (_clientService != null)
                {
                    await PaintEntityRenderer.LoadFilesAsync(_clientService, entities);
                }

                TimeSpan trimStartTime = default;
                TimeSpan trimStopTime = default;

                if (TrimToolbar != null)
                {
                    trimStartTime = TrimToolbar.TrimStartTime;
                    trimStopTime = TrimToolbar.TrimStopTime;
                }

                _media.EditState = new ImageGeneration
                {
                    Rectangle = Surface.Cropper.CropRectangle,
                    Proportions = Surface.Cropper.Proportions,
                    Strokes = Surface.Canvas.Strokes,
                    Entities = entities,
                    Flip = Surface.Cropper.Flip,
                    Rotation = Surface.Cropper.Orientation,
                    Angle = Surface.Cropper.Angle,
                    TrimStartTime = trimStartTime,
                    TrimStopTime = trimStopTime,
                    FrameSize = FrameSize,
                    FrameTopColor = Surface.FrameTopColor,
                    FrameBottomColor = Surface.FrameBottomColor
                };

                if (_media is StorageVideo video && TrimToolbar?.CanMute == true)
                {
                    video.IsMuted = TrimToolbar.IsMuted;
                }

                // Over the editor rather than after it, so that backing out of it is going back to
                // editing.
                _postOptions ??= new PostStoryPopup(_clientService, _navigationService);

                var confirm = await _navigationService.ShowPopupAsync(_postOptions, requestedTheme: ElementTheme.Dark);
                _accepting = false;

                if (confirm == ContentDialogResult.Primary)
                {
                    Hide(ContentDialogResult.Primary);
                }
            }
            else
            {
                PaintToolbar.Commit();
                ShowCropTools();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (Surface.Cropper.IsCropEnabled)
            {
                Hide(ContentDialogResult.Secondary);
            }
            else
            {
                PaintToolbar.Revert();
                ShowCropTools();

                Cancel();
            }
        }

        private void ShowCropTools()
        {
            Surface.Cropper.IsCropEnabled = true;

            CropToolbar.Visibility = Visibility.Visible;
            BasicToolbar.Visibility = Visibility.Visible;
            TrimToolbar?.Visibility = Visibility.Visible;
            PaintTabs.Visibility = Visibility.Collapsed;
        }

        private void Rotate_Click(object sender, RoutedEventArgs e)
        {
            Surface.Cropper.RotateCounterclockwise();
        }

        private void Flip_Click(object sender, RoutedEventArgs e)
        {
            Surface.Cropper.Mirror();
        }

        private void Cropper_Changed(object sender, EventArgs e)
        {
            Rotate.IsChecked = Surface.Cropper.Orientation != ImageRotation.None;
            Flip.IsChecked = Surface.Cropper.Flip != ImageFlip.None;
        }

        private void Draw_Click(object sender, RoutedEventArgs e)
        {
            Surface.Cropper.IsCropEnabled = false;

            CropToolbar.Visibility = Visibility.Collapsed;
            BasicToolbar.Visibility = Visibility.Collapsed;
            TrimToolbar?.Visibility = Visibility.Collapsed;

            if (PaintToolbar == null)
            {
                FindName(nameof(PaintToolbar));
                FindName(nameof(PaintTabs));

                PaintToolbar.Attach(Surface, PaintTabs, _clientService);
            }

            PaintTabs.Visibility = Visibility.Visible;
            PaintToolbar.Open();
        }
    }
}
