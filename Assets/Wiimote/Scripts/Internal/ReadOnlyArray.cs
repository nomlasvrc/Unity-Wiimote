using System.Collections;

namespace WiimoteApi.Util;

/// <summary>A live, read-only view over an array owned by a data component.</summary>
public sealed class ReadOnlyArray<T> : IReadOnlyList<T>
{
    private readonly T[] _data;

    public ReadOnlyArray(T[] data) =>
        _data = data ?? throw new ArgumentNullException(nameof(data));

    public int Count => _data.Length;

    public T this[int index] => _data[index];

    public ReadOnlySpan<T> AsSpan() => _data;

    public T[] ToArray() => [.. _data];

    public IEnumerator<T> GetEnumerator() =>
        ((IEnumerable<T>)_data).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>A live, read-only view over a two-dimensional array.</summary>
public sealed class ReadOnlyMatrix<T>
{
    private readonly T[,] _data;

    public ReadOnlyMatrix(T[,] data) =>
        _data = data ?? throw new ArgumentNullException(nameof(data));

    public T this[int row, int column] => _data[row, column];

    public int RowCount => _data.GetLength(0);

    public int ColumnCount => _data.GetLength(1);

    public int GetLength(int dimension) => _data.GetLength(dimension);

    public T[,] ToArray() => (T[,])_data.Clone();
}
