using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype.Lens_Module
{
    class UsbCtrl
    {
        public IntPtr connectedDevice;
        public byte i2cAddr = ConfigVal.I2CSLAVEADDR * 2;
        public byte[] receivedData;

        // out → 튜플 반환 (retval, numDevices)
        public async Task<(int retval, uint numDevices)> UsbGetNumDevices()
        {
            uint numDevices = 0;
            try
            {
                int retval = CP2112_DLL.HidSmbus_GetNumDevices(ref numDevices,
                    ConfigVal.VID, ConfigVal.PID);
                return (retval, numDevices);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        // out → 튜플 반환 (retval, SnString)
        public async Task<(int retval, string SnString)> UsbGetSnDevice(uint index)
        {
            try
            {
                StringBuilder serialString = new StringBuilder(" ", 260);
                int retval = CP2112_DLL.HidSmbus_GetString(index,
                    ConfigVal.VID, ConfigVal.PID,
                    serialString, CP2112_DLL.HID_SMBUS_GET_SERIAL_STR);
                return (retval, serialString.ToString());
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, string.Empty);
            }
        }

        public async Task<int> UsbOpen(uint deviceNumber)
        {
            try
            {
                int retval = CP2112_DLL.HidSmbus_Open(ref connectedDevice,
                    deviceNumber, ConfigVal.VID, ConfigVal.PID);
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task UsbClose()
        {
            try
            {
                CP2112_DLL.HidSmbus_Close(connectedDevice);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        public async Task<int> UsbSetConfig()
        {
            try
            {
                int retval = CP2112_DLL.HidSmbus_SetSmbusConfig(connectedDevice,
                    ConfigVal.BITRATE, i2cAddr, ConfigVal.AUTOREADRESPOND,
                    ConfigVal.WRITETIMEOUT, ConfigVal.READTIMEOUT,
                    ConfigVal.SCLLOWTIMEOUT, ConfigVal.TRANSFARRETRIES);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;

                retval = CP2112_DLL.HidSmbus_SetGpioConfig(connectedDevice,
                    ConfigVal.DIRECTION, ConfigVal.MODE,
                    ConfigVal.SPECIAL, ConfigVal.CLKDIV);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;

                retval = CP2112_DLL.HidSmbus_SetTimeouts(connectedDevice,
                    ConfigVal.RESPONSETIMEOUT);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;

                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> UsbRead(ushort segmentOffset, ushort receiveSize)
        {
            try
            {
                byte[] sendData = new byte[ConfigVal.SEGMENTOFFSET_LENGTH];
                sendData[0] = (byte)(segmentOffset >> 8);
                sendData[1] = (byte)segmentOffset;
                byte sendSize = (byte)sendData.Length;

                int retval = CP2112_DLL.HidSmbus_WriteRequest(connectedDevice, i2cAddr,
                    sendData, sendSize);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;

                retval = CP2112_DLL.HidSmbus_ReadRequest(connectedDevice, i2cAddr, receiveSize);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;

                retval = CP2112_DLL.HidSmbus_ForceReadResponse(connectedDevice, receiveSize);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;

                byte[] receiveData = new byte[62];
                byte status = 0;
                byte totalBytesRead = 0;
                var buffer = new byte[62];
                byte bufferSize = 62;
                byte bytesRead = 0;
                do
                {
                    retval = CP2112_DLL.HidSmbus_GetReadResponse(connectedDevice, ref status,
                        buffer, bufferSize, ref bytesRead);
                    if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                        return retval;

                    Buffer.BlockCopy(buffer, 0, receiveData, totalBytesRead, bytesRead);
                    totalBytesRead += bytesRead;

                } while (totalBytesRead < receiveSize);

                Array.Resize(ref receiveData, totalBytesRead);
                receivedData = receiveData;

                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<ushort> UsbRead2Bytes()
        {
            try
            {
                if (receivedData == null || receivedData.Length < 2)
                    return 0;

                return (ushort)((receivedData[0] << 8) | receivedData[1]);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return 0;
            }
        }

        public async Task<int> CountRead()
        {
            try
            {
                return ((receivedData[0] << 24) | (receivedData[1] << 16) |
                    (receivedData[2] << 8) | (receivedData[3]));
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> UsbWrite(ushort segmentOffset, ushort writeData)
        {
            try
            {
                byte[] sendData = new byte[ConfigVal.SEGMENTOFFSET_LENGTH + ConfigVal.DATA_LENGTH];
                sendData[0] = (byte)(segmentOffset >> 8);
                sendData[1] = (byte)segmentOffset;
                sendData[2] = (byte)(writeData >> 8);
                sendData[3] = (byte)writeData;
                byte sendSize = (byte)sendData.Length;
                int retval = CP2112_DLL.HidSmbus_WriteRequest(connectedDevice, i2cAddr,
                    sendData, sendSize);
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }
    }
}
