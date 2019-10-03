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
    public sealed partial class WalletReceivePage : Page, IWalletReceiveDelegate
    {
        private CanvasBitmap _gemBitmap;

        public WalletReceiveViewModel ViewModel => DataContext as WalletReceiveViewModel;

        public WalletReceivePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<WalletReceiveViewModel, IWalletReceiveDelegate>(this);
        }

        private void OnCreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            args.TrackAsyncAction(OnCreateResourcesAsync(sender).AsAsyncAction());
        }

        private async Task OnCreateResourcesAsync(CanvasControl sender)
        {
            _gemBitmap = await CanvasBitmap.LoadAsync(sender, new Uri("ms-appx:///Assets/Images/WalletGem.png"));
        }

        private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null || viewModel.Address == null)
            {
                return;
            }

            var writer = new BarcodeWriterPixelData();
            writer.Options.Hints[EncodeHintType.ERROR_CORRECTION] = ErrorCorrectionLevel.H;
            writer.Options.Width = 768;
            writer.Options.Height = 768;
            writer.Format = BarcodeFormat.QR_CODE;

            var data = writer.Write($"ton://{viewModel.Address}");
            var bitmap = CanvasBitmap.CreateFromBytes(sender, data.Pixels, data.Width, data.Height, DirectXPixelFormat.B8G8R8A8UIntNormalized);

            args.DrawingSession.Transform = System.Numerics.Matrix3x2.CreateScale(256f / 768f);

            args.DrawingSession.DrawImage(bitmap);
            args.DrawingSession.DrawImage(_gemBitmap, new System.Numerics.Vector2((data.Width - _gemBitmap.SizeInPixels.Width) / 2f, (data.Height - _gemBitmap.SizeInPixels.Height) / 2f));
        }

        #region Delegate

        public void UpdateAddress(string address)
        {
            Canvas.Invalidate();
        }

        #endregion
    }
}
