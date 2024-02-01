//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Windows.Management.Deployment;

namespace Telegram.Views.Popups
{
    public sealed partial class CloudUpdatePopup : ContentPopup
    {
        private bool _block = true;

        public CloudUpdatePopup()
        {
            InitializeComponent();
            Closing += OnClosing;
        }

        private void OnClosing(Windows.UI.Xaml.Controls.ContentDialog sender, Windows.UI.Xaml.Controls.ContentDialogClosingEventArgs args)
        {
            args.Cancel = _block;
        }

        public void Destroy()
        {
            _block = false;
            Hide();
        }

        public void UpdateProgress(DeploymentProgress progress)
        {
            Status.IsIndeterminate = progress.state == DeploymentProgressState.Queued || progress.percentage == 0;
            Status.Value = progress.percentage;
        }
    }
}
