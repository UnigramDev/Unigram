//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Entities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Controls
{
    /// <summary>
    /// The options row of a media editor's crop tab: quarter turns, the mirror, the proportions,
    /// the free rotation wheel and the button that resets the crop of an <see cref="ImageCropper"/>.
    /// </summary>
    public sealed partial class CropToolbar : Grid
    {
        private ImageCropper _cropper;

        private readonly ThemeShadowEx _shadow;

        public CropToolbar()
        {
            InitializeComponent();

            _shadow = new ThemeShadowEx(Card, 16);
        }

        public bool IsRotationVisible
        {
            get => RotationWheel.Visibility == Visibility.Visible;
            set => RotationWheel.Visibility = WheelSeparator.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Offers the proportions, which a story's fixed frame and a round crop do not.
        /// </summary>
        public bool CanChangeProportions
        {
            get => Proportions.Visibility == Visibility.Visible;
            set => Proportions.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        public void Attach(ImageCropper cropper)
        {
            _cropper = cropper;
            _cropper.Changed += Cropper_Changed;
        }

        public void Detach()
        {
            if (_cropper != null)
            {
                _cropper.Changed -= Cropper_Changed;
                _cropper = null;
            }
        }

        private void Cropper_Changed(object sender, EventArgs e)
        {
            RotationWheel.Angle = _cropper.Angle;
            ResetCrop.IsEnabled = !_cropper.IsDefault;

            Rotate.IsChecked = _cropper.Orientation != ImageRotation.None;
            Flip.IsChecked = _cropper.Flip != ImageFlip.None;
            Proportions.IsChecked = _cropper.Proportions != BitmapProportions.Custom;
        }

        private void Rotate_Click(object sender, RoutedEventArgs e)
        {
            _cropper.RotateCounterclockwise();
        }

        private void Flip_Click(object sender, RoutedEventArgs e)
        {
            _cropper.Mirror();
        }

        private void Proportions_Click(object sender, RoutedEventArgs e)
        {
            if (_cropper.Proportions != BitmapProportions.Custom)
            {
                _cropper.SetProportions(BitmapProportions.Custom);
                Proportions.IsChecked = false;
                return;
            }

            // Still unchecked until a ratio is picked.
            Proportions.IsChecked = false;

            var flyout = new MenuFlyout();

            foreach (var item in _cropper.GetProportions())
            {
                var option = new MenuFlyoutItem();
                option.Click += ProportionsItem_Click;
                option.Text = ProportionsToLabelConverter.Convert(item);
                option.Tag = item;
                option.MinWidth = 140;
                option.HorizontalContentAlignment = HorizontalAlignment.Center;

                flyout.Items.Add(option);
            }

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(Proportions, FlyoutPlacementMode.Bottom);
            }
        }

        private void ProportionsItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: BitmapProportions proportions })
            {
                _cropper.SetProportions(proportions);
                Proportions.IsChecked = true;
            }
        }

        private void ResetCrop_Click(object sender, RoutedEventArgs e)
        {
            _cropper.Reset();
        }

        private void RotationWheel_RotationStarted(object sender, EventArgs e)
        {
            _cropper.BeginRotation();
        }

        private void RotationWheel_RotationChanged(object sender, EventArgs e)
        {
            _cropper.SetAngle(RotationWheel.Angle);
        }

        private void RotationWheel_RotationCompleted(object sender, EventArgs e)
        {
            _cropper.EndRotation();
        }
    }
}
