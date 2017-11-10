using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls.Views
{
    public sealed partial class EditYourPhotoView : ContentDialogBase
    {
        //private InkPresenter _inkPresenter;

        public StorageFile Result { get; private set; }

        public EditYourPhotoView(StorageFile file)
        {
            InitializeComponent();

            //_inkPresenter = Canvas.InkPresenter;
            //_inkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Touch;

            Loaded += async (s, args) =>
            {
                await Cropper.SetSourceAsync(file);
            };
        }
        
        public bool IsCropEnabled
        {
            get { return this.Cropper.IsCropEnabled; }
            set { this.Cropper.IsCropEnabled = value; }
        }

        public ImageCroppingProportions CroppingProportions
        {
            get { return this.Cropper.Proportions; }
            set { this.Cropper.Proportions = value; }
        }

        public Rect CropRectangle
        {
            get { return this.Cropper.CropRectangle; }
        }

        public int MaxZoomFactor
        {
            get { return this.Cropper.MaxZoomFactor; }
            set { this.Cropper.MaxZoomFactor = value; }
        }

        public int ZoomFactor
        {
            get { return this.Cropper.CurrentZoomFactor; }
        }

        private async void Accept_Click(object sender, RoutedEventArgs e)
        {
            Result = await Cropper.CropAsync(640, 640);
            Hide(ContentDialogBaseResult.OK);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogBaseResult.Cancel);
        }
    }
}
