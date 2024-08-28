namespace Rg.DiffUtils
{
    class CenteredArray
    {
        int[] _data;
        int _mid;

        public CenteredArray(int size)
        {
            _data = new int[size];
            _mid = _data.Length / 2;
        }

        public int Get(int index) => _data[index + _mid];

        public int[] BackingData() => _data;

        public void Set(int index, int value) => _data[index + _mid] = value;

        public void Fill(int value) => _data.Fill(value);
    }
}
