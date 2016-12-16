using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace Telegram.Api.TL
{
    public abstract partial class TLWebPageBase
    {
        public virtual Visibility SiteNameVisibility
        {
            get { return Visibility.Collapsed; }
        }

        public virtual Visibility AuthorVisibility
        {
            get { return Visibility.Collapsed; }
        }

        public virtual Visibility TitleVisibility
        {
            get { return Visibility.Collapsed; }
        }

        public virtual Visibility DescriptionVisibility
        {
            get { return Visibility.Collapsed; }
        }

        public virtual Visibility SummaryVisibility
        {
            get { return Visibility.Collapsed; }
        }
    }

    public partial class TLWebPage
    {
        public override Visibility SiteNameVisibility
        {
            get
            {
                if (!string.IsNullOrEmpty(SiteName))
                {
                    return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }

        public override Visibility AuthorVisibility
        {
            get
            {
                if (!string.IsNullOrEmpty(Title))
                {
                    return Visibility.Collapsed;
                }

                if (!string.IsNullOrEmpty(Author))
                {
                    return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }

        public override Visibility TitleVisibility
        {
            get
            {
                if (!string.IsNullOrEmpty(Title))
                {
                    return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }

        public override Visibility DescriptionVisibility
        {
            get
            {
                if (!string.IsNullOrEmpty(Description))
                {
                    return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }

        public override Visibility SummaryVisibility
        {
            get
            {
                if (!string.IsNullOrEmpty(SiteName) || !string.IsNullOrEmpty(Author) || !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Description))
                {
                    return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }
    }
}
