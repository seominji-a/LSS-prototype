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

        //public DateTime? BirthDate { get; set; }
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


        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public ICommand OpenKeypadCommand { get; }

        public ICommand OpenPatientCodeKeypadCommand { get; }


        private readonly IDialogService _dialogService;
        public PatientAddViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
            OpenKeypadCommand = new RelayCommand(OpenKeypad);
            OpenPatientCodeKeypadCommand = new RelayCommand(OpenPatientCodeKeypad);
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
            this.KeypadVm.IsDateMode = true;

            // 기존에 입력 중이던 값이 있다면 (BirthDate가 null이라도 프리뷰 텍스트가 있다면) 로드
            // 만약 BirthDatePreview를 사용 중이라면 그것을 기반으로 숫자를 추출하여 전달
            if (!string.IsNullOrEmpty(this.BirthDatePreview))
            {
                this.KeypadVm.InputText = this.BirthDatePreview.Replace("-", "");
            }

            this.KeypadVm.InputChanged += (input) => {
                // [중요] 입력 중에는 공백으로 만들지 않고 오직 '프리뷰'만 업데이트합니다.
                BirthDatePreview = FormatDatePreview(input);

                // 8자리가 완벽할 때만 실제 데이터(BirthDate)에 할당
                if (input.Length == 8 && DateTime.TryParseExact(input, "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out DateTime date))
                {
                    this.BirthDate = date;
                }
                else
                {
                    // 아직 8자리가 아니거나 유효하지 않아도 BirthDate만 null로 유지하고 
                    // InputText(키패드 내부 값)는 건드리지 않습니다.
                    this.BirthDate = null;
                }
            };

            this.KeypadVm.CloseRequested += OnKeypadClosed;
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

            // --- 중요: 날짜 체크를 하지 않도록 설정 ---
            this.KeypadVm.IsDateMode = false;
            this.KeypadVm.MaxLength = 10; // 환자 코드가 8자리보다 길 수 있다면 조정 가능

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
    }
}
