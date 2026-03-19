using LSS_prototype.Patient_Page;
using LSS_prototype.Scan_Page;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LSS_prototype.VideoComment_Page
{
    public partial class VideoComment : UserControl
    {
        // ═══════════════════════════════════════════
        //  코드비하인드 역할
        //  MediaElement UI 조작만 담당
        //  비즈니스 로직(파일목록, 삭제, 경로, 배속)은 전부 ViewModel
        //
        //  터치 이동:
        //  좌 20% 터치 → 이전 영상 / 우 20% 터치 → 다음 영상
        //  재생 중 / 정지 중 구분 없이 항상 동작
        //  이동 시 배속 초기화
        //
        //  영상 끝 → 무한 반복 재생
        // ═══════════════════════════════════════════

        // ── 재생 상태 (UI 전용) ──
        private bool _isPlaying = false;
        private bool _isDraggingSeek = false;
        private bool _disposed = false;

        // ── 타이머: 경과시간 + Seek 슬라이더 100ms 주기 업데이트 ──
        private readonly DispatcherTimer _timer;

        private VideoCommentViewModel VM => DataContext as VideoCommentViewModel;

        // ═══════════════════════════════════════════
        //  생성자
        // ═══════════════════════════════════════════
        public VideoComment(PatientModel patient, string studyId)
        {
            InitializeComponent();
            DataContext = new VideoCommentViewModel(patient, studyId);

            VM.PropertyChanged += OnViewModelPropertyChanged;
            VM.RequestNavigateToScan += () => MainPage.Instance.NavigateTo(new Scan(patient, studyId)); // 1장있는데, 삭제 후 파일없을때 scan화면으로 이동 

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += Timer_Tick;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        // ═══════════════════════════════════════════
        //  Loaded
        //  1. MediaElement 조작 Action → ViewModel 에 주입
        //  2. VM.Initialize() → 파일 목록 수집 + 첫 파일 재생
        //  3. 터치 이동 이벤트 등록
        // ═══════════════════════════════════════════
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                VM.SetMediaActions(
                    seekBack: () =>
                    {
                        try
                        {
                            if (VideoPlayer.Source == null) return;
                            var target = VideoPlayer.Position - TimeSpan.FromSeconds(1);
                            VideoPlayer.Position = target < TimeSpan.Zero
                                ? TimeSpan.Zero
                                : target;
                        }
                        catch (Exception ex) { Common.WriteLog(ex); }
                    },
                    seekForward: () =>
                    {
                        try
                        {
                            if (VideoPlayer.Source == null) return;
                            if (!VideoPlayer.NaturalDuration.HasTimeSpan) return;
                            var total = VideoPlayer.NaturalDuration.TimeSpan;
                            var target = VideoPlayer.Position + TimeSpan.FromSeconds(1);
                            VideoPlayer.Position = target > total ? total : target;
                        }
                        catch (Exception ex) { Common.WriteLog(ex); }
                    },
                    playPause: () =>
                    {
                        try
                        {
                            if (VideoPlayer.Source == null) return;

                            if (_isPlaying)
                            {
                                VideoPlayer.Pause();
                                _isPlaying = false;
                                VM.PlayPauseIcon = "▶";
                                _timer.Stop();
                            }
                            else
                            {
                                VideoPlayer.Play();
                                VideoPlayer.SpeedRatio = VM.CurrentSpeedRatio; // 배속적용
                                _isPlaying = true;
                                VM.PlayPauseIcon = "⏸";
                                _timer.Start();
                            }
                        }
                        catch (Exception ex) { Common.WriteLog(ex); }
                    }
                );

                VM.Initialize();

                // 터치 이동 이벤트 등록
                // VideoPlayer 가 아닌 VideoPlayer 의 부모 Grid 에 등록
                // → 하단 오버레이 영역 제외하고 영상 영역 전체에서 터치 감지
                VideoPlayer.PreviewMouseDown += OnVideoPreviewMouseDown;
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  Unloaded → 리소스 정리
        // ═══════════════════════════════════════════
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        // ═══════════════════════════════════════════
        //  터치 이동
        //  ImageComment 와 동일한 비율 (좌 20% / 우 20%)
        //  재생 중 / 정지 중 구분 없이 항상 동작
        //  이동 시 배속 초기화
        // ═══════════════════════════════════════════
        private void OnVideoPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (VideoPlayer.ActualWidth <= 0) return;

                double ratio = e.GetPosition(VideoPlayer).X / VideoPlayer.ActualWidth;

                bool goNext = ratio >= 0.80;
                bool goPrev = ratio <= 0.20;

                if (!goNext && !goPrev) return;

                e.Handled = true;

                // 배속 초기화 → 다음/이전 영상은 항상 1x 로 시작
                VM.ResetSpeed();

                if (goNext)
                    VM.MoveNext();
                else
                    VM.MovePrev();
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  ViewModel 프로퍼티 변경 감지
        //
        //  CurrentVideoPath 변경
        //    → null : StopAndReset()
        //    → 경로 : 새 파일 로드 + 자동 재생
        //
        //  CurrentSpeedRatio 변경
        //    → MediaElement.SpeedRatio 적용
        // ═══════════════════════════════════════════
        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                switch (e.PropertyName)
                {
                    case nameof(VM.CurrentVideoPath):
                        OnCurrentVideoPathChanged();
                        break;

                    case nameof(VM.CurrentSpeedRatio):
                        VideoPlayer.SpeedRatio = VM.CurrentSpeedRatio;
                        break;
                }
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        private void OnCurrentVideoPathChanged()
        {
            try
            {
                if (string.IsNullOrEmpty(VM.CurrentVideoPath))
                {
                    StopAndReset();
                    return;
                }

                VideoPlayer.Source = new Uri(VM.CurrentVideoPath);
                VideoPlayer.SpeedRatio = VM.CurrentSpeedRatio;
                VideoPlayer.Play();

                _isPlaying = true;
                VM.PlayPauseIcon = "⏸";
                SliderSeek.Value = 0;
                TxtCurrentTime.Text = "00:00";
                TxtTotalTime.Text = "00:00";

                _timer.Start();
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  MediaElement 이벤트
        // ═══════════════════════════════════════════

        // 영상 열림 → 전체시간 표시 + Seek 최대값 설정
        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!VideoPlayer.NaturalDuration.HasTimeSpan) return;

                var total = VideoPlayer.NaturalDuration.TimeSpan;
                TxtTotalTime.Text = total.ToString(@"mm\:ss");
                SliderSeek.Maximum = total.TotalSeconds;
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // 영상 끝 → 처음으로 돌아가 무한 반복 재생
        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            try
            {
                VideoPlayer.Position = TimeSpan.Zero;
                VideoPlayer.Play();

                SliderSeek.Value = 0;
                TxtCurrentTime.Text = "00:00";
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  타이머 Tick → Seek 슬라이더 + 경과시간 업데이트
        //  매 100ms 호출 → 로그 폭주 방지로 조용히 무시
        // ═══════════════════════════════════════════
        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_isDraggingSeek) return;
                if (VideoPlayer?.Source == null) return;
                if (!VideoPlayer.NaturalDuration.HasTimeSpan) return;

                var pos = VideoPlayer.Position;
                SliderSeek.Value = pos.TotalSeconds;
                TxtCurrentTime.Text = pos.ToString(@"mm\:ss");
            }
            catch { }
        }

        // ═══════════════════════════════════════════
        //  Seek 슬라이더
        //  PreviewMouseDown → 드래그 시작 플래그 ON
        //  PreviewMouseUp   → 이동 + 플래그 OFF (finally 보장)
        //  ValueChanged     → 드래그 중 경과시간 미리보기
        // ═══════════════════════════════════════════
        private void SliderSeek_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSeek = true;
        }


        private void SliderSeek_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                VideoPlayer.Position = TimeSpan.FromSeconds(SliderSeek.Value);
            }
            catch (Exception ex) { Common.WriteLog(ex); }
            finally
            {
                _isDraggingSeek = false;
            }
        }

        private void SliderSeek_ValueChanged(object sender,
            System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDraggingSeek)
                TxtCurrentTime.Text =
                    TimeSpan.FromSeconds(SliderSeek.Value).ToString(@"mm\:ss");
        }

        // ═══════════════════════════════════════════
        //  헬퍼
        // ═══════════════════════════════════════════

        // MediaElement + 타이머 + UI 상태 완전 초기화
        private void StopAndReset()
        {
            try
            {
                _timer.Stop();
                VideoPlayer.Stop();
                VideoPlayer.Close(); // 이 close를 해줘야지만 딜레이없이 삭제 및 복구가 가능함.. 다른 페이지에서도 영상 정지 시 꼭 참조할것 ( 0319 박한용 ) 
                VideoPlayer.Source = null;

                _isPlaying = false;
                _isDraggingSeek = false;
                VM.PlayPauseIcon = "▶";
                SliderSeek.Value = 0;
                TxtCurrentTime.Text = "00:00";
                TxtTotalTime.Text = "00:00";
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }


        // ═══════════════════════════════════════════
        //  Dispose
        //  Unloaded 에서 자동 호출
        // ═══════════════════════════════════════════
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;

                if (VM != null)
                    VM.PropertyChanged -= OnViewModelPropertyChanged;

                VideoPlayer.PreviewMouseDown -= OnVideoPreviewMouseDown;
                VideoPlayer.Stop();
                VideoPlayer.Close();
                VideoPlayer.Source = null;

                VideoPlayer.MediaOpened -= VideoPlayer_MediaOpened;
                VideoPlayer.MediaEnded -= VideoPlayer_MediaEnded;

                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;
            }
            catch (Exception ex) { Common.WriteLog(ex); }
        }
    }
}