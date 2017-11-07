﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class BubbleListViewItem : ListViewItem
    {
        public readonly BubbleListView Owner;

        public BubbleListViewItem(BubbleListView owner)
        {
            Owner = owner;
            //RegisterPropertyChangedCallback(IsSelectedProperty, OnIsSelectedChanged);
        }

        //private void OnIsSelectedChanged(DependencyObject sender, DependencyProperty dp)
        //{
        //    if (!(Content is TLMessageService))
        //    {
        //        (Content as TLMessageBase).IsSelected = IsSelected;

        //        if (Owner.DataContext is DialogViewModel && Owner.SelectionMode == ListViewSelectionMode.Multiple)
        //        {
        //            (Owner.DataContext as DialogViewModel).MessagesDeleteCommand.RaiseCanExecuteChanged();
        //            (Owner.DataContext as DialogViewModel).MessagesForwardCommand.RaiseCanExecuteChanged();
        //        }
        //    }
        //}

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            if (Owner.SelectionMode == ListViewSelectionMode.Multiple)
            {
                if (Content is TLMessageService serviceMessage && !(serviceMessage.Action is TLMessageActionPhoneCall))
                {
                    e.Handled = true;
                }
                else if (Content is TLMessage message)
                {
                    if (message.Media is TLMessageMediaPhoto photoMedia && photoMedia.HasTTLSeconds)
                    {
                        e.Handled = true;
                    }
                    else if (message.Media is TLMessageMediaDocument documentMedia && documentMedia.HasTTLSeconds)
                    {
                        e.Handled = true;
                    }
                }
            }

            base.OnPointerPressed(e);
        }
    }
}
