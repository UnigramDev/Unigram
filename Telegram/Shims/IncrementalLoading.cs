//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace System.Runtime.InteropServices.WindowsRuntime
{
    public static class IncrementalLoading
    {
        public static IAsyncOperation<LoadMoreItemsResult> Run(Func<CancellationToken, Task<LoadMoreItemsResult>> taskProvider, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Telegram.Logger.Info(member: member, filePath: filePath, line: line);
            return AsyncInfo.Run(taskProvider);
        }
    }
}
