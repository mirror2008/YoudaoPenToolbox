using System;
using System.Collections;
using System.Collections.Generic;

namespace YoudaoPenToolbox.Helpers
{
    public sealed class HexDumpVirtualCollection : IList<HexDumpLine>, IList
    {
        private readonly byte[] _data;

        public HexDumpVirtualCollection(byte[] data)
        {
            _data = data ?? Array.Empty<byte>();
        }

        public int Count => HexDumpFormatter.GetLineCount(_data);

        public bool IsReadOnly => true;

        public HexDumpLine this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return HexDumpFormatter.FormatLine(_data, index);
            }
            set => throw new NotSupportedException();
        }

        object IList.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException();
        }

        public bool IsFixedSize => true;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        public bool Contains(HexDumpLine item) => false;

        public void CopyTo(HexDumpLine[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            for (var i = 0; i < Count; i++)
            {
                array[arrayIndex + i] = this[i];
            }
        }

        public void CopyTo(Array array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            for (var i = 0; i < Count; i++)
            {
                array.SetValue(this[i], index + i);
            }
        }

        public IEnumerator<HexDumpLine> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int IndexOf(HexDumpLine item) => -1;

        public void Insert(int index, HexDumpLine item) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        public void Add(HexDumpLine item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Remove(HexDumpLine item) => throw new NotSupportedException();

        int IList.Add(object value) => throw new NotSupportedException();

        bool IList.Contains(object value) => false;

        int IList.IndexOf(object value) => -1;

        void IList.Insert(int index, object value) => throw new NotSupportedException();

        void IList.Remove(object value) => throw new NotSupportedException();

        void IList.RemoveAt(int index) => throw new NotSupportedException();
    }
}
