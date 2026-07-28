namespace WiimoteApi
{
    public sealed partial class ClassicControllerData : IWiimoteData
    {

        /// Classic Controller left stick analog values.  This is a size-2 array [X,Y]
        /// of RAW (unprocessed) stick data.  These values are in the range 0-63
        /// in both X and Y.
        ///
        /// \sa GetLeftStick01()
        private byte[] _lstick;

        /// Classic Controller right stick analog values.  This is a size-2 array [X,Y]
        /// of RAW (unprocessed) stick data.  These values are in the range 0-31
        /// in both X and Y.
        /// 
        /// \note The Right analog stick reports one less bit of precision than the left
        ///       stick (the left stick is in the range 0-63 while the right is 0-31).
        ///
        /// \sa GetRightStick01()
        private byte[] _rstick;

        /// Classic Controller left trigger analog value.  This is RAW (unprocessed) analog
        /// data.  It is in the range 0-31 (with 0 being unpressed and 31 being fully pressed).
        ///
        /// \sa rtrigger_range, ltrigger_switch, ltrigger_switch
        private byte ltrigger_range => _ltrigger_range;
        private byte _ltrigger_range;

        /// Classic Controller right trigger analog value.  This is RAW (unprocessed) analog
        /// data.  It is in the range 0-31 (with 0 being unpressed and 31 being fully pressed).
        ///
        /// \sa ltrigger_range, rtrigger_switch, rtrigger_switch
        private byte rtrigger_range => _rtrigger_range;
        private byte _rtrigger_range;

        /// Button: Left trigger (bottom out switch)
        /// \sa rtrigger_switch, rtrigger_range, ltrigger_range
        private bool ltrigger_switch => _ltrigger_switch;
        private bool _ltrigger_switch;

        /// Button: Right trigger (button out switch)
        /// \sa ltrigger_switch, ltrigger_range, rtrigger_range
        private bool rtrigger_switch => _rtrigger_switch;
        private bool _rtrigger_switch;

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

        internal ClassicControllerData()
        {
            _lstick = new byte[2];

            _rstick = new byte[2];
        }

        bool IWiimoteData.InterpretData(ReadOnlySpan<byte> data) => InterpretData(data);

        internal bool InterpretData(ReadOnlySpan<byte> data)
        {
            if (data.Length < 6)
                return false;

            _lstick[0] = (byte)(data[0] & 0x3f);
            _lstick[1] = (byte)(data[1] & 0x3f);

            _rstick[0] = (byte)(((data[0] & 0xc0) >> 3) |
                                ((data[1] & 0xc0) >> 5) |
                                ((data[2] & 0x80) >> 7));
            _rstick[1] = (byte)(data[2] & 0x1f);

            _ltrigger_range = (byte)(((data[2] & 0x60) >> 2) |
                                ((data[3] & 0xe0) >> 5));

            _rtrigger_range = (byte)(data[3] & 0x1f);

            // Bit is zero when pressed, one when up.  This is really weird so I reverse
            // the bit with !=
            _dpad_right = (data[4] & 0x80) != 0x80;
            _dpad_down = (data[4] & 0x40) != 0x40;
            _ltrigger_switch = (data[4] & 0x20) != 0x20;
            _minus = (data[4] & 0x10) != 0x10;
            _home = (data[4] & 0x08) != 0x08;
            _plus = (data[4] & 0x04) != 0x04;
            _rtrigger_switch = (data[4] & 0x02) != 0x02;

            _zl = (data[5] & 0x80) != 0x80;
            _b = (data[5] & 0x40) != 0x40;
            _y = (data[5] & 0x20) != 0x20;
            _a = (data[5] & 0x10) != 0x10;
            _x = (data[5] & 0x08) != 0x08;
            _zr = (data[5] & 0x04) != 0x04;
            _dpad_left = (data[5] & 0x02) != 0x02;
            _dpad_up = (data[5] & 0x01) != 0x01;

            return true;
        }

    }
}
