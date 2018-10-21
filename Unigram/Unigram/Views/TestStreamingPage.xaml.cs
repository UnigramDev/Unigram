using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.Streams;
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
    public sealed partial class TestStreamingPage : Page, IHandle<UpdateFile>
    {
        private File _file;
        private StreamingRandomAccessStream _stream;
        private MseSourceBuffer _buffer;
        private MseStreamSource _source;
        private int _prefix;

        public TestStreamingPage()
        {
            this.InitializeComponent();
        }

        public async void Handle(UpdateFile update)
        {
            if (_file == null || _file.Id != update.File.Id)
            {
                return;
            }

            _file = update.File;

            this.BeginOnUIThread(() =>
            {
                //_stream.Update(update.File);

                Progress.Maximum = update.File.Size;
                Progress.Value = update.File.Local.DownloadedPrefixSize;
            });

            var length = (uint)(_file.Local.DownloadedPrefixSize - _prefix);
            if (length <= 1024 * 1024 * 4)
            {
                return;
            }

            var file = await StorageFile.GetFileFromPathAsync(_file.Local.Path);
            var stream = await file.OpenReadAsync();
            stream.Seek((ulong)_prefix);

            var buffer = new Windows.Storage.Streams.Buffer(length);
            var result = await stream.ReadAsync(buffer, length, InputStreamOptions.None);

            _buffer.AppendBuffer(result);

            _prefix = _file.Local.DownloadedPrefixSize;

            stream.Dispose();

            if (_file.Local.IsDownloadingCompleted)
            {
                _source.EndOfStream(MseEndOfStreamStatus.Success);
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var service = TLContainer.Current.Resolve<IProtoService>();
            var aggregator = TLContainer.Current.Resolve<IEventAggregator>();

            var chat = await service.SendAsync(new SearchPublicChat("streamTest")) as Chat;
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
            _stream = new StreamingRandomAccessStream(_file);
            aggregator.Subscribe(this);

            //Element.Source = MediaSource.CreateFromStream(_stream, "video/mp4");

            _source = new MseStreamSource();
            _source.Opened += (s, args) =>
            {
                _source.Duration = TimeSpan.FromSeconds(video.Video.Duration);
                _buffer = _source.AddSourceBuffer("video/mp4");
                _buffer.AppendWindowStart = TimeSpan.FromSeconds(10);
                _buffer.AppendWindowEnd = TimeSpan.MaxValue;
                _buffer.Mode = MseAppendMode.Sequence;
            };

            Element.Source = MediaSource.CreateFromMseStreamSource(_source);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var service = TLContainer.Current.Resolve<IProtoService>();
            service.Send(new CancelDownloadFile(_file.Id, false));
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var service = TLContainer.Current.Resolve<IProtoService>();
            service.Send(new DownloadFile(_file.Id, 32));
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            var service = TLContainer.Current.Resolve<IProtoService>();
            service.Send(new DeleteFileW(_file.Id));
        }

        class StreamingRandomAccessStream : IRandomAccessStream
        {
            private File _file;
            private ulong _position;
            private uint _count;
            private AutoResetEvent _wait = new AutoResetEvent(false);

            public StreamingRandomAccessStream(File file)
            {
                _file = file;
            }

            public void Update(File file)
            {
                _file = file;

                if ((ulong)file.Local.DownloadedPrefixSize > _position + _count)
                {
                    _wait.Set();
                }
            }

            public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
            {
                return AsyncInfo.Run<IBuffer, uint>(async (cancellationToken, progress) =>
                {
                    progress.Report(0);

                    if (_position + count > (ulong)_file.Local.DownloadedPrefixSize && _position + count < (ulong)_file.Size)
                    {
                        _count = count;
                        _wait.WaitOne();
                    }

                    var file = await StorageFile.GetFileFromPathAsync(_file.Local.Path);
                    var stream = await file.OpenReadAsync();
                    stream.Seek(_position);

                    var result = await stream.ReadAsync(buffer, count, options).AsTask(cancellationToken, progress);
                    stream.Dispose();

                    return result;
                });
            }

            public IInputStream GetInputStreamAt(ulong position)
            {
                throw new NotImplementedException();
            }

            public IOutputStream GetOutputStreamAt(ulong position)
            {
                throw new NotImplementedException();
            }

            public void Seek(ulong position)
            {
                _position = position;
            }

            public IRandomAccessStream CloneStream()
            {
                throw new NotImplementedException();
            }

            public bool CanRead => true;

            public bool CanWrite => false;

            public ulong Position => _position;

            public ulong Size { get => (ulong)_file.Size; set => throw new NotImplementedException(); }

            public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
            {
                throw new NotImplementedException();
            }

            public IAsyncOperation<bool> FlushAsync()
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }
        }
    }
}
