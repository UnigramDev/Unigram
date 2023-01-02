//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Popups
{
    public sealed partial class CreateLinkPopup : TeachingTip
    {
        public CreateLinkPopup()
        {
            InitializeComponent();

            Title = Strings.Resources.CreateLink;
            ActionButtonContent = Strings.Resources.OK;
            CloseButtonContent = Strings.Resources.Cancel;
        }

        public string Text
        {
            get => TextField.Text;
            set => TextField.Text = value;
        }

        public string Link
        {
            get => LinkField.Text;
            set => LinkField.Text = value;
        }

        public bool IsValid { get; set; }

        private void TeachingTip_ActionButtonClick(TeachingTip sender, object args)
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                VisualUtilities.ShakeView(TextField);
                return;
            }

            if (IsUrlInvalid(Link))
            {
                VisualUtilities.ShakeView(LinkField);
                return;
            }

            IsValid = true;
            IsOpen = false;
        }

        private bool IsUrlInvalid(string url)
        {
            return !url.IsValidUrl();
        }

        private void TextField_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                LinkField.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
        }

        private void LinkField_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                TeachingTip_ActionButtonClick(null, null);
                e.Handled = true;
            }
        }

        private void TextField_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextField.Text))
            {
                TextField.Focus(FocusState.Keyboard);
            }
            else
            {
                LinkField.Focus(FocusState.Keyboard);
            }
        }

        public Task<bool> ShowQueuedAsync()
        {
            var tsc = new TaskCompletionSource<bool>();
            void handler(TeachingTip sender, TeachingTipClosedEventArgs args)
            {
                Closed -= handler;
                tsc.SetResult(IsValid);
            }

            if (Window.Current.Content is FrameworkElement element)
            {
                element.Resources["TeachingTip"] = this;
            }

            Closed += handler;
            IsOpen = true;

            return tsc.Task;
        }
    }
}
