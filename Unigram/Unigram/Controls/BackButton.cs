using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToVisualTree;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class BackButton : GlyphButton
    {
        public BackButton()
        {
            DefaultStyleKey = typeof(BackButton);
        }

        protected override void OnApplyTemplate()
        {
            var page = this.Ancestors<Page>().FirstOrDefault() as Page;
            base.OnApplyTemplate();
        }
    }
}
