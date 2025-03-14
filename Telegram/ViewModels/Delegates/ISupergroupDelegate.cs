//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;

namespace Telegram.ViewModels.Delegates
{
    public interface ISupergroupDelegate : IChatDelegate
    {
        void UpdateSupergroup(Chat chat, Supergroup group);
        void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo);
    }
}
