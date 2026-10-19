//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Windows.UI.Xaml;

namespace Telegram.Views.Popups
{
    public sealed partial class MoveGiftToWalletPopup : ModalPopup
    {
        public MoveGiftToWalletPopup(string address)
        {
            InitializeComponent();

            Title = Strings.Gift2MoveToMyWallet;

            UpdateAddress(WalletHelper.DisplayAddress(address));

            AppearIn.Text = string.Format("{0} {1} > {2} > {3}", Strings.Gift2MoveAppearIn, Strings.Settings, Strings.WalletAttachMoney, Strings.WalletCollectibles);

            PrimaryButtonContent = Strings.Gift2Move;
            PrimaryButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            SecondaryButtonContent = Strings.Cancel;
        }

        public bool KeepOnProfile => KeepOnProfileCheck.IsChecked == true;

        // No address yet when the wallet is still to be created, which the move does on its own.
        private void UpdateAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                AddressInfo.Visibility = Visibility.Collapsed;
                return;
            }

            var groups = new[]
            {
                AddressGroup0, AddressGroup1, AddressGroup2, AddressGroup3,
                AddressGroup4, AddressGroup5, AddressGroup6, AddressGroup7,
                AddressGroup8, AddressGroup9, AddressGroup10, AddressGroup11
            };

            for (int i = 0; i < groups.Length; i++)
            {
                var offset = i * 4;

                groups[i].Text = offset < address.Length
                    ? address.Substring(offset, Math.Min(4, address.Length - offset))
                    : string.Empty;
            }
        }
    }
}
