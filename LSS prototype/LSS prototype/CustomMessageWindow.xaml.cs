using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LSS_prototype
{
    public partial class CustomMessageWindow : Window
    {
        public enum MessageBoxType
        {
            Ok,           // 확인만
            YesNo,        // 예/아니오
            AutoClose     // 자동 닫기
        }

        public enum MessageBoxResult
        {
            None,
            Ok,
            Yes,
            No
        }

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public CustomMessageWindow(string message, MessageBoxType type = MessageBoxType.Ok, int autoCloseSeconds = 0)
        {
            InitializeComponent();

            MessageText.Text = message;

            // 버튼 타입에 따라 표시
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
                    break;

                case MessageBoxType.AutoClose:
                    OkButton.Visibility = Visibility.Collapsed;
                    YesButton.Visibility = Visibility.Collapsed;
                    NoButton.Visibility = Visibility.Collapsed;

                    // 자동 닫기 타이머
                    if (autoCloseSeconds > 0)
                    {
                        var timer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(autoCloseSeconds)
                        };
                        timer.Tick += (s, e) =>
                        {
                            timer.Stop();
                            Result = MessageBoxResult.Ok;
                            this.Close();
                        };
                        timer.Start();

                        // 남은 시간 표시 (선택사항)
                        MessageText.Text = $"{message}\n\n{autoCloseSeconds}초 후 자동으로 닫힙니다.";
                    }
                    break;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Ok;
            this.Close();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            this.Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            this.Close();
        }

        // 어두운 배경 클릭 시 - 세션은 연장되지만 창은 안 닫힘
        private void Overlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 이벤트가 버튼이 아닌 배경에서 발생했는지 확인
            if (e.Source is Grid)
            {
                System.Diagnostics.Debug.WriteLine("[CustomMessageWindow] 배경 클릭 - 세션 연장됨");
                // 창은 닫지 않음 (버튼만 클릭 시 닫힘)
            }
        }
    }
}