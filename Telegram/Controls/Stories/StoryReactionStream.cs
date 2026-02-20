//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls.Media;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Stories
{
    // TODO: Rewrite to use plain animations without rendering callback
    public partial class StoryReactionStream : Canvas
    {
        private class ItemLayer
        {
            private readonly Visual _visual;

            public float Amplitude;
            public float Period;
            public float PhaseOffset;
            public float BaseX;
            public float VerticalVelocity;
            public float TimeValue = 0.0f;

            public ItemLayer(UIElement image, float amplitude, float period, float phaseOffset, float baseX, float verticalVelocity)
            {
                Amplitude = amplitude;
                Period = period;
                PhaseOffset = phaseOffset;
                BaseX = baseX;
                VerticalVelocity = verticalVelocity;

                _visual = ElementComposition.GetElementVisual(image);
                //super.init()


                //self.contents = image.cgImage
                //self.allowsEdgeAntialiasing = true

                var compositor = _visual.Compositor;
                var props = compositor.CreatePropertySet();
                props.InsertScalar("Amplitude", amplitude);
                props.InsertScalar("Period", period);
                props.InsertScalar("PhaseOffset", phaseOffset);
                props.InsertScalar("BaseX", baseX);
                props.InsertScalar("VerticalVelocity", verticalVelocity);
                props.InsertScalar("TimeValue", 0);
                props.InsertScalar("PhaseAngle", 0);

                Properties = props;
            }

            public CompositionPropertySet Properties { get; init; }

            public Vector2 Position
            {
                get => new Vector2(_visual.Offset.X, _visual.Offset.Y);
                set => _visual.Offset = new Vector3(value, 0);
            }

            public float RotationAngle
            {
                get => _visual.RotationAngleInDegrees;
                set => _visual.RotationAngleInDegrees = value;
            }

            public void StartAnimation(string propertyName, CompositionAnimation animation)
            {
                _visual.StartAnimation(propertyName, animation);
            }
        }

        private uint nextId = 0;
        private Dictionary<uint, ItemLayer> itemLayers = [];
        private object itemLayerContainer;
        private double previousTimestamp = 0.0;
        private CompositionVSync displayLink = new(60);
        private double previousPhysicsTimestamp = 0.0;

        public StoryReactionStream()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            //displayLink.Rendering += OnRendering;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            displayLink.Rendering -= OnRendering;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            UpdatePhysics();
        }

        public void Add(IClientService clientService, MessageSender senderId, long count)
        {
            //if (!IsConnected)
            //{
            //    return;
            //}

            var timestamp = Logger.TickCount / 1000d;
            if (timestamp < previousTimestamp + 0.2)
            {
                return;
            }

            previousTimestamp = timestamp;

            if (Children.Empty())
            {
                displayLink.Rendering += OnRendering;
            }

            var image = CreateBadge(clientService, senderId, count);

            void handler(object sender, object e)
            {
                image.Loaded -= handler;
                AddRenderedItem(image);
            }

            image.Loaded += handler;
            Children.Add(image);
        }

        private FrameworkElement CreateBadge(IClientService clientService, MessageSender senderId, long count)
        {
            var photo = new ProfilePicture
            {
                Source = ProfilePictureSource.MessageSender(clientService, senderId),
                Size = 16
            };

            var text = new TextBlock
            {
                Text = string.Format("{0} {1}", Icons.Premium, count),
                FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSymbols"] as FontFamily,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 2, 1)
            };

            Grid.SetColumn(text, 1);

            var root = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE8, 0xAB, 0x02)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(2)
            };

            root.ColumnDefinitions.Add(1, GridUnitType.Auto);
            root.ColumnDefinitions.Add(1, GridUnitType.Auto);

            root.Children.Add(photo);
            root.Children.Add(text);

            return root;
        }

        private void AddRenderedItem(UIElement image)
        {
            var id = nextId;
            nextId += 1;

            if (image is FrameworkElement element)
            {
                element.Margin = new Thickness(0, 0, -element.ActualWidth, -20);
            }


            var random = new LokiRng(seed0: id, seed1: 1, seed2: 0);
            var itemX = -image.ActualSize.X - 8.0f + 20.0f * (LokiRng.Random(withSeed0: id, seed1: 0, seed2: 0) - 0.5f);
            var phaseOffset = random.Next();
            var itemLayer = new ItemLayer(image, 0.0f + random.Next() * 6.0f, 1.5f + random.Next() * 2.0f, phaseOffset, itemX, -(1.0f + random.Next() * 0.2f) * 90.0f);
            //itemLayer.frame = CGRect(origin: CGPoint(x: itemX, y: -image.size.height * 0.5), size: image.size)
            itemLayer.Position = new Vector2(itemX, -20 * 0.5f);
            itemLayers[id] = itemLayer;
            //self.itemLayerContainer.addSublayer(itemLayer)


            var itemDuration = 1.2f + random.Next() * 0.8f;
            var delay = itemDuration - 0.1f - 0.18f;

            var visual = ElementComposition.GetElementVisual(image);
            var compositor = visual.Compositor;

            //var scale = compositor.CreateVector3KeyFrameAnimation();
            //scale.InsertKeyFrame(0, new Vector3(0.001f));
            //scale.InsertKeyFrame(1, new Vector3(1));
            //scale.Duration = TimeSpan.FromSeconds(0.2);

            //var alpha = compositor.CreateScalarKeyFrameAnimation();
            //alpha.InsertKeyFrame(0, 0);
            //alpha.InsertKeyFrame(1, 1);
            //alpha.Duration = TimeSpan.FromSeconds(0.1);

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                itemLayers.Remove(id);
                Children.Remove(image);

                if (Children.Empty())
                {
                    displayLink.Rendering -= OnRendering;
                }
            };

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector3(0.001f));
            scale.InsertKeyFrame(0.2f / itemDuration, new Vector3(1));
            scale.InsertKeyFrame(delay / itemDuration, new Vector3(1));
            scale.InsertKeyFrame(1, new Vector3(0.001f));
            scale.Duration = TimeSpan.FromSeconds(itemDuration);

            var alpha = compositor.CreateScalarKeyFrameAnimation();
            alpha.InsertKeyFrame(0, 0);
            alpha.InsertKeyFrame(0.1f / itemDuration, 1);
            alpha.InsertKeyFrame(delay / itemDuration, 1);
            alpha.InsertKeyFrame(1, 0);
            alpha.Duration = TimeSpan.FromSeconds(itemDuration);

            visual.CenterPoint = new Vector3(image.ActualSize / 2, 0);
            visual.StartAnimation("Scale", scale);
            visual.StartAnimation("Opacity", alpha);

            batch.End();
        }

        private void UpdatePhysics()
        {
            var timestamp = Logger.TickCount / 1000f;
            var dt = (float)Math.Max(1.0 / 120.0, Math.Min(1.0 / 30.0, timestamp - previousPhysicsTimestamp));

            previousPhysicsTimestamp = timestamp;

            foreach (var itemLayer in itemLayers.Values)
            {
                itemLayer.TimeValue += dt;
                var itemPhase = MathF.IEEERemainder((MathF.IEEERemainder(itemLayer.TimeValue, itemLayer.Period) / itemLayer.Period + itemLayer.PhaseOffset), 1.0f);
                var phaseAngle = itemPhase * MathF.PI * 2.0f;
                var phaseFraction = MathF.Sin(phaseAngle);


                var newX = itemLayer.BaseX + phaseFraction * itemLayer.Amplitude;
                var newY = itemLayer.Position.Y + itemLayer.VerticalVelocity * dt;
                itemLayer.Position = new Vector2(x: newX, y: newY);

                var horizontalVelocity = itemLayer.Amplitude * MathF.Cos(phaseAngle) * (MathF.PI * 2.0f / itemLayer.Period);
                var rotationAngle = MathF.Atan2(itemLayer.VerticalVelocity, horizontalVelocity) + MathF.PI * 0.5f;
                itemLayer.RotationAngle = rotationAngle;
            }
        }
    }
}
