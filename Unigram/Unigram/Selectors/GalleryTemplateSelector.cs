using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class GalleryTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ImageTemplate { get; set; }

        public DataTemplate VideoTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is GalleryItem gallery && gallery.IsVideo)
            {
                return VideoTemplate;
            }

            return ImageTemplate;
        }
    }
}
