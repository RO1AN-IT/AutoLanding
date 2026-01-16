using System;
using System.Collections.Generic;

namespace AutoLanding
{
    public interface ISequence<T>
    {
        T GetFirst();
        T GetLast();
        T Get(int index);
        int GetLength();
        
        ISequence<T> Set(T value, int index);
        ISequence<T> Append(T item);
        ISequence<T> Prepend(T item);
        ISequence<T> InsertAt(T item, int index);
        ISequence<T> GetSubsequence(int start, int end);
        ISequence<T> Concat(ISequence<T> other);
        
        ISequence<T> Map(Func<T, T> func);
        ISequence<T> Where(Func<T, bool> predicate);
        T Reduce(Func<T, T, T> func, T startValue);
    }
}


