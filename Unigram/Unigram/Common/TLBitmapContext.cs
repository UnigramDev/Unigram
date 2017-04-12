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
        private ConcurrentDictionary<object, WeakReference<TLBitmapSource>> _context = new ConcurrentDictionary<object, WeakReference<TLBitmapSource>>();

        public BitmapImage this[TLPhoto photo]
        {
            get
            {
                if (photo == null)
                {
                    return null;
                }

                if (_context.TryGetValue(photo, out WeakReference<TLBitmapSource> reference) && 
                    reference.TryGetTarget(out TLBitmapSource target))
                {
                    return target.Image;
                }

                target = new TLBitmapSource(photo);
                _context[photo] = new WeakReference<TLBitmapSource>(target);
                return target.Image;
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

                if (_context.TryGetValue(document, out WeakReference<TLBitmapSource> reference) && 
                    reference.TryGetTarget(out TLBitmapSource target))
                {
                    return target.Image;
                }

                target = new TLBitmapSource(document);
                _context[document] = new WeakReference<TLBitmapSource>(target);
                return target.Image;
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

                if (_context.TryGetValue(key, out WeakReference<TLBitmapSource> reference) && 
                    reference.TryGetTarget(out TLBitmapSource target))
                {
                    return target.Image;
                }

                target = new TLBitmapSource(user);
                _context[key] = new WeakReference<TLBitmapSource>(target);
                return target.Image;
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

                if (_context.TryGetValue(chat.Photo, out WeakReference<TLBitmapSource> reference) && 
                    reference.TryGetTarget(out TLBitmapSource target))
                {
                    return target.Image;
                }

                target = new TLBitmapSource(chat);
                _context[chat.Photo] = new WeakReference<TLBitmapSource>(target);
                return target.Image;
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

                if (_context.TryGetValue(forbiddenChat.Title, out WeakReference<TLBitmapSource> reference) &&
                    reference.TryGetTarget(out TLBitmapSource target))
                {
                    return target.Image;
                }

                target = new TLBitmapSource(forbiddenChat);
                _context[forbiddenChat.Title] = new WeakReference<TLBitmapSource>(target);
                return target.Image;
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

                if (_context.TryGetValue(key, out WeakReference<TLBitmapSource> reference) && 
                    reference.TryGetTarget(out TLBitmapSource target))
                {
                    return target.Image;
                }

                target = new TLBitmapSource(channel);
                _context[key] = new WeakReference<TLBitmapSource>(target);
                return target.Image;
            }
        }

        public void Clear()
        {
            _context.Clear();
        }
    }
}
