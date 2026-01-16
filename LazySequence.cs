using System;
using System.Collections.Generic;

namespace AutoLanding
{
    public class LazySequence<T> : ISequence<T>
    {
        private struct Cardinal
        {
            public int value;
            public bool isInfinite;

            public Cardinal(int v)
            {
                value = v;
                isInfinite = false;
            }

            public Cardinal(bool infinite)
            {
                value = 0;
                isInfinite = infinite;
            }
        }

        private class Generator
        {
            private Func<Queue<T>, T> rule;
            private Queue<T> window;
            private int windowSize;

            public Generator(Func<Queue<T>, T> rule, int windowSize)
            {
                this.rule = rule;
                this.windowSize = windowSize;
                this.window = new Queue<T>();
            }

            public Generator(Generator other)
            {
                this.rule = other.rule;
                this.windowSize = other.windowSize;
                this.window = new Queue<T>(other.window);
            }

            public void SetWindow(Queue<T> w)
            {
                window = new Queue<T>(w);
            }

            public bool HasRule()
            {
                return rule != null;
            }

            public int WindowSize()
            {
                return windowSize;
            }

            public T Next()
            {
                if (rule == null)
                    throw new InvalidOperationException("No generator rule");
                if (window.Count < windowSize)
                    throw new InvalidOperationException("Not enough elements in window");

                T next = rule(window);
                window.Enqueue(next);
                if (window.Count > windowSize)
                {
                    window.Dequeue();
                }
                return next;
            }
        }

        private ArraySequence<T> data;
        private Generator generator;
        private int materializedCount;
        private ArraySequence<Func<T, T>> maps;
        private ArraySequence<Func<T, bool>> wheres;
        private Cardinal cardinal;

        private bool ApplyWheres(T value)
        {
            for (int i = 0; i < wheres.GetLength(); i++)
            {
                if (!wheres.Get(i)(value))
                    return false;
            }
            return true;
        }

        private T ApplyMaps(T value)
        {
            T result = value;
            for (int i = 0; i < maps.GetLength(); i++)
            {
                result = maps.Get(i)(result);
            }
            return result;
        }

        private void EnsureGeneratedUntil(int index)
        {
            if (generator == null || !generator.HasRule())
                return;

            while (data.GetLength() <= index)
            {
                T next = generator.Next();
                next = ApplyMaps(next);
                if (ApplyWheres(next))
                {
                    data = (ArraySequence<T>)data.Append(next);
                    materializedCount++;
                }
            }
        }

        public LazySequence()
        {
            data = new ArraySequence<T>();
            maps = new ArraySequence<Func<T, T>>();
            wheres = new ArraySequence<Func<T, bool>>();
            generator = null;
            materializedCount = 0;
            cardinal = new Cardinal(0);
        }

        public LazySequence(T[] items)
        {
            data = new ArraySequence<T>(items);
            maps = new ArraySequence<Func<T, T>>();
            wheres = new ArraySequence<Func<T, bool>>();
            generator = null;
            materializedCount = 0;
            cardinal = new Cardinal(items.Length);
        }

        public LazySequence(ISequence<T> seq)
        {
            if (seq == null)
                throw new ArgumentNullException("Base sequence is null");

            data = new ArraySequence<T>();
            for (int i = 0; i < seq.GetLength(); i++)
            {
                data = (ArraySequence<T>)data.Append(seq.Get(i));
            }
            maps = new ArraySequence<Func<T, T>>();
            wheres = new ArraySequence<Func<T, bool>>();
            generator = null;
            materializedCount = 0;
            cardinal = new Cardinal(data.GetLength());
        }

        public LazySequence(Func<Queue<T>, T> rule, ISequence<T> start, int windowSize)
        {
            if (start == null)
                throw new ArgumentNullException("Base sequence is null");

            data = new ArraySequence<T>();
            for (int i = 0; i < start.GetLength(); i++)
            {
                data = (ArraySequence<T>)data.Append(start.Get(i));
            }

            generator = new Generator(rule, windowSize);

            Queue<T> window = new Queue<T>();
            int startLen = start.GetLength();
            int from = Math.Max(0, startLen - windowSize);
            for (int i = from; i < startLen; i++)
            {
                window.Enqueue(start.Get(i));
            }
            generator.SetWindow(window);

            maps = new ArraySequence<Func<T, T>>();
            wheres = new ArraySequence<Func<T, bool>>();
            materializedCount = 0;
            cardinal = new Cardinal(true);
        }

        public LazySequence(LazySequence<T> other)
        {
            data = new ArraySequence<T>(other.data);
            maps = new ArraySequence<Func<T, T>>(other.maps);
            wheres = new ArraySequence<Func<T, bool>>(other.wheres);
            materializedCount = other.materializedCount;
            cardinal = other.cardinal;
            if (other.generator != null)
            {
                generator = new Generator(other.generator);
            }
        }

        public T GetFirst()
        {
            EnsureGeneratedUntil(0);
            return data.GetFirst();
        }

        public T GetLast()
        {
            if (cardinal.isInfinite)
                throw new InvalidOperationException("Cannot get last element of an infinite sequence");
            if (data.GetLength() == 0)
                throw new IndexOutOfRangeException("Empty sequence");
            return data.GetLast();
        }

        public T Get(int index)
        {
            if (index < 0)
                throw new IndexOutOfRangeException("Negative index");
            EnsureGeneratedUntil(index);
            if (index >= data.GetLength() && !cardinal.isInfinite)
                throw new IndexOutOfRangeException("Index too large");
            return data.Get(index);
        }

        public int GetLength()
        {
            if (cardinal.isInfinite)
                return -1;
            return data.GetLength();
        }

        public ISequence<T> Set(T value, int index)
        {
            LazySequence<T> copy = new LazySequence<T>(this);
            copy.data = (ArraySequence<T>)copy.data.Set(value, index);
            return copy;
        }

        public ISequence<T> Append(T item)
        {
            if (cardinal.isInfinite)
                throw new InvalidOperationException("Cannot append to infinite sequence");
            LazySequence<T> copy = new LazySequence<T>(this);
            copy.data = (ArraySequence<T>)copy.data.Append(item);
            copy.cardinal.value++;
            return copy;
        }

        public ISequence<T> Prepend(T item)
        {
            LazySequence<T> copy = new LazySequence<T>(this);
            copy.data = (ArraySequence<T>)copy.data.Prepend(item);
            copy.cardinal.value++;
            return copy;
        }

        public ISequence<T> InsertAt(T item, int index)
        {
            LazySequence<T> copy = new LazySequence<T>(this);
            copy.data = (ArraySequence<T>)copy.data.InsertAt(item, index);
            copy.cardinal.value++;
            return copy;
        }

        public ISequence<T> GetSubsequence(int start, int end)
        {
            EnsureGeneratedUntil(end - 1);
            return data.GetSubsequence(start, end);
        }

        public ISequence<T> Concat(ISequence<T> seq)
        {
            LazySequence<T> other = seq as LazySequence<T>;
            if (other == null)
                throw new ArgumentException("Concat only supports LazySequence");

            if (!this.cardinal.isInfinite && !other.cardinal.isInfinite)
            {
                LazySequence<T> copy = new LazySequence<T>(this);
                for (int i = 0; i < other.GetLength(); i++)
                {
                    copy.data = (ArraySequence<T>)copy.data.Append(other.Get(i));
                }
                copy.cardinal.value = this.cardinal.value + other.cardinal.value;
                return copy;
            }

            if (!this.cardinal.isInfinite && other.cardinal.isInfinite)
            {
                LazySequence<T> copy = new LazySequence<T>(other);
                for (int i = this.GetLength() - 1; i >= 0; i--)
                {
                    copy.data = (ArraySequence<T>)copy.data.Prepend(this.Get(i));
                }
                copy.cardinal = other.cardinal;
                return copy;
            }

            if (this.cardinal.isInfinite)
                throw new InvalidOperationException("Cannot concat to an infinite sequence");

            return null;
        }

        public ISequence<T> Map(Func<T, T> func)
        {
            LazySequence<T> copy = new LazySequence<T>(this);
            copy.maps = (ArraySequence<Func<T, T>>)copy.maps.Append(func);
            return copy;
        }

        public ISequence<T> Where(Func<T, bool> predicate)
        {
            LazySequence<T> copy = new LazySequence<T>(this);
            copy.wheres = (ArraySequence<Func<T, bool>>)copy.wheres.Append(predicate);
            return copy;
        }

        public T Reduce(Func<T, T, T> func, T startValue)
        {
            T result = startValue;
            int len = cardinal.isInfinite ? materializedCount : data.GetLength();
            for (int i = 0; i < len; i++)
            {
                result = func(Get(i), result);
            }
            return result;
        }

        public IEnumerable<T> Enumerate(int maxCount = -1)
        {
            int count = 0;
            int index = 0;
            while (true)
            {
                if (maxCount > 0 && count >= maxCount)
                    yield break;

                if (!cardinal.isInfinite && index >= data.GetLength())
                    yield break;

                if (cardinal.isInfinite && index >= data.GetLength() && (generator == null || !generator.HasRule()))
                    yield break;

                T value;
                try
                {
                    value = Get(index);
                }
                catch (IndexOutOfRangeException)
                {
                    yield break;
                }

                yield return value;
                index++;
                count++;
            }
        }
    }
}

