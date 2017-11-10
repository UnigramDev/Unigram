using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class MediaControl : ContentControl
    {
        public object Media
        {
            get
            {
                return Content;
            }
            set
            {
                var message = value as TLMessage;
                if (message?.Media is TLMessageMediaEmpty || !(message?.Media is TLMessageMediaBase))
                {
                    Content = null;
                }
                else
                {
                    Content = null;
                    Content = value;
                }

                ContentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        //protected override void OnContentChanged(object oldContent, object newContent)
        //{
        //    base.OnContentChanged(oldContent, newContent);

        //    ContentChanged?.Invoke(this, EventArgs.Empty);
        //}

        public event EventHandler ContentChanged;
    }
}
