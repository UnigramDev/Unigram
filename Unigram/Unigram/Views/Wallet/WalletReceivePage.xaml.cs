using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Wallet;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Graphics.DirectX;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ZXing;
using ZXing.QrCode.Internal;

namespace Unigram.Views.Wallet
{
    public sealed partial class WalletReceivePage : Page
    {
        public WalletReceiveViewModel ViewModel => DataContext as WalletReceiveViewModel;

        public WalletReceivePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<WalletReceiveViewModel>();

            if (ApiInformation.IsEnumNamedValuePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode", "BottomEdgeAlignedRight"))
            {
                MenuFlyout.Placement = FlyoutPlacementMode.BottomEdgeAlignedRight;
            }
        }

        #region Binding

        private string ConvertUrl(string address)
        {
            return $"ton://transfer/{address}";
        }

        private string ConvertAddress(string address)
        {
            if (address == null)
            {
                return string.Empty;
            }

            return address.Substring(0, address.Length / 2) + Environment.NewLine + address.Substring(address.Length / 2);
        }

        #endregion
    }
}
