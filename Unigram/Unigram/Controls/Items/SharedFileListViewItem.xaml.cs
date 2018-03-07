using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Items
{
    public sealed partial class SharedFileListViewItem : UserControl
    {
        private IProtoService _protoService;
        private IMessageDelegate _delegate;
        private Message _message;

        public SharedFileListViewItem()
        {
            InitializeComponent();
        }

        public void UpdateMessage(IProtoService protoService, IMessageDelegate delegato, Message message)
        {
            _protoService = protoService;
            _delegate = delegato;
            _message = message;

            var document = message.Content as MessageDocument;
            if (document == null)
            {
                return;
            }

            Ellipse.Background = UpdateEllipseBrush(document.Document);
            Title.Text = document.Document.FileName;

            UpdateFile(message, document.Document.DocumentValue);
        }

        public void UpdateFile(Message message, File file)
        {
            var document = message.Content as MessageDocument;
            if (document == null)
            {
                return;
            }

            if (document.Document.DocumentValue.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                Button.Glyph = "\uE10A";
                Button.Progress = (double)file.Local.DownloadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Remote.IsUploadingActive)
            {

                Button.Glyph = "\uE10A";
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Remote.UploadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                Button.Glyph = "\uE118";
                Button.Progress = 0;

                Subtitle.Text = FileSizeConverter.Convert(size) + " — " + UpdateTimeLabel(message);
            }
            else
            {
                Button.Glyph = "\uE160";
                Button.Progress = 1;

                Subtitle.Text = FileSizeConverter.Convert(size) + " — " + UpdateTimeLabel(message);
            }
        }

        private Brush UpdateEllipseBrush(Document document)
        {
            var brushes = new[]
            {
                App.Current.Resources["Placeholder0Brush"],
                App.Current.Resources["Placeholder1Brush"],
                App.Current.Resources["Placeholder2Brush"],
                App.Current.Resources["Placeholder3Brush"]
            };

            if (document == null)
            {
                return brushes[0] as SolidColorBrush;
            }

            var name = document.FileName.ToLower();
            if (name.Length > 0)
            {
                var color = -1;
                if (name.EndsWith(".doc") || name.EndsWith(".txt") || name.EndsWith(".psd"))
                {
                    color = 0;
                }
                else if (name.EndsWith(".xls") || name.EndsWith(".csv"))
                {
                    color = 1;
                }
                else if (name.EndsWith(".pdf") || name.EndsWith(".ppt") || name.EndsWith(".key"))
                {
                    color = 2;
                }
                else if (name.EndsWith(".zip") || name.EndsWith(".rar") || name.EndsWith(".ai") || name.EndsWith(".mp3") || name.EndsWith(".mov") || name.EndsWith(".avi"))
                {
                    color = 3;
                }
                else
                {
                    int idx;
                    var extension = (idx = name.LastIndexOf(".", StringComparison.Ordinal)) == -1 ? string.Empty : name.Substring(idx + 1);
                    if (extension.Length != 0)
                    {
                        color = extension[0] % brushes.Length;
                    }
                    else
                    {
                        color = name[0] % brushes.Length;
                    }
                }

                return brushes[color] as SolidColorBrush;
            }

            return brushes[0] as SolidColorBrush;
        }

        private string UpdateTimeLabel(Message message)
        {
            return BindConvert.Current.BannedUntil(message.Date);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var document = _message?.Content as MessageDocument;
            if (document == null)
            {
                return;
            }

            var file = document.Document.DocumentValue;
            if (file.Local.IsDownloadingActive)
            {
                _protoService.Send(new CancelDownloadFile(file.Id, false));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                _protoService.Send(new DownloadFile(file.Id, 1));
            }
            else
            {
                _delegate.OpenFile(file);
            }
        }
    }
}
