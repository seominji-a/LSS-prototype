using LSS_prototype.Lens_Module;
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
    public class CameraService : IDisposable
    {
        // ── Spinnaker 핵심 객체 ( 버전이 안맞으면 catch 로 안빠져서 그냥 뻗어버림 주의 ( C++ DLL LOAD 단에서 에러나는거라 CATCH 어려움 ) ──
        private ManagedSystem _managedSystem;
        private ManagedCameraList _managedCameras;
        private List<BackgroundWorker> _workers = new List<BackgroundWorker>();

        public event Action<double> SharpnessUpdated; // 선명도 출력 이벤트 

        private Mat _lastFrame = null;

        // ── 상태 플래그 ──
        private bool _camOpen = false;
        private bool _camConnection = false;
        private bool _disposed = false;

        // ── UI 로 프레임 전달하는 이벤트 ──
        public event Action<WriteableBitmap> FrameArrived;
        public event Action<string> ErrorOccurred;



        public string ColorMap { get; set; } = "Origin";

        public bool IsConnected => _camConnection;
        public bool IsOpen => _camOpen;

        private Thread _testVideoThread;
        private bool _testVideoRunning = false;

        // ── 카메라 zoom In/Out 관련 변수  ──

        private const int _zoomStep = 300; // 한번 누를 때 증가 감소 범위 


        // ────────────────────────────────────────────
        // 생성자 - ManagedSystem 초기화
        // ────────────────────────────────────────────
        public CameraService()
        {
            _managedSystem = new ManagedSystem();
            InitLens();
        }

        // ────────────────────────────────────────────
        // 렌즈 초기화 - 현재 줌 위치 및 파라미터 읽기
        // ────────────────────────────────────────────
        private void InitLens()
        {
            try
            {
                LensCtrl.Instance.UsbOpen(0);           // USB 연결
                LensCtrl.Instance.UsbSetConfig();       // USB 설정
                LensCtrl.Instance.ZoomParameterReadSet(); // zoomMin, zoomMax, zoomSpeed 읽기
                LensCtrl.Instance.ZoomCurrentAddrReadSet(); // 현재 줌 위치 읽기
                LensCtrl.Instance.FocusParameterReadSet(); // 포커스 읽기
                LensCtrl.Instance.FocusCurrentAddrReadSet();
                Console.WriteLine($"> 렌즈 초기화 완료: zoom={LensCtrl.Instance.zoomCurrentAddr} min={LensCtrl.Instance.zoomMinAddr} max={LensCtrl.Instance.zoomMaxAddr}");
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                Common.WriteSessionLog($"렌즈 초기화 실패: {ex.Message}");
            }
        }


        public void ZoomIn()
        {
            try
            {
                // 현재 줌 위치에서 _zoomStep 만큼 증가
                ushort nextZoom = (ushort)(LensCtrl.Instance.zoomCurrentAddr + _zoomStep);

                // 최대값 초과하면 최대값으로 고정
                if (nextZoom > LensCtrl.Instance.zoomMaxAddr)
                    nextZoom = LensCtrl.Instance.zoomMaxAddr;

                LensCtrl.Instance.ZoomMove(nextZoom);
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
                ushort nextZoom = (ushort)(LensCtrl.Instance.zoomCurrentAddr - _zoomStep);

                // 최소값 미만이면 최소값으로 고정 ( 별도의 소리? 뭐 안넣어도되는지 고려하기 )
                if (nextZoom < LensCtrl.Instance.zoomMinAddr)
                    nextZoom = LensCtrl.Instance.zoomMinAddr;

                LensCtrl.Instance.ZoomMove(nextZoom);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        public void FocusIn()
        {
            try
            {
                if (LensCtrl.Instance.focusMaxAddr == 0)
                {
                    LensCtrl.Instance.FocusParameterReadSet();
                    LensCtrl.Instance.FocusCurrentAddrReadSet();
                }

                ushort nextFocus = (ushort)(LensCtrl.Instance.focusCurrentAddr + _zoomStep);

                if (nextFocus > LensCtrl.Instance.focusMaxAddr)
                    nextFocus = LensCtrl.Instance.focusMaxAddr;

                LensCtrl.Instance.FocusMove(nextFocus);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        public void FocusOut()
        {
            try
            {
                ushort nextFocus = (ushort)(LensCtrl.Instance.focusCurrentAddr - _zoomStep);

                if (nextFocus < LensCtrl.Instance.focusMinAddr)
                    nextFocus = LensCtrl.Instance.focusMinAddr;

                LensCtrl.Instance.FocusMove(nextFocus);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }


        private Mat ApplyColorMap(Mat src)
        {
            try
            {
                Mat src3ch = new Mat();
                Cv2.CvtColor(src, src3ch, ColorConversionCodes.GRAY2BGR);

                Mat dst = new Mat();

                if (ColorMap == "Origin")
                {
                    src3ch.CopyTo(dst);
                }
                else if (ColorMap == "Rainbow")
                {
                    Mat notImg = new Mat();
                    Cv2.BitwiseNot(src3ch, notImg);
                    Cv2.ApplyColorMap(notImg, dst, ColormapTypes.Rainbow);
                    notImg.Dispose();
                }
                else if (ColorMap == "Invert")
                {
                    Cv2.BitwiseNot(src3ch, dst);
                }

                src3ch.Dispose();
                return dst;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return src;
            }
        }



        // ────────────────────────────────────────────
        // 카메라 연결 + Init
        // ────────────────────────────────────────────
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

        // ────────────────────────────────────────────
        // 라이브뷰 시작
        // BeginAcquisition 먼저 → Worker 시작 순서 중요!
        // ────────────────────────────────────────────
        public void StartLiveView()
        {
            if (_managedCameras == null) return;

            for (int i = 0; i < _managedCameras.Count; i++)
            {
                try
                {
                    // 1. 카메라 스트리밍 시작
                    _managedCameras[i].BeginAcquisition();

                    // 2. 프레임 읽기 Worker 시작
                    BackgroundWorker worker = new BackgroundWorker();
                    worker.DoWork += Worker_DoWork;
                    worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
                    _workers.Add(worker);
                    worker.RunWorkerAsync(argument: _managedCameras[i]);
                }
                catch (SpinnakerException se)
                {
                    if (se.Message.Contains("Stream has been aborted"))
                        return;
                    Common.WriteLog(se);
                }
                catch (Exception ex)
                {
                    Common.WriteLog(ex);
                }
            }
            _camOpen = true;
        }

        /// <summary>
        /// 선명도 계산 함수 
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public double CalcSharpness(Mat src)
        {
            try
            {
                int roiW = src.Width / 4;
                int roiH = src.Height / 4;
                int roiX = src.Width / 2 - roiW / 2;
                int roiY = src.Height / 2 - roiH / 2;

                using (Mat roi = new Mat(src, new OpenCvSharp.Rect(roiX, roiY, roiW, roiH)))
                using (Mat gray = new Mat())
                using (Mat sobelX = new Mat())
                using (Mat sobelY = new Mat())
                {
                    if (roi.Channels() == 3)
                        Cv2.CvtColor(roi, gray, ColorConversionCodes.BGR2GRAY);
                    else
                        roi.CopyTo(gray);

                    // Tenengrad - Sobel X, Y 계산
                    Cv2.Sobel(gray, sobelX, MatType.CV_64F, 1, 0);
                    Cv2.Sobel(gray, sobelY, MatType.CV_64F, 0, 1);

                    // 제곱합 계산
                    Mat sobelX2 = sobelX.Mul(sobelX);
                    Mat sobelY2 = sobelY.Mul(sobelY);
                    Scalar sumVal = Cv2.Sum(sobelX2 + sobelY2);

                    sobelX2.Dispose();
                    sobelY2.Dispose();

                    return sumVal.Val0;
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return 0;
            }
        }

        public async Task AutoFocus()
        {
            await Task.Run(() =>
            {
                try
                {
                    if (_lastFrame == null) return;

                    int low = LensCtrl.Instance.focusMinAddr;
                    int high = LensCtrl.Instance.focusMaxAddr;
                    double golden = 0.618;

                    while (high - low > 50)
                    {
                        int mid1 = (int)(low + (high - low) * (1 - golden));
                        int mid2 = (int)(low + (high - low) * golden);

                        LensCtrl.Instance.FocusMove((ushort)mid1);
                        Thread.Sleep(200); 
                        double sharp1 = CalcSharpness(_lastFrame);

                        LensCtrl.Instance.FocusMove((ushort)mid2);
                        Thread.Sleep(200); // 새 프레임 도착 대기
                        double sharp2 = CalcSharpness(_lastFrame);

                        if (sharp1 > sharp2) high = mid2;
                        else low = mid1;

                        Console.WriteLine($"> mid1:{mid1} s1:{sharp1:F0} mid2:{mid2} s2:{sharp2:F0}");
                    }

                    LensCtrl.Instance.FocusMove((ushort)((low + high) / 2));
                    Console.WriteLine($"> 오토포커스 완료: {LensCtrl.Instance.focusCurrentAddr}");
                }
                catch (Exception ex)
                {
                    Common.WriteLog(ex);
                }
            });
        }

        /// <summary>
        /// 테스트 비디오 실행 함수
        /// </summary>
        /// <param name="videoPath"></param>
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

                    // 원본 FPS 유지 (없으면 30 기본값)
                    double fps = cap.Get(VideoCaptureProperties.Fps);
                    if (fps <= 0) fps = 30;
                    int delay = (int)(1000.0 / fps);

                    using (Mat frame = new Mat())
                    {
                        while (_testVideoRunning)
                        {
                            if (!cap.Read(frame) || frame.Empty())
                            {
                                // 영상 끝나면 처음부터 반복
                                cap.Set(VideoCaptureProperties.PosFrames, 0);
                                continue;
                            }

                            int width = frame.Width;
                            int height = frame.Rows;
                            int stride = width * 3; // BGR = 3 bytes
                            byte[] data = new byte[height * stride];
                            Marshal.Copy(frame.Data, data, 0, data.Length);

                            WriteableBitmap bitmap = null;
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr24, null);
                                bitmap.Lock();
                                Marshal.Copy(data, 0, bitmap.BackBuffer, data.Length);
                                bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                                bitmap.Unlock();
                                bitmap.Freeze();
                            });

                            if (bitmap != null)
                                FrameArrived?.Invoke(bitmap);

                            Thread.Sleep(delay);
                        }
                    }
                }
            });

            _testVideoThread.IsBackground = true;
            _testVideoThread.Start();
        }


        public void StopLiveView()
        {
            if (!_camOpen) return;

            // 테스트 영상 정지
            _testVideoRunning = false;
            _testVideoThread?.Join(500);

            // 카메라 정지
            if (_managedCameras != null)
            {
                for (int i = 0; i < _managedCameras.Count; i++)
                    try { _managedCameras[i].EndAcquisition(); } catch { }
            }

            _camOpen = false;
        }


        // ────────────────────────────────────────────
        // Worker - 프레임 1장 캡처
        // GetNextImage → BGR8 변환 → WriteableBitmap
        // ────────────────────────────────────────────
        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            IManagedCamera cam = (IManagedCamera)e.Argument;

            try
            {
                // 1. 카메라에서 이미지 1장 가져오기
                using (IManagedImage rawImage = cam.GetNextImage(1000))
                {
                    if (rawImage.IsIncomplete) return;

                    // 2. Convert 없이 rawImage 직접 전달
                    WriteableBitmap bitmap = ToBitmap(rawImage);
   
                    if (bitmap != null)
                        FrameArrived?.Invoke(bitmap);
                }
            }
            catch (SpinnakerException se)
            {
                if (se.Message.Contains("Stream has been aborted"))
                    return;
                Common.WriteLog(se);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        // ────────────────────────────────────────────
        // Worker 완료 → 카메라 스트리밍 중이면 다음 프레임 요청
        // ────────────────────────────────────────────
        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;
            int idx = _workers.IndexOf(worker);

            if (idx >= 0 && idx < _managedCameras.Count && _managedCameras[idx].IsStreaming())
            {
                // 스트리밍 중이면 계속 다음 프레임 요청
                worker.RunWorkerAsync(argument: _managedCameras[idx]);
            }
            else
            {
                // 스트리밍 종료
                _workers.Remove(worker);
                worker.Dispose();
                Console.WriteLine($"> Camera [{idx}] Worker 종료");
            }
        }

        // ────────────────────────────────────────────
        // IManagedImage (BGR8) → WriteableBitmap
        // 필터 없음, 날것 원본 그대로 변환
        // ────────────────────────────────────────────
        private WriteableBitmap ToBitmap(IManagedImage image)
        {
            try
            {
                int width = (int)image.Width;
                int height = (int)image.Height;
                byte[] data = image.ManagedData;

                // Mat 처리는 using 블록 안에서 완전히 끝내고
                // byte[] 로 복사해서 using 블록 밖으로 꺼냄
                byte[] processedData;
                int stride;
                bool isColor;

                using (Mat src = new Mat(height, width, MatType.CV_8UC1, data))
                using (Mat processed = ApplyColorMap(src))
                {
                    _lastFrame = src.Clone(); 
                    stride = (int)processed.Step();
                    isColor = processed.Channels() == 3;
                    processedData = new byte[height * stride];
                    Marshal.Copy(processed.Data, processedData, 0, processedData.Length);
                    double sharpness = CalcSharpness(src);
                    SharpnessUpdated?.Invoke(sharpness);
                } 

                WriteableBitmap bitmap = null;
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var format = isColor ? PixelFormats.Bgr24 : PixelFormats.Gray8;
                    bitmap = new WriteableBitmap(width, height, 96, 96, format, null);
                    bitmap.Lock();
                    Marshal.Copy(processedData, 0, bitmap.BackBuffer, processedData.Length);
                    bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                    bitmap.Unlock();
                    bitmap.Freeze();
                });

                return bitmap;
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return null;
            }
        }

        // ────────────────────────────────────────────
        // 연결 해제
        // ────────────────────────────────────────────
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

        // ────────────────────────────────────────────
        // Dispose
        // ────────────────────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Disconnect();
            _managedSystem?.Dispose();
        }
    }
}