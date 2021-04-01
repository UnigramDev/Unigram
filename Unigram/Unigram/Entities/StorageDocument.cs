using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Unigram.Entities
{
    public class StorageDocument : StorageMedia
    {
        public StorageDocument(StorageFile file, BasicProperties basic)
            : base(file, basic)
        {
        }
    }
}
