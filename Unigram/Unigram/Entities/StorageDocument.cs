using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Unigram.Entities
{
    public class StorageDocument : StorageMedia
    {
        public StorageDocument(StorageFile file)
            : base(file, null)
        {
        }

        public override StorageMedia Clone()
        {
            throw new NotImplementedException();
        }
    }
}
