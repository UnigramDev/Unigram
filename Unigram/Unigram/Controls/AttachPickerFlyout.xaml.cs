using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Core.Models;
using Unigram.Models;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls
{
    public sealed partial class AttachPickerFlyout : UserControl
    {
        public AttachPickerFlyout()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateView(Window.Current.Bounds.Width);
            Window.Current.SizeChanged += OnSizeChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.SizeChanged -= OnSizeChanged;
        }

        private void OnSizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            UpdateView(e.Size.Width);
        }

        private void UpdateView(double width)
        {
            //Library.MaxWidth = width < 500 ? width - 16 - 2 : 360;
            Library.MaxWidth = width < 500 ? width - 16 - 2 : 360;
            Library.MinWidth = Library.MaxWidth;
        }

        private void Library_ItemClick(object sender, ItemClickEventArgs e)
        {
            ItemClick?.Invoke(this, new MediaSelectedEventArgs((StorageMedia)e.ClickedItem, true));
        }

        private async void Camera_Click(object sender, RoutedEventArgs e)
        {
            var capture = new CameraCaptureUI();
            capture.PhotoSettings.AllowCropping = true;
            capture.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
            capture.PhotoSettings.MaxResolution = CameraCaptureUIMaxPhotoResolution.MediumXga;
            capture.VideoSettings.Format = CameraCaptureUIVideoFormat.Mp4;
            capture.VideoSettings.MaxResolution = CameraCaptureUIMaxVideoResolution.StandardDefinition;

            var file = await capture.CaptureFileAsync(CameraCaptureUIMode.PhotoOrVideo);
            if (file != null)
            {
                if (file.ContentType.Equals("video/mp4"))
                {
                    await file.CopyAsync(KnownFolders.CameraRoll, DateTime.Now.ToString("WIN_yyyyMMdd_HH_mm_ss") + ".mp4", NameCollisionOption.GenerateUniqueName);
                    ItemClick?.Invoke(this, new MediaSelectedEventArgs(await StorageVideo.CreateAsync(file, true), false));
                }
                else
                {
                    await file.CopyAsync(KnownFolders.CameraRoll, DateTime.Now.ToString("WIN_yyyyMMdd_HH_mm_ss") + ".jpg", NameCollisionOption.GenerateUniqueName);
                    ItemClick?.Invoke(this, new MediaSelectedEventArgs(await StoragePhoto.CreateAsync(file, true), false));
                }
            }
        }

        public event EventHandler<MediaSelectedEventArgs> ItemClick;
    }

    public class MediaSelectedEventArgs
    {
        public StorageMedia Item { get; private set; }

        public bool IsLocal { get; private set; }

        public MediaSelectedEventArgs(StorageMedia item, bool local)
        {
            Item = item;
            IsLocal = local;
        }
    }
}
