namespace WiimoteApi
{
    public sealed partial class StatusData : IWiimoteData
    {
        /// Size: 4.  An array of what Player LEDs are on as reported by
        /// the Wii Remote.  This is only updated when the Wii Remote sends status reports.
        private bool[] _led;

        /// \brief True if the Wii Remote's batteries are low, as reported by the Wii Remote.
        ///        This is only updated when the Wii Remote sends status reports.
        /// \sa battery_level
        private bool _battery_low;

        /// True if an extension controller is connected, as reported by the Wii Remote.
        /// This is only updated when the Wii Remote sends status reports.
        private bool _ext_connected;

        /// True if the speaker is currently enabled, as reported by the Wii Remote.
        /// This is only updated when the Wii Remote sends status reports.
        private bool _speaker_enabled;

        /// True if IR is currently enabled, as reported by the Wii Remote.
        /// This is only updated when the Wii Remote sends status reports.
        private bool _ir_enabled;

        /// \brief The current battery level, as reported by the Wii Remote.
        ///        This is only updated when the Wii Remote sends status reports.
        /// \sa battery_low
        private byte _battery_level;

        internal StatusData()
        {
            _led = new bool[4];
        }

        bool IWiimoteData.InterpretData(ReadOnlySpan<byte> data) => InterpretData(data);

        internal bool InterpretData(ReadOnlySpan<byte> data)
        {
            if (data.Length != 2) return false;

            byte flags = data[0];
            _battery_low = (flags & 0x01) == 0x01;
            _ext_connected = (flags & 0x02) == 0x02;
            _speaker_enabled = (flags & 0x04) == 0x04;
            _ir_enabled = (flags & 0x08) == 0x08;
            _led[0] = (flags & 0x10) == 0x10;
            _led[1] = (flags & 0x20) == 0x20;
            _led[2] = (flags & 0x40) == 0x40;
            _led[3] = (flags & 0x80) == 0x80;

            _battery_level = data[1];

            return true;
        }
    }
}
