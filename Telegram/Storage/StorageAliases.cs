//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

// The app names files and folders through these, never through Windows.Storage. On UWP they are
// the WinRT types themselves, so the shipping build compiles to the code it always did; on WinUI
// they are the classes in this folder. Spelling a type out as global::Windows.Storage.StorageFile
// skips the alias without a word, so don't.
global using Telegram.Storage;
#if !WINUI3
global using FileItem = Windows.Storage.StorageFile;
global using FileItemIO = Windows.Storage.FileIO;
global using FileItemProperties = Windows.Storage.FileProperties.BasicProperties;
global using FolderItem = Windows.Storage.StorageFolder;
global using IFileSystemItem = Windows.Storage.IStorageItem;
#endif
