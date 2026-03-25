using LSS_prototype.Lens_Module;
using LSS_prototype.User_Page;
using OpenCvSharp;
using SpinnakerNET;
using SpinnakerNET.GenApi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LSS_prototype.Common_Module
{
    public partial class CameraService : IDisposable
    {
        #region 필드 & 이벤트

        private ManagedSystem _managedSystem;
        private ManagedCameraList _managedCameras;
        private List<BackgroundWorker> _workers = new List<BackgroundWorker>();

        private Timer _reconnectTimer;

        private Mat _lastFrame = null;
        private readonly object _frameLock = new object();

        private WriteableBitmap _reusableBitmap = null;
        private readonly object _bitmapLock = new object();

        private byte[] _processedDataBuffer = null;

        private Mat _colorSrc3ch = new Mat();
        private Mat _colorDst = new Mat();
        private Mat _colorNotImg = new Mat();

        private Mat _sharpGray = new Mat();
        private Mat _sharpSobelX = new Mat();
        private Mat _sharpSobelY = new Mat();
        private Mat _sharpSobelX2 = new Mat();
        private Mat _sharpSobelY2 = new Mat();
        private Mat _sharpCombined = new Mat();

        private bool _camOpen = false;
        private bool _camConnection = false;
        private bool _disposed = false;

        private Thread _testVideoThread;
        private bool _testVideoRunning = false;

        public event Action<WriteableBitmap> FrameArrived;
        public event Action<string> ErrorOccurred;
        public event Action<double> SharpnessUpdated;
        public event Action CameraDisconnected;
        public event Action CameraReconnected;

        public string ColorMap { get; set; } = "Origin";
        public bool IsConnected => _camConnection;
        public bool IsOpen => _camOpen;

        #endregion

        #region 생성자 & 렌즈 초기화

        public CameraService()
        {
            _managedSystem = new ManagedSystem();
        }

        // ═══════════════════════════════════════════
        //  InitializeAsync
        //  생성자에서 async 불가 → Loaded 이벤트에서 호출
        //  렌즈 초기화 완료 보장 후 카메라 연결
        // ═══════════════════════════════════════════
        public async Task InitializeAsync()
        {
            await InitLens();
        }

        // ═══════════════════════════════════════════
        //  InitLens
        //  LensCtrl 함수들이 async Task 로 변환됐으므로
        //  전부 await 로 호출 → 순서 보장
        // ═══════════════════════════════════════════
        private async Task InitLens()
        {
            try
            {
                await LensCtrl.Instance.UsbOpen(0);
                await LensCtrl.Instance.UsbSetConfig();
                await LensCtrl.Instance.ZoomParameterReadSet();
                await LensCtrl.Instance.ZoomCurrentAddrReadSet();
                await LensCtrl.Instance.FocusParameterReadSet();
                await LensCtrl.Instance.FocusCurrentAddrReadSet();
                Console.WriteLine($"> 렌즈 초기화 완료: zoom={LensCtrl.Instance.zoomCurrentAddr} min={LensCtrl.Instance.zoomMinAddr} max={LensCtrl.Instance.zoomMaxAddr}");
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                Common.WriteSessionLog($"렌즈 초기화 실패: {ex.Message}");
            }
        }

        #endregion

        #region 카메라 연결 & 라이브뷰

        public bool Connect()
        {
            try
            {
                _managedCameras = _managedSystem.GetCameras();
                Console.WriteLine($"> Camera Count: {_managedCameras.Count}");

                if (_managedCameras.Count == 0)
                {
                    ErrorOccurred?.Invoke("연결된 카메라가 없습니다.");
                    return false;
                }

                foreach (IManagedCamera cam in _managedCameras)
                    cam.Init();

                _camConnection = true;
                Console.WriteLine("> 카메라 Init 완료");
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"카메라 연결 실패: {ex.Message}");
                return false;
            }
        }

        public async Task StartLiveView()
        {
            if (_managedCameras == null) return;

            for (int i = 0; i < _managedCameras.Count; i++)
            {
                try
                {
                    _managedCameras[i].BeginAcquisition();

                    BackgroundWorker worker = new BackgroundWorker();
                    worker.DoWork += Worker_DoWork;
                    worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
                    _workers.Add(worker);
                    worker.RunWorkerAsync(argument: _managedCameras[i]);
                }
                catch (SpinnakerException se)
                {
                    if (se.Message.Contains("Stream has been aborted")) return;
                    await Common.WriteLog(se);
                }
                catch (Exception ex)
                {
                    await Common.WriteLog(ex);
                }
            }
            _camOpen = true;
        }

        public void StopLiveView()
        {
            if (!_camOpen) return;

            _testVideoRunning = false;
            _testVideoThread?.Join(500);

            if (_managedCameras != null)
            {
                for (int i = 0; i < _managedCameras.Count; i++)
                    try { _managedCameras[i].EndAcquisition(); } catch { }
            }

            _camOpen = false;
        }

        public void Disconnect()
        {
            StopLiveView();

            if (_managedCameras != null)
            {
                foreach (IManagedCamera cam in _managedCameras)
                    try { cam.DeInit(); } catch { }

                _managedCameras.Clear();
            }
            _camConnection = false;
        }

        // ═══════════════════════════════════════════
        //  StartReconnectTimer
        //  Timer 콜백은 async void 람다 사용
        //  Timer 가 void 반환을 요구하므로 어쩔 수 없음
        // ═══════════════════════════════════════════
        private void StartReconnectTimer()
        {
            _reconnectTimer?.Dispose();
            _reconnectTimer = new Timer(async _ =>
            {
                if (_disposed) return;
                if (_camConnection || _managedSystem == null) return;
                try
                {
                    var cameras = _managedSystem.GetCameras();
                    if (cameras.Count > 0)
                    {
                        _reconnectTimer?.Dispose();
                        _testVideoRunning = false;
                        _testVideoThread?.Join(500);
                        _managedCameras = cameras;
                        foreach (IManagedCamera cam in _managedCameras)
                            cam.Init();
                        _camConnection = true;
                        _camOpen = true;
                        await StartLiveView();
                        CameraReconnected?.Invoke();
                    }
                }
                catch (Exception ex) { await Common.WriteLog(ex); }
            }, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
        }

        #endregion

        #region 프레임 처리 (Worker & 이미지 변환)

        // ═══════════════════════════════════════════
        //  Worker_DoWork
        //  BackgroundWorker 이벤트 핸들러 → async void 유지
        // ═══════════════════════════════════════════
        private async void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            IManagedCamera cam = (IManagedCamera)e.Argument;

            try
            {
                using (IManagedImage rawImage = cam.GetNextImage(1000))
                {
                    if (rawImage.IsIncomplete) return;

                    WriteableBitmap bitmap = await ToBitmap(rawImage);

                    if (bitmap != null)
                        FrameArrived?.Invoke(bitmap);
                }
            }
            catch (SpinnakerException se)
            {
                if (se.Message.Contains("Stream has been aborted")) return;
                await Common.WriteLog(se);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;
            int idx = _workers.IndexOf(worker);

            if (idx >= 0 && idx < _managedCameras.Count && _managedCameras[idx].IsStreaming())
            {
                worker.RunWorkerAsync(argument: _managedCameras[idx]);
            }
            else
            {
                if (_camOpen && _camConnection)
                {
                    _camOpen = false;
                    _camConnection = false;
                    ErrorOccurred?.Invoke("카메라 연결이 끊어졌습니다.\n 5초 마다 재연결을 시도합니다.");
                    CameraDisconnected?.Invoke();
                    StartReconnectTimer();
                }
                _workers.Remove(worker);
                worker.Dispose();
                Console.WriteLine($"> Camera [{idx}] Worker 종료");
            }
        }

        private async Task<WriteableBitmap> ToBitmap(IManagedImage image)
        {
            try
            {
                int width = (int)image.Width;
                int height = (int)image.Height;
                byte[] data = image.ManagedData;

                byte[] processedData;
                int stride;
                bool isColor;
                int needed = 0;

                using (Mat src = Mat.FromPixelData(height, width, MatType.CV_8UC1, data))
                {
                    Mat processed = await ApplyColorMap(src);

                    using (Mat safeProcessed = processed.Clone())
                    {
                        lock (_frameLock)
                        {
                            _lastFrame?.Dispose();
                            _lastFrame = safeProcessed.Clone();
                        }

                        stride = (int)safeProcessed.Step();
                        isColor = safeProcessed.Channels() == 3;
                        needed = height * stride;

                        if (_processedDataBuffer == null || _processedDataBuffer.Length < needed)
                            _processedDataBuffer = new byte[needed];

                        processedData = _processedDataBuffer;
                        Marshal.Copy(safeProcessed.Data, processedData, 0, needed);

                        // ★ CalcSharpness 가 async Task<double> 로 변환됐으므로 await 추가
                        double sharpness = await CalcSharpness(src);
                        SharpnessUpdated?.Invoke(sharpness);
                    }
                }

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var format = isColor ? PixelFormats.Bgr24 : PixelFormats.Gray8;

                    lock (_bitmapLock)
                    {
                        if (_reusableBitmap == null
                            || _reusableBitmap.PixelWidth != width
                            || _reusableBitmap.PixelHeight != height
                            || _reusableBitmap.Format != format)
                        {
                            _reusableBitmap = new WriteableBitmap(width, height, 96, 96, format, null);
                        }

                        _reusableBitmap.Lock();
                        Marshal.Copy(processedData, 0, _reusableBitmap.BackBuffer, needed);
                        _reusableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                        _reusableBitmap.Unlock();
                    }
                });

                return _reusableBitmap;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return null;
            }
        }

        public Mat GetCurrentFrame()
        {
            lock (_frameLock)
            {
                return _lastFrame?.Clone();
            }
        }

        private async Task<Mat> ApplyColorMap(Mat src)
        {
            try
            {
                Cv2.CvtColor(src, _colorSrc3ch, ColorConversionCodes.GRAY2BGR);

                if (ColorMap == "Rainbow")
                {
                    Cv2.BitwiseNot(_colorSrc3ch, _colorNotImg);
                    Cv2.ApplyColorMap(_colorNotImg, _colorDst, ColormapTypes.Rainbow);
                }
                else if (ColorMap == "Invert")
                {
                    Cv2.BitwiseNot(_colorSrc3ch, _colorDst);
                }
                else
                {
                    _colorSrc3ch.CopyTo(_colorDst);
                }

                return _colorDst;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return src;
            }
        }

        #endregion

        #region 선명도 계산 및 프레임 계산

        // ★ async Task<double> 로 변환
        // await Common.WriteLog(ex) 사용을 위해 필요
        public async Task<double> CalcSharpness(Mat src)
        {
            try
            {
                int roiW = src.Width / 4;
                int roiH = src.Height / 4;
                int roiX = src.Width / 2 - roiW / 2;
                int roiY = src.Height / 2 - roiH / 2;

                using (Mat roi = new Mat(src, new OpenCvSharp.Rect(roiX, roiY, roiW, roiH)))
                {
                    if (roi.Channels() == 3)
                        Cv2.CvtColor(roi, _sharpGray, ColorConversionCodes.BGR2GRAY);
                    else
                        roi.CopyTo(_sharpGray);

                    Cv2.Sobel(_sharpGray, _sharpSobelX, MatType.CV_64F, 1, 0);
                    Cv2.Sobel(_sharpGray, _sharpSobelY, MatType.CV_64F, 0, 1);
                    Cv2.Multiply(_sharpSobelX, _sharpSobelX, _sharpSobelX2);
                    Cv2.Multiply(_sharpSobelY, _sharpSobelY, _sharpSobelY2);
                    Cv2.Add(_sharpSobelX2, _sharpSobelY2, _sharpCombined);

                    return Cv2.Sum(_sharpCombined).Val0;
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return 0;
            }
        }

        // ── GetCurrentFps ──
        // try-catch 에 await 없으므로 async 불필요 → 동기 유지
        public double GetCurrentFps()
        {
            try
            {
                if (_managedCameras == null || _managedCameras.Count == 0)
                    return 30.0;

                return _managedCameras[0].AcquisitionResultingFrameRate.Value;
            }
            catch
            {
                return 30.0;
            }
        }

        #endregion

        #region 테스트 영상

        public void StartTestVideo(string videoPath)
        {
            if (!File.Exists(videoPath))
            {
                ErrorOccurred?.Invoke($"테스트 영상 파일이 없습니다: {videoPath}");
                return;
            }

            _testVideoRunning = true;
            _camOpen = true;

            _testVideoThread = new Thread(() =>
            {
                using (VideoCapture cap = new VideoCapture(videoPath))
                {
                    if (!cap.IsOpened())
                    {
                        ErrorOccurred?.Invoke("테스트 영상 파일을 열 수 없습니다.");
                        return;
                    }

                    double fps = cap.Get(VideoCaptureProperties.Fps);
                    if (fps <= 0) fps = 30;
                    int delay = (int)(1000.0 / fps);

                    using (Mat frame = new Mat())
                    {
                        while (_testVideoRunning)
                        {
                            if (!cap.Read(frame) || frame.Empty())
                            {
                                cap.Set(VideoCaptureProperties.PosFrames, 0);
                                continue;
                            }

                            int width = frame.Width;
                            int height = frame.Rows;
                            int stride = width * 3;
                            int needed = height * stride;

                            if (_processedDataBuffer == null || _processedDataBuffer.Length < needed)
                                _processedDataBuffer = new byte[needed];
                            Marshal.Copy(frame.Data, _processedDataBuffer, 0, needed);

                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                lock (_bitmapLock)
                                {
                                    if (_reusableBitmap == null
                                        || _reusableBitmap.PixelWidth != width
                                        || _reusableBitmap.PixelHeight != height)
                                    {
                                        _reusableBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr24, null);
                                    }

                                    _reusableBitmap.Lock();
                                    Marshal.Copy(_processedDataBuffer, 0, _reusableBitmap.BackBuffer, needed);
                                    _reusableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                                    _reusableBitmap.Unlock();
                                }
                            });

                            if (_reusableBitmap != null)
                                lock (_frameLock)
                                {
                                    _lastFrame?.Dispose();
                                    _lastFrame = frame.Clone();
                                }

                            FrameArrived?.Invoke(_reusableBitmap);
                            Thread.Sleep(delay);
                        }
                    }
                }
            });

            _testVideoThread.IsBackground = true;
            _testVideoThread.Start();
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _reconnectTimer?.Dispose();
            _reconnectTimer = null;

            Disconnect();
            lock (_frameLock) { _lastFrame?.Dispose(); _lastFrame = null; }
            lock (_bitmapLock) { _reusableBitmap = null; }

            _colorSrc3ch?.Dispose();
            _colorDst?.Dispose();
            _colorNotImg?.Dispose();

            _sharpGray?.Dispose();
            _sharpSobelX?.Dispose();
            _sharpSobelY?.Dispose();
            _sharpSobelX2?.Dispose();
            _sharpSobelY2?.Dispose();
            _sharpCombined?.Dispose();

            _managedSystem?.Dispose();
            _managedSystem = null;
        }

        #endregion
    }
}
