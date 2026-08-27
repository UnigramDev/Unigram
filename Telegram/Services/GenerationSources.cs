//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.Foundation;
using Windows.Storage;
using SAP = Windows.Storage.AccessCache.StorageApplicationPermissions;

namespace Telegram.Services
{
    /// <summary>
    /// The app's handles on files it is sending.
    ///
    /// A token goes into the conversion string of every <c>inputFileGenerated</c>, and it is all
    /// TDLib keeps of the source: nothing else can reopen a file the app has no path to. A file
    /// sent as <c>inputFileLocal</c> needs one too, for a different reason - TDLib uploads straight
    /// from the user's path, and the picker's grant on it lasts only as long as the session, so an
    /// upload the user reopens the app to resume would find nothing to read.
    ///
    /// Nothing is ever released, and giving it back is harder than it looks:
    /// <c>FileManager::run_generate</c> skips generation only once the file has a local location or
    /// can be downloaded from the server, so a send that failed and is retried starts generation
    /// again from the same conversion string. Entries are reclaimed by
    /// <see cref="DownloadFolderService.RemoveOverflow"/> when the list nears its limit, which is
    /// also the only thing holding these and the download folder's entries to one budget.
    /// </summary>
    public static class GenerationSources
    {
        /// <summary>
        /// Takes a handle on a file TDLib is about to read, and returns the token that names it in
        /// the conversion string.
        /// </summary>
        public static string Add(IStorageItem item)
        {
            DownloadFolderService.RemoveOverflow();

            try
            {
                return SAP.FutureAccessList.Add(item);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
                return null;
            }
        }

        /// <summary>
        /// Throws when the file is gone, as the access list does: every caller is a conversion that
        /// reports the failure back to TDLib.
        /// </summary>
        public static IAsyncOperation<StorageFile> GetFileAsync(string token)
        {
            return SAP.FutureAccessList.GetFileAsync(token);
        }
    }
}
