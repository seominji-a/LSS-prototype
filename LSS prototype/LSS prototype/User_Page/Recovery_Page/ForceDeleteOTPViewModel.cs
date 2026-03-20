using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{
    public class ForceDeleteOTPViewModel : INotifyPropertyChanged
    {
        private readonly ForceDeleteOTP _window;

        // ── OTP 입력값 (TxtOtp 바인딩) ──
        private string _otpInput = string.Empty;
        public string OtpInput
        {
            get => _otpInput;
            set { _otpInput = value; OnPropertyChanged(); }
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public ForceDeleteOTPViewModel(ForceDeleteOTP window)
        {
            _window = window;

            ConfirmCommand = new RelayCommand(async _ => await ExecuteConfirm());
            CancelCommand = new RelayCommand(_ => ExecuteCancel());
        }

        // ═══════════════════════════════════════════
        //  ExecuteConfirm()
        //  OTP 검증 → 성공 시 true 반환 후 닫기
        //  실패 시 입력창 초기화 후 재입력 유도
        // ═══════════════════════════════════════════
        private async Task ExecuteConfirm()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(OtpInput))
                {
                    await CustomMessageWindow.ShowAsync(
                        "OTP 번호를 입력해주세요.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                if (!await Common.VerifyOtpOnly(OtpInput))
                {
                    await CustomMessageWindow.ShowAsync("OTP 번호가 올바르지 않습니다.\n다시 확인해주세요.", CustomMessageWindow.MessageBoxType.AutoClose, 2, CustomMessageWindow.MessageIconType.Warning);
                    OtpInput = string.Empty; // 입력창 초기화
                    return;
                }

                // OTP 검증 성공 → true 반환 후 닫기
                _window.CloseWithResult(true);
            }
            catch (System.Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  ExecuteCancel()
        //  취소 → false 반환 후 닫기
        // ═══════════════════════════════════════════
        private void ExecuteCancel()
        {
            _window.CloseWithResult(false);
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}