//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls.Media;
using Telegram.Navigation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public sealed partial class UsernameInfoCell : Grid
    {
        public UsernameInfoCell()
        {
            InitializeComponent();
        }

        public string Value
        {
            set => SetValue(value);
        }

        private void SetValue(string value)
        {
            Placeholder.Text = string.IsNullOrEmpty(value)
                ? Strings.Username.ToLower()
                : string.Empty;
        }

        public string DisplayValue
        {
            set => SetDisplayValue(value);
        }

        private void SetDisplayValue(string value)
        {
            TitleLabel.Text = value;
        }

        public bool IsActive
        {
            set => SetIsActive(value);
        }

        private void SetIsActive(bool value)
        {
            Badge.Style = BootStrapper.Current.Resources[value ? "AccentCaptionBorderStyle" : "InfoCaptionBorderStyle"] as Style;
            SubtitleLabel.TextStyle = BootStrapper.Current.Resources[value ? "AccentCaptionTextBlockStyle" : "InfoCaptionTextBlockStyle"] as Style;

            SubtitleLabel.Text = value
                ? Strings.UsernameProfileLinkActive
                : Strings.UsernameProfileLinkInactive;
            BadgeIcon.Text = value
                ? Icons.LinkSide
                : Icons.LinkSideBroken;

            Handle.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
