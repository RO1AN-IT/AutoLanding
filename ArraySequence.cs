using System;
using System.Collections.Generic;

namespace AutoLanding
{
    public class ArraySequence<T> : ISequence<T>
    {
        private List<T> data;

        public ArraySequence()
        {
            data = new List<T>();
        }

        public ArraySequence(int count)
        {
            data = new List<T>(count);
            for (int i = 0; i < count; i++)
            {
                data.Add(default(T));
            }
        }

        public ArraySequence(T[] items)
        {
            data = new List<T>(items);
        }

        public ArraySequence(IEnumerable<T> items)
        {
            data = new List<T>(items);
        }

        public ArraySequence(ArraySequence<T> other)
        {
            data = new List<T>(other.data);
        }

        public T GetFirst()
        {
            if (data.Count == 0)
                throw new IndexOutOfRangeException("Sequence is empty");
            return data[0];
        }

        public T GetLast()
        {
            if (data.Count == 0)
                throw new IndexOutOfRangeException("Sequence is empty");
            return data[data.Count - 1];
        }

        public T Get(int index)
        {
            if (index < 0 || index >= data.Count)
                throw new IndexOutOfRangeException($"Index {index} out of range");
            return data[index];
        }

        public int GetLength()
        {
            return data.Count;
        }

        public ISequence<T> Set(T value, int index)
        {
            if (index < 0 || index >= data.Count)
                throw new IndexOutOfRangeException($"Index {index} out of range");
            ArraySequence<T> result = new ArraySequence<T>(this);
            result.data[index] = value;
            return result;
        }

        public ISequence<T> Append(T item)
        {
            ArraySequence<T> result = new ArraySequence<T>(this);
            result.data.Add(item);
            return result;
        }

        public ISequence<T> Prepend(T item)
        {
            ArraySequence<T> result = new ArraySequence<T>(this);
            result.data.Insert(0, item);
            return result;
        }

        public ISequence<T> InsertAt(T item, int index)
        {
            if (index < 0 || index > data.Count)
                throw new IndexOutOfRangeException($"Index {index} out of range");
            ArraySequence<T> result = new ArraySequence<T>(this);
            result.data.Insert(index, item);
            return result;
        }

        public ISequence<T> GetSubsequence(int start, int end)
        {
            if (start < 0 || end > data.Count || start > end)
                throw new IndexOutOfRangeException("Invalid subsequence range");
            ArraySequence<T> result = new ArraySequence<T>();
            for (int i = start; i < end; i++)
            {
                result.data.Add(data[i]);
            }
            return result;
        }

        public ISequence<T> Concat(ISequence<T> other)
        {
            ArraySequence<T> result = new ArraySequence<T>(this);
            for (int i = 0; i < other.GetLength(); i++)
            {
                result.data.Add(other.Get(i));
            }
            return result;
        }

        public ISequence<T> Map(Func<T, T> func)
        {
            ArraySequence<T> result = new ArraySequence<T>();
            foreach (var item in data)
            {
                result.data.Add(func(item));
            }
            return result;
        }

        public ISequence<T> Where(Func<T, bool> predicate)
        {
            ArraySequence<T> result = new ArraySequence<T>();
            foreach (var item in data)
            {
                if (predicate(item))
                {
                    result.data.Add(item);
                }
            }
            return result;
        }

        public T Reduce(Func<T, T, T> func, T startValue)
        {
            T result = startValue;
            foreach (var item in data)
            {
                result = func(item, result);
            }
            return result;
        }

        public T this[int index]
        {
            get => Get(index);
            set
            {
                if (index < 0 || index >= data.Count)
                    throw new IndexOutOfRangeException($"Index {index} out of range");
                data[index] = value;
            }
        }

        public List<T> ToList()
        {
            return new List<T>(data);
        }
    }
}


