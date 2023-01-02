//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Unigram.Converters;
using Unigram.Navigation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Cells
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
                ? Strings.Resources.Username.ToLower()
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
            SubtitleLabel.Style = BootStrapper.Current.Resources[value ? "AccentCaptionTextBlockStyle" : "InfoCaptionTextBlockStyle"] as Style;

            SubtitleLabel.Text = value
                ? Strings.Resources.UsernameLinkActive
                : Strings.Resources.UsernameLinkInactive;
            BadgeIcon.Text = value
                ? Icons.LinkSide
                : Icons.LinkSideBroken;

            Handle.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
