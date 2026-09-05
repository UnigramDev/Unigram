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
        /// Takes a handle on a file the app itself is about to read by path, under a name the
        /// caller owns.
        /// <para>
        /// A path-based read goes through the <c>*FromApp</c> entry points, and those match the
        /// path against the app's persisted grants - the grant the picker gave lives on the
        /// <see cref="StorageFile"/> and never reaches them - so the file has to be in this list
        /// before it can be opened at all.
        /// </para>
        /// <para>
        /// One fixed token per feature, reused: unlike <see cref="Add"/>, whose entries TDLib may
        /// still need on a retry and which are therefore never released, these have no life beyond
        /// the read, and a token of their own keeps a feature to one entry instead of leaving one
        /// behind per use. Nothing protects it from <see cref="DownloadFolderService.RemoveOverflow"/>,
        /// which is harmless: the next call puts it back.
        /// </para>
        /// </summary>
        public static void AddOrReplace(string token, IStorageItem item)
        {
            DownloadFolderService.RemoveOverflow();

            try
            {
                SAP.FutureAccessList.AddOrReplace(token, item);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
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
