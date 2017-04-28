using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class StatsDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate BaseTemplate { get; set; }
        public DataTemplate ItemTemplate { get; set; }
        public DataTemplate CallTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            switch (item)
            {
                case SettingsStatsCallData call:
                    return CallTemplate;
                case SettingsStatsData data:
                    return ItemTemplate;
                default:
                    return BaseTemplate;
            }
        }
    }
}
