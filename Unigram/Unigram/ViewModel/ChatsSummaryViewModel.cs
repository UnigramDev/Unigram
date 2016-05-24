using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.ViewModel
{
    public class ChatsSummaryViewModel : ViewModelBase
    {
        public ChatsSummaryViewModel()
        {
            if (ChatsCollection == null)
                ChatsCollection = new ObservableCollection<string>();

            for (int i = 0; i < 5; i++)
            {
                ChatsCollection.Add("item" + i);
            }
        }

        private ObservableCollection<string> chatsCollection;
        public ObservableCollection<string> ChatsCollection
        {
            get { return chatsCollection; }
            set
            {
                chatsCollection = value;
            }
        }

    }
}
