//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.UI.Xaml.Automation.Peers;

namespace Telegram.Services
{
    /// <summary>
    /// Centralizes UI Automation client detection.
    /// </summary>
    public static class AccessibilityService
    {
        public static bool IsScreenReaderActive =>
            AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged);

        public static bool HasPropertyChangedListeners =>
            AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged);
    }
}
