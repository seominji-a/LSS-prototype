using LSS_prototype.DB_CRUD;
using LSS_prototype.Patient_Page;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace LSS_prototype.Patient_Page
{
    public class PatientEditViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // 속성 변경 알림 헬퍼 메서드
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // 의존성 주입을 위한 필드
        private readonly IDialogService _dialogService;

        public string PatientName { get; set; }
        public int? PatientCode { get; set; }

        public int Patient_id { get; set; }

        private DateTime? _birthDate;
        public DateTime? BirthDate
        {
            get => _birthDate;
            set { _birthDate = value; OnPropertyChanged(); }
        }

        public string Sex { get; set; }

        public ICommand EditCommand { get; }
        public ICommand CancelCommand { get; }

        public ICommand OpenKeypadCommand { get; }

        public Action<bool?> CloseAction { get; set; }
        
        // IDialogService와 PatientModel을 모두 받음
        public PatientEditViewModel(IDialogService dialogService, PatientModel selected)
        {
            _dialogService = dialogService;

            Patient_id = selected.PatientId;
            PatientCode = selected.PatientCode;
            PatientName = selected.PatientName;
            BirthDate = selected.BirthDate;
            Sex = selected.Sex;

            EditCommand = new RelayCommand(UpdatePatient);
            CancelCommand = new RelayCommand(Cancel);
            OpenKeypadCommand = new RelayCommand(OpenKeypad); // 커맨드 연결
        }

        private void UpdatePatient()
        {
            try
            {

                if (string.IsNullOrWhiteSpace(PatientName)) { ShowWarning("환자 이름을 입력해주세요."); return; }
                if (PatientCode == null || PatientCode == 0) { ShowWarning("환자 코드를 입력해주세요."); return; }
                if (BirthDate == null) { ShowWarning("생년월일을 선택해주세요."); return; }
                if (string.IsNullOrWhiteSpace(Sex)) { ShowWarning("성별을 선택해주세요."); return; }

                var repo = new DB_Manager();

                // 1. 중복 체크 (자기 자신은 제외)
                // Patient_id는 생성자에서 SelectedPatient로부터 받아온 고유 값입니다.
                if (repo.ExistsPatientCodeExceptSelf(this.PatientCode.Value, this.Patient_id))
                {
                    CustomMessageWindow.Show("해당 환자 번호는 이미 다른 환자가 사용 중입니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Danger);
                    return;
                }

                var model = new PatientModel
                {
                    PatientId = this.Patient_id,    
                    PatientCode = this.PatientCode.Value,
                    PatientName = this.PatientName,
                    BirthDate = this.BirthDate.Value,
                    Sex = this.Sex
                };

                if (repo.UpdatePatient(model))
                {
                    CustomMessageWindow.Show("수정되었습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Info);
                    CloseAction?.Invoke(true);
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private void ShowWarning(string message)
        {
            CustomMessageWindow.Show(message,
                CustomMessageWindow.MessageBoxType.AutoClose, 1,
                CustomMessageWindow.MessageIconType.Warning);
        }

        // --- 키패드 관련 로직 추가 ---
        private KeypadViewModel _keypadVm;
        public KeypadViewModel KeypadVm
        {
            get => _keypadVm;
            set { _keypadVm = value; OnPropertyChanged(); }
        }

        private bool _isKeypadOpen;
        public bool IsKeypadOpen
        {
            get => _isKeypadOpen;
            set { _isKeypadOpen = value; OnPropertyChanged(); }
        }

        private void OpenKeypad()
        {
            KeypadVm = new KeypadViewModel();
            KeypadVm.CloseRequested += OnKeypadClosed;
            IsKeypadOpen = true;
        }

        private void OnKeypadClosed(bool? result)
        {
            IsKeypadOpen = false;
            if (result == true && KeypadVm.ResultDate != null)
            {
                BirthDate = KeypadVm.ResultDate;
            }
        }

        private void Cancel()
        {
            CloseAction?.Invoke(false);
        }
    }
}
