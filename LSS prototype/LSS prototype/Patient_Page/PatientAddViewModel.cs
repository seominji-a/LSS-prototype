using LSS_prototype.DB_CRUD;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LSS_prototype.Patient_Page
{
    

    public class PatientAddViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string PatientName { get; set; }
        public int? PatientCode { get; set; }

        public DateTime? BirthDate { get; set; }

        public string Sex { get; set; }


        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public ICommand OpenKeypadCommand { get; }

        private readonly IDialogService _dialogService;
        public PatientAddViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
            OpenKeypadCommand = new RelayCommand(OpenKeypad);
        }


        public Action<bool?> CloseAction { get; set; }

        private void Save()//환자 정보 추가에 관한 입력, 검증만 담당
        {
            if (string.IsNullOrWhiteSpace(PatientName)) { ShowWarning("환자 이름을 입력해주세요."); return; }
            if (PatientCode == null || PatientCode == 0) { ShowWarning("환자 코드를 입력해주세요."); return; }
            if (BirthDate == null) { ShowWarning("생년월일을 선택해주세요."); return; }
            if (string.IsNullOrWhiteSpace(Sex)) { ShowWarning("성별을 선택해주세요."); return; }

            // 중복 체크 추가 (창이 닫히기 전에 수행)
            var repo = new DB_Manager();
            if (repo.ExistsPatientCode(PatientCode.Value))
            {
                CustomMessageWindow.Show("중복된 환자가 존재합니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose, 2,
                    CustomMessageWindow.MessageIconType.Danger);

                // 여기서 return을 하면 CloseAction이 실행되지 않아 창이 유지됩니다.
                return;
            }

            //저장하지 말고 그냥 닫기만
            CloseAction?.Invoke(true);
        }

        private void Cancel()
        {
            CloseAction?.Invoke(false);
        }

        private void ShowWarning(string message)
        {
            CustomMessageWindow.Show(message,
                CustomMessageWindow.MessageBoxType.AutoClose, 1,
                CustomMessageWindow.MessageIconType.Warning);
        }

        private KeypadViewModel _keypadVm;
        public KeypadViewModel KeypadVm
        {
            get => _keypadVm;
            private set // set 추가
            {
                _keypadVm = value;
                OnPropertyChanged(); // UI에 객체가 생성되었음을 알림
            }
        }

        private void OpenKeypad()
        {
            this.KeypadVm = new KeypadViewModel();
            this.KeypadVm.CloseRequested += OnKeypadClosed;
            IsKeypadOpen = true;
        }

        private bool _isKeypadOpen;
        public bool IsKeypadOpen
        {
            get => _isKeypadOpen;
            set
            {
                _isKeypadOpen = value;
                OnPropertyChanged();
            }
        }
        private void OnKeypadClosed(bool? result)
        {
            IsKeypadOpen = false;

            if (result == true && _keypadVm.ResultDate != null)
            {
                BirthDate = _keypadVm.ResultDate;
                OnPropertyChanged(nameof(BirthDate));
            }
        }

    }
}
