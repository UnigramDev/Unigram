//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System.Numerics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public partial class NavigationButton : ToggleButton
    {
        private AnimatedIcon BackContent;
        private AnimatedIcon PaneContent;

        public NavigationButton()
        {
            DefaultStyleKey = typeof(NavigationButton);

            Checked += OnToggle;
            Unchecked += OnToggle;
        }

        protected override void OnApplyTemplate()
        {
            BackContent = GetTemplateChild(nameof(BackContent)) as AnimatedIcon;
            PaneContent = GetTemplateChild(nameof(PaneContent)) as AnimatedIcon;

            base.OnApplyTemplate();
        }

        protected override void OnToggle()
        {
            // Do nothing
        }

        private void OnToggle(object sender, RoutedEventArgs e)
        {
            var show = IsChecked == true;

            var visualShow = ElementComposition.GetElementVisual(show ? BackContent : PaneContent);
            var visualHide = ElementComposition.GetElementVisual(show ? PaneContent : BackContent);

            BackContent.Visibility = Visibility.Visible;
            PaneContent.Visibility = Visibility.Visible;

            visualShow.CenterPoint = new Vector3(8);
            visualHide.CenterPoint = new Vector3(8);

            var hide1 = visualShow.Compositor.CreateVector3KeyFrameAnimation();
            hide1.InsertKeyFrame(0, new Vector3(1));
            hide1.InsertKeyFrame(1, new Vector3(0));

            var hide2 = visualShow.Compositor.CreateScalarKeyFrameAnimation();
            hide2.InsertKeyFrame(0, 1);
            hide2.InsertKeyFrame(1, 0);

            visualHide.StartAnimation("Scale", hide1);
            visualHide.StartAnimation("Opacity", hide2);

            var show1 = visualShow.Compositor.CreateVector3KeyFrameAnimation();
            show1.InsertKeyFrame(1, new Vector3(1));
            show1.InsertKeyFrame(0, new Vector3(0));

            var show2 = visualShow.Compositor.CreateScalarKeyFrameAnimation();
            show2.InsertKeyFrame(1, 1);
            show2.InsertKeyFrame(0, 0);

            visualShow.StartAnimation("Scale", show1);
            visualShow.StartAnimation("Opacity", show2);
        }
    }
}
