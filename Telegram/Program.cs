//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

#if DISABLE_XAML_GENERATED_MAIN
using Telegram.Native;
using Telegram.Services;

namespace Telegram
{
    public static class Program
    {
        static void Main(string[] args)
        {
            if (AnimationEffects.Supported)
            {
                AnimationEffects.Initialize();

                if (AnimationEffects.Enabled)
                {
                    AnimationEffects.State = SettingsService.Current.AreSmoothTransitionsEnabled
                        ? AnimationEffectsState.Enabled
                        : AnimationEffectsState.Disabled;
                }
            }

            global::Windows.UI.Xaml.Application.Start((p) => new App());
        }
    }
}
#endif
