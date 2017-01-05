using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class FileExistsToGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var documentMedia = value as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                var document = documentMedia.Document as TLDocument;
                if (document != null)
                {
                    var fileName = document.GetFileName();
                    if (File.Exists(Path.Combine(ApplicationData.Current.TemporaryFolder.Path, fileName)))
                    {
                        if (TLMessage.IsVideo(document))
                        {
                            return Symbol.Play;
                        }

                        if (TLMessage.IsVoice(document))
                        {
                            return Symbol.Play;
                        }

                        return Symbol.Page2;
                    }
                }
            }

            //var videoMedia = value as TLMessageMediaVideo;
            //if (videoMedia != null)
            //{
            //    var video = videoMedia.Video as TLVideo;
            //    if (video != null)
            //    {
            //        var fileName = video.GetFileName();
            //        if (File.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName)) || Task.Run(() => File.Exists(videoMedia.IsoFileName)).Result)
            //        {
            //            return Symbol.Play;
            //        }
            //    }
            //}

            //var audioMedia = value as TLMessageMediaAudio;
            //if (audioMedia != null)
            //{
            //    var audio = audioMedia.Audio as TLAudio;
            //    if (audio != null)
            //    {
            //        var fileName = audio.GetFileName();
            //        if (File.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName)))
            //        {
            //            return Symbol.Play;
            //        }
            //    }
            //}

            return Symbol.Download;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
