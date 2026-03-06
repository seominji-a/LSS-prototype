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

        private string _patientName;
        public string PatientName
        {
            get => _patientName;
            set
            {
                _patientName = value;
                OnPropertyChanged();
                (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
        private int? _patientCode;
        public int? PatientCode
        {
            get => _patientCode;
            set
            {
                _patientCode = value;
                OnPropertyChanged();
                (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public int Patient_id { get; set; }

        private bool _isDobConfirmed;
        public bool IsDobConfirmed
        {
            get => _isDobConfirmed;
            set
            {
                _isDobConfirmed = value;
                OnPropertyChanged();
                EditCommand?.RaiseCanExecuteChanged();
            }
        }

        private bool _isCodeConfirmed = true;
        public bool IsCodeConfirmed
        {
            get => _isCodeConfirmed;
            set
            {
                _isCodeConfirmed = value;
                OnPropertyChanged();
                EditCommand?.RaiseCanExecuteChanged();
            }
        }


        private DateTime? _birthDate;
        public DateTime? BirthDate
        {
            get => _birthDate;
            set
            {
                _birthDate = value;
                OnPropertyChanged();
                (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

        private string _sex;
        public string Sex
        {
            get => _sex;
            set
            {
                _sex = value;
                OnPropertyChanged();
                (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private string OriginalName;
        private int? OriginalCode;
        private DateTime? OriginalBirthDate;
        private string OriginalSex;

        public RelayCommand EditCommand { get; }



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

            OriginalName = PatientName;
            OriginalCode = PatientCode;
            OriginalBirthDate = BirthDate;
            OriginalSex = Sex;

            IsDobConfirmed = true;
            IsCodeConfirmed = true;

            EditCommand = new RelayCommand(UpdatePatient, CanEditPatient);
            CancelCommand = new RelayCommand(Cancel);
            OpenKeypadCommand = new RelayCommand(OpenKeypad);
            OpenPatientCodeKeypadCommand = new RelayCommand(OpenPatientCodeKeypad);
        }

        private bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(PatientName)) return false;
            if (PatientCode == null || PatientCode == 0) return false;
            if (BirthDate == null) return false;
            if (string.IsNullOrWhiteSpace(Sex)) return false;
            return true;
        }
        private bool IsDirty()
        {
            return
                PatientName != OriginalName ||
                PatientCode != OriginalCode ||
                BirthDate != OriginalBirthDate ||
                Sex != OriginalSex;
        }
        private bool CanEditPatient()
        {
            return IsValid() && IsDirty() && IsDobConfirmed && IsCodeConfirmed;
        }

        private void UpdatePatient()
        {
            try
            {
                var repo = new DB_Manager();
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
            IsDobConfirmed = false; // 입력 시작,  Edit 비활성

            this.KeypadVm = new KeypadViewModel();
            this.KeypadVm.IsDateMode = true;

            if (!string.IsNullOrEmpty(this.BirthDatePreview))
            {
                this.KeypadVm.InputText = this.BirthDatePreview.Replace("-", "");
            }

            this.KeypadVm.InputChanged += (input) =>
            {
                // 입력 중에는 Preview만 표시
                BirthDatePreview = FormatDatePreview(input);

                // 입력 중에는 실제 DOB 확정하지 않음
                this.BirthDate = null;
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



        private void OnKeypadClosed(bool? result)
        {
            IsKeypadOpen = false;

            // Cancel
            if (result != true)
            {
                BirthDate = OriginalBirthDate;
                BirthDatePreview = OriginalBirthDate?.ToString("yyyy-MM-dd");
                return;
            }

            // Confirm
            if (KeypadVm.ResultDate != null)
            {
                BirthDate = KeypadVm.ResultDate;
                BirthDatePreview = BirthDate.Value.ToString("yyyy-MM-dd");

                IsDobConfirmed = true;
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
            IsCodeConfirmed = false; // 입력 시작, Edit 비활성
            this.KeypadVm = new KeypadViewModel();
            this.KeypadVm.IsDateMode = false;
            this.KeypadVm.MaxLength = 10;
            if (this.PatientCode.HasValue)
            {
                this.KeypadVm.InputText = this.PatientCode.Value.ToString();
            }
            this.KeypadVm.InputChanged += (input) =>
            {
                // 입력 중에는 확정하지 않음
                if (string.IsNullOrEmpty(input))
                {
                    this.PatientCode = null;
                }
                else if (int.TryParse(input, out int code))
                {
                    this.PatientCode = code;
                }
                OnPropertyChanged(nameof(PatientCode));
            };
            this.KeypadVm.CloseRequested += OnPatientCodeKeypadClosed;
            IsCodeKeypadOpen = true;
            IsKeypadOpen = false;
        }

        private void OnPatientCodeKeypadClosed(bool? result)
        {
            IsCodeKeypadOpen = false;
            // Cancel
            if (result != true)
            {
                PatientCode = OriginalCode;
                OnPropertyChanged(nameof(PatientCode));
                return;
            }
            // Confirm
            IsCodeConfirmed = true; // ⭐ Confirm 완료 → Edit 활성 가능
        }

        private void Cancel()
        {
            CloseAction?.Invoke(false);
        }
    }
}
