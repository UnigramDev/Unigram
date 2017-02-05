using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class TransferButton : GlyphButton
    {
        #region Document

        public TLDocument Document
        {
            get { return (TLDocument)GetValue(DocumentProperty); }
            set { SetValue(DocumentProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Document.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DocumentProperty =
            DependencyProperty.Register("Document", typeof(TLDocument), typeof(TransferButton), new PropertyMetadata(null, OnDocumentChanged));

        private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TransferButton)d).OnDocumentChanged((TLDocument)e.NewValue, (TLDocument)e.OldValue);
        }

        private void OnDocumentChanged(TLDocument newValue, TLDocument oldValue)
        {
            if (newValue != null)
            {
                var fileName = newValue.GetFileName();
                if (File.Exists(FileUtils.GetTempFileName(fileName)))
                {
                    if (TLMessage.IsVideo(newValue))
                    {
                        Glyph = "\uE102";
                        return;
                    }

                    Glyph = "\uE160";
                    return;
                }

                Glyph = "\uE118";
            }
        }

        #endregion

        #region Media

        public TLMessageMediaBase Media
        {
            get { return (TLMessageMediaBase)GetValue(MediaProperty); }
            set { SetValue(MediaProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Media.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MediaProperty =
            DependencyProperty.Register("Media", typeof(TLMessageMediaBase), typeof(TransferButton), new PropertyMetadata(null, OnMediaChanged));

        private static void OnMediaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
        }

        #endregion

        public void UpdateGlyph()
        {
            OnDocumentChanged(Document, Document);
        }
    }
}
