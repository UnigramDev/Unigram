using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class ParticipantTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ItemTemplate { get; set; }
        public DataTemplate AdminTemplate { get; set; }
        public DataTemplate CreatorTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            switch (item)
            {
                case TLChannelParticipantCreator channelCreator:
                case TLChatParticipantCreator chatCreator:
                    return CreatorTemplate ?? AdminTemplate ?? ItemTemplate;
                case TLChannelParticipantAdmin channelAdmin:
                case TLChatParticipantAdmin chatAdmin:
                    return AdminTemplate ?? ItemTemplate;
                default:
                    return ItemTemplate;
            }
        }
    }
}
