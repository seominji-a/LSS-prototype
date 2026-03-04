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
            // 1. 일반 숫자 모드 (PatientCode 등)
            // 이 모드에서는 8자리 제한 없이 값이 있기만 하면 창을 닫습니다.
            if (!IsDateMode)
            {
                if (!string.IsNullOrEmpty(InputText))
                {
                    CloseRequested?.Invoke(true);
                }
                return;
            }

            // 2. 날짜 모드 (엄격한 유효성 검사)
            // ENTER를 누른 시점에 8자리가 아니면 경고를 띄우고 초기화합니다.
            if (string.IsNullOrEmpty(InputText) || InputText.Length != 8)
            {
                CustomMessageWindow.Show("숫자 8자리를 입력하지 않았습니다. 다시 입력해주십시오.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Warning);

                InputText = string.Empty; // 여기서 초기화되므로 외부 클릭 시에는 값이 유지됩니다.
                return;
            }

            // 3. 날짜 형식 유효성 체크
            if (DateTime.TryParseExact(InputText, "yyyyMMdd",
                null,
                System.Globalization.DateTimeStyles.None,
                out DateTime date))
            {
                ResultDate = date;
                CloseRequested?.Invoke(true); // 성공 시에만 결과값을 들고 창을 닫음
            }
            else
            {
                CustomMessageWindow.Show("유효하지 않은 날짜 형식입니다. \n 다시 확인해주세요.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Danger);

                InputText = string.Empty; // 날짜 형식이 틀려도 초기화
                ResultDate = null;
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
