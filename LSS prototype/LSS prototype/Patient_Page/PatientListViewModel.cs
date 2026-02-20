using LSS_prototype.DB_CRUD;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LSS_prototype
{
    /// <summary>
    /// 환자 목록 관리 및 CRUD(생성, 조회, 수정, 삭제) 기능 수행을 위한 로직
    /// 2026-02-09 서민지
    /// </summary>
    internal class PatientListViewModel : INotifyPropertyChanged
    {
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

        public PatientListViewModel()
        {
            _dialogService = new Dialog();

            PatientAddCommand = new RelayCommand(_ => AddPatient());
            PatientEditCommand = new RelayCommand(_ => EditPatient());
            PatientDeleteCommand = new RelayCommand(_ => DeletePatient());
            SyncClickCommand = new AsyncRelayCommand(async _ => await SyncButtonClicked());

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
                new CustomMessageWindow($"데이터 로드 중 오류 발생: {ex.Message}").Show();
            }
        }

        private void AddPatient()
        {
            try
            {
                var vm = new PatientAddViewModel();

                if (_dialogService.ShowDialog(vm) == true)
                {
                    var confirm = CustomMessageWindow.Show(
                        $"{vm.PatientName} 환자 정보를 생성하시겠습니까?",
                        CustomMessageWindow.MessageBoxType.YesNo);

                    if (confirm == CustomMessageWindow.MessageBoxResult.Yes)
                    {
                        var repo = new DB_Manager();
                        bool result = repo.AddPatient(vm);

                        if (result)
                        {
                            CustomMessageWindow.Show("환자가 정상적으로 등록되었습니다.");
                            LoadPatients(); // ⭐ 새 환자 맨 앞(최신순)으로 바로 반영
                        }
                        else
                        {
                            CustomMessageWindow.Show("등록 중 오류가 발생했습니다.");
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message + "AddPatient Function Check");
            }
           
        }

        private void EditPatient()
        {
            if (SelectedPatient == null)
            {
                new CustomMessageWindow("수정할 환자를 선택해주세요.").Show();
                return;
            }

            var vm = new PatientEditViewModel(SelectedPatient);
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
                    new CustomMessageWindow("삭제할 환자를 선택해주세요.").Show();
                    return;
                }

                if (CustomMessageWindow.Show(
                        $"{SelectedPatient.Name} 환자 정보를 삭제하시겠습니까?",
                        CustomMessageWindow.MessageBoxType.YesNo
                    ) == CustomMessageWindow.MessageBoxResult.Yes)
                {
                    var repo = new DB_Manager();

                    if (repo.DeletePatient(SelectedPatient.PatientId))
                    {
                        CustomMessageWindow.Show("삭제되었습니다.");
                        LoadPatients();
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message + "DeletePatient Function Check");
            }
        }

        private async Task SyncButtonClicked()
        {
            await new CustomMessageWindow(
                "EMR 환자 정보가 최신 상태로 업데이트되었습니다.",
                CustomMessageWindow.MessageBoxType.AutoClose,
                3
            ).ShowAsync();
        }
    }
}
