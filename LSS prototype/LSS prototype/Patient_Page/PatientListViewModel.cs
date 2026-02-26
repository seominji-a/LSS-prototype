using LSS_prototype.DB_CRUD;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LSS_prototype.Patient_Page
{
    /// <summary>
    /// 환자 목록 관리 및 CRUD(생성, 조회, 수정, 삭제) 기능 수행을 위한 로직
    /// 2026-02-09 서민지
    /// </summary>
    internal class PatientListViewModel : INotifyPropertyChanged
    {
        private string _searchText;
        public string SearchText
        {
            get { return _searchText; }
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                //OnPropertyChanged(); 검색로직 추가 시 해당 부분 주석 해제 0223 박한용
            }
        }

        private readonly IDialogService _dialogService;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // ===== Patients (단일 리스트) =====
        private ObservableCollection<PatientModel> _patients;
        public ObservableCollection<PatientModel> Patients
        {
            get => _patients;
            set { _patients = value; OnPropertyChanged(); }
        }

        private PatientModel _selectedPatient;
        public PatientModel SelectedPatient
        {
            get => _selectedPatient;
            set { _selectedPatient = value; OnPropertyChanged(); }
        }

        // ===== Commands =====
        public ICommand PatientAddCommand { get; }
        public ICommand PatientEditCommand { get; }
        public ICommand PatientDeleteCommand { get; }
        public ICommand SyncClickCommand { get; }
        public ICommand NavScanCommand { get; }
        public ICommand NavImageReviewCommand { get; }
        public ICommand NavVideoReviewCommand { get; }

        public PatientListViewModel()
        {
            _dialogService = new Dialog();

            PatientAddCommand = new RelayCommand(_ => AddPatient());
            PatientEditCommand = new RelayCommand(_ => EditPatient());
            PatientDeleteCommand = new RelayCommand(_ => DeletePatient());
            SyncClickCommand = new AsyncRelayCommand(async _ => await SyncButtonClicked());

            NavScanCommand = new RelayCommand(_ => MainPage.Instance.NavigateTo(new Scan_Page.Scan()));
            //NavImageReviewCommand = new RelayCommand(_ => MainPage.Instance.NavigateTo(new ImageReview_Page.ImageReview()));
            //NavVideoReviewCommand = new RelayCommand(_ => MainPage.Instance.NavigateTo(new VideoReview_Page.VideoReview()));

            LoadPatients();
        }

        /// <summary>
        /// DB에서 전체 환자 목록을 불러와 최신순(내림차순)으로 UI에 반영
        /// </summary>
        public void LoadPatients()
        {
            try
            {
                var repo = new DB_Manager();
                List<PatientModel> data = repo.GetAllPatients(); // 최신순으로 보장하는 쿼리문 수정해야함 ( 2월19일 기준 ) 

                Patients = new ObservableCollection<PatientModel>(data);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private void AddPatient()
        {
            try
            {
                var vm = new PatientAddViewModel(_dialogService);

                if (_dialogService.ShowDialog(vm) == true)
                {
                    var confirm = CustomMessageWindow.Show(
                            $"{vm.PatientName} 환자 정보를 생성하시겠습니까?",
                            CustomMessageWindow.MessageBoxType.YesNo,
                            0,
                            CustomMessageWindow.MessageIconType.Info);

                    if (confirm == CustomMessageWindow.MessageBoxResult.Yes)
                    {
                        var model = new PatientModel
                        {
                            PatientCode = vm.PatientCode.Value,
                            PatientName = vm.PatientName,
                            BirthDate = vm.BirthDate.Value,
                            Sex = vm.Sex
                        };


                        var repo = new DB_Manager();

                        bool result = repo.AddPatient(model);

                        if (result)
                        {
                            CustomMessageWindow.Show("환자가 정상적으로 등록되었습니다.",
                                CustomMessageWindow.MessageBoxType.AutoClose, 1,
                                CustomMessageWindow.MessageIconType.Info);
                            LoadPatients();
                        }
                        else
                        {
                            CustomMessageWindow.Show("등록 중 오류가 발생했습니다.",
                                CustomMessageWindow.MessageBoxType.AutoClose, 1,
                                CustomMessageWindow.MessageIconType.Danger);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private void EditPatient()
        {
            if (SelectedPatient == null)
            {
                CustomMessageWindow.Show("수정할 환자를 선택해주세요.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Info);
                return;
            }

            // ✅ 생성자에 _dialogService를 첫 번째 인자로 추가하여 전달합니다.
            var vm = new PatientEditViewModel(_dialogService, SelectedPatient);

            var result = _dialogService.ShowDialog(vm);

            if (result == true)
            {
                LoadPatients();
            }
        }

        private void DeletePatient()
        {
            try
            {
                if (SelectedPatient == null)
                {
                    CustomMessageWindow.Show("삭제할 환자를 선택해주세요.",
                            CustomMessageWindow.MessageBoxType.AutoClose, 1,
                            CustomMessageWindow.MessageIconType.Info);
                    return;
                }

                if (CustomMessageWindow.Show(
                        $"{SelectedPatient.PatientName} 환자 정보를 삭제하시겠습니까?",
                        CustomMessageWindow.MessageBoxType.YesNo,0,CustomMessageWindow.MessageIconType.Danger
                    ) == CustomMessageWindow.MessageBoxResult.Yes)
                {
                    var repo = new DB_Manager();

                    if (repo.DeletePatient(SelectedPatient.PatientId))
                    {
                        CustomMessageWindow.Show("삭제되었습니다.",
                            CustomMessageWindow.MessageBoxType.AutoClose, 1,
                            CustomMessageWindow.MessageIconType.Info);
                        LoadPatients();
                    }
                }
            }
            catch(Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private async Task SyncButtonClicked()
        {
            await CustomMessageWindow.ShowAsync(
                    "EMR 환자 정보가 최신 상태로 \n 업데이트되었습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose,
                    3,
                    CustomMessageWindow.MessageIconType.Info);
        }
    }
}
