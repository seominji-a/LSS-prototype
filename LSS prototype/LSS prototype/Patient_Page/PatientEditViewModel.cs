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

        // ✅ 병합 가능 상태면 항상 MERGE 표시
        public string EditButtonText => CanMergeWithoutEdit ? "MERGE" : "EDIT";

        private async Task UpdatePatient()
        {
            try
            {
                var repo = new DB_Manager();

                // ✅ 병합 가능한 상태면 수정 여부와 관계없이 부모 ViewModel 쪽 병합 흐름으로 넘김
                if (CanMergeWithoutEdit)
                {
                    CloseAction?.Invoke(true);
                    return;
                }

                // 여기부터는 일반 수정
                if (IsDirty())
                {
                    var locals = repo.GetLocalPatients();
                    var emrs = repo.GetEmrPatients();
                    var allPatients = locals.Concat(emrs);

                    var duplicated = allPatients
                        .FirstOrDefault(x => x.PatientCode == this.PatientCode
                                          && x.PatientId != this.Patient_id);

                    if (duplicated != null)
                    {
                        await CustomMessageWindow.ShowAsync(
                            "이미 사용 중인 환자번호입니다.\n환자 번호를 확인해주세요.",
                            CustomMessageWindow.MessageBoxType.Ok,
                            0,
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

        private void OpenKeypad(object _)
        {
            // 기존 구현 유지
        }

        private void OpenPatientCodeKeypad(object _)
        {
            // 기존 구현 유지
        }
    }
}