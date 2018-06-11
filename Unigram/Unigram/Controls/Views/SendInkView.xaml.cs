using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Core.Common;
using Unigram.Core.Models;
using Unigram.Entities;
using Unigram.Native;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class SendInkView : ContentDialogBase
    {
        public DialogViewModel ViewModel { get; set; }

        public SendInkView()
        {
            InitializeComponent();
            DataContext = this;
        }

        protected override void OnBackRequestedOverride(object sender, HandledEventArgs e)
        {
            e.Handled = true;
            Hide(ContentDialogBaseResult.None);
        }

        private async void Accept_Click(object sender, RoutedEventArgs e)
        {
            await GeneratePngFile(); //The PNG has to be generated now because it uses RenderTargetBitmap, the GIF can be generated later
            Hide(ContentDialogBaseResult.OK);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogBaseResult.Cancel);
        }

        public async Task<StorageFile> GetInkAsGifFile()
        {
            StorageFolder storageFolder =  ApplicationData.Current.TemporaryFolder;
            StorageFile temporaryFile = await storageFolder.CreateFileAsync("inkDrawing.gif", Windows.Storage.CreationCollisionOption.ReplaceExisting);
            using (IRandomAccessStream stream = await temporaryFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                await inkCanvas.InkPresenter.StrokeContainer.SaveAsync(stream);
            }
            return temporaryFile;
        }

        StorageFile pngFile;

        private async Task GeneratePngFile()
        {
            StorageFolder storageFolder = ApplicationData.Current.TemporaryFolder;
            StorageFile temporaryFile = await storageFolder.CreateFileAsync("inkDrawing.png", Windows.Storage.CreationCollisionOption.ReplaceExisting);
            using (IRandomAccessStream stream = await temporaryFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                RenderTargetBitmap rtb = new RenderTargetBitmap();
                await rtb.RenderAsync(inkCanvasBorder);
                var pixelBuffer = await rtb.GetPixelsAsync();
                var encoderSave = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoderSave.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)rtb.PixelWidth, (uint)rtb.PixelHeight, DisplayInformation.GetForCurrentView().LogicalDpi, DisplayInformation.GetForCurrentView().LogicalDpi, pixelBuffer.ToArray());
                await encoderSave.FlushAsync();

            }
            pngFile = temporaryFile;
        }

        public async Task<StorageFile> GetInkAsPngFile()
        {
            return pngFile;
        }

        private void backgroundButton_Click(object sender, RoutedEventArgs e)
        {
            if((inkCanvasBorder.Background as SolidColorBrush).Color == Windows.UI.Colors.White)
            {
                (inkCanvasBorder.Background as SolidColorBrush).Color = Windows.UI.Colors.Black;
            }
            else
            {
                (inkCanvasBorder.Background as SolidColorBrush).Color = Windows.UI.Colors.White;
            }
        }
    }
}
