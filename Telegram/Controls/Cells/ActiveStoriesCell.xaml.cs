//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Globalization;
using System.Numerics;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Windows.UI.Composition;
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

                Photo.SetUser(activeStories.ClientService, user, 40);

            }
            else
            {
                _automationName = string.Format(Strings.AccDescrStoryBy, chat.Title);
                Title.Text = chat.Title;

                Photo.SetChat(activeStories.ClientService, chat, 40);
            }

            Segments.UpdateActiveStories(activeStories.Item, 48, true);
            SegmentsSmall.UpdateActiveStories(activeStories.Item, 48, false);
        }

        public ChatActiveStories Trigger
        {
            set
            {
                Segments.UpdateActiveStories(value, 48, true);
                SegmentsSmall.UpdateActiveStories(value, 48, false);
            }
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

            var included = index >= f && index <= l;
            var clamp = Math.Clamp(index, f, l);

            var prevX = 64 * index + 10f - (12 * clamp) /* + 14 */;
            var nextX = 0;

            var diffX = prevX - nextX;
            //var boh = 1 - progress;

            ElementCompositionPreview.SetIsTranslationEnabled(container, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Title, true);

            //visual.Properties.InsertVector3("Translation", new Vector3(-prevX + diffX * boh, 0, 0));
            visual.CenterPoint = new Vector3(8 + 24);
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

            var device = CanvasDevice.GetSharedDevice();

            if (index >= f && index < l)
            {
                // TODO: replace this with an ellipse in the UI
                var rect1 = CanvasGeometry.CreateRectangle(device, 0, 0, 48, 48);
                var elli1 = CanvasGeometry.CreateEllipse(device, 48 + 64 * 0, 24, 22, 22);
                var elli2 = CanvasGeometry.CreateEllipse(device, 48 + 64 * 1, 24, 22, 22);
                var group1 = CanvasGeometry.CreateGroup(device, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);
                var group2 = CanvasGeometry.CreateGroup(device, new[] { elli2, rect1 }, CanvasFilledRegionDetermination.Alternate);

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

                ElementCompositionPreview.SetElementChildVisual(PhotoRoot, null);
            }
            else
            {
                gradient.Clip = null;
            }

            if (index > f && index <= l)
            {
                // TODO: replace this with an ellipse in the UI
                var rect1 = CanvasGeometry.CreateRectangle(device, 0, 0, 48, 48);
                var elli1 = CanvasGeometry.CreateEllipse(device, -0 + -64 * 0, 24, 22, 22);
                var elli2 = CanvasGeometry.CreateEllipse(device, -0 + -64 * 1, 24, 22, 22);
                var group1 = CanvasGeometry.CreateGroup(device, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);
                var group2 = CanvasGeometry.CreateGroup(device, new[] { elli2, rect1 }, CanvasFilledRegionDetermination.Alternate);

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
            cross1.StopAnimation("Opacity");
            cross2.StopAnimation("Opacity");
            ciccio.StopAnimation("Opacity");

            title.Opacity = 1;
            cross1.Opacity = 1;
            cross2.Opacity = 0;
            ciccio.Opacity = 1;
        }
    }
}
