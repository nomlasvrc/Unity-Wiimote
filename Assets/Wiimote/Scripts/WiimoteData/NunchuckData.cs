namespace WiimoteApi
{
    public sealed partial class NunchuckData : IWiimoteData
    {
        /// Nunchuck accelerometer values.  These are in the same (RAW) format
        /// as Wiimote::accel.
        private int[] _accel;

        /// Nunchuck Analog Stick values.  This is a size 2 Array [X, Y] of
        /// RAW (unprocessed) stick data.  Generally the analog stick returns
        /// values in the range 35-228 for X and 27-220 for Y.  The center for
        /// both is around 128.
        private byte[] _stick;

        /// Button: C
        private bool _c;
        /// Button: Z
        private bool _z;

        internal NunchuckData()
        {
            _accel = new int[3];
            _stick = new byte[2];
        }

        bool IWiimoteData.InterpretData(ReadOnlySpan<byte> data) => InterpretData(data);

        internal bool InterpretData(ReadOnlySpan<byte> data)
        {
            if (data.Length < 6)
            {
                _accel[0] = 0; _accel[1] = 0; _accel[2] = 0;
                _stick[0] = 128; _stick[1] = 128;
                _c = false;
                _z = false;
                return false;
            }

            _stick[0] = data[0];
            _stick[1] = data[1];

            _accel[0] = (int)data[2] << 2; _accel[0] |= (data[5] & 0xc0) >> 6;
            _accel[1] = (int)data[3] << 2; _accel[1] |= (data[5] & 0x30) >> 4;
            _accel[2] = (int)data[4] << 2; _accel[2] |= (data[5] & 0x0c) >> 2;

            _c = (data[5] & 0x02) != 0x02;
            _z = (data[5] & 0x01) != 0x01;
            return true;
        }

    }
}
