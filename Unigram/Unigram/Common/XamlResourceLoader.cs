//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Resources;
using Unigram.Services;

namespace Unigram.Common
{
    public class XamlResourceLoader : CustomXamlResourceLoader
    {
        protected override object GetResource(string resourceId, string objectType, string propertyName, string propertyType)
        {
            return LocaleService.Current.GetString(resourceId);
        }
    }
}
