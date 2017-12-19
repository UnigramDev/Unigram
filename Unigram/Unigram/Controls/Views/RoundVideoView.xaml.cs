using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Native;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls.Views
{
    public sealed partial class RoundVideoView : ContentDialogBase
    {
        public RoundVideoView()
        {
            this.InitializeComponent();

            //Loaded += OnLoaded;
            //Unloaded += RoundVideoView_Unloaded;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            OuterClip.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
            InnerClip.Center = new Point(e.NewSize.Width / 2, e.NewSize.Height / 2);
        }

        public IAsyncAction SetAsync(MediaCapture media, bool mirror)
        {
            return Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                Capture.Source = media;
                Capture.FlowDirection = mirror ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

                await media.StartPreviewAsync();
            });
        }
    }
}
