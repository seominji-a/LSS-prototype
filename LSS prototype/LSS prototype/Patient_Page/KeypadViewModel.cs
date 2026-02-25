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
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private string _inputText = "";
        public string InputText
        {
            get => _inputText;
            set
            {
                _inputText = value;
                OnPropertyChanged();
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
            if (InputText.Length < 8)
                InputText += number;
        }

        private void RemoveLast()
        {
            if (InputText.Length > 0)
                InputText = InputText.Substring(0, InputText.Length - 1);
        }

        //Keypad ENTER 클릭
        private void Confirm()
        {
            // 8자리 체크
            if (string.IsNullOrEmpty(InputText) || InputText.Length != 8)
            {
                CustomMessageWindow.Show("숫자 8자리를 입력하지 않았습니다. 다시 입력해주십시오.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Warning);

                // 입력 값 수정하지 않고 return만 수행
                // InputText는 입력된 8자리 미만의 숫자를 그대로 유지 가능
                return;
            }

            // 날짜 유효성 체크
            if (DateTime.TryParseExact(InputText, "yyyyMMdd",
                null,
                System.Globalization.DateTimeStyles.None,
                out DateTime date))
            {
                ResultDate = date;
                CloseRequested?.Invoke(true); // 날짜가 완벽할 때만 팝업 종료.
            }
            else
            {
                CustomMessageWindow.Show("유효하지 않은 날짜 형식입니다. 다시 확인해주세요.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Danger);

                // 날짜가 틀렸을 때도 return만 하여 입력된 8자리 유지 가능.
                return;
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
