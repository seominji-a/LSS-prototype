using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Effects;

namespace LSS_prototype.User_Page
{
    public partial class ForceDeleteOTP : Window
    {
        private ForceDeleteOTPViewModel VM => DataContext as ForceDeleteOTPViewModel;

        // ── 블러 적용된 창 목록 (닫힐 때 복원용) ──
        private readonly List<Window> _blurredWindows = new List<Window>();

        // ── ShowAsync() 결과 반환용 ──
        private TaskCompletionSource<bool> _tcs;

        public ForceDeleteOTP()
        {
            InitializeComponent();
            DataContext = new ForceDeleteOTPViewModel(this);

            // Owner 설정 (CustomMessageWindow 와 동일)
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

            // 뒤 창 블러 (CustomMessageWindow 와 동일)
            Loaded += async (s, e) => await ApplyBlurToAllWindows();
            this.Closed += async  (s, e) => await RemoveBlurFromAllWindows();
        }

        // ═══════════════════════════════════════════
        //  ShowAsync()
        //  Show() 로 UI 스레드 안 막고 띄운 뒤
        //  TaskCompletionSource 로 결과 대기
        //  확인(OTP 통과) → true / 취소 → false
        // ═══════════════════════════════════════════
        public Task<bool> ShowAsync()
        {
            _tcs = new TaskCompletionSource<bool>();
            this.Show();
            return _tcs.Task;
        }

        // ViewModel 에서 호출 → 결과 세팅 후 닫기
        public void CloseWithResult(bool result)
        {
            _tcs?.TrySetResult(result);
            this.Close();
        }

        // ═══════════════════════════════════════════
        //  숫자만 입력 허용 (키보드)
        // ═══════════════════════════════════════════
        private void TxtOtp_PreviewTextInput(object sender,
            System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        // ═══════════════════════════════════════════
        //  숫자만 입력 허용 (붙여넣기)
        // ═══════════════════════════════════════════
        private void TxtOtp_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!Regex.IsMatch(text, "^[0-9]+$"))
                    e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }

        // ═══════════════════════════════════════════
        //  블러 처리 (CustomMessageWindow 방식 COPY)
        // ═══════════════════════════════════════════
        private async Task ApplyBlurToAllWindows()
        {
            try
            {
                var blurEffect = new BlurEffect { Radius = 10 };
                foreach (Window window in Application.Current.Windows)
                {
                    if (window != this && window.IsVisible)
                    {
                        window.Effect = blurEffect;
                        _blurredWindows.Add(window);
                    }
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task RemoveBlurFromAllWindows()
        {
            try
            {
                foreach (var window in _blurredWindows)
                {
                    if (window != null)
                        window.Effect = null;
                }
                _blurredWindows.Clear();
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }
    }
}