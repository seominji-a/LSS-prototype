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
            set
            {
                _birthDate = value;
                OnPropertyChanged();
            }
        }

        private string _birthDatePreview;
        public string BirthDatePreview
        {
            get => _birthDatePreview;
            set
            {
                _birthDatePreview = value;
                OnPropertyChanged();
            }
        }

        public string Sex { get; set; }

        public ICommand EditCommand { get; }
        public ICommand CancelCommand { get; }

        public ICommand OpenKeypadCommand { get; }

        public ICommand OpenPatientCodeKeypadCommand { get; }

        public Action<bool?> CloseAction { get; set; }
        
        // IDialogService와 PatientModel을 모두 받음
        public PatientEditViewModel(IDialogService dialogService, PatientModel selected)
        {
            _dialogService = dialogService;

            Patient_id = selected.PatientId;
            PatientCode = selected.PatientCode;
            PatientName = selected.PatientName;
            BirthDate = selected.BirthDate;
            BirthDatePreview = BirthDate?.ToString("yyyy-MM-dd");  
            Sex = selected.Sex;

            EditCommand = new RelayCommand(UpdatePatient);
            CancelCommand = new RelayCommand(Cancel);
            OpenKeypadCommand = new RelayCommand(OpenKeypad); // 커맨드 연결
            OpenPatientCodeKeypadCommand = new RelayCommand(OpenPatientCodeKeypad);
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
                    CustomMessageWindow.Show("이미 사용 중인 환자 번호입니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 2,
                        CustomMessageWindow.MessageIconType.Warning);
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
            private set // set 추가
            {
                _keypadVm = value;
                OnPropertyChanged(); // UI에 객체가 생성되었음을 알림
            }
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

        private void OpenKeypad()
        {
            this.KeypadVm = new KeypadViewModel();

            // 기존 날짜가 있다면 키패드에 미리 채워넣기
            if (this.BirthDate.HasValue)
            {
                string existingDate = this.BirthDate.Value.ToString("yyyyMMdd");

                // KeypadViewModel의 실제 속성인 InputText에 값을 대입
                this.KeypadVm.InputText = existingDate;

                // 메인 화면의 프리뷰도 업데이트
                BirthDatePreview = FormatDatePreview(existingDate);
            }

            this.KeypadVm.CloseRequested += OnKeypadClosed;
            this.KeypadVm.InputChanged += OnKeypadInputChanged;

            // 팝업 열기
            IsKeypadOpen = true;
            IsCodeKeypadOpen = false;
        }

        private void OnKeypadInputChanged(string input)
        {
            BirthDatePreview = FormatDatePreview(input);
        }

        private string FormatDatePreview(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
            if (input.Length <= 4)
                return input;
            if (input.Length <= 6)
                return input.Insert(4, "-");
            return input.Insert(4, "-").Insert(7, "-");
        }

        

        private void OnKeypadClosed(bool? result)
        {
            IsKeypadOpen = false;
            if (_keypadVm != null)
            {
                _keypadVm.CloseRequested -= OnKeypadClosed;
                _keypadVm.InputChanged -= OnKeypadInputChanged;
            }
            if (result == true && _keypadVm.ResultDate != null)
            {
                BirthDate = _keypadVm.ResultDate;
                BirthDatePreview = BirthDate?.ToString("yyyy-MM-dd");
            }
        }

        private bool _isCodeKeypadOpen;
        public bool IsCodeKeypadOpen
        {
            get => _isCodeKeypadOpen;
            set { _isCodeKeypadOpen = value; OnPropertyChanged(); }
        }

        private void OpenPatientCodeKeypad()
        {
            this.KeypadVm = new KeypadViewModel();
            this.KeypadVm.IsDateMode = false;
            this.KeypadVm.MaxLength = 10;
            if (this.PatientCode.HasValue)
            {
                this.KeypadVm.InputText = this.PatientCode.Value.ToString();
            }
            // CloseRequested 이벤트 연결
            this.KeypadVm.CloseRequested += (result) =>
            {
                IsCodeKeypadOpen = false; // 팝업 닫기
            };
            // -------------------------------------------------------------
            this.KeypadVm.InputChanged += (input) => {
                if (string.IsNullOrEmpty(input))
                {
                    this.PatientCode = null;
                    OnPropertyChanged(nameof(PatientCode));
                }
                else if (int.TryParse(input, out int code))
                {
                    this.PatientCode = code;
                    OnPropertyChanged(nameof(PatientCode));
                }
            };
            IsCodeKeypadOpen = true;
            IsKeypadOpen = false; // 다른 키패드는 확실히 닫기
        }

        private void Cancel()
        {
            CloseAction?.Invoke(false);
        }
    }
}
