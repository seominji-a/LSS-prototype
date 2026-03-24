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

        //
        private bool _canMergeWithoutEdit;
        public bool CanMergeWithoutEdit
        {
            get => _canMergeWithoutEdit;
            set
            {
                _canMergeWithoutEdit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EditButtonText));
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

        public PatientEditViewModel(IDialogService dialogService, PatientModel selected, bool canMergeWithoutEdit = false)
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
            CanMergeWithoutEdit = canMergeWithoutEdit;

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

        private bool CanEditPatient()
        {
            return IsValid() && (IsDirty() || CanMergeWithoutEdit) && IsDobConfirmed && IsCodeConfirmed;
        }

        public string EditButtonText =>
            CanMergeWithoutEdit && !IsDirty() ? "MERGE" : "EDIT";
        private async Task UpdatePatient()
        {
            try
            {
                var repo = new DB_Manager();

                if (IsDirty())
                {
                    // 현재 DB 전체 조회
                    var locals = repo.GetLocalPatients();
                    var emrs = repo.GetEmrPatients();

                    var allPatients = locals.Concat(emrs);

                    // 자기 자신 제외하고 같은 코드 찾기
                    var duplicated = allPatients
                        .FirstOrDefault(x => x.PatientCode == this.PatientCode
                                          && x.PatientId != this.Patient_id);

                    if (duplicated != null)
                    {
                        bool sameDetail =
                            duplicated.BirthDate.Date == this.BirthDate.Value.Date &&
                            string.Equals((duplicated.Sex ?? "").Trim(),
                                          (this.Sex ?? "").Trim(),
                                          StringComparison.OrdinalIgnoreCase);

                        // 1. 완전 동일 → 병합 대상
                        if (sameDetail)
                        {
                            await CustomMessageWindow.ShowAsync(
                                "동일한 환자번호가 이미 존재합니다.\n" +
                                "생년월일과 성별이 일치하여 병합 대상입니다.\n\n" +
                                "수정으로 중복 생성할 수 없습니다.\n" +
                                "병합을 진행해주세요.",
                                CustomMessageWindow.MessageBoxType.Ok,
                                0,
                                CustomMessageWindow.MessageIconType.Warning);

                            return;
                        }

                        //  2. 코드만 동일 → 완전 차단
                        await CustomMessageWindow.ShowAsync(
                            "이미 사용 중인 환자번호입니다.\n" +
                            "동일한 환자번호로 수정할 수 없습니다.\n"+
                            "환자 번호를 확인해주세요.",
                            CustomMessageWindow.MessageBoxType.Ok,
                            0,
                            CustomMessageWindow.MessageIconType.Warning);

                        return;
                    }

                    // 중복 없으면 저장
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
                        await CustomMessageWindow.ShowAsync("수정되었습니다.",
                            CustomMessageWindow.MessageBoxType.Ok, 1,
                            CustomMessageWindow.MessageIconType.Info);

                        CloseAction?.Invoke(true);
                    }
                    else
                    {
                        await CustomMessageWindow.ShowAsync("수정 중 오류가 발생했습니다.",
                            CustomMessageWindow.MessageBoxType.Ok, 1,
                            CustomMessageWindow.MessageIconType.Warning);
                    }
                }
                else if (CanMergeWithoutEdit)
                {
                    CloseAction?.Invoke(true);
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

        //  모든 Keypad 닫기
        private void CloseAllKeypads()
        {
            IsKeypadOpen = false;
            IsCodeKeypadOpen = false;
            KeypadVm = null;
        }

        // -------------------------------
        // DOB Keypad 영역 
        // -------------------------------

        private void OpenKeypad()
        {
            if (IsKeypadOpen) // 키패드 중복 생성 방지
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

        // -------------------------------
        // Code Keypad 영역
        // -------------------------------

        private void OpenPatientCodeKeypad()
        {
            if (IsCodeKeypadOpen) //키패드 중복 생성 방지 
                return;

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

        // -------------------------------
        // Helper 영역
        // -------------------------------

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
    }
}