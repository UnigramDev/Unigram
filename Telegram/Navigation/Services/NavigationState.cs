//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;

namespace Telegram.Navigation.Services
{
    public class NavigationState : Dictionary<string, object>
    {
        public static NavigationState GetSwitchQuery(string query, long botId)
        {
            return new NavigationState { { "switch_query", query }, { "switch_bot", botId } };
        }
    }
}
