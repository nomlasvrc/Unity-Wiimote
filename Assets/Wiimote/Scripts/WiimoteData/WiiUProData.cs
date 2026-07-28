namespace WiimoteApi
{
    public sealed partial class WiiUProData : IWiimoteData
    {

        /// Pro Controller left stick analog values.  This is a size-2 array [X,Y]
        /// of RAW (unprocessed) stick data.  These values are in the range 803-3225
        /// in the X direction and 843-3291 in the Y direction.
        ///
        /// \note Min/Max values may vary between controllers (untested).  One way to calibrate
        ///		  is to prompt the user to spin the control sticks in circles and record the min/max values.
        ///
        /// \sa GetLeftStick01()
        private ushort[] _lstick;

        /// Pro Controller right stick analog values.  This is a size-2 array [X,Y]
        /// of RAW (unprocessed) stick data.  These values are in the range 852-3169
        /// in the X direction and 810-3315 in the Y direction.
        ///
        /// \note Min/Max values may vary between controllers (untested).  One way to calibrate
        ///		  is to prompt the user to spin the control sticks in circles and record the min/max values.
        ///
        /// \sa GetRightStick01()
        private ushort[] _rstick;

        /// Button: Left Stick Button (push down switch)
        private bool lstick_button => _lstick_button;
        private bool _lstick_button;

        /// Button: Right Stick Button (push down switch)
        private bool rstick_button => _rstick_button;
        private bool _rstick_button;

        /// Button: A
        private bool a => _a;
        private bool _a;

        /// Button: B
        private bool b => _b;
        private bool _b;

        /// Button: X
        private bool x => _x;
        private bool _x;

        /// Button: Y
        private bool y => _y;
        private bool _y;

        /// Button: + (plus)
        private bool plus => _plus;
        private bool _plus;

        /// Button: - (minus)
        private bool minus => _minus;
        private bool _minus;

        /// Button: home
        private bool home => _home;
        private bool _home;

        /// Button:  L
        private bool l => _l;
        private bool _l;

        /// Button: R
        private bool r => _r;
        private bool _r;

        /// Button:  ZL
        private bool zl => _zl;
        private bool _zl;

        /// Button: ZR
        private bool zr => _zr;
        private bool _zr;

        /// Button: D-Pad Up
        private bool dpad_up => _dpad_up;
        private bool _dpad_up;

        /// Button: D-Pad Down
        private bool dpad_down => _dpad_down;
        private bool _dpad_down;

        /// Button: D-Pad Left
        private bool dpad_left => _dpad_left;
        private bool _dpad_left;

        /// Button: D-Pad Right
        private bool dpad_right => _dpad_right;
        private bool _dpad_right;

        private ushort[] lmax = { 3225, 3291 };
        private ushort[] lmin = { 803, 843 };
        private ushort[] rmax = { 3169, 3315 };
        private ushort[] rmin = { 852, 810 };

        internal WiiUProData()
        {
            _lstick = new ushort[2];

            _rstick = new ushort[2];
        }

        bool IWiimoteData.InterpretData(ReadOnlySpan<byte> data) => InterpretData(data);

        internal bool InterpretData(ReadOnlySpan<byte> data)
        {
            if (data.Length < 11)
                return false;

            _lstick[0] = (ushort)((ushort)data[0] | ((ushort)(data[1] & 0x0f) << 8));
            _lstick[1] = (ushort)((ushort)data[4] | ((ushort)(data[5] & 0x0f) << 8));

            _rstick[0] = (ushort)((ushort)data[2] | ((ushort)(data[3] & 0x0f) << 8));
            _rstick[1] = (ushort)((ushort)data[6] | ((ushort)(data[7] & 0x0f) << 8));

            _dpad_right = (data[8] & 0x80) != 0x80;
            _dpad_down = (data[8] & 0x40) != 0x40;
            _l = (data[8] & 0x20) != 0x20;
            _minus = (data[8] & 0x10) != 0x10;
            _home = (data[8] & 0x08) != 0x08;
            _plus = (data[8] & 0x04) != 0x04;
            _r = (data[8] & 0x02) != 0x02;

            _zl = (data[9] & 0x80) != 0x80;
            _b = (data[9] & 0x40) != 0x40;
            _y = (data[9] & 0x20) != 0x20;
            _a = (data[9] & 0x10) != 0x10;
            _x = (data[9] & 0x08) != 0x08;
            _zr = (data[9] & 0x04) != 0x04;
            _dpad_left = (data[9] & 0x02) != 0x02;
            _dpad_up = (data[9] & 0x01) != 0x01;

            _lstick_button = (data[10] & 0x02) != 0x02;
            _rstick_button = (data[10] & 0x01) != 0x01;

            return true;
        }

    }
}
