using System;
using Unigram.Controls;
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.UI.Xaml;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Popups
{
    public sealed partial class RoundVideoPopop : OverlayPage
    {
        public RoundVideoPopop()
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
