namespace WiimoteApi
{
    public sealed partial class ButtonData : IWiimoteData
    {
        /// Button: D-Pad Left
        private bool d_left => _d_left;
        private bool _d_left;
        /// Button: D-Pad Right
        private bool d_right => _d_right;
        private bool _d_right;
        /// Button: D-Pad Up
        private bool d_up => _d_up;
        private bool _d_up;
        /// Button: D-Pad Down
        private bool d_down => _d_down;
        private bool _d_down;
        /// Button: A
        private bool a => _a;
        private bool _a;
        /// Button: B
        private bool b => _b;
        private bool _b;
        /// Button: 1 (one)
        private bool one => _one;
        private bool _one;
        /// Button: 2 (two)
        private bool two => _two;
        private bool _two;
        /// Button: + (plus)
        private bool plus => _plus;
        private bool _plus;
        /// Button: - (minus)
        private bool minus => _minus;
        private bool _minus;
        /// Button: Home
        private bool home => _home;
        private bool _home;

        internal ButtonData() { }

        bool IWiimoteData.InterpretData(ReadOnlySpan<byte> data) => InterpretData(data);

        internal bool InterpretData(ReadOnlySpan<byte> data)
        {
            if (data.Length != 2) return false;

            _d_left = (data[0] & 0x01) == 0x01;
            _d_right = (data[0] & 0x02) == 0x02;
            _d_down = (data[0] & 0x04) == 0x04;
            _d_up = (data[0] & 0x08) == 0x08;
            _plus = (data[0] & 0x10) == 0x10;

            _two = (data[1] & 0x01) == 0x01;
            _one = (data[1] & 0x02) == 0x02;
            _b = (data[1] & 0x04) == 0x04;
            _a = (data[1] & 0x08) == 0x08;
            _minus = (data[1] & 0x10) == 0x10;

            _home = (data[1] & 0x80) == 0x80;

            return true;
        }
    }
}
