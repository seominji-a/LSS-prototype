using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace LSS_prototype
{
    public partial class CustomMessageWindow : Window
    {
        public enum MessageBoxType
        {
            Ok,
            YesNo,
            AutoClose
        }

        public enum MessageBoxResult
        {
            None,
            Ok,
            Yes,
            No,
            Timeout
        }
        private List<Window> _blurredWindows = new List<Window>();  //  블러 적용된 창 목록 ( 메시지창을 제외한 모든 창 블러를 주기 위해 선언 )
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private TaskCompletionSource<MessageBoxResult> _tcs;
        private DispatcherTimer _timeoutTimer;

        public CustomMessageWindow(string message, MessageBoxType type = MessageBoxType.Ok, int autoCloseSeconds = 0)
        {
            InitializeComponent();

            // NOTE:
            // CustomMessageWindow는 앱 초기화(DB Init / Version Check) 단계에서도 호출됨.
            // 이 시점에서는 MainWindow가 아직 표시되지 않았을 수 있음.
            // WPF는 "표시된 적 없는 Window"를 Owner로 지정하면 예외 발생함.
            //
            // 그래서:
            // 1. Active Window → Owner
            // 2. Visible Window → Owner
            // 3. 없으면 Owner 미설정 + CenterScreen
            //
            // 이 로직 제거하면 Init 단계에서 팝업 호출 시 에러 발생 가능. ( DB 초기화 시 에러 발생하여 추가하였음 ) 

            var owner = Application.Current?.Windows?
                .OfType<Window>()
                .FirstOrDefault(w => w.IsActive)
                ?? Application.Current?.Windows?
                .OfType<Window>()
                .FirstOrDefault(w => w.IsVisible);

            if (owner != null && owner.IsVisible)
            {
                this.Owner = owner;
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {  
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            MessageText.Text = message;

            if ( this.Owner != null)
            {

                // 부모 창에 블러 효과
                if (this.Owner != null)
                {
                    ApplyBlurToOwner();
                }
            }
            // 기본적으로 타이머 숨김
            CountdownText.Visibility = Visibility.Collapsed;

            switch (type)
            {
                case MessageBoxType.Ok:
                    OkButton.Visibility = Visibility.Visible;
                    YesButton.Visibility = Visibility.Collapsed;
                    NoButton.Visibility = Visibility.Collapsed;
                    break;

                case MessageBoxType.YesNo:
                    OkButton.Visibility = Visibility.Collapsed;
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;

                    if (autoCloseSeconds > 0)
                    {
                        StartTimeout(autoCloseSeconds);
                    }
                    break;

                case MessageBoxType.AutoClose:
                    OkButton.Visibility = Visibility.Collapsed;
                    YesButton.Visibility = Visibility.Collapsed;
                    NoButton.Visibility = Visibility.Collapsed;

                    if (autoCloseSeconds > 0)
                    {
                        var timer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(autoCloseSeconds)
                        };
                        timer.Tick += (s, e) =>
                        {
                            timer.Stop();
                            CloseWithResult(MessageBoxResult.Ok);
                        };
                        timer.Start();
                    }
                    break;
            }

            // ★  닫힐 때 블러 제거
            this.Closed += (s, e) =>
            {
                if (this.Owner != null)
                {
                    RemoveBlurFromOwner();
                }
            };
        }

        private void ApplyBlurToOwner()
        {
            var blurEffect = new BlurEffect
            {
                Radius = 15
            };

            foreach (Window window in Application.Current.Windows)
            {
                if (window != this && window.IsVisible)
                {
                    window.Effect = blurEffect;
                    _blurredWindows.Add(window);
                }
            }
        }

        // 블러 효과 제거
        private void RemoveBlurFromOwner()
        {
            foreach (var window in _blurredWindows)
            {
                if (window != null)
                {
                    window.Effect = null;
                }
            }
            _blurredWindows.Clear();
        }


        private void StartTimeout(int seconds)
        {
            int remainingSeconds = seconds;

            CountdownText.Text = $"{remainingSeconds}초 남음";
            CountdownText.Visibility = Visibility.Visible;

            _timeoutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _timeoutTimer.Tick += (s, e) =>
            {
                remainingSeconds--;

                if (remainingSeconds > 0)
                {
                    CountdownText.Text = $"{remainingSeconds}초 남음";
                }
                else
                {
                    _timeoutTimer.Stop();
                    CloseWithResult(MessageBoxResult.Timeout);
                }
            };

            _timeoutTimer.Start();
        }

        public Task<MessageBoxResult> ShowAsync()
        {
            _tcs = new TaskCompletionSource<MessageBoxResult>();
            this.Show();
            return _tcs.Task;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWithResult(MessageBoxResult.Ok);
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWithResult(MessageBoxResult.Yes);
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWithResult(MessageBoxResult.No);
        }

        private void CloseWithResult(MessageBoxResult result)
        {
            _timeoutTimer?.Stop();

            Result = result;
            _tcs?.TrySetResult(result);
            this.Close();
        }

        private void Overlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.Source is Grid)
            {
                System.Diagnostics.Debug.WriteLine("[CustomMessageWindow] 배경 클릭 - 세션 연장됨");
            }
        }

        public static MessageBoxResult Show(string message,MessageBoxType type = MessageBoxType.Ok, int autoCloseSeconds = 0, bool enableBlur = false)
        {
            var win = new CustomMessageWindow(message, type, autoCloseSeconds);
            win.ShowDialog();
            return win.Result;
        }
    }


}