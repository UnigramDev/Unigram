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

namespace Windows.Storage.Pickers
{
    public static class PickerExtensions
    {
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
