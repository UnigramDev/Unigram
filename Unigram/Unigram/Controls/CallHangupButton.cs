//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

namespace Unigram.Controls
{
    public class CallHangupButton : AnimatedGlyphToggleButton
    {
        private Visual _visual;

        public CallHangupButton()
        {
            DefaultStyleKey = typeof(CallHangupButton);

            Checked += OnToggle;
            Unchecked += OnToggle;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new AnimatedGlyphToggleButtonAutomationPeer(this);
        }

        protected override void OnApplyTemplate()
        {
            var presenter = GetTemplateChild("ContentPresenter") as TextBlock;
            if (presenter != null)
            {
                var hangup = IsChecked == true;

                _visual = ElementCompositionPreview.GetElementVisual(presenter);
                _visual.CenterPoint = new Vector3(12, 12, 0);
                _visual.RotationAngleInDegrees = hangup ? 120 : 0;
                //_visual.Scale = hangup ? new Vector3(1.1f, 1.1f, 1) : Vector3.One;
            }

            base.OnApplyTemplate();
        }

        protected override void OnToggle()
        {
            //base.OnToggle();
        }

        private void OnToggle(object sender, RoutedEventArgs e)
        {
            if (_visual == null)
            {
                return;
            }

            var hangup = IsChecked == true;

            var rotation = _visual.Compositor.CreateScalarKeyFrameAnimation();
            rotation.InsertKeyFrame(0, hangup ? 0 : 120);
            rotation.InsertKeyFrame(1, hangup ? 120 : 0);

            //var scale = _visual.Compositor.CreateVector3KeyFrameAnimation();
            //scale.InsertKeyFrame(0, hangup ? Vector3.One : new Vector3(1.1f, 1.1f, 1));
            //scale.InsertKeyFrame(1, hangup ? new Vector3(1.1f, 1.1f, 1) : Vector3.One);

            _visual.StartAnimation("RotationAngleInDegrees", rotation);
            //_visual.StartAnimation("Scale", scale);
        }
    }
}
