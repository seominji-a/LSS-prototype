using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
    }
}