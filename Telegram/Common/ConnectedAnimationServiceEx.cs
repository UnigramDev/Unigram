//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Navigation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Animation;

namespace Telegram.Common
{
    public static class ConnectedAnimationServiceEx
    {
        public static TimeSpan DefaultDuration
        {
            get => ConnectedAnimationService.GetForCurrentView().DefaultDuration;
            set => ConnectedAnimationService.GetForCurrentView().DefaultDuration = value;
        }

        public static CompositionEasingFunction DefaultEasingFunction
        {
            get => ConnectedAnimationService.GetForCurrentView().DefaultEasingFunction;
            set => ConnectedAnimationService.GetForCurrentView().DefaultEasingFunction = value;
        }

        public static ConnectedAnimation PrepareToAnimate(string key, UIElement source)
        {
            return ConnectedAnimationService.GetForCurrentView().PrepareToAnimate(key + "_" + WindowContext.ForXamlRoot(source.XamlRoot).Id, source);
        }

        public static ConnectedAnimation GetAnimation(string key, XamlRoot xamlRoot)
        {
            return ConnectedAnimationService.GetForCurrentView().GetAnimation(key + "_" + WindowContext.ForXamlRoot(xamlRoot).Id);
        }

        public static bool TryStart(string key, UIElement destination, ConnectedAnimationConfiguration configuration = null)
        {
            var animation = GetAnimation(key, destination.XamlRoot);
            if (animation != null)
            {
                if (configuration != null)
                {
                    animation.Configuration = configuration;
                }

                return animation.TryStart(destination);
            }

            return false;
        }
    }
}
