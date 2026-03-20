using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LSS_prototype.Lens_Module
{
    class LensCtrl : UsbCtrl
    {
        private LensCtrl()
        {
        }
        public ushort zoomMaxAddr, zoomMinAddr, focusMaxAddr, focusMinAddr;
        public ushort irisMaxAddr, irisMinAddr, optFilMaxAddr;
        public ushort zoomCurrentAddr, focusCurrentAddr, irisCurrentAddr, optCurrentAddr;
        public ushort zoomSpeedPPS, focusSpeedPPS, irisSpeedPPS;
        public ushort status2;

        public async Task<ushort> NoErrChk2BytesRead(ushort segmentOffset)
        {
            try
            {
                await UsbRead(segmentOffset, ConfigVal.DATA_LENGTH);
                return await UsbRead2Bytes();
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return 0;
            }
        }

        public async Task<string> StringRead(int retval)
        {
            try
            {
                if ((retval == CP2112_DLL.HID_SMBUS_SUCCESS) & (receivedData != null))
                    return Encoding.ASCII.GetString(receivedData);
                return null;
            }
            catch (Exception ex)
            {
                await Common .WriteLog(ex);
                return null;
            }
        }

        public async Task<(int retval, string model)> ModelName()
        {
            try
            {
                int retval = await UsbRead(DevAddr.LENS_MODEL_NAME, ConfigVal.LENSMODEL_LENGTH);
                string model = await StringRead(retval);
                return (retval, model);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, null);
            }
        }

        public async Task<(int retval, string userName)> UserAreaRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.USER_AREA, ConfigVal.USERAREA_LENGTH);
                string userName = await StringRead (retval);
                return (retval, userName);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, null);
            }
        }

        public async Task<string> VersionRead(int retval)
        {
            try
            {
                if ((retval == CP2112_DLL.HID_SMBUS_SUCCESS) & (receivedData != null))
                    return String.Format("{0}.{1:D2}", receivedData[0], receivedData[1]);
                return null;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return null;
            }
        }

        public async Task<(int retval, string version)> FWVersion()
        {
            try
            {
                int retval = await UsbRead(DevAddr.FIRMWARE_VERSION, ConfigVal.DATA_LENGTH);
                string version = await VersionRead(retval);
                return (retval, version);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, null);
            }
        }

        public async Task<(int retval, string version)> ProtocolVersion()
        {
            try
            {
                int retval = await UsbRead(DevAddr.PROTOCOL_VERSION, ConfigVal.DATA_LENGTH);
                string version = await VersionRead(retval);
                return (retval, version);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, null);
            }
        }

        public async Task<(int retval, int revision)> LensRevision()
        {
            try
            {
                int retval = await UsbRead(DevAddr.LENS_REVISION, ConfigVal.DATA_LENGTH);
                int revision = await UsbRead2Bytes();
                return (retval, revision);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<(int retval, int i2cAddress)> LensAddress()
        {
            try
            {
                int retval = await UsbRead(DevAddr.LENS_ADDRESS, ConfigVal.LENSADDRESS_LENGTH);
                int i2cAddress = receivedData[0];
                return (retval, i2cAddress);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<(int retval, ushort capabilities)> CapabilitiesRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.CAPABILITIES, ConfigVal.DATA_LENGTH);
                ushort capabilities = await UsbRead2Bytes();
                return (retval, capabilities);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<(int retval, ushort status1)> Status1Read()
        {
            try
            {
                int retval = await UsbRead(DevAddr.STATUS1, ConfigVal.DATA_LENGTH);
                ushort status1 = await UsbRead2Bytes();
                return (retval, status1);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<int> Status2ReadSet()
        {
            try
            {
                int retval = await UsbRead(DevAddr.STATUS2, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                status2 = await UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<(int retval, int kelvinValue)> TempKelvin()
        {
            try
            {
                int retval = await UsbRead(DevAddr.TEMPERATURE_VAL, 2);
                int kelvinValue = await UsbRead2Bytes();
                return (retval, kelvinValue);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<int> UserAreaWrite(byte[] userName)
        {
            try
            {
                byte[] sendData;
                byte userNameSize = (byte)userName.Length;
                byte sendSize = (byte)(userNameSize + ConfigVal.SEGMENTOFFSET_LENGTH);

                ushort SegmentOffset = DevAddr.USER_AREA;
                sendData = new byte[ConfigVal.USERAREA_LENGTH + ConfigVal.SEGMENTOFFSET_LENGTH];
                sendData[0] = (byte)(SegmentOffset >> 8);
                sendData[1] = (byte)SegmentOffset;
                userName.CopyTo(sendData, 2);
                if (userNameSize < ConfigVal.USERAREA_LENGTH)
                {
                    int space = ConfigVal.USERAREA_LENGTH - userNameSize;
                    for (int i = 0; i < space; i++)
                        sendData[sendSize + i] = 0;
                }
                sendSize = (byte)sendData.Length;
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

        async Task<int> WaitCalc(ushort moveValue, int speedPPS)
        {
            try
            {
                int waitTime = ConfigVal.WAIT_MAG * moveValue / speedPPS;
                if (2000 > waitTime)
                    waitTime = 2000;
                return waitTime;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return 2000;
            }
        }

        public async Task<int> ZoomCurrentAddrReadSet()
        {
            try
            {
                int retval = await UsbRead(DevAddr.ZOOM_POSITION_VAL, ConfigVal.DATA_LENGTH);
                zoomCurrentAddr = await UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> ZoomParameterReadSet()
        {
            try
            {
                int retval = await UsbRead(DevAddr.ZOOM_POSITION_MIN, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                zoomMinAddr = await UsbRead2Bytes();

                retval = await UsbRead(DevAddr.ZOOM_POSITION_MAX, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                zoomMaxAddr = await UsbRead2Bytes();

                retval = await UsbRead(DevAddr.ZOOM_SPEED_VAL, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                zoomSpeedPPS = await UsbRead2Bytes();

                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<(int retval, ushort flag)> ZoomBacklashRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.ZOOM_BACKLASH_CANCEL, ConfigVal.DATA_LENGTH);
                ushort flag = await UsbRead2Bytes();
                return (retval, flag);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<int> ZoomBacklashWrite(ushort flag)
        {
            try
            {
                int retval = await UsbWrite(DevAddr.ZOOM_BACKLASH_CANCEL, flag);
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<(int retval, ushort speedPPS)> ZoomSpeedMinRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.ZOOM_SPEED_MIN, ConfigVal.DATA_LENGTH);
                ushort speedPPS = await UsbRead2Bytes();
                return (retval, speedPPS);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<(int retval, ushort speedPPS)> ZoomSpeedMaxRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.ZOOM_SPEED_MAX, ConfigVal.DATA_LENGTH);
                ushort speedPPS = await UsbRead2Bytes();
                return (retval, speedPPS);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<int> ZoomSpeedWrite(ushort speedPPS)
        {
            try
            {
                int retval = await UsbWrite(DevAddr.ZOOM_SPEED_VAL, speedPPS);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                Thread.Sleep(1);
                retval = await UsbRead(DevAddr.ZOOM_SPEED_VAL, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                zoomSpeedPPS = await UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<(int retval, int count)> ZoomCountValRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.ZOOM_COUNT_VAL, ConfigVal.LENSCOUNT_LENGTH);
                int count = await CountRead();
                return (retval, count);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<(int retval, int count)> ZoomCountMaxRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.ZOOM_COUNT_MAX, ConfigVal.LENSCOUNT_LENGTH);
                int count = await CountRead();
                return (retval, count);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<int> ZoomInit()
        {
            try
            {
                int waitTime = await WaitCalc((ushort)(zoomMaxAddr - zoomMinAddr), zoomSpeedPPS);
                int retval = await UsbWrite(DevAddr.ZOOM_INITIALIZE, ConfigVal.INIT_RUN_BIT);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                {
                    retval = await StatusWait(DevAddr.STATUS1, ConfigVal.ZOOM_MASK, waitTime);
                    if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    {
                        retval = await UsbRead(DevAddr.ZOOM_POSITION_VAL, ConfigVal.DATA_LENGTH);
                        if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                        {
                            zoomCurrentAddr = await UsbRead2Bytes();
                            await Status2ReadSet();
                            return retval;
                        }
                    }
                }
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> ZoomMove(ushort addrData)
        {
            try
            {
                int moveValue = Math.Abs(addrData - zoomCurrentAddr);
                int waitTime = await WaitCalc((ushort)moveValue, zoomSpeedPPS);
                int retval = await DeviceMove(DevAddr.ZOOM_POSITION_VAL, addrData,
                    ConfigVal.ZOOM_MASK, waitTime);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    zoomCurrentAddr = addrData;
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> FocusCurrentAddrReadSet()
        {
            try
            {
                int retval = await UsbRead(DevAddr.FOCUS_POSITION_VAL, ConfigVal.DATA_LENGTH);
                focusCurrentAddr = await UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> FocusParameterReadSet()
        {
            try
            {
                int retval = await UsbRead(DevAddr.FOCUS_MECH_STEP_MIN, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                focusMinAddr = await UsbRead2Bytes();

                retval = await UsbRead(DevAddr.FOCUS_MECH_STEP_MAX, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                focusMaxAddr = await UsbRead2Bytes();

                retval = await UsbRead(DevAddr.FOCUS_SPEED_VAL, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                focusSpeedPPS = await UsbRead2Bytes();

                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<(int retval, ushort flag)> FocusBacklashRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.FOCUS_BACKLASH_CANCEL, 2);
                ushort flag = await UsbRead2Bytes();
                return (retval, flag);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<int> FocusBacklashWrite(ushort flag)
        {
            try
            {
                int retval = await UsbWrite(DevAddr.FOCUS_BACKLASH_CANCEL, flag);
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<(int retval, ushort speedPPS)> FocusSpeedMinRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.FOCUS_SPEED_MIN, ConfigVal.DATA_LENGTH);
                ushort speedPPS = await UsbRead2Bytes();
                return (retval, speedPPS);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<(int retval, ushort speedPPS)> FocusSpeedMaxRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.FOCUS_SPEED_MAX, ConfigVal.DATA_LENGTH);
                ushort speedPPS = await UsbRead2Bytes();
                return (retval, speedPPS);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<int> FocusSpeedWrite(ushort speedPPS)
        {
            try
            {
                int retval = await UsbWrite(DevAddr.FOCUS_SPEED_VAL, speedPPS);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                Thread.Sleep(1);
                retval = await UsbRead(DevAddr.FOCUS_SPEED_VAL, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                focusSpeedPPS = await UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<(int retval, int count)> FocusCountValRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.FOCUS_COUNT_VAL, ConfigVal.LENSCOUNT_LENGTH);
                int count = await CountRead();
                return (retval, count);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<(int retval, int count)> FocusCountMaxRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.FOCUS_COUNT_MAX, ConfigVal.LENSCOUNT_LENGTH);
                int count = await CountRead();
                return (retval, count);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<int> FocusInit()
        {
            try
            {
                int waitTime = await WaitCalc((ushort)(focusMaxAddr - focusMinAddr), focusSpeedPPS);
                int retval = await UsbWrite(DevAddr.FOCUS_INITIALIZE, ConfigVal.INIT_RUN_BIT);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                {
                    retval = await StatusWait(DevAddr.STATUS1, ConfigVal.FOCUS_MASK, waitTime);
                    if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    {
                        retval = await UsbRead(DevAddr.FOCUS_POSITION_VAL, ConfigVal.DATA_LENGTH);
                        if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                        {
                            focusCurrentAddr = await UsbRead2Bytes();
                            await Status2ReadSet();
                            return retval;
                        }
                    }
                }
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> FocusMove(ushort addrData)
        {
            try
            {
                int moveValue = Math.Abs(addrData - focusCurrentAddr);
                int waitTime = await WaitCalc((ushort)moveValue, focusSpeedPPS);
                int retval = await DeviceMove(DevAddr.FOCUS_POSITION_VAL, addrData,
                    ConfigVal.FOCUS_MASK, waitTime);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    focusCurrentAddr = addrData;
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> IrisCurrentAddrReadSet()
        {
            try
            {
                int retval = await UsbRead(DevAddr.IRIS_POSITION_VAL, ConfigVal.DATA_LENGTH);
                irisCurrentAddr = await UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> IrisParameterReadSet()
        {
            try
            {
                int retval = await UsbRead(DevAddr.IRIS_MECH_STEP_MIN, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                irisMinAddr = await UsbRead2Bytes();

                retval = await UsbRead(DevAddr.IRIS_MECH_STEP_MAX, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                irisMaxAddr = await UsbRead2Bytes();

                retval = await UsbRead(DevAddr.IRIS_SPEED_VAL, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                irisSpeedPPS = await UsbRead2Bytes();

                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<(int retval, ushort flag)> IrisBacklashRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.IRIS_BACKLASH_CANCEL, ConfigVal.DATA_LENGTH);
                ushort flag = await UsbRead2Bytes();
                return (retval, flag);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<int> IrisBacklashWrite(ushort flag)
        {
            try
            {
                int retval = await UsbWrite(DevAddr.IRIS_BACKLASH_CANCEL, flag);
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<(int retval, ushort speedPPS)> IrisSpeedMinRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.IRIS_SPEED_MIN, ConfigVal.DATA_LENGTH);
                ushort speedPPS = await UsbRead2Bytes();
                return (retval, speedPPS);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<(int retval, ushort speedPPS)> IrisSpeedMaxRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.IRIS_SPEED_MAX, ConfigVal.DATA_LENGTH);
                ushort speedPPS = await UsbRead2Bytes();
                return (retval, speedPPS);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<int> IrisSpeedWrite(ushort speedPPS)
        {
            try
            {
                int retval = await UsbWrite(DevAddr.IRIS_SPEED_VAL, speedPPS);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                Thread.Sleep(1);
                retval = await UsbRead(DevAddr.IRIS_SPEED_VAL, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                irisSpeedPPS = await UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<(int retval, int count)> IrisCountValRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.IRIS_COUNT_VAL, ConfigVal.LENSCOUNT_LENGTH);
                int count = await CountRead();
                return (retval, count);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<(int retval, int count)> IrisCountMaxRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.IRIS_COUNT_MAX, ConfigVal.LENSCOUNT_LENGTH);
                int count = await CountRead();
                return (retval, count);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<int> IrisInit()
        {
            try
            {
                int waitTime = await WaitCalc((ushort)(irisMaxAddr - irisMinAddr), irisSpeedPPS);
                int retval = await UsbWrite(DevAddr.IRIS_INITIALIZE, ConfigVal.INIT_RUN_BIT);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                {
                    retval = await StatusWait(DevAddr.STATUS1, ConfigVal.IRIS_MASK, waitTime);
                    if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    {
                        retval = await UsbRead(DevAddr.IRIS_POSITION_VAL, ConfigVal.DATA_LENGTH);
                        if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                        {
                            irisCurrentAddr = await UsbRead2Bytes();
                            await Status2ReadSet();
                            return retval;
                        }
                    }
                }
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> IrisMove(ushort addrData)
        {
            try
            {
                int moveValue = Math.Abs(addrData - irisCurrentAddr);
                int waitTime = await WaitCalc((ushort)moveValue, irisSpeedPPS);
                int retval = await DeviceMove(DevAddr.IRIS_POSITION_VAL, addrData,
                    ConfigVal.IRIS_MASK, waitTime);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    irisCurrentAddr = addrData;
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> OptFilterCurrentAddrReadSet()
        {
            try
            {
                int retval = await UsbRead(DevAddr.OPT_FILTER_POSITION_VAL, ConfigVal.DATA_LENGTH);
                optCurrentAddr = await UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> OptFilterParameterReadSet()
        {
            try
            {
                int retval = await UsbRead(DevAddr.OPT_FILTER_MECH_STEP_MAX, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS) return retval;
                optFilMaxAddr = await UsbRead2Bytes();

                retval = await OptFilterCurrentAddrReadSet();
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<(int retval, int count)> OptFilterCountValRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.OPT_FILTER_COUNT_VAL, ConfigVal.LENSCOUNT_LENGTH);
                int count = await CountRead();
                return (retval, count);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<(int retval, int count)> OptFilterCountMaxRead()
        {
            try
            {
                int retval = await UsbRead(DevAddr.OPT_FILTER_COUNT_MAX, ConfigVal.LENSCOUNT_LENGTH);
                int count = await CountRead();
                return (retval, count);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return (-1, 0);
            }
        }

        public async Task<int> OptFilterInit()
        {
            try
            {
                int waitTime = await WaitCalc((ushort)(optFilMaxAddr + 1), ConfigVal.OPT_FILTER_SPEED);
                int retval = await UsbWrite(DevAddr.OPT_FILTER_INITIALIZE, ConfigVal.INIT_RUN_BIT);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                {
                    retval = await StatusWait(DevAddr.STATUS1, ConfigVal.OPT_FILTER_MASK, waitTime);
                    if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    {
                        retval = await UsbRead(DevAddr.OPT_FILTER_POSITION_VAL, ConfigVal.DATA_LENGTH);
                        if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                        {
                            optCurrentAddr = await UsbRead2Bytes();
                            await Status2ReadSet();
                            return retval;
                        }
                    }
                }
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> OptFilterMove(ushort addrData)
        {
            try
            {
                int moveValue = Math.Abs(addrData - optCurrentAddr);
                int waitTime = await WaitCalc((ushort)moveValue, ConfigVal.OPT_FILTER_SPEED);
                int retval = await DeviceMove(DevAddr.OPT_FILTER_POSITION_VAL, addrData,
                    ConfigVal.OPT_FILTER_MASK, waitTime);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    optCurrentAddr = addrData;
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> DeviceMove(ushort segmentOffset, ushort addrData, ushort mask, int waitTime)
        {
            try
            {
                int retval = await UsbWrite(segmentOffset, addrData);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                {
                    retval = await StatusWait(DevAddr.STATUS1, mask, waitTime);
                    if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    {
                        retval = await UsbRead(segmentOffset, ConfigVal.DATA_LENGTH);
                        if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                            return retval;
                        addrData = await UsbRead2Bytes();
                        return retval;
                    }
                    return retval;
                }
                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<int> StatusWait(ushort segmentOffset, ushort statusMask, int waitTime)
        {
            try
            {
                int tmp = 0;
                ushort readStatus;
                int retval;
                do
                {
                    retval = await UsbRead(segmentOffset, ConfigVal.DATA_LENGTH);
                    if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                        return retval;

                    readStatus = await UsbRead2Bytes();
                    tmp += 1;
                    if (tmp >= ConfigVal.LOW_HIGH_WAIT)
                        return ConfigVal.LOWHI_ERROR;

                } while ((readStatus & statusMask) != statusMask);

                tmp = 0;
                do
                {
                    retval = await UsbRead(segmentOffset, ConfigVal.DATA_LENGTH);
                    if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                        return retval;

                    readStatus = await UsbRead2Bytes();
                    tmp += 1;
                    if (tmp >= waitTime)
                        return ConfigVal.HILOW_ERROR;

                    Thread.Sleep(1);
                } while ((readStatus & statusMask) != 0);

                return retval;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        public async Task<string> ErrorTxt(int returnCode)
        {
            try
            {
                switch (returnCode)
                {
                    case 0x01: return "Device not found.";
                    case 0x02: return "Invalid handle.";
                    case 0x03: return "Invalid device object.";
                    case 0x04: return "Invalid parameter.";
                    case 0x05: return "Invalid request length.";
                    case 0x10: return "Read error.";
                    case 0x11: return "Write error.";
                    case 0x12: return "Read time out.";
                    case 0x13: return "Write time out.";
                    case 0x14: return "Device IO failed.";
                    case 0x15: return "Device access error.\r\nThe device may already be running.";
                    case 0x16: return "Device not supported.";
                    case 0x50: return "Status bit high error.";
                    case 0x51: return "Status bit low error.";
                }
                return "";
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return "";
            }
        }

        public static LensCtrl Instance = new LensCtrl();
    }
}
