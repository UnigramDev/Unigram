using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Common
{
    public class TLBitmapContext
    {
        private ConcurrentDictionary<object, Tuple<TLBitmapSource, WeakReference<ImageSource>>> _context = new ConcurrentDictionary<object, Tuple<TLBitmapSource, WeakReference<ImageSource>>>();

        public ImageSource this[TLPhoto photo, bool thumbnail = true]
        {
            get
            {
                if (photo == null)
                {
                    return null;
                }

                if (_context.TryGetValue(photo, out Tuple<TLBitmapSource, WeakReference<ImageSource>> reference) && 
                    reference.Item2.TryGetTarget(out ImageSource target))
                {
                    if (thumbnail == false)
                    {
                        reference.Item1.Download();
                    }

                    return target;
                }

                var bitmap = new TLBitmapSource(photo);
                _context[photo] = new Tuple<TLBitmapSource, WeakReference<ImageSource>>(bitmap, new WeakReference<ImageSource>(bitmap.Image));

                if (!thumbnail)
                {
                    bitmap.Download();
                }

                return bitmap.Image;
            }
        }

        public ImageSource this[TLDocument document, bool? thumbnail]
        {
            get
            {
                if (document == null)
                {
                    return null;
                }

                var key = (object)document;
                if (thumbnail == true)
                {
                    key = document.Id + "_thumbnail";
                }

                if (_context.TryGetValue(key, out Tuple<TLBitmapSource, WeakReference<ImageSource>> reference) &&
                    reference.Item2.TryGetTarget(out ImageSource target))
                {
                    if (thumbnail == null)
                    {
                        reference.Item1.Download();
                    }

                    return target;
                }

                var bitmap = new TLBitmapSource(document, thumbnail == true);
                _context[key] = new Tuple<TLBitmapSource, WeakReference<ImageSource>>(bitmap, new WeakReference<ImageSource>(bitmap.Image));

                if (thumbnail == null)
                {
                    bitmap.Download();
                }

                return bitmap.Image;


                //var context = _context;
                //if (thumbnail)
                //{
                //    context = _contextThumb;
                //}

                //if (context.TryGetValue(document, out Tuple<TLBitmapSource, WeakReference<ImageSource>> reference) &&
                //    reference.Item2.TryGetTarget(out ImageSource target))
                //{
                //    return target;
                //}

                //var bitmap = new TLBitmapSource(document, thumbnail);
                //context[document] = new Tuple<TLBitmapSource, WeakReference<ImageSource>>(bitmap, new WeakReference<ImageSource>(bitmap.Image));
                //return bitmap.Image;
            }
        }

        public ImageSource this[TLWebDocument document]
        {
            get
            {
                if (document == null)
                {
                    return null;
                }

                if (_context.TryGetValue(document, out Tuple<TLBitmapSource, WeakReference<ImageSource>> reference) &&
                    reference.Item2.TryGetTarget(out ImageSource target))
                {
                    return target;
                }

                var bitmap = new TLBitmapSource(document);
                _context[document] = new Tuple<TLBitmapSource, WeakReference<ImageSource>>(bitmap, new WeakReference<ImageSource>(bitmap.Image));
                return bitmap.Image;
            }
        }

        public ImageSource this[TLUser user]
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

                if (_context.TryGetValue(key, out Tuple<TLBitmapSource, WeakReference<ImageSource>> reference) &&
                    reference.Item2.TryGetTarget(out ImageSource target))
                {
                    return target;
                }

                var bitmap = new TLBitmapSource(user);
                _context[key] = new Tuple<TLBitmapSource, WeakReference<ImageSource>>(bitmap, new WeakReference<ImageSource>(bitmap.Image));
                return bitmap.Image;
            }
        }

        public ImageSource this[TLChat chat]
        {
            get
            {
                if (chat == null)
                {
                    return null;
                }

                if (_context.TryGetValue(chat.Photo, out Tuple<TLBitmapSource, WeakReference<ImageSource>> reference) &&
                    reference.Item2.TryGetTarget(out ImageSource target))
                {
                    return target;
                }

                var bitmap = new TLBitmapSource(chat);
                _context[chat.Photo] = new Tuple<TLBitmapSource, WeakReference<ImageSource>>(bitmap, new WeakReference<ImageSource>(bitmap.Image));
                return bitmap.Image;
            }
        }

        public ImageSource this[TLChatForbidden forbiddenChat]
        {
            get
            {
                if (forbiddenChat == null)
                {
                    return null;
                }

                if (_context.TryGetValue(forbiddenChat.Title, out Tuple<TLBitmapSource, WeakReference<ImageSource>> reference) &&
                    reference.Item2.TryGetTarget(out ImageSource target))
                {
                    return target;
                }

                var bitmap = new TLBitmapSource(forbiddenChat);
                _context[forbiddenChat.Title] = new Tuple<TLBitmapSource, WeakReference<ImageSource>>(bitmap, new WeakReference<ImageSource>(bitmap.Image));
                return bitmap.Image;
            }
        }

        public ImageSource this[TLChannel channel]
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

                if (_context.TryGetValue(key, out Tuple<TLBitmapSource, WeakReference<ImageSource>> reference) &&
                    reference.Item2.TryGetTarget(out ImageSource target))
                {
                    return target;
                }

                var bitmap = new TLBitmapSource(channel);
                _context[key] = new Tuple<TLBitmapSource, WeakReference<ImageSource>>(bitmap, new WeakReference<ImageSource>(bitmap.Image));
                return bitmap.Image;
            }
        }

        public ImageSource this[TLChannelForbidden forbiddenChannel]
        {
            get
            {
                if (forbiddenChannel == null)
                {
                    return null;
                }

                if (_context.TryGetValue(forbiddenChannel.Title, out Tuple<TLBitmapSource, WeakReference<ImageSource>> reference) &&
                    reference.Item2.TryGetTarget(out ImageSource target))
                {
                    return target;
                }

                var bitmap = new TLBitmapSource(forbiddenChannel);
                _context[forbiddenChannel.Title] = new Tuple<TLBitmapSource, WeakReference<ImageSource>>(bitmap, new WeakReference<ImageSource>(bitmap.Image));
                return bitmap.Image;
            }
        }

        public void Clear()
        {
            _context.Clear();
        }
    }
}
