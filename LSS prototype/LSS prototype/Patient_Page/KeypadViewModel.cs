using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LSS_prototype.Patient_Page
{
    public class KeypadViewModel : INotifyPropertyChanged
    {

        // 추가: 날짜 모드인지 확인하는 플래그 (기본값 true)
        public bool IsDateMode { get; set; } = true;
        // 추가: 최대 입력 길이 (기본값 8)
        public int MaxLength { get; set; } = 8;


        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event Action<string> InputChanged;

        private string _inputText = "";
        public string InputText
        {
            get => _inputText;
            set
            {
                _inputText = value;
                OnPropertyChanged();

                
                InputChanged?.Invoke(_inputText);
            }
        }

        public DateTime? ResultDate { get; private set; }

        public ICommand NumberCommand { get; }
        public ICommand BackspaceCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public KeypadViewModel()
        {
            NumberCommand = new RelayCommand(param =>
            {
                if (param is string number)
                    AddNumber(number);
            });
            BackspaceCommand = new RelayCommand(RemoveLast);
            ConfirmCommand = new RelayCommand(Confirm);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void AddNumber(string number)
        {
            // MaxLength를 사용하여 유연하게 제한
            if (InputText.Length < MaxLength)
                InputText += number;
        }

        private void RemoveLast()
        {
            if (!string.IsNullOrEmpty(InputText) && InputText.Length > 0)
            {
                // 마지막 글자를 제거한 새 문자열 할당
                InputText = InputText.Remove(InputText.Length - 1);
            }
        }

        //Keypad ENTER 클릭
        private void Confirm()
        {
            // --- 일반 숫자 모드(PatientCode 등)일 때 ---
            if (!IsDateMode)
            {
                // 빈 값이 아니라면 바로 확인 처리
                if (!string.IsNullOrEmpty(InputText))
                {
                    CloseRequested?.Invoke(true);
                }
                return;
            }
            // --- 날짜 모드일 때 (기존 로직 유지) ---
            if (string.IsNullOrEmpty(InputText) || InputText.Length != 8)
            {
                CustomMessageWindow.Show("숫자 8자리를 입력하지 않았습니다. 다시 입력해주십시오.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Warning);
                return;
            }
            if (DateTime.TryParseExact(InputText, "yyyyMMdd",
                null,
                System.Globalization.DateTimeStyles.None,
                out DateTime date))
            {
                ResultDate = date;
                CloseRequested?.Invoke(true);
            }
            else
            {
                CustomMessageWindow.Show("유효하지 않은 날짜 형식입니다. \n 다시 확인해주세요.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Danger);
            }
        }

        //Keypad <- 버튼 클릭
        private void Cancel()
        {
            CloseRequested?.Invoke(false);
        }

        public event Action<bool?> CloseRequested;
    }
}
