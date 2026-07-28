namespace WiimoteApi
{
    public partial class ButtonData : WiimoteData
    {
        /// Button: D-Pad Left
        public bool d_left => _d_left;
        private bool _d_left;
        /// Button: D-Pad Right
        public bool d_right => _d_right;
        private bool _d_right;
        /// Button: D-Pad Up
        public bool d_up => _d_up;
        private bool _d_up;
        /// Button: D-Pad Down
        public bool d_down => _d_down;
        private bool _d_down;
        /// Button: A
        public bool a => _a;
        private bool _a;
        /// Button: B
        public bool b => _b;
        private bool _b;
        /// Button: 1 (one)
        public bool one => _one;
        private bool _one;
        /// Button: 2 (two)
        public bool two => _two;
        private bool _two;
        /// Button: + (plus)
        public bool plus => _plus;
        private bool _plus;
        /// Button: - (minus)
        public bool minus => _minus;
        private bool _minus;
        /// Button: Home
        public bool home => _home;
        private bool _home;

        public ButtonData(Wiimote Owner) : base(Owner) { }

        public override bool InterpretData(byte[] data)
        {
            if (data == null || data.Length != 2) return false;

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