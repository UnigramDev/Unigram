using System;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Unigram.Native.Streaming;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TestStreamingPage : Page, IHandle<UpdateFile>
    {
        private File _file;
        private IProtoService _protoService;

        public TestStreamingPage()
        {
            this.InitializeComponent();

            _protoService = TLContainer.Current.Resolve<IProtoService>();
        }

        public void Handle(UpdateFile update)
        {
            if (_file == null || _file.Id != update.File.Id)
            {
                return;
            }

            _file = update.File;
            _ffmpegInterop?.UpdateFile(update.File);

            this.BeginOnUIThread(() =>
            {
                //_stream.Update(update.File);

                Progress.Maximum = update.File.Size;
                Progress.Value = update.File.Local.DownloadedPrefixSize;
            });
        }

        private FFmpegInteropMSS _ffmpegInterop;

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            _ffmpegInterop?.Dispose();

            //var test = await FFmpegInteropMSS.CreateFromStreamAsync(stream);
            _ffmpegInterop = new FFmpegInteropMSS(new FFmpegInteropConfig());
            var test = await _ffmpegInterop.CreateFromFileAsync(_protoService.Client, _file);
            var mss = test.GetMediaStreamSource();

            Element.Source = MediaSource.CreateFromMediaStreamSource(mss);
            Element.MediaPlayer.RealTimePlayback = true;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            _protoService.Send(new CancelDownloadFile(_file.Id, false));
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            _protoService.Send(new DownloadFile(_file.Id, 32, 0, 0));
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            _protoService.Send(new DeleteFileW(_file.Id));
            Get.IsEnabled = true;
        }

        private async void Button_Click_4(object sender, RoutedEventArgs e)
        {
            var chat = await _protoService.SendAsync(new SearchPublicChat("streamTest")) as Chat;
            if (chat == null || chat.LastMessage == null)
            {
                return;
            }

            var video = chat.LastMessage.Content as MessageVideo;
            if (video == null)
            {
                return;
            }

            _file = video.Video.VideoValue;

            Get.IsEnabled = false;
        }
    }
}
