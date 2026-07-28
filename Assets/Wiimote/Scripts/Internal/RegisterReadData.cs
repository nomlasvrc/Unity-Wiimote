namespace WiimoteApi.Internal;

internal sealed class RegisterReadData(
    int offset,
    int size,
    Action<byte[]> responder,
    Action<Exception> errorResponder)
{
    private readonly byte[] _buffer = new byte[size];

    internal int ExpectedOffset { get; private set; } = offset;

    internal int Offset { get; } = offset;

    internal int Size { get; } = size;

    internal void Fail(Exception exception) => errorResponder(exception);

    internal bool AppendData(ReadOnlySpan<byte> data)
    {
        int start = ExpectedOffset - Offset;
        int end = start + data.Length;
        if (start < 0 || end > _buffer.Length)
            return false;

        data.CopyTo(_buffer.AsSpan(start));
        ExpectedOffset += data.Length;
        if (ExpectedOffset >= Offset + Size)
            responder(_buffer);

        return true;
    }
}
