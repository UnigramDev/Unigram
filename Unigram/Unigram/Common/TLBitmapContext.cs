using System;
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
        private Dictionary<object, WeakReference<TLBitmapSource>> _context = new Dictionary<object, WeakReference<TLBitmapSource>>();

        public BitmapImage this[TLPhoto photo]
        {
            get
            {
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
                if (user.Photo == null)
                {
                    user.Photo = new TLUserProfilePhotoEmpty();
                }

                if (_context.TryGetValue(user.Photo, out WeakReference<TLBitmapSource> reference) && 
                    reference.TryGetTarget(out TLBitmapSource target))
                {
                    return target.Image;
                }

                target = new TLBitmapSource(user);
                _context[user.Photo] = new WeakReference<TLBitmapSource>(target);
                return target.Image;
            }
        }

        public BitmapImage this[TLChat chat]
        {
            get
            {
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

        public BitmapImage this[TLChannel channel]
        {
            get
            {
                if (_context.TryGetValue(channel.Photo, out WeakReference<TLBitmapSource> reference) && 
                    reference.TryGetTarget(out TLBitmapSource target))
                {
                    return target.Image;
                }

                target = new TLBitmapSource(channel);
                _context[channel.Photo] = new WeakReference<TLBitmapSource>(target);
                return target.Image;
            }
        }

        public void Clear()
        {
            _context.Clear();
        }
    }
}
