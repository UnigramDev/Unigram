using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Unigram.Core.Media;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PlaygroundPage2 : Page
    {
        public PlaygroundPage2()
        {
            this.InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var id = "d5NTPDG7Js0";
            var token = new CancellationTokenSource();
            var youtube = new YouTubeVideoPlayerTask();
            var result = await youtube.DoWork(id, token.Token);

            //var id = "9430705";
            //var vimeo = new VimeoVideoPlayerTask();
            //var result = await vimeo.DoWork(id, new CancellationTokenSource().Token);

            //Element.Source = MediaSource.CreateFromUri(new Uri(result));
            //Element.MediaPlayer.Play();
        }
    }
}
