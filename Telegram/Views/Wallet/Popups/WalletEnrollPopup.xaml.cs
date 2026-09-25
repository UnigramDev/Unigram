//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services.Wallet;
using Windows.Security.Credentials;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// What the wallet should ask for before it spends anything. Modelled on tdesktop's.
    /// </summary>
    /// <remarks>
    /// The three are alternatives rather than layers - two of them would mean the weaker one
    /// decides - which is why they are radio buttons rather than switches.
    ///
    /// It only reports the method. Collecting the passcode is left to the caller, because both
    /// screens that can produce one are popups themselves and would have to open over this one.
    /// </remarks>
    public sealed partial class WalletEnrollPopup : ModalPopup
    {
        private readonly WalletVaultMethod? _current;

        private WalletEnrollPopup(WalletVaultMethod? current, WalletEnrollReason reason)
        {
            InitializeComponent();

            _current = current;

            if (reason == WalletEnrollReason.PasscodeDisabled)
            {
                Title = "[Passcode Is Being Turned Off]";
                SubtitleLabel.Text = "[Your wallet is protected by this passcode. Choose what should protect it instead.]";

                // Not "start asking for it" but "leave it on": the passcode is on its way out, and
                // this option is what stops it going.
                PasscodeTitle.Text = "[Keep asking for this passcode]";
                PasscodeSubtitle.Text = "[The passcode stays on, and keeps confirming your spending.]";
            }
            else
            {
                Title = "[Protect Your Wallet]";
            }

            PrimaryButtonText = Strings.Save;
            SecondaryButtonText = Strings.Cancel;
        }

        /// <summary>
        /// Asks, and answers with null if the user left without confirming.
        /// </summary>
        /// <param name="current">
        /// What guards the wallet now, pre-selected so the popup opens on the answer the user
        /// already gave. Null when nothing has been chosen yet.
        /// </param>
        public static async Task<WalletVaultMethod?> ShowAsync(INavigationService navigation, WalletVaultMethod? current, WalletEnrollReason reason)
        {
            var popup = new WalletEnrollPopup(current, reason);

            var confirm = await navigation.ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            return popup.Selected();
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();

            UpdateOptions();
        }

        private async void UpdateOptions()
        {
            var supported = false;

            try
            {
                // Whether a TPM-held credential can be created here, which is exactly the question
                // this option raises - unlike an unlock gate, where the same call says nothing
                // about whether the user can actually verify.
                supported = await KeyCredentialManager.IsSupportedAsync();
            }
            catch
            {
                // Hello is not something this device offers. Nothing to tell the user: an option
                // they never see is not one they are missing.
            }

            HelloOption.Visibility = supported
                ? Visibility.Visible
                : Visibility.Collapsed;

            switch (_current)
            {
                case WalletVaultMethod.Hello when supported:
                    HelloOption.IsChecked = true;
                    break;
                case WalletVaultMethod.Passcode:
                    PasscodeOption.IsChecked = true;
                    break;
                case WalletVaultMethod.None:
                    NoneOption.IsChecked = true;
                    break;
                default:
                    // Nothing chosen yet, so it opens on the strongest thing this device offers
                    // rather than on nothing: Save has to mean something when pressed without
                    // touching anything, and it should not mean "don't ask".
                    if (supported)
                    {
                        HelloOption.IsChecked = true;
                    }
                    else
                    {
                        PasscodeOption.IsChecked = true;
                    }
                    break;
            }
        }

        private WalletVaultMethod? Selected()
        {
            if (HelloOption.IsChecked == true)
            {
                return WalletVaultMethod.Hello;
            }
            else if (PasscodeOption.IsChecked == true)
            {
                return WalletVaultMethod.Passcode;
            }
            else if (NoneOption.IsChecked == true)
            {
                return WalletVaultMethod.None;
            }

            return null;
        }
    }
}
