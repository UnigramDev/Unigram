//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.Td
{
    class TdCompletionSource : TaskCompletionSource<BaseObject>, ClientResultHandler
    {
        private readonly Action<BaseObject> _closure;

        public TdCompletionSource(Action<BaseObject> closure)
        {
            _closure = closure;
        }

        public void OnResult(BaseObject result)
        {
            _closure(result);
            SetResult(result);
        }
    }
}
