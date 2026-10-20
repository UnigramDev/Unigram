//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    /// <summary>
    /// The free rotation wheel under an <see cref="ImageCropper"/>, and the button that resets the crop.
    /// </summary>
    public sealed partial class CropToolbar : Grid
    {
        private ImageCropper _cropper;

        public CropToolbar()
        {
            InitializeComponent();
        }

        public bool IsRotationVisible
        {
            get => RotationWheel.Visibility == Visibility.Visible;
            set => RotationWheel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
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
            ResetCrop.Visibility = _cropper.IsDefault
                ? Visibility.Collapsed
                : Visibility.Visible;
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
