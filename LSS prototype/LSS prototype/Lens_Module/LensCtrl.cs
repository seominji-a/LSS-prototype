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

        public ushort NoErrChk2BytesRead(ushort segmentOffset)
        {
            try
            {
                UsbRead(segmentOffset, ConfigVal.DATA_LENGTH);
                return UsbRead2Bytes();
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return 0;
            }
        }

        public string StringRead(int retval)
        {
            try
            {
                if ((retval == CP2112_DLL.HID_SMBUS_SUCCESS) & (receivedData != null))
                    return Encoding.ASCII.GetString(receivedData);
                return null;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return null;
            }
        }

        public int ModelName(out string model)
        {
            model = null;
            try
            {
                int retval = UsbRead(DevAddr.LENS_MODEL_NAME, ConfigVal.LENSMODEL_LENGTH);
                model = StringRead(retval);
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int UserAreaRead(out string userName)
        {
            userName = null;
            try
            {
                int retval = UsbRead(DevAddr.USER_AREA, ConfigVal.USERAREA_LENGTH);
                userName = StringRead(retval);
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public string VersionRead(int retval)
        {
            try
            {
                if ((retval == CP2112_DLL.HID_SMBUS_SUCCESS) & (receivedData != null))
                    return String.Format("{0}.{1:D2}", receivedData[0], receivedData[1]);
                return null;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return null;
            }
        }

        public int FWVersion(out string version)
        {
            version = null;
            try
            {
                int retval = UsbRead(DevAddr.FIRMWARE_VERSION, ConfigVal.DATA_LENGTH);
                version = VersionRead(retval);
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int ProtocolVersion(out string version)
        {
            version = null;
            try
            {
                int retval = UsbRead(DevAddr.PROTOCOL_VERSION, ConfigVal.DATA_LENGTH);
                version = VersionRead(retval);
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int LensRevision(out int revision)
        {
            revision = 0;
            try
            {
                int retval = UsbRead(DevAddr.LENS_REVISION, ConfigVal.DATA_LENGTH);
                revision = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int LensAddress(out int i2cAddress)
        {
            i2cAddress = 0;
            try
            {
                int retval = UsbRead(DevAddr.LENS_ADDRESS, ConfigVal.LENSADDRESS_LENGTH);
                i2cAddress = receivedData[0];
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int CapabilitiesRead(out ushort capabilities)
        {
            capabilities = 0;
            try
            {
                int retval = UsbRead(DevAddr.CAPABILITIES, ConfigVal.DATA_LENGTH);
                capabilities = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int Status1Read(out ushort status1)
        {
            status1 = 0;
            try
            {
                int retval = UsbRead(DevAddr.STATUS1, ConfigVal.DATA_LENGTH);
                status1 = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int Status2ReadSet()
        {
            try
            {
                int retval = UsbRead(DevAddr.STATUS2, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                status2 = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int TempKelvin(out int kelvinValue)
        {
            kelvinValue = 0;
            try
            {
                int retval = UsbRead(DevAddr.TEMPERATURE_VAL, 2);
                kelvinValue = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int UserAreaWrite(byte[] userName)
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
                    {
                        sendData[sendSize + i] = 0;
                    }
                }
                sendSize = (byte)sendData.Length;
                int retval = CP2112_DLL.HidSmbus_WriteRequest(connectedDevice, i2cAddr,
                    sendData, sendSize);
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        int WaitCalc(ushort moveValue, int speedPPS)
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
                Common.WriteLog(ex);
                return 2000;
            }
        }

        public int ZoomCurrentAddrReadSet()
        {
            try
            {
                int retval = UsbRead(DevAddr.ZOOM_POSITION_VAL, ConfigVal.DATA_LENGTH);
                zoomCurrentAddr = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int ZoomParameterReadSet()
        {
            try
            {
                int retval = UsbRead(DevAddr.ZOOM_POSITION_MIN, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                zoomMinAddr = UsbRead2Bytes();

                retval = UsbRead(DevAddr.ZOOM_POSITION_MAX, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                zoomMaxAddr = UsbRead2Bytes();

                retval = UsbRead(DevAddr.ZOOM_SPEED_VAL, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                zoomSpeedPPS = UsbRead2Bytes();

                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int ZoomBacklashRead(out ushort flag)
        {
            flag = 0;
            try
            {
                int retval = UsbRead(DevAddr.ZOOM_BACKLASH_CANCEL, ConfigVal.DATA_LENGTH);
                flag = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int ZoomBacklashWrite(ushort flag)
        {
            try
            {
                int retval = UsbWrite(DevAddr.ZOOM_BACKLASH_CANCEL, flag);
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int ZoomSpeedMinRead(out ushort speedPPS)
        {
            speedPPS = 0;
            try
            {
                int retval = UsbRead(DevAddr.ZOOM_SPEED_MIN, ConfigVal.DATA_LENGTH);
                speedPPS = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int ZoomSpeedMaxRead(out ushort speedPPS)
        {
            speedPPS = 0;
            try
            {
                int retval = UsbRead(DevAddr.ZOOM_SPEED_MAX, ConfigVal.DATA_LENGTH);
                speedPPS = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int ZoomSpeedWrite(ushort speedPPS)
        {
            try
            {
                int retval = UsbWrite(DevAddr.ZOOM_SPEED_VAL, speedPPS);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                Thread.Sleep(1);
                retval = UsbRead(DevAddr.ZOOM_SPEED_VAL, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                zoomSpeedPPS = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int ZoomCountValRead(out int count)
        {
            count = 0;
            try
            {
                int retval = UsbRead(DevAddr.ZOOM_COUNT_VAL, ConfigVal.LENSCOUNT_LENGTH);
                count = CountRead();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int ZoomCountMaxRead(out int count)
        {
            count = 0;
            try
            {
                int retval = UsbRead(DevAddr.ZOOM_COUNT_MAX, ConfigVal.LENSCOUNT_LENGTH);
                count = CountRead();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int ZoomInit()   // When using it, do ZoomParameterReadSet
        {
            try
            {
                int waitTime = WaitCalc((ushort)(zoomMaxAddr - zoomMinAddr), zoomSpeedPPS);
                int retval = UsbWrite(DevAddr.ZOOM_INITIALIZE, ConfigVal.INIT_RUN_BIT);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                {
                    retval = StatusWait(DevAddr.STATUS1, ConfigVal.ZOOM_MASK, waitTime);
                    if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    {
                        retval = UsbRead(DevAddr.ZOOM_POSITION_VAL, ConfigVal.DATA_LENGTH);
                        if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                        {
                            zoomCurrentAddr = UsbRead2Bytes();
                            Status2ReadSet();
                            return retval;
                        }
                    }
                }
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int ZoomMove(ushort addrData)
        {
            try
            {
                int moveValue = Math.Abs(addrData - zoomCurrentAddr);
                int waitTime = WaitCalc((ushort)moveValue, zoomSpeedPPS);
                int retval = DeviceMove(DevAddr.ZOOM_POSITION_VAL, ref addrData,
                    ConfigVal.ZOOM_MASK, waitTime);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    zoomCurrentAddr = addrData;
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int FocusCurrentAddrReadSet()
        {
            try
            {
                int retval = UsbRead(DevAddr.FOCUS_POSITION_VAL, ConfigVal.DATA_LENGTH);
                focusCurrentAddr = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int FocusParameterReadSet()
        {
            try
            {
                int retval = UsbRead(DevAddr.FOCUS_MECH_STEP_MIN, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                focusMinAddr = UsbRead2Bytes();

                retval = UsbRead(DevAddr.FOCUS_MECH_STEP_MAX, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                focusMaxAddr = UsbRead2Bytes();

                retval = UsbRead(DevAddr.FOCUS_SPEED_VAL, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                focusSpeedPPS = UsbRead2Bytes();

                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int FocusBacklashRead(out ushort flag)
        {
            flag = 0;
            try
            {
                int retval = UsbRead(DevAddr.FOCUS_BACKLASH_CANCEL, 2);
                flag = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int FocusBacklashWrite(ushort flag)
        {
            try
            {
                int retval = UsbWrite(DevAddr.FOCUS_BACKLASH_CANCEL, flag);
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int FocusSpeedMinRead(out ushort speedPPS)
        {
            speedPPS = 0;
            try
            {
                int retval = UsbRead(DevAddr.FOCUS_SPEED_MIN, ConfigVal.DATA_LENGTH);
                speedPPS = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int FocusSpeedMaxRead(out ushort speedPPS)
        {
            speedPPS = 0;
            try
            {
                int retval = UsbRead(DevAddr.FOCUS_SPEED_MAX, ConfigVal.DATA_LENGTH);
                speedPPS = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int FocusSpeedWrite(ushort speedPPS)
        {
            try
            {
                int retval = UsbWrite(DevAddr.FOCUS_SPEED_VAL, speedPPS);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                Thread.Sleep(1);
                retval = UsbRead(DevAddr.FOCUS_SPEED_VAL, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                focusSpeedPPS = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int FocusCountValRead(out int count)
        {
            count = 0;
            try
            {
                int retval = UsbRead(DevAddr.FOCUS_COUNT_VAL, ConfigVal.LENSCOUNT_LENGTH);
                count = CountRead();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int FocusCountMaxRead(out int count)
        {
            count = 0;
            try
            {
                int retval = UsbRead(DevAddr.FOCUS_COUNT_MAX, ConfigVal.LENSCOUNT_LENGTH);
                count = CountRead();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int FocusInit()  // When using it, do FocusParameterReadSet
        {
            try
            {
                int waitTime = WaitCalc((ushort)(focusMaxAddr - focusMinAddr), focusSpeedPPS);
                int retval = UsbWrite(DevAddr.FOCUS_INITIALIZE, ConfigVal.INIT_RUN_BIT);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                {
                    retval = StatusWait(DevAddr.STATUS1, ConfigVal.FOCUS_MASK, waitTime);
                    if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    {
                        retval = UsbRead(DevAddr.FOCUS_POSITION_VAL, ConfigVal.DATA_LENGTH);
                        if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                        {
                            focusCurrentAddr = UsbRead2Bytes();
                            Status2ReadSet();
                            return retval;
                        }
                    }
                }
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int FocusMove(ushort addrData)
        {
            try
            {
                int moveValue = Math.Abs(addrData - focusCurrentAddr);
                int waitTime = WaitCalc((ushort)moveValue, focusSpeedPPS);
                int retval = DeviceMove(DevAddr.FOCUS_POSITION_VAL, ref addrData,
                    ConfigVal.FOCUS_MASK, waitTime);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    focusCurrentAddr = addrData;
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int IrisCurrentAddrReadSet()
        {
            try
            {
                int retval = UsbRead(DevAddr.IRIS_POSITION_VAL, ConfigVal.DATA_LENGTH);
                irisCurrentAddr = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int IrisParameterReadSet()
        {
            try
            {
                int retval = UsbRead(DevAddr.IRIS_MECH_STEP_MIN, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                irisMinAddr = UsbRead2Bytes();

                retval = UsbRead(DevAddr.IRIS_MECH_STEP_MAX, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                irisMaxAddr = UsbRead2Bytes();

                retval = UsbRead(DevAddr.IRIS_SPEED_VAL, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                irisSpeedPPS = UsbRead2Bytes();

                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int IrisBacklashRead(out ushort flag)
        {
            flag = 0;
            try
            {
                int retval = UsbRead(DevAddr.IRIS_BACKLASH_CANCEL, ConfigVal.DATA_LENGTH);
                flag = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int IrisBacklashWrite(ushort flag)
        {
            try
            {
                int retval = UsbWrite(DevAddr.IRIS_BACKLASH_CANCEL, flag);
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int IrisSpeedMinRead(out ushort speedPPS)
        {
            speedPPS = 0;
            try
            {
                int retval = UsbRead(DevAddr.IRIS_SPEED_MIN, ConfigVal.DATA_LENGTH);
                speedPPS = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int IrisSpeedMaxRead(out ushort speedPPS)
        {
            speedPPS = 0;
            try
            {
                int retval = UsbRead(DevAddr.IRIS_SPEED_MAX, ConfigVal.DATA_LENGTH);
                speedPPS = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int IrisSpeedWrite(ushort speedPPS)
        {
            try
            {
                int retval = UsbWrite(DevAddr.IRIS_SPEED_VAL, speedPPS);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                Thread.Sleep(1);
                retval = UsbRead(DevAddr.IRIS_SPEED_VAL, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                irisSpeedPPS = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int IrisCountValRead(out int count)
        {
            count = 0;
            try
            {
                int retval = UsbRead(DevAddr.IRIS_COUNT_VAL, ConfigVal.LENSCOUNT_LENGTH);
                count = CountRead();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int IrisCountMaxRead(out int count)
        {
            count = 0;
            try
            {
                int retval = UsbRead(DevAddr.IRIS_COUNT_MAX, ConfigVal.LENSCOUNT_LENGTH);
                count = CountRead();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int IrisInit()   // When using it, do IrisParameterReadSet
        {
            try
            {
                int waitTime = WaitCalc((ushort)(irisMaxAddr - irisMinAddr), irisSpeedPPS);
                int retval = UsbWrite(DevAddr.IRIS_INITIALIZE, ConfigVal.INIT_RUN_BIT);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                {
                    retval = StatusWait(DevAddr.STATUS1, ConfigVal.IRIS_MASK, waitTime);
                    if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    {
                        retval = UsbRead(DevAddr.IRIS_POSITION_VAL, ConfigVal.DATA_LENGTH);
                        if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                        {
                            irisCurrentAddr = UsbRead2Bytes();
                            Status2ReadSet();
                            return retval;
                        }
                    }
                }
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int IrisMove(ushort addrData)
        {
            try
            {
                int moveValue = Math.Abs(addrData - irisCurrentAddr);
                int waitTime = WaitCalc((ushort)moveValue, irisSpeedPPS);
                int retval = DeviceMove(DevAddr.IRIS_POSITION_VAL, ref addrData,
                    ConfigVal.IRIS_MASK, waitTime);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    irisCurrentAddr = addrData;
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int OptFilterCurrentAddrReadSet()
        {
            try
            {
                int retval = UsbRead(DevAddr.OPT_FILTER_POSITION_VAL, ConfigVal.DATA_LENGTH);
                optCurrentAddr = UsbRead2Bytes();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int OptFilterParameterReadSet()
        {
            try
            {
                int retval = UsbRead(DevAddr.OPT_FILTER_MECH_STEP_MAX, ConfigVal.DATA_LENGTH);
                if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                    return retval;
                optFilMaxAddr = UsbRead2Bytes();

                retval = OptFilterCurrentAddrReadSet();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int OptFilterCountValRead(out int count)
        {
            count = 0;
            try
            {
                int retval = UsbRead(DevAddr.OPT_FILTER_COUNT_VAL, ConfigVal.LENSCOUNT_LENGTH);
                count = CountRead();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int OptFilterCountMaxRead(out int count)
        {
            count = 0;
            try
            {
                int retval = UsbRead(DevAddr.OPT_FILTER_COUNT_MAX, ConfigVal.LENSCOUNT_LENGTH);
                count = CountRead();
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int OptFilterInit()  // When using it, do OptFilterParameterReadSet
        {
            try
            {
                int waitTime = WaitCalc((ushort)(optFilMaxAddr + 1), ConfigVal.OPT_FILTER_SPEED);
                int retval = UsbWrite(DevAddr.OPT_FILTER_INITIALIZE, ConfigVal.INIT_RUN_BIT);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                {
                    retval = StatusWait(DevAddr.STATUS1, ConfigVal.OPT_FILTER_MASK, waitTime);
                    if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    {
                        retval = UsbRead(DevAddr.OPT_FILTER_POSITION_VAL, ConfigVal.DATA_LENGTH);
                        if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                        {
                            optCurrentAddr = UsbRead2Bytes();
                            Status2ReadSet();
                            return retval;
                        }
                    }
                }
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int OptFilterMove(ushort addrData)
        {
            try
            {
                int moveValue = Math.Abs(addrData - optCurrentAddr);
                int waitTime = WaitCalc((ushort)moveValue, ConfigVal.OPT_FILTER_SPEED);
                int retval = DeviceMove(DevAddr.OPT_FILTER_POSITION_VAL, ref addrData,
                    ConfigVal.OPT_FILTER_MASK, waitTime);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    optCurrentAddr = addrData;
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int DeviceMove(ushort segmentOffset, ref ushort addrData, ushort mask, int waitTime)
        {
            try
            {
                int retval = UsbWrite(segmentOffset, addrData);
                if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                {
                    retval = StatusWait(DevAddr.STATUS1, mask, waitTime);
                    if (retval == CP2112_DLL.HID_SMBUS_SUCCESS)
                    {
                        retval = UsbRead(segmentOffset, ConfigVal.DATA_LENGTH);
                        if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                            return retval;
                        addrData = UsbRead2Bytes();
                        return retval;
                    }
                    return retval;
                }
                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public int StatusWait(ushort segmentOffset, ushort statusMask, int waitTime)
        {
            try
            {
                int tmp = 0;
                ushort readStatus;
                int retval;
                do
                {
                    retval = UsbRead(segmentOffset, ConfigVal.DATA_LENGTH);
                    if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                        return retval;

                    readStatus = UsbRead2Bytes();
                    tmp += 1;
                    if (tmp >= ConfigVal.LOW_HIGH_WAIT)
                        return ConfigVal.LOWHI_ERROR;

                } while ((readStatus & statusMask) != statusMask);

                tmp = 0;
                do
                {
                    retval = UsbRead(segmentOffset, ConfigVal.DATA_LENGTH);
                    if (retval != CP2112_DLL.HID_SMBUS_SUCCESS)
                        return retval;

                    readStatus = UsbRead2Bytes();
                    tmp += 1;
                    if (tmp >= waitTime)
                        return ConfigVal.HILOW_ERROR;

                    Thread.Sleep(1);
                } while ((readStatus & statusMask) != 0);

                return retval;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return -1;
            }
        }

        public string ErrorTxt(int returnCode)
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
                Common.WriteLog(ex);
                return "";
            }
        }

        public static LensCtrl Instance = new LensCtrl();
    }
}