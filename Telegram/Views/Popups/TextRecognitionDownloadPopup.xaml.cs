//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class TextRecognitionDownloadPopup : ContentPopup
    {
        private long _fileToken;

        public TextRecognitionDownloadPopup(IClientService clientService, IEventAggregator aggregator, File file)
        {
            InitializeComponent();

            aggregator.Subscribe<UpdateTextRecognition>(this, Handle);

            UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile);
            UpdateFile(file);
        }

        private void Handle(UpdateTextRecognition update)
        {
            this.BeginOnUIThread(() => Hide(ContentDialogResult.Primary));
        }

        private void UpdateFile(object target, File file)
        {
            this.BeginOnUIThread(() => UpdateFile(file));
        }

        private void UpdateFile(File file)
        {
            Status.IsIndeterminate = false;
            Status.Value = (double)file.Local.DownloadedSize / file.Size;
        }
    }
}
