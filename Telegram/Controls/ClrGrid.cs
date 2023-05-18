//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    // This is needed by deprecated UpdateManager.Subscribe method.
    // Since Grid is a RCW type, it gets garbage collected as
    // EventAggregator uses a ConditionalWeakTable to hold subscribers.
    public class ClrGrid : Grid
    {

    }
}
