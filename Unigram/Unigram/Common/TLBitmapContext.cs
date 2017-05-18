using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Common
{
    public class TLBitmapContext
    {
        private ConcurrentDictionary<object, Tuple<TLBitmapSource, WeakReference<BitmapImage>>> _context = new ConcurrentDictionary<object, Tuple<TLBitmapSource, WeakReference<BitmapImage>>>();

        private ConcurrentDictionary<long, TLBitmapSource> _context2 = new ConcurrentDictionary<long, TLBitmapSource>();

        public BitmapImage this[TLPhoto photo, bool thumbnail = true]
        {
            get
            {
                if (photo == null)
                {
                    return null;
                }

                if (_context.TryGetValue(photo.Id, out Tuple<TLBitmapSource, WeakReference<BitmapImage>> reference) && 
                    reference.Item2.TryGetTarget(out BitmapImage target))
                {
                    if (thumbnail == false)
                    {
                        reference.Item1.Download();
                    }

                    return target;
                }

                var bitmap = new TLBitmapSource(photo);
                _context[photo.Id] = new Tuple<TLBitmapSource, WeakReference<BitmapImage>>(bitmap, new WeakReference<BitmapImage>(bitmap.Image));

                if (thumbnail == false)
                {
                    bitmap.Download();
                }

                return bitmap.Image;
            }
        }

        public BitmapImage this[TLDocument document]
        {
            get
            {
                if (document == null)
                {
                    return null;
                }

                if (_context.TryGetValue(document.Id, out Tuple<TLBitmapSource, WeakReference<BitmapImage>> reference) &&
                    reference.Item2.TryGetTarget(out BitmapImage target))
                {
                    return target;
                }

                var bitmap = new TLBitmapSource(document);
                _context[document.Id] = new Tuple<TLBitmapSource, WeakReference<BitmapImage>>(bitmap, new WeakReference<BitmapImage>(bitmap.Image));
                return bitmap.Image;
            }
        }

        public BitmapImage this[TLUser user]
        {
            get
            {
                if (user == null)
                {
                    return null;
                }

                var key = (object)user.Photo;
                if (key == null)
                {
                    key = user.FullName;
                }

                if (_context.TryGetValue(key, out Tuple<TLBitmapSource, WeakReference<BitmapImage>> reference) &&
                    reference.Item2.TryGetTarget(out BitmapImage target))
                {
                    return target;
                }

                var bitmap = new TLBitmapSource(user);
                _context[key] = new Tuple<TLBitmapSource, WeakReference<BitmapImage>>(bitmap, new WeakReference<BitmapImage>(bitmap.Image));
                return bitmap.Image;
            }
        }

        public BitmapImage this[TLChat chat]
        {
            get
            {
                if (chat == null)
                {
                    return null;
                }

                if (_context.TryGetValue(chat.Photo, out Tuple<TLBitmapSource, WeakReference<BitmapImage>> reference) &&
                    reference.Item2.TryGetTarget(out BitmapImage target))
                {
                    return target;
                }

                var bitmap = new TLBitmapSource(chat);
                _context[chat.Photo] = new Tuple<TLBitmapSource, WeakReference<BitmapImage>>(bitmap, new WeakReference<BitmapImage>(bitmap.Image));
                return bitmap.Image;
            }
        }

        public BitmapImage this[TLChatForbidden forbiddenChat]
        {
            get
            {
                if (forbiddenChat == null)
                {
                    return null;
                }

                if (_context.TryGetValue(forbiddenChat.Title, out Tuple<TLBitmapSource, WeakReference<BitmapImage>> reference) &&
                    reference.Item2.TryGetTarget(out BitmapImage target))
                {
                    return target;
                }

                var bitmap = new TLBitmapSource(forbiddenChat);
                _context[forbiddenChat.Title] = new Tuple<TLBitmapSource, WeakReference<BitmapImage>>(bitmap, new WeakReference<BitmapImage>(bitmap.Image));
                return bitmap.Image;
            }
        }

        public BitmapImage this[TLChannel channel]
        {
            get
            {
                if (channel == null)
                {
                    return null;
                }

                var key = (object)channel.Photo;
                if (key == null)
                {
                    key = channel.Title;
                }

                if (_context.TryGetValue(key, out Tuple<TLBitmapSource, WeakReference<BitmapImage>> reference) &&
                    reference.Item2.TryGetTarget(out BitmapImage target))
                {
                    return target;
                }

                var bitmap = new TLBitmapSource(channel);
                _context[key] = new Tuple<TLBitmapSource, WeakReference<BitmapImage>>(bitmap, new WeakReference<BitmapImage>(bitmap.Image));
                return bitmap.Image;
            }
        }

        public void Clear()
        {
            _context.Clear();
        }
    }
}
