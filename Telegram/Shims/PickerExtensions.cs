//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.UI.Xaml;
#if WINUI3
using System;
using System.Threading.Tasks;
#endif

namespace Windows.Storage.Pickers
{
    public static class PickerExtensions
    {
#if WINUI3
        public static async Task<FileItem> PickSingleFileAsync(this FileOpenPicker picker, XamlRoot xamlRoot)
        {
            WindowContext.InitializeWithWindow(picker, xamlRoot);

            return (await picker.PickSingleFileAsync()).ToFileItem();
        }

        public static async Task<IReadOnlyList<FileItem>> PickMultipleFilesAsync(this FileOpenPicker picker, XamlRoot xamlRoot)
        {
            WindowContext.InitializeWithWindow(picker, xamlRoot);

            return (await picker.PickMultipleFilesAsync()).ToFileItems();
        }

        public static async Task<FileItem> PickSaveFileAsync(this FileSavePicker picker, XamlRoot xamlRoot)
        {
            WindowContext.InitializeWithWindow(picker, xamlRoot);

            return (await picker.PickSaveFileAsync()).ToFileItem();
        }

        public static async Task<FolderItem> PickSingleFolderAsync(this FolderPicker picker, XamlRoot xamlRoot)
        {
            WindowContext.InitializeWithWindow(picker, xamlRoot);

            return (await picker.PickSingleFolderAsync()).ToFolderItem();
        }
#else
        public static IAsyncOperation<StorageFile> PickSingleFileAsync(this FileOpenPicker picker, XamlRoot xamlRoot)
        {
            WindowContext.InitializeWithWindow(picker, xamlRoot);

            return picker.PickSingleFileAsync();
        }

        public static IAsyncOperation<IReadOnlyList<StorageFile>> PickMultipleFilesAsync(this FileOpenPicker picker, XamlRoot xamlRoot)
        {
            WindowContext.InitializeWithWindow(picker, xamlRoot);

            return picker.PickMultipleFilesAsync();
        }

        public static IAsyncOperation<StorageFile> PickSaveFileAsync(this FileSavePicker picker, XamlRoot xamlRoot)
        {
            WindowContext.InitializeWithWindow(picker, xamlRoot);

            return picker.PickSaveFileAsync();
        }

        public static IAsyncOperation<StorageFolder> PickSingleFolderAsync(this FolderPicker picker, XamlRoot xamlRoot)
        {
            WindowContext.InitializeWithWindow(picker, xamlRoot);

            return picker.PickSingleFolderAsync();
        }
#endif
    }
}

namespace Windows.Graphics.Capture
{
    public static class PickerExtensions
    {
        public static IAsyncOperation<GraphicsCaptureItem> PickSingleItemAsync(this GraphicsCapturePicker picker, XamlRoot xamlRoot)
        {
            WindowContext.InitializeWithWindow(picker, xamlRoot);

            return picker.PickSingleItemAsync();
        }
    }
}
