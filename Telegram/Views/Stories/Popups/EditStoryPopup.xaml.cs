//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Text.Json;
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
        private bool _confirming;

        // What GetStateKey said once the media was in.
        private string _initialState;

        public EditStoryPopup(INavigationService navigationService, IClientService clientService, StorageMedia media)
            : base(navigationService.XamlRoot)
        {
            InitializeComponent();

            _navigationService = navigationService;
            _clientService = clientService;
            _media = media;

            Surface.Initialize(clientService, media);
            Surface.InitializeFrame(FrameSize);
            Surface.HistoryChanged += Surface_HistoryChanged;

            CropToolbar.Attach(Surface.Cropper);

            // A story opens on the drawing, its main screen.
            PaintTabs.Attach(Surface, clientService, false);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            if (Surface.IsVideo)
            {
                FindName(nameof(TrimToolbar));

                Surface.InitializePlayer(false);

                TrimToolbar.Initialize(XamlRoot, Surface.Player, media, MaximumLength, false);
                TrimToolbar.EnableCover(Surface);

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
            // The decode first, as it runs off the UI thread, then the tools while it does: before
            // the media is in, so the editor never shows the crop tools first.
            var loading = Surface.Cropper.SetSourceAsync(_media.File, proportions: BitmapProportions.NineOverSixteen);

            ShowDrawTools();

            await loading;

            Surface.StartHistory();
            _initialState = GetStateKey();
        }

        // The toolbars let go of the player before the surface closes it.
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            PaintTabs.Deactivate();
            PaintToolbar?.Detach();
            TrimToolbar?.Close();

            CropToolbar.Detach();

            Surface.HistoryChanged -= Surface_HistoryChanged;
            Surface.Close();
        }

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (TrimToolbar != null && args.Key == VirtualKey.Space && !Surface.Entities.IsEditorFocused)
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

            var handled = true;

            if (args.Key == VirtualKey.Enter && args.Modifiers == VirtualKeyModifiers.None)
            {
                Accept_Click(null, null);
            }
            else if (args.Key == VirtualKey.Z && args.Modifiers == VirtualKeyModifiers.Control)
            {
                Surface.Undo();
            }
            else if ((args.Key == VirtualKey.Y && args.Modifiers == VirtualKeyModifiers.Control)
                || (args.Key == VirtualKey.Z && args.Modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)))
            {
                Surface.Redo();
            }
            else if (args.Key == VirtualKey.R && args.Modifiers == VirtualKeyModifiers.Control)
            {
                PaintTabs.OpenCropping();
                Surface.Cropper.RotateCounterclockwise();
            }
            else if (args.Key == VirtualKey.D && args.Modifiers == VirtualKeyModifiers.Control && PaintTabs.IsCropping)
            {
                PaintTabs.OpenDrawing();
            }
            else
            {
                // The drawing's keys while a drawing tab is open, then the video's.
                handled = (!PaintTabs.IsCropping && PaintToolbar?.ProcessKeyboardAccelerator(args.Key, args.Modifiers) == true)
                    || TrimToolbar?.ProcessKeyboardAccelerator(args.Key, args.Modifiers) == true;
            }

            if (handled)
            {
                args.Handled = true;
            }
        }

        protected override void OnBackRequestedOverride(object sender, BackRequestedRoutedEventArgs e)
        {
            if (Surface.HandleBack())
            {
                // Spent on what was in hand: the next back is free to leave.
                Cancel();
            }
            else
            {
                Cancel_Click(null, null);
            }

            e.Handled = true;
        }

        private async void Accept_Click(object sender, RoutedEventArgs e)
        {
            // Waiting for the files below leaves room for a second press.
            if (_accepting)
            {
                return;
            }

            _accepting = true;

            PaintToolbar.Save();

            var entities = Surface.Entities.Items;

            if (_clientService != null)
            {
                await PaintEntityRenderer.LoadFilesAsync(_clientService, entities);
            }

            _media.EditState = CreateEditState(entities);

            if (_media is StorageVideo video && TrimToolbar?.CanMute == true)
            {
                video.IsMuted = TrimToolbar.IsMuted;
            }

            // Over the editor rather than after it, so that backing out of it is going back to
            // editing.
            _postOptions ??= new PostStoryPopup(_clientService, _navigationService);

            // Paused under the sheet, and resumed only on going back to editing.
            var playing = Surface.Player?.IsPlaying == true;
            if (playing)
            {
                Surface.Player.Pause(true);
            }

            var confirm = await _navigationService.ShowPopupAsync(_postOptions, requestedTheme: ElementTheme.Dark);
            _accepting = false;

            if (confirm == ContentDialogResult.Primary)
            {
                Hide(ContentDialogResult.Primary);
            }
            else if (playing)
            {
                Surface.Player?.Pause(false);
            }
        }

        // Closing asks first only when it would drop something: the edit, as Done would write it,
        // differs from what it was once the media was in.
        private async void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_confirming)
            {
                return;
            }

            if (_initialState != null && GetStateKey() != _initialState)
            {
                _confirming = true;

                var confirm = await MessagePopup.ShowNestedAsync(XamlRoot, Strings.PhotoEditorDiscardAlert, Strings.DiscardChanges, Strings.Discard, Strings.Cancel, destructive: true, requestedTheme: ElementTheme.Dark);

                _confirming = false;

                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }
            }

            Hide(ContentDialogResult.Secondary);
        }

        // The edit as Next writes it, from what the layers last saved.
        private ImageGeneration CreateEditState(IReadOnlyList<PaintEntity> entities)
        {
            TimeSpan trimStartTime = default;
            TimeSpan trimStopTime = default;
            TimeSpan coverTime = default;

            if (TrimToolbar != null)
            {
                trimStartTime = TrimToolbar.TrimStartTime;
                trimStopTime = TrimToolbar.TrimStopTime;
                coverTime = TrimToolbar.CoverTime;
            }

            return new ImageGeneration
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
                CoverTime = coverTime,
                FrameSize = FrameSize,
                FrameTopColor = Surface.FrameTopColor,
                FrameBottomColor = Surface.FrameBottomColor
            };
        }

        // Everything Next would keep, the mute outside the edit included. Moving or restyling an
        // object is not in the undo history, so the history cannot tell this.
        private string GetStateKey()
        {
            PaintToolbar?.Save();

            var state = JsonSerializer.Serialize(CreateEditState(Surface.Entities.Items), GenerationJsonContext.Default.ImageGeneration);
            return TrimToolbar != null
                ? $"{state}|{TrimToolbar.IsMuted}"
                : state;
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            Surface.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            Surface.Redo();
        }

        private void Surface_HistoryChanged(object sender, EventArgs e)
        {
            UndoButton.IsEnabled = Surface.CanUndo;
            RedoButton.IsEnabled = Surface.CanRedo;
        }

        // The tools the editor opens with.
        private void ShowDrawTools()
        {
            Surface.Cropper.IsCropEnabled = false;

            FindName(nameof(PaintToolbar));

            PaintToolbar.Attach(Surface, PaintTabs);
            PaintToolbar.Resume(0);
        }

        // The crop is the first tab, so its row comes from the left and leaves that way. The
        // drawing stays as it is: the crop moves the media under it.
        private void PaintTabs_CroppingChanged(object sender, EventArgs e)
        {
            if (PaintTabs.IsCropping)
            {
                PaintToolbar.Suspend(-1);

                Surface.Cropper.IsCropEnabled = true;
                MediaEditorTransitions.SlideIn(CropToolbar, -1);
            }
            else
            {
                Surface.Cropper.IsCropEnabled = false;
                MediaEditorTransitions.SlideOut(CropToolbar, 1);

                PaintToolbar.Resume(1);
            }
        }
    }
}
