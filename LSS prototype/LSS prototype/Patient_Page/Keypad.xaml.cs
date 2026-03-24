using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LSS_prototype.Patient_Page
{
    /// <summary>
    /// Keypad.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Keypad : UserControl
    {
        // ── 꾹누름 타이머 ──
        // 0.5초 후 시작, 이후 100ms 마다 반복 삭제
        private readonly DispatcherTimer _holdTimer = new DispatcherTimer();
        private bool _isHolding = false;

        private KeypadViewModel VM => DataContext as KeypadViewModel;

        public Keypad()
        {
            InitializeComponent();

            // 처음 1초 대기 후 → 100ms 마다 반복
            _holdTimer.Interval = TimeSpan.FromMilliseconds(100);
            _holdTimer.Tick += (s, e) => VM?.RemoveLast();
        }

        // ── 마우스 꾹누름 ──
        private void BtnBackspace_MouseDown(object sender, MouseButtonEventArgs e)
            => StartHold();

        private void BtnBackspace_MouseUp(object sender, MouseButtonEventArgs e)
            => StopHold();

        // ── 터치 꾹누름 ──
        private void BtnBackspace_TouchDown(object sender, TouchEventArgs e)
            => StartHold();

        private void BtnBackspace_TouchUp(object sender, TouchEventArgs e)
            => StopHold();

        private void StartHold()
        {
            if (_isHolding) return;
            _isHolding = true;

            // 1초 후 반복 삭제 시작
            _holdTimer.Interval = TimeSpan.FromMilliseconds(500);
            _holdTimer.Tick -= OnHoldFirstTick; // 중복 방지
            _holdTimer.Tick += OnHoldFirstTick;
            _holdTimer.Start();
        }

        private void OnHoldFirstTick(object sender, EventArgs e)
        {
            // 첫 틱 이후 100ms 간격으로 전환
            _holdTimer.Tick -= OnHoldFirstTick;
            _holdTimer.Tick += OnHoldRepeatTick;
            _holdTimer.Interval = TimeSpan.FromMilliseconds(100);
            VM?.RemoveLast();
        }

        private void OnHoldRepeatTick(object sender, EventArgs e)
            => VM?.RemoveLast();

        private void StopHold()
        {
            _isHolding = false;
            _holdTimer.Stop();
            _holdTimer.Tick -= OnHoldFirstTick;
            _holdTimer.Tick -= OnHoldRepeatTick;
        }


    }
}
