//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Windows.ApplicationModel.DataTransfer
{
    public static class ClipboardEx
    {
        public static void TrySetContent(DataPackage content)
        {
            try
            {
                Clipboard.SetContent(content);
                Clipboard.Flush();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public static DataPackageView TryGetContent()
        {
            try
            {
                return Clipboard.GetContent();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
                return null;
            }
        }

    }
}
