using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class MessageGroupTemplateSelector : DataTemplateSelector
    {
        public DataTemplate EmptyMessageTemplate { get; set; } = new DataTemplate();

        public DataTemplate PlaceholderMessageTemplate { get; set; }

        public DataTemplate ChatFriendMessageTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            //var group = item as MessageGroup;
            //if (group.ToId is TLPeerChat || group.ToId is TLPeerChannel) // TODO: probably some addtional check needed for channels
            //{
            //    if (group.IsOut)
            //    {
            //        // We need a placeholder instead of an empty template
            //        // for messages sent in a group, because all groups
            //        // of messages should have the header of the same width.
            //        return PlaceholderMessageTemplate;
            //    }
            //    else
            //    {
            //        return ChatFriendMessageTemplate;
            //    }
            //}

            return EmptyMessageTemplate;
        }
    }
}
