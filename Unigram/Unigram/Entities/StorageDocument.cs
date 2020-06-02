using Windows.Storage;

namespace Unigram.Entities
{
    public class StorageDocument : StorageMedia
    {
        public StorageDocument(StorageFile file)
            : base(file, null)
        {
        }
    }
}
