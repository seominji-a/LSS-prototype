using LSS_prototype.Lens_Module;
using LSS_prototype.User_Page;
using OpenCvSharp;
using SpinnakerNET.GenApi;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LSS_prototype.Common_Module
{
    public partial class CameraService
    {
        #region 카메라/렌즈 설정값 변수

        //── 카메라 및 렌즈 설정 변수   ──
        private double _exposureValue = 100000;
        private const double _exposureStep = 100000;
        private const double _exposureMin = 100000;
        private const double _exposureMax = 1000000;

        private double _gainValue = 0.0;
        private const double _gainStep = 3.0; // 증가 감소값
        private const double _gainMin = 0.0;
        private const double _gainMax = 30.0;

        private double _gammaValue = 0.25;
        private const double _gammaStep = 0.1;
        private const double _gammaMin = 0.25;
        private const double _gammaMax = 4.0;

        private double _irisValue = 0;
        private const double _irisStep = 50;
        private const double _irisMin = 0;
        private const double _irisMax = 656;

        // ── 카메라 zoom In/Out 관련 변수  ──
        private const int _zoomStep = 300; // 한번 누를 때 증가 감소 범위
        private const int _focusStep = 300;

        #endregion

        #region 카메라 설정 초기화 (DB값 적용)

        public void InitializeCameraSettings(DefaultModel data)
        {
            try
            {
                // ── 카메라 설정 ──
                if (_managedCameras != null)
                {
                    for (int i = 0; i < _managedCameras.Count; i++)
                    {
                        INodeMap nodeMap = _managedCameras[i].GetNodeMap();

                        IEnum iExposureAuto = nodeMap.GetNode<IEnum>("ExposureAuto");
                        iExposureAuto.Value = iExposureAuto.GetEntryByName("Off").Symbolic;
                        IFloat iExposureTime = nodeMap.GetNode<IFloat>("ExposureTime");
                        iExposureTime.Value = data.ExposureTime;
                        _exposureValue = data.ExposureTime;

                        IEnum iGainAuto = nodeMap.GetNode<IEnum>("GainAuto");
                        iGainAuto.Value = iGainAuto.GetEntryByName("Off").Symbolic;
                        IFloat iGain = nodeMap.GetNode<IFloat>("Gain");
                        iGain.Value = data.Gain;
                        _gainValue = data.Gain;

                        IBool iGammaEnable = nodeMap.GetNode<IBool>("GammaEnable");
                        iGammaEnable.Value = true;
                        IFloat iGamma = nodeMap.GetNode<IFloat>("Gamma");
                        iGamma.Value = data.Gamma;
                        _gammaValue = data.Gamma;
                    }
                    Console.WriteLine($"> 카메라 초기 설정 완료: exposure={data.ExposureTime} gain={data.Gain} gamma={data.Gamma}");
                }

                // ── 렌즈파라미터 읽기  ──
                LensCtrl.Instance.ZoomParameterReadSet();
                LensCtrl.Instance.FocusParameterReadSet();
                LensCtrl.Instance.IrisParameterReadSet();
                LensCtrl.Instance.OptFilterParameterReadSet();

                // ── 렌즈 설정 ──
                LensCtrl.Instance.ZoomMove((ushort)data.Zoom);
                LensCtrl.Instance.FocusMove((ushort)data.Focus);
                LensCtrl.Instance.IrisMove((ushort)data.Iris);
                LensCtrl.Instance.OptFilterMove(data.Filter == 0 ? (ushort)0 : (ushort)1);
                _irisValue = data.Iris;

                Console.WriteLine($"> 렌즈 초기 설정 완료: zoom={data.Zoom} focus={data.Focus} iris={data.Iris} filter={data.Filter}");
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        #endregion

        #region Exposure 제어

        public void ExposureInc()
        {
            try
            {
                double next = _exposureValue + _exposureStep;
                if (next > _exposureMax) next = _exposureMax;
                ApplyExposure(next);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        public void ExposureDec()
        {
            try
            {
                double next = _exposureValue - _exposureStep;
                if (next < _exposureMin) next = _exposureMin;
                ApplyExposure(next);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        private void ApplyExposure(double value)
        {
            if (_managedCameras == null) return;
            for (int i = 0; i < _managedCameras.Count; i++)
            {
                INodeMap nodeMap = _managedCameras[i].GetNodeMap();
                IFloat iExposure = nodeMap.GetNode<IFloat>("ExposureTime");
                iExposure.Value = value;
            }
            _exposureValue = value;
            Console.WriteLine($"> Exposure 변경: {value / 1000000:F1}s");
        }

        public double ExposureCurrentRead()
        {
            try
            {
                if (_managedCameras == null) return _exposureValue;
                INodeMap nodeMap = _managedCameras[0].GetNodeMap();
                IFloat iExposure = nodeMap.GetNode<IFloat>("ExposureTime");
                _exposureValue = iExposure.Value;
                return _exposureValue;
            }
            catch (Exception ex) { Common.WriteLog(ex); return _exposureValue; }
        }

        #endregion

        #region Gain 제어

        public void GainInc()
        {
            try
            {
                double next = _gainValue + _gainStep;
                if (next > _gainMax) next = _gainMax;
                ApplyGain(next);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        public void GainDec()
        {
            try
            {
                double next = _gainValue - _gainStep;
                if (next < _gainMin) next = _gainMin;
                ApplyGain(next);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        private void ApplyGain(double value)
        {
            if (_managedCameras == null) return;

            for (int i = 0; i < _managedCameras.Count; i++)
            {
                INodeMap nodeMap = _managedCameras[i].GetNodeMap();
                IFloat iGain = nodeMap.GetNode<IFloat>("Gain");
                iGain.Value = value;
            }

            _gainValue = value;
            Console.WriteLine($"> Gain 변경: {_gainValue}");
        }

        public double GainCurrentRead()
        {
            try
            {
                if (_managedCameras == null) return _gainValue;

                INodeMap nodeMap = _managedCameras[0].GetNodeMap();
                IFloat iGain = nodeMap.GetNode<IFloat>("Gain");
                _gainValue = iGain.Value;
                return _gainValue;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return _gainValue;
            }
        }

        #endregion

        #region Gamma 제어

        public void GammaInc()
        {
            try
            {
                double next = Math.Round(_gammaValue + _gammaStep, 2);
                if (next > _gammaMax) next = _gammaMax;
                ApplyGamma(next);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        public void GammaDec()
        {
            try
            {
                double next = Math.Round(_gammaValue - _gammaStep, 2);
                if (next < _gammaMin) next = _gammaMin;
                ApplyGamma(next);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        private void ApplyGamma(double value)
        {
            if (_managedCameras == null) return;
            for (int i = 0; i < _managedCameras.Count; i++)
            {
                INodeMap nodeMap = _managedCameras[i].GetNodeMap();
                IFloat iGamma = nodeMap.GetNode<IFloat>("Gamma");
                iGamma.Value = value;
            }
            _gammaValue = value;
            Console.WriteLine($"> Gamma 변경: {_gammaValue}");
        }

        public double GammaCurrentRead()
        {
            try
            {
                if (_managedCameras == null) return _gammaValue;
                INodeMap nodeMap = _managedCameras[0].GetNodeMap();
                IFloat iGamma = nodeMap.GetNode<IFloat>("Gamma");
                _gammaValue = iGamma.Value;
                return _gammaValue;
            }
            catch (Exception ex) { Common.WriteLog(ex); return _gammaValue; }
        }

        #endregion

        #region Iris 제어 (LensCtrl)

        public void IrisInc()
        {
            try
            {
                double next = _irisValue + _irisStep;
                if (next > _irisMax) next = _irisMax;
                ApplyIris(next);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        public void IrisDec()
        {
            try
            {
                double next = _irisValue - _irisStep;
                if (next < _irisMin) next = _irisMin;
                ApplyIris(next);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        private void ApplyIris(double value)
        {
            LensCtrl.Instance.IrisMove((ushort)value);
            _irisValue = value;
            Console.WriteLine($"> Iris 변경: {_irisValue}");
        }

        public double IrisCurrentRead()
        {
            try
            {
                _irisValue = LensCtrl.Instance.irisCurrentAddr;
                return _irisValue;
            }
            catch (Exception ex) { Common.WriteLog(ex); return _irisValue; }
        }

        #endregion

        #region Zoom 제어 (LensCtrl)

        public void ZoomIn()
        {
            try
            {
                int nextZoom = LensCtrl.Instance.zoomCurrentAddr + _zoomStep;
                if (nextZoom > LensCtrl.Instance.zoomMaxAddr)
                    nextZoom = LensCtrl.Instance.zoomMaxAddr;

                LensCtrl.Instance.ZoomMove((ushort)nextZoom);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        public void ZoomOut()
        {
            try
            {
                // ── int 로 먼저 계산 → 음수 언더플로우 방지 ──
                int nextZoom = LensCtrl.Instance.zoomCurrentAddr - _zoomStep;
                if (nextZoom < LensCtrl.Instance.zoomMinAddr)
                    nextZoom = LensCtrl.Instance.zoomMinAddr;

                LensCtrl.Instance.ZoomMove((ushort)nextZoom);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        #endregion

        #region Focus 제어 (LensCtrl)

        public void FocusIn()
        {
            try
            {
                if (LensCtrl.Instance.focusMaxAddr == 0)
                {
                    LensCtrl.Instance.FocusParameterReadSet();
                    LensCtrl.Instance.FocusCurrentAddrReadSet();
                }

                int nextFocus = LensCtrl.Instance.focusCurrentAddr + _focusStep;
                if (nextFocus > LensCtrl.Instance.focusMaxAddr)
                    nextFocus = LensCtrl.Instance.focusMaxAddr;

                LensCtrl.Instance.FocusMove((ushort)nextFocus);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        public void FocusOut()
        {
            try
            {
                if (LensCtrl.Instance.focusMaxAddr == 0)
                {
                    LensCtrl.Instance.FocusParameterReadSet();
                    LensCtrl.Instance.FocusCurrentAddrReadSet();
                }

                int nextFocus = LensCtrl.Instance.focusCurrentAddr - _focusStep;
                if (nextFocus < LensCtrl.Instance.focusMinAddr)
                    nextFocus = LensCtrl.Instance.focusMinAddr;

                LensCtrl.Instance.FocusMove((ushort)nextFocus);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        public async Task AutoFocus()
        {
            await Task.Run(() =>
            {
                try
                {
                    Mat frame;
                    lock (_frameLock) { frame = _lastFrame?.Clone(); }
                    if (frame == null) return;

                    int low = LensCtrl.Instance.focusMinAddr;
                    int high = LensCtrl.Instance.focusMaxAddr;
                    double golden = 0.618;

                    while (high - low > 50)
                    {
                        int mid1 = (int)(low + (high - low) * (1 - golden));
                        int mid2 = (int)(low + (high - low) * golden);

                        LensCtrl.Instance.FocusMove((ushort)mid1);
                        Thread.Sleep(200);

                        // ──  루프 내에서도 매번 최신 프레임을 lock 으로 안전하게 복사 ──
                        lock (_frameLock) { frame?.Dispose(); frame = _lastFrame?.Clone(); }
                        double sharp1 = CalcSharpness(frame);

                        LensCtrl.Instance.FocusMove((ushort)mid2);
                        Thread.Sleep(200);

                        lock (_frameLock) { frame?.Dispose(); frame = _lastFrame?.Clone(); }
                        double sharp2 = CalcSharpness(frame);

                        if (sharp1 > sharp2) high = mid2;
                        else low = mid1;

                        Console.WriteLine($"> mid1:{mid1} s1:{sharp1:F0} mid2:{mid2} s2:{sharp2:F0}");
                    }

                    LensCtrl.Instance.FocusMove((ushort)((low + high) / 2));
                    frame?.Dispose();
                    Console.WriteLine($"> 오토포커스 완료: {LensCtrl.Instance.focusCurrentAddr}");
                }
                catch (Exception ex)
                {
                    Common.WriteLog(ex);
                }
            });
        }

        #endregion
    }
}