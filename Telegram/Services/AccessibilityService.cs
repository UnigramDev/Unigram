//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;

namespace Telegram.Services
{
    /// <summary>
    /// Centralizes UI Automation client detection and notification events.
    /// </summary>
    public static class AccessibilityService
    {
        public static bool IsScreenReaderActive =>
            AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged);

        public static bool HasPropertyChangedListeners =>
            AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged);

        public static bool RaiseNotification(
            FrameworkElement owner,
            string text,
            string activityId,
            AutomationNotificationKind kind = AutomationNotificationKind.Other,
            AutomationNotificationProcessing processing = AutomationNotificationProcessing.ImportantMostRecent)
        {
            if (owner == null || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(owner);
            if (peer == null)
            {
                return false;
            }

            peer.RaiseNotificationEvent(kind, processing, text, activityId);
            return true;
        }
    }
}
