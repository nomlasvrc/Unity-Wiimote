namespace WiimoteApi;

internal interface IWiimoteData
{
    bool InterpretData(ReadOnlySpan<byte> data);
}
