using LSS_prototype.DB_CRUD;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LSS_prototype.Patient_Page
{
    public class PatientEditViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private readonly IDialogService _dialogService;

        private string _patientName;
        public string PatientName
        {
            get => _patientName;
            set
            {
                _patientName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EditButtonText));
                EditCommand?.RaiseCanExecuteChanged();
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
                OnPropertyChanged(nameof(EditButtonText));
                EditCommand?.RaiseCanExecuteChanged();
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

       

        private bool _isCodeKeypadOpen;
        public bool IsCodeKeypadOpen
        {
            get => _isCodeKeypadOpen;
            set
            {
                _isCodeKeypadOpen = value;
                OnPropertyChanged();
            }
        }

        private bool _isNameKeypadOpen;
        public bool IsNameKeypadOpen
        {
            get => _isNameKeypadOpen;
            set
            {
                _isNameKeypadOpen = value;
                OnPropertyChanged();
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
                OnPropertyChanged(nameof(EditButtonText));
                EditCommand?.RaiseCanExecuteChanged();
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
                OnPropertyChanged(nameof(EditButtonText));
                EditCommand?.RaiseCanExecuteChanged();
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
            

            EditCommand = new RelayCommand(async _ => await UpdatePatient(), _ => CanEditPatient());
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

        //수정 없이도 버튼 클릭 가능
        private bool CanEditPatient()
        {
            return IsValid() && IsDirty() && IsDobConfirmed && IsCodeConfirmed;
        }


        private string N(string value)
        {
            return (value ?? string.Empty).Trim();
        }


        // 병합 가능 상태면 항상 MERGE 표시
        public string EditButtonText => "EDIT";



        private async Task UpdatePatient()
        {
            try
            {
                var repo = new DB_Manager();

                

                // 일반 수정
                if (!IsDirty())
                    return;

                var editingPatient = new PatientModel
                {
                    PatientId = this.Patient_id,
                    PatientCode = this.PatientCode.Value,
                    PatientName = this.PatientName,
                    BirthDate = this.BirthDate.Value,
                    Sex = this.Sex,
                    AccessionNumber = string.Empty
                };

                var allOthers = repo.GetAllPatients()
                    .Where(x => x.PatientId != editingPatient.PatientId)
                    .ToList();

                var duplicatedCodePatient = allOthers
                    .FirstOrDefault(x => x.PatientCode == editingPatient.PatientCode);

                if (duplicatedCodePatient != null)
                {
                    await CustomMessageWindow.ShowAsync(
                        "중복된 환자번호가 존재합니다.\n같은 환자번호로 수정할 수 없습니다.",
                        CustomMessageWindow.MessageBoxType.Ok,
                        0,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                if (repo.UpdatePatient(editingPatient))
                {
                    await CustomMessageWindow.ShowAsync(
                        "수정되었습니다.",
                        CustomMessageWindow.MessageBoxType.Ok,
                        1,
                        CustomMessageWindow.MessageIconType.Info);

                    CloseAction?.Invoke(true);
                }
                else
                {
                    await CustomMessageWindow.ShowAsync(
                        "수정 중 오류가 발생했습니다.",
                        CustomMessageWindow.MessageBoxType.Ok,
                        1,
                        CustomMessageWindow.MessageIconType.Warning);
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private void Cancel()
        {
            CloseAction?.Invoke(false);
        }

        // -------------------------------
        // Keypad 영역
        // -------------------------------

        private KeypadViewModel _keypadVm;
        public KeypadViewModel KeypadVm
        {
            get => _keypadVm;
            private set
            {
                _keypadVm = value;
                OnPropertyChanged();
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

        private void OpenKeypad(object _ = null)
        {
            if (IsKeypadOpen)
                return;

            CloseAllKeypads();

            IsDobConfirmed = false;

            KeypadVm = new KeypadViewModel();
            KeypadVm.IsDateMode = true;

            if (!string.IsNullOrEmpty(BirthDatePreview))
                KeypadVm.InputText = BirthDatePreview.Replace("-", "");

            KeypadVm.InputChanged += (input) =>
            {
                BirthDatePreview = FormatDatePreview(input);
                BirthDate = null;
            };

            KeypadVm.CloseRequested += OnKeypadClosed;

            IsKeypadOpen = true;
        }

        private void OnKeypadClosed(bool? result)
        {
            if (KeypadVm != null)
                KeypadVm.CloseRequested -= OnKeypadClosed;

            IsKeypadOpen = false;

            if (result == true)
            {
                BirthDate = KeypadVm.ResultDate;
                BirthDatePreview = BirthDate?.ToString("yyyy-MM-dd");
                IsDobConfirmed = true;
            }
            else if (result == false)
            {
                BirthDate = OriginalBirthDate;
                BirthDatePreview = OriginalBirthDate?.ToString("yyyy-MM-dd");
                IsDobConfirmed = false;
            }

            KeypadVm = null;
            EditCommand?.RaiseCanExecuteChanged();
        }

        private void OpenPatientCodeKeypad(object _ = null)
        {
            if (IsCodeKeypadOpen)
                return;

            CloseAllKeypads();

            IsCodeConfirmed = false;

            KeypadVm = new KeypadViewModel();
            KeypadVm.IsDateMode = false;
            KeypadVm.MaxLength = 10;

            if (PatientCode.HasValue)
                KeypadVm.InputText = PatientCode.Value.ToString();

            KeypadVm.InputChanged += (input) =>
            {
                if (string.IsNullOrEmpty(input))
                    PatientCode = null;
                else if (int.TryParse(input, out int code))
                    PatientCode = code;

                OnPropertyChanged(nameof(PatientCode));
            };

            KeypadVm.CloseRequested += OnPatientCodeKeypadClosed;

            IsCodeKeypadOpen = true;
        }

        private void OnPatientCodeKeypadClosed(bool? result)
        {
            if (KeypadVm != null)
                KeypadVm.CloseRequested -= OnPatientCodeKeypadClosed;

            IsCodeKeypadOpen = false;

            if (result == true)
            {
                IsCodeConfirmed = true;
            }
            else if (result == false)
            {
                PatientCode = OriginalCode;
                OnPropertyChanged(nameof(PatientCode));
            }

            KeypadVm = null;
            EditCommand?.RaiseCanExecuteChanged();
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

        private void CloseAllKeypads()
        {
            IsKeypadOpen = false;
            IsCodeKeypadOpen = false;
            KeypadVm = null;
        }
    }
}