namespace WiimoteApi
{
    public sealed partial class AccelData : IWiimoteData
    {
        /// \brief Current remote-space acceleration, in the Wii Remote's coordinate system.
        ///        These are RAW values, so they are not with respect to a zero point.  See CalibrateAccel().
        ///        This is only updated if the Wii Remote has a report mode that supports
        ///        the Accelerometer.
        ///
        /// \warning This should not be used unless if you want to calibrate the accelerometer manually.  Use
        ///          CalibrateAccel() instead.
        ///
        /// Range:            0 - 1024\n
        /// *The sign of the directions below are with respect to the zero point of the accelerometer:*\n
        /// Up/Down:          +Z/-Z\n
        /// Left/Right:       +X/-X\n
        /// Forward/Backward: -Y/+Y\n
        private int[] _accel;

        /// \brief Size: 3x3. Calibration data for the accelerometer. This is not reported
        ///        by the Wii Remote directly - it is instead collected from normal
        ///        Wii Remote accelerometer data.
        /// \sa  AccelCalibrationStep,  CalibrateAccel(AccelCalibrationStep)
        ///
        /// Here are the 3 calibration steps:
        /// 1. Horizontal with the A button facing up
        /// 2. IR sensor down on the table so the expansion port is facing up
        /// 3. Laying on its side, so the left side is facing up
        /// 
        /// By default this is set to experimental calibration data.
        /// 
        /// int[calibration step,calibration data] (size 3x3)
        private readonly int[,] _accelCalibration = {
                                    { 479, 478, 569 },
                                    { 472, 568, 476 },
                                    { 569, 469, 476 }
                                };

        internal AccelData()
        {
            _accel = new int[3];
        }

        bool IWiimoteData.InterpretData(ReadOnlySpan<byte> data) => InterpretData(data);

        internal bool InterpretData(ReadOnlySpan<byte> data)
        {
            if (data.Length != 5) return false;

            // Note: data[0 - 1] is the buttons data.  data[2 - 4] is the accel data.
            // Accel data and buttons data is interleaved to reduce packet size.
            _accel[0] = ((int)data[2] << 2) | ((data[0] >> 5) & 0x03);
            _accel[1] = ((int)data[3] << 2) | ((data[1] >> 5) & 0x01);
            _accel[2] = ((int)data[4] << 2) | ((data[1] >> 6) & 0x01);

            //for (int x = 0; x < 3; x++) _accel[x] -= 0x200; // center around zero.

            return true;
        }

        /// \brief Interprets raw byte data reported by the Wii Remote when in interleaved data reporting mode.
        ///        The format of the actual bytes passed to this depends on the Wii Remote's current data report
        ///        mode and the type of data being passed.
        /// 
        /// \sa Wiimote::ReadWiimoteData()
        internal bool InterpretDataInterleaved(ReadOnlySpan<byte> data1, ReadOnlySpan<byte> data2)
        {
            if (data1.Length != 21 || data2.Length != 21)
                return false;

            _accel[0] = (int)data1[2] << 2;
            _accel[1] = (int)data2[2] << 2;
            _accel[2] = (int)(((data1[0] & 0x60) >> 1) |
                                ((data1[1] & 0x60) << 1) |
                                ((data2[0] & 0x60) >> 5) |
                                ((data2[1] & 0x60) >> 3)) << 2;

            //for (int x = 0; x < 3; x++) _accel[x] -= 0x200; // center around zero.

            return true;
        }

    }
}
