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

        private double _exposureValue = 100000;
        private const double _exposureStep = 100000;
        private const double _exposureMin = 100000;
        private const double _exposureMax = 1000000;

        private double _gainValue = 0.0;
        private const double _gainStep = 3.0;
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

        private const int _zoomStep = 300;
        private const int _focusStep = 300;

        #endregion

        #region 카메라 설정 초기화 (DB값 적용)

        // ★ LensCtrl 함수들이 async Task 로 바뀌었으므로 async Task 로 변환
        public async Task InitializeCameraSettings(DefaultModel data)
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

                // ── 렌즈 파라미터 읽기 (async Task 로 변환됐으므로 await) ──
                await LensCtrl.Instance.ZoomParameterReadSet();
                await LensCtrl.Instance.FocusParameterReadSet();
                await LensCtrl.Instance.IrisParameterReadSet();
                await LensCtrl.Instance.OptFilterParameterReadSet();

                // ── 렌즈 설정 ──
                await LensCtrl.Instance.ZoomMove((ushort)data.Zoom);
                await LensCtrl.Instance.FocusMove((ushort)data.Focus);
                await LensCtrl.Instance.IrisMove((ushort)data.Iris);
                await LensCtrl.Instance.OptFilterMove(data.Filter == 0 ? (ushort)0 : (ushort)1);
                _irisValue = data.Iris;

                Console.WriteLine($"> 렌즈 초기 설정 완료: zoom={data.Zoom} focus={data.Focus} iris={data.Iris} filter={data.Filter}");
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        #endregion

        #region Exposure 제어

        public async Task ExposureInc()
        {
            try
            {
                double next = _exposureValue + _exposureStep;
                if (next > _exposureMax) next = _exposureMax;
                ApplyExposure(next);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        public async Task ExposureDec()
        {
            try
            {
                double next = _exposureValue - _exposureStep;
                if (next < _exposureMin) next = _exposureMin;
                ApplyExposure(next);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ── ApplyExposure ──
        // await 없음 → 동기 유지
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

        public async Task<double> ExposureCurrentRead()
        {
            try
            {
                if (_managedCameras == null || _managedCameras.Count == 0) return _exposureValue;
                INodeMap nodeMap = _managedCameras[0].GetNodeMap();
                IFloat iExposure = nodeMap.GetNode<IFloat>("ExposureTime");
                _exposureValue = iExposure.Value;
                return _exposureValue;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return _exposureValue; }
        }

        #endregion

        #region Gain 제어

        public async Task GainInc()
        {
            try
            {
                double next = _gainValue + _gainStep;
                if (next > _gainMax) next = _gainMax;
                ApplyGain(next);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        public async Task GainDec()
        {
            try
            {
                double next = _gainValue - _gainStep;
                if (next < _gainMin) next = _gainMin;
                ApplyGain(next);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ── ApplyGain ──
        // await 없음 → 동기 유지
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

        public async Task<double> GainCurrentRead()
        {
            try
            {
                if (_managedCameras == null || _managedCameras.Count == 0) return _gainValue;
                INodeMap nodeMap = _managedCameras[0].GetNodeMap();
                IFloat iGain = nodeMap.GetNode<IFloat>("Gain");
                _gainValue = iGain.Value;
                return _gainValue;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return _gainValue;
            }
        }

        #endregion

        #region Gamma 제어

        public async Task GammaInc()
        {
            try
            {
                double next = Math.Round(_gammaValue + _gammaStep, 2);
                if (next > _gammaMax) next = _gammaMax;
                ApplyGamma(next);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        public async Task GammaDec()
        {
            try
            {
                double next = Math.Round(_gammaValue - _gammaStep, 2);
                if (next < _gammaMin) next = _gammaMin;
                ApplyGamma(next);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ── ApplyGamma ──
        // await 없음 → 동기 유지
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

        public async Task<double> GammaCurrentRead()
        {
            try
            {
                if (_managedCameras == null || _managedCameras.Count == 0) return _gammaValue;
                INodeMap nodeMap = _managedCameras[0].GetNodeMap();
                IFloat iGamma = nodeMap.GetNode<IFloat>("Gamma");
                _gammaValue = iGamma.Value;
                return _gammaValue;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return _gammaValue; }
        }

        #endregion

        #region Iris 제어 (LensCtrl)

        public async Task IrisInc()
        {
            try
            {
                double next = _irisValue + _irisStep;
                if (next > _irisMax) next = _irisMax;
                await ApplyIris(next);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        public async Task IrisDec()
        {
            try
            {
                double next = _irisValue - _irisStep;
                if (next < _irisMin) next = _irisMin;
                await ApplyIris(next);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ★ LensCtrl.Instance.IrisMove 가 async Task 로 바뀌었으므로 await
        private async Task ApplyIris(double value)
        {
            await LensCtrl.Instance.IrisMove((ushort)value);
            _irisValue = value;
            Console.WriteLine($"> Iris 변경: {_irisValue}");
        }

        public async Task<double> IrisCurrentRead()
        {
            try
            {
                _irisValue = LensCtrl.Instance.irisCurrentAddr;
                return _irisValue;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return _irisValue; }
        }

        #endregion

        #region Zoom 제어 (LensCtrl)

        public async Task ZoomIn()
        {
            try
            {
                int nextZoom = LensCtrl.Instance.zoomCurrentAddr + _zoomStep;
                if (nextZoom > LensCtrl.Instance.zoomMaxAddr)
                    nextZoom = LensCtrl.Instance.zoomMaxAddr;

                // ★ ZoomMove 가 async Task 로 바뀌었으므로 await
                await LensCtrl.Instance.ZoomMove((ushort)nextZoom);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        public async Task ZoomOut()
        {
            try
            {
                int nextZoom = LensCtrl.Instance.zoomCurrentAddr - _zoomStep;
                if (nextZoom < LensCtrl.Instance.zoomMinAddr)
                    nextZoom = LensCtrl.Instance.zoomMinAddr;

                await LensCtrl.Instance.ZoomMove((ushort)nextZoom);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        #endregion

        #region Focus 제어 (LensCtrl)

        public async Task FocusIn()
        {
            try
            {
                if (LensCtrl.Instance.focusMaxAddr == 0)
                {
                    await LensCtrl.Instance.FocusParameterReadSet();
                    await LensCtrl.Instance.FocusCurrentAddrReadSet();
                }

                int nextFocus = LensCtrl.Instance.focusCurrentAddr + _focusStep;
                if (nextFocus > LensCtrl.Instance.focusMaxAddr)
                    nextFocus = LensCtrl.Instance.focusMaxAddr;

                await LensCtrl.Instance.FocusMove((ushort)nextFocus);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        public async Task FocusOut()
        {
            try
            {
                if (LensCtrl.Instance.focusMaxAddr == 0)
                {
                    await LensCtrl.Instance.FocusParameterReadSet();
                    await LensCtrl.Instance.FocusCurrentAddrReadSet();
                }

                int nextFocus = LensCtrl.Instance.focusCurrentAddr - _focusStep;
                if (nextFocus < LensCtrl.Instance.focusMinAddr)
                    nextFocus = LensCtrl.Instance.focusMinAddr;

                await LensCtrl.Instance.FocusMove((ushort)nextFocus);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        public async Task AutoFocus()
        {
            await Task.Run(async () =>  // ★ async 람다로 변환 → await 사용 가능
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

                        // ★ FocusMove 가 async Task 로 바뀌었으므로 await
                        await LensCtrl.Instance.FocusMove((ushort)mid1);
                        Thread.Sleep(200);

                        lock (_frameLock) { frame?.Dispose(); frame = _lastFrame?.Clone(); }
                        // ★ CalcSharpness 가 async Task<double> 로 바뀌었으므로 await
                        double sharp1 = await CalcSharpness(frame);

                        await LensCtrl.Instance.FocusMove((ushort)mid2);
                        Thread.Sleep(200);

                        lock (_frameLock) { frame?.Dispose(); frame = _lastFrame?.Clone(); }
                        double sharp2 = await CalcSharpness(frame);

                        if (sharp1 > sharp2) high = mid2;
                        else low = mid1;

                        Console.WriteLine($"> mid1:{mid1} s1:{sharp1:F0} mid2:{mid2} s2:{sharp2:F0}");
                    }

                    await LensCtrl.Instance.FocusMove((ushort)((low + high) / 2));
                    frame?.Dispose();
                    Console.WriteLine($"> 오토포커스 완료: {LensCtrl.Instance.focusCurrentAddr}");
                }
                catch (Exception ex)
                {
                    await Common.WriteLog(ex);
                }
            });
        }

        #endregion
    }
}
