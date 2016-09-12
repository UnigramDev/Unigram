using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Telegram.Api.TL;

namespace Unigram.ViewModels
{
    // Temporary model for the DialogPage
    public class MessageModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _msg;
        public string Message
        {
            get { return _msg; }
            set
            {
                _msg = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
            }
        }

        private string _date;
        public string Date
        {
            get { return _date; }
            set
            {
                _date = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Date)));
            }
        }

        private string _senderId;
        public string SenderId
        {
            get { return _senderId; }
            set
            {
                _senderId = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SenderId)));
            }
        }

        //public bool IsSelf
        //{
        //    get
        //    {

        //    }
        //}

        // Constructor
        public MessageModel()
        {
        }

        // Methods
        public static MessageModel ConvertToMessage(TLMessage tlMessage)
        {
            MessageModel message = new MessageModel();

            message._msg = tlMessage.Message;
            message._date = TLUtils.ToDateTime(tlMessage.Date).ToString();
            message._senderId = tlMessage.FromId.ToString();

            return message;
        }
    }
}
