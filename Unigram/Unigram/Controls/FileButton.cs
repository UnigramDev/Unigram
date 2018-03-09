using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Xaml;

namespace Unigram.Controls
{
    public class FileButton : GlyphHyperlinkButton
    {
        public FileButton()
        {
            DefaultStyleKey = typeof(FileButton);
        }

        #region Progress



        public double Progress
        {
            get { return (double)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Progress.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register("Progress", typeof(double), typeof(FileButton), new PropertyMetadata(0.0));



        #endregion

        public void UpdateFile(MessageViewModel message, File file)
        {
            string glyph = null;
            var progress = 0;

            if (file.Local.IsDownloadingActive)
            {
                glyph = "\uE10A";
                progress = file.Local.DownloadedSize;
            }
            else if (file.Remote.IsUploadingActive)
            {
                glyph = "\uE10A";
                progress = file.Remote.UploadedSize;
            }
            else if (file.Local.IsDownloadingCompleted || file.Remote.IsUploadingCompleted)
            {
                //switch (owner)
                //{
                //    case Animation animation:
                //        glyph = "\uE906";
                //        break;
                //    case Audio audio:
                //    case Video video:
                //    case VideoNote videoNote:
                //    case VoiceNote voiceNote:
                //        glyph = "\uE102";
                //        break;
                //    case Document document:
                //        glyph = "\uE906";
                //        break;
                //}
            }
            else if (file.Local.CanBeDownloaded)
            {
                glyph = "\uE118";
            }

            Glyph = glyph;
            Progress = (double)progress / (double)file.Size;
        }
    }
}
