//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Telegram.Common
{
    public static partial class ConnectedAnimationServiceEx
    {
        // Crash dump Telegram.exe.79384, 2026-08-28, opening the gallery on the island host:
        //
        //   ThemeShadowScene::SetupLights          <- DCompTreeHost + 0x258 is null
        //   ThemeShadowScene::EnsureInitialized
        //   ProjectedShadowManager::EnsureScene
        //   ProjectedShadowManager::UpdateCasterStatus
        //   CConnectedAnimation::StartSpriteAnimations
        //
        // The visual it wants is one a DCompTreeHost only has when the tree is hosted by a
        // CoreWindow, so any connected animation over a shadow caster takes the process down. There
        // is nothing to fall back to, so they are off here until the framework grows an island path.
        static partial void Unsupported(ref bool supported)
        {
            supported = false;
        }
    }
}
