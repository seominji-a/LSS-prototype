using OpenCvSharp;
using SpinnakerNET;
using SpinnakerNET.GenApi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LSS_prototype.Common_Module
{
    public class CameraService : IDisposable
    {
        // ── Spinnaker 핵심 객체 ──
        private ManagedSystem _managedSystem;
        private ManagedCameraList _managedCameras;
        private List<BackgroundWorker> _workers = new List<BackgroundWorker>();

        // ── 상태 플래그 ──
        private bool _camOpen = false;
        private bool _camConnection = false;
        private bool _disposed = false;

        // ── UI 로 프레임 전달하는 이벤트 ──
        public event Action<WriteableBitmap> FrameArrived;
        public event Action<string> ErrorOccurred;

        public bool IsConnected => _camConnection;
        public bool IsOpen => _camOpen;

        private Thread _testVideoThread;
        private bool _testVideoRunning = false;

        // ────────────────────────────────────────────
        // 생성자 - ManagedSystem 초기화
        // ────────────────────────────────────────────
        public CameraService()
        {
            _managedSystem = new ManagedSystem();
            LibraryVersion ver = _managedSystem.GetLibraryVersion();
            Console.WriteLine($"## Spinnaker Version: {ver.major}.{ver.minor}.{ver.type}.{ver.build}");
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
                    Console.WriteLine($"[Err] BeginAcquisition [{i}]: {se.Message}");
                }
            }
            _camOpen = true;
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
                    Console.WriteLine($"> TestVideo FPS: {fps}");

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

                            // Mat → byte[] → WriteableBitmap → FrameArrived
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
            Console.WriteLine("> 테스트 영상 재생 시작");
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
                Console.WriteLine($"[GetNextImage Error] {se.Message}");
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

                // DataPtr 대신 ManagedData 사용
                byte[] data = image.ManagedData;

                int stride = data.Length / height;

                WriteableBitmap bitmap = null;
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray8, null);
                    bitmap.Lock();
                    Marshal.Copy(data, 0, bitmap.BackBuffer, data.Length);
                    bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                    bitmap.Unlock();
                    bitmap.Freeze();
                });

                return bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ToBitmap Error] {ex.Message}");
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