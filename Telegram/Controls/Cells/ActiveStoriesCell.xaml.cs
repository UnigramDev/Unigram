//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Globalization;
using System.Numerics;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls.Cells
{
    public sealed partial class ActiveStoriesCell : UserControl
    {
        public ActiveStoriesCell()
        {
            InitializeComponent();
        }

        private ActiveStoriesViewModel _viewModel;
        public ActiveStoriesViewModel ViewModel => _viewModel;

        private string _automationName;
        public string GetAutomationName()
        {
            return _automationName;
        }

        public void Update(ActiveStoriesViewModel activeStories)
        {
            _viewModel = activeStories;

            var chat = activeStories.Chat;
            if (activeStories.ClientService.TryGetUser(chat, out User user))
            {
                if (activeStories.IsMyStory)
                {
                    _automationName = Strings.MyStory;
                    Title.Text = Strings.MyStory;
                }
                else
                {
                    _automationName = string.Format(Strings.AccDescrStoryBy, user.FirstName);
                    Title.Text = user.FirstName;
                }

                Photo.Source = ProfilePictureSource.User(activeStories.ClientService, user);

            }
            else
            {
                _automationName = string.Format(Strings.AccDescrStoryBy, chat.Title);
                Title.Text = chat.Title;

                Photo.Source = ProfilePictureSource.Chat(activeStories.ClientService, chat);
            }

            UpdatePosting();
        }

        public ChatActiveStories Trigger
        {
            set
            {
                Segments.UpdateActiveStories(value, 48, true);
                SegmentsSmall.UpdateActiveStories(value, 48, false);

                LiveBadge.Visibility = Segments.HasLiveBadge
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                // The binding can run before Update hands over a recycled cell's new item.
                if (_viewModel != null && _viewModel.ChatId == value?.ChatId)
                {
                    UpdatePosting();
                }
            }
        }

        private StoryViewModel _posting;
        private long _postingToken;

        // Your own posts, while any is in flight or failed, take over the title and the ring.
        private void UpdatePosting()
        {
            var posting = default(StoryViewModel);
            var failed = false;

            if (_viewModel.IsMyStory)
            {
                foreach (var story in _viewModel.Items)
                {
                    if (story.IsPostFailed)
                    {
                        failed = true;
                    }
                    else if (story.IsBeingPosted)
                    {
                        posting ??= story;
                    }
                }
            }

            if (failed)
            {
                Title.Text = Strings.FailedStory;

                Segments.ShowFailed(48);
                SegmentsSmall.ShowFailed(48, 3);

                posting = null;
            }
            else if (posting != null)
            {
                Title.Text = Strings.UploadingStory;
                UpdatePostingProgress(posting);
            }
            else if (_viewModel.IsMyStory)
            {
                Title.Text = Strings.MyStory;
            }

            // TDLib posts a chat's stories one at a time, so the first in flight is the one moving.
            if (_posting != posting)
            {
                _posting = posting;

                var file = posting?.GetFile();
                if (file != null)
                {
                    UpdateManager.Subscribe(this, posting.ClientService, file, ref _postingToken, UpdatePostingFile);

                    // The upload comes from the file, the conversion before it from GenerationService.
                    _viewModel.Aggregator.Subscribe<UpdateFileGenerationProgress>(this, Handle);
                }
                else
                {
                    UpdateManager.Unsubscribe(this, ref _postingToken);
                    _viewModel.Aggregator.Unsubscribe<UpdateFileGenerationProgress>(this);
                }
            }
        }

        private void Handle(UpdateFileGenerationProgress update)
        {
            this.BeginOnUIThread(UpdatePostingProgress);
        }

        private void UpdatePostingFile(File file)
        {
            UpdatePostingProgress();
        }

        private void UpdatePostingProgress()
        {
            if (_posting != null)
            {
                UpdatePostingProgress(_posting);
            }
        }

        private void UpdatePostingProgress(StoryViewModel posting)
        {
            var progress = posting.GetPostProgress();

            Segments.ShowProgress(48, progress);
            SegmentsSmall.ShowProgress(48, progress, 3);
        }

        public void Update(SelectorItem container, int index, int f, int l, CompositionPropertySet tracker, ExpressionAnimation expression)
        {
            if (tracker == null)
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(container);
            var ciccio = ElementComposition.GetElementVisual(PhotoCiccio);
            var photo = ElementComposition.GetElementVisual(PhotoRoot);
            var title = ElementComposition.GetElementVisual(Title);
            var gradient = ElementComposition.GetElementVisual(SegmentsRoot);
            var cross1 = ElementComposition.GetElementVisual(Segments);
            var cross2 = ElementComposition.GetElementVisual(SegmentsSmall);
            var live = ElementComposition.GetElementVisual(LiveBadge);

            var included = index >= f && index <= l;
            var clamp = Math.Clamp(index, f, l);

            var prevX = 72 * index + 6f - (12 * clamp) /* + 14 */;
            var nextX = 0;

            var diffX = prevX - nextX;
            //var boh = 1 - progress;

            ElementCompositionPreview.SetIsTranslationEnabled(container, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Title, true);

            //visual.Properties.InsertVector3("Translation", new Vector3(-prevX + diffX * boh, 0, 0));
            visual.CenterPoint = new Vector3(12 + 24);
            //visual.Scale = new Vector3(min + max * (1 - progress));

            var compositor = visual.Compositor;

            var visualTranslationX = compositor.CreateExpressionAnimation(string.Format(CultureInfo.InvariantCulture, "-{0} + {1} * _.Progress", prevX, diffX));

            var visualScale = compositor.CreateExpressionAnimation("0.5 + 0.5 * _.Progress");
            var clean = compositor.CreateExpressionAnimation("_.Progress");

            visualTranslationX.SetReferenceParameter("_", tracker);
            visualScale.SetReferenceParameter("_", tracker);
            clean.SetReferenceParameter("_", tracker);
            visual.StartAnimation("Translation.X", visualTranslationX);
            visual.StartAnimation("Scale.X", visualScale);
            visual.StartAnimation("Scale.Y", visualScale);

            if (index >= f && index < l)
            {
                // TODO: replace this with an ellipse in the UI
                var rect1 = CanvasGeometry.CreateRectangle(null, 0, 0, 48, 48);
                var elli1 = CanvasGeometry.CreateEllipse(null, 48 + 72 * 0, 24, 22, 22);
                var elli2 = CanvasGeometry.CreateEllipse(null, 48 + 72 * 1, 24, 22, 22);
                var group1 = CanvasGeometry.CreateGroup(null, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);
                var group2 = CanvasGeometry.CreateGroup(null, new[] { elli2, rect1 }, CanvasFilledRegionDetermination.Alternate);

                var geometry1 = compositor.CreatePathGeometry(new CompositionPath(group1));
                var clip1 = compositor.CreateGeometricClip(geometry1);

                var linear = compositor.CreateLinearEasingFunction();
                var pathAnimation = compositor.CreatePathKeyFrameAnimation();
                pathAnimation.InsertKeyFrame(0, new CompositionPath(group1), linear);
                pathAnimation.InsertKeyFrame(1, new CompositionPath(group2), linear);

                geometry1.StartAnimation("Path", pathAnimation);
                var controller = geometry1.TryGetAnimationController("Path");
                controller.Pause();
                controller.StartAnimation("Progress", clean);

                gradient.Clip = clip1;
            }
            else
            {
                gradient.Clip = null;
            }

            if (index > f && index <= l)
            {
                // TODO: replace this with an ellipse in the UI
                var rect1 = CanvasGeometry.CreateRectangle(null, 0, 0, 48, 48);
                var elli1 = CanvasGeometry.CreateEllipse(null, -0 + -72 * 0, 24, 22, 22);
                var elli2 = CanvasGeometry.CreateEllipse(null, -0 + -72 * 1, 24, 22, 22);
                var group1 = CanvasGeometry.CreateGroup(null, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);
                var group2 = CanvasGeometry.CreateGroup(null, new[] { elli2, rect1 }, CanvasFilledRegionDetermination.Alternate);

                var geometry1 = compositor.CreatePathGeometry(new CompositionPath(group1));
                var clip1 = compositor.CreateGeometricClip(geometry1);

                var linear = compositor.CreateLinearEasingFunction();
                var pathAnimation = compositor.CreatePathKeyFrameAnimation();
                pathAnimation.InsertKeyFrame(0, new CompositionPath(group1), linear);
                pathAnimation.InsertKeyFrame(1, new CompositionPath(group2), linear);

                geometry1.StartAnimation("Path", pathAnimation);
                var controller = geometry1.TryGetAnimationController("Path");
                controller.Pause();
                controller.StartAnimation("Progress", clean);

                photo.Clip = clip1;
            }
            else
            {
                photo.Clip = null;
            }

            photo.CenterPoint = new Vector3(24);
            //photo.Scale = new Vector3(min + max * (1 - progress));
            //title.Scale = new Vector3(min + max * (1 - progress));
            //photo.Opacity = included ? 1 : 1 - progress;
            //title.Opacity = 1 - progress;

            var distance = Math.Max(0, index - l) / 10f;
            var multiplier = 2 + Math.Max(0, 0.5f - distance);
            var normalizer = 1 + 0;

            var visualScale2 = compositor.CreateExpressionAnimation(string.Format(CultureInfo.InvariantCulture, "Max(0, {0} * _.Progress - {1})", multiplier, normalizer));
            var visualScale3 = compositor.CreateExpressionAnimation(string.Format(CultureInfo.InvariantCulture, "Max(0, 1 - ({0} * _.Progress - {1}))", multiplier, normalizer));

            visualScale2.SetReferenceParameter("_", tracker);
            visualScale3.SetReferenceParameter("_", tracker);

            title.StartAnimation("Opacity", visualScale2);
            live.StartAnimation("Opacity", visualScale2);
            cross1.StartAnimation("Opacity", visualScale2);
            cross2.StartAnimation("Opacity", visualScale3);

            if (included)
            {

                ciccio.StopAnimation("Opacity");
                ciccio.Opacity = 1;
            }
            else
            {
                ciccio.StartAnimation("Opacity", visualScale2);
            }

            return;
            //title.Properties.InsertVector3("Translation", new Vector3(0, -28 * progress, 0));







            var ellisss = compositor.CreateEllipseGeometry();
            ellisss.Radius = new Vector2(23);
            ellisss.Center = new Vector2(24, 24);

            var shape2 = compositor.CreateSpriteShape();
            shape2.Geometry = ellisss;
            shape2.StrokeBrush = compositor.CreateColorBrush(index == 1 ? Windows.UI.Colors.Red : Windows.UI.Colors.Blue);
            shape2.StrokeThickness = 2;

            var test = compositor.CreateShapeVisual();
            test.Size = new Vector2(48, 48);
            test.Shapes.Add(shape2);


            var visualScalezzzz = compositor.CreateExpressionAnimation(
                $"2 - 1 * _.Progress");

            visualScalezzzz.SetReferenceParameter("_", tracker);
            shape2.StartAnimation("StrokeThickness", visualScalezzzz);

            //ElementCompositionPreview.SetElementChildVisual(PhotoRoot, test);
        }

        public void Disconnect(SelectorItem container)
        {
            var visual = ElementComposition.GetElementVisual(container);
            var ciccio = ElementComposition.GetElementVisual(PhotoCiccio);
            var photo = ElementComposition.GetElementVisual(PhotoRoot);
            var title = ElementComposition.GetElementVisual(Title);
            var gradient = ElementComposition.GetElementVisual(SegmentsRoot);
            var cross1 = ElementComposition.GetElementVisual(Segments);
            var cross2 = ElementComposition.GetElementVisual(SegmentsSmall);
            var live = ElementComposition.GetElementVisual(LiveBadge);

            ElementCompositionPreview.SetIsTranslationEnabled(container, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Title, true);

            visual.StopAnimation("Translation.X");
            visual.StopAnimation("Scale.X");
            visual.StopAnimation("Scale.Y");

            visual.Properties.InsertVector3("Translation", Vector3.Zero);
            visual.Scale = Vector3.One;

            gradient.Clip = null;
            photo.Clip = null;

            photo.CenterPoint = new Vector3(24);

            title.StopAnimation("Opacity");
            live.StopAnimation("Opacity");
            cross1.StopAnimation("Opacity");
            cross2.StopAnimation("Opacity");
            ciccio.StopAnimation("Opacity");

            title.Opacity = 1;
            live.Opacity = 1;
            cross1.Opacity = 1;
            cross2.Opacity = 0;
            ciccio.Opacity = 1;
        }
    }
}
