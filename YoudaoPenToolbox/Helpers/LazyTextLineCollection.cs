using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YoudaoPenToolbox.Helpers
{
    public sealed class LazyTextLineCollection : IList<string>, IList
    {
        public const int MaxLineLength = 4096;
        private const int ParallelThreshold = 512 * 1024;
        private const int ChunkSize = 128 * 1024;

        private readonly string _text;
        private readonly int[] _lineStarts;

        private LazyTextLineCollection(string text, int[] lineStarts)
        {
            _text = text ?? string.Empty;
            _lineStarts = lineStarts ?? new[] { 0 };
        }

        public static LazyTextLineCollection Create(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new LazyTextLineCollection(string.Empty, new[] { 0 });
            }

            return new LazyTextLineCollection(text, BuildLineStarts(text));
        }

        public static Task<LazyTextLineCollection> CreateAsync(byte[] data)
        {
            return Task.Run(() =>
            {
                var text = TextEncodingHelper.DecodeText(data);
                return Create(text);
            });
        }

        private static int[] BuildLineStarts(string text)
        {
            return text.Length >= ParallelThreshold
                ? BuildLineStartsParallel(text)
                : BuildLineStartsSequential(text);
        }

        private static int[] BuildLineStartsSequential(string text)
        {
            var starts = new List<int>(Math.Max(16, text.Length / 40)) { 0 };
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            return starts.ToArray();
        }

        private static int[] BuildLineStartsParallel(string text)
        {
            var chunkCount = (text.Length + ChunkSize - 1) / ChunkSize;
            var partialLists = new List<int>[chunkCount];

            Parallel.For(0, chunkCount, chunkIndex =>
            {
                var chunkStart = chunkIndex * ChunkSize;
                var chunkEnd = Math.Min(text.Length, chunkStart + ChunkSize);
                var list = new List<int>();
                for (var i = chunkStart; i < chunkEnd; i++)
                {
                    if (text[i] == '\n')
                    {
                        list.Add(i + 1);
                    }
                }

                partialLists[chunkIndex] = list;
            });

            var merged = new List<int>(Math.Max(16, text.Length / 40)) { 0 };
            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                var list = partialLists[chunkIndex];
                if (list == null || list.Count == 0)
                {
                    continue;
                }

                merged.AddRange(list);
            }

            return merged.ToArray();
        }

        public int Count => Math.Max(1, _lineStarts.Length);

        public bool IsReadOnly => true;

        public string this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                if (_text.Length == 0)
                {
                    return string.Empty;
                }

                var start = _lineStarts[index];
                if (start >= _text.Length)
                {
                    return string.Empty;
                }

                var end = index + 1 < _lineStarts.Length ? _lineStarts[index + 1] : _text.Length;
                if (end > start && _text[end - 1] == '\r')
                {
                    end--;
                }

                var length = end - start;
                if (length <= 0)
                {
                    return string.Empty;
                }

                if (length > MaxLineLength)
                {
                    return _text.Substring(start, MaxLineLength) + "…";
                }

                return _text.Substring(start, length);
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

        public bool Contains(string item) => false;

        public void CopyTo(string[] array, int arrayIndex) => throw new NotSupportedException();

        public void CopyTo(Array array, int index) => throw new NotSupportedException();

        public IEnumerator<string> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int IndexOf(string item) => -1;

        public void Insert(int index, string item) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        public void Add(string item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Remove(string item) => throw new NotSupportedException();

        int IList.Add(object value) => throw new NotSupportedException();

        bool IList.Contains(object value) => false;

        int IList.IndexOf(object value) => -1;

        void IList.Insert(int index, object value) => throw new NotSupportedException();

        void IList.Remove(object value) => throw new NotSupportedException();

        void IList.RemoveAt(int index) => throw new NotSupportedException();
    }
}
