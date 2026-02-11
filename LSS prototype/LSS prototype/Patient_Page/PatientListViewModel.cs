using LSS_prototype;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace LSS_prototype
{
    internal class PatientListViewModel : INotifyPropertyChanged
    {
        private readonly IDialogService _dialogService;

        public event PropertyChangedEventHandler PropertyChanged;

        // 3. 프로퍼티 값이 바뀌었을 때 UI에 알리는 헬퍼 메서드
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private ObservableCollection<PatientModel> _registeredPatients;
        public ObservableCollection<PatientModel> RegisteredPatients
        {
            get => _registeredPatients;
            set { _registeredPatients = value;
                OnPropertyChanged();
            }
        }

        public ICommand PatientAddCommand { get; }
        public ICommand SyncClickCommand { get; }

        public ICommand PatientEditCommand { get; }

        public PatientListViewModel()
        {
            _dialogService = new Dialog();
            PatientAddCommand = new RelayCommand(AddPatient);
            PatientEditCommand = new RelayCommand(EditPatient);
            SyncClickCommand = new RelayCommand(SyncButtonClicked);
            LoadPatients();
        }

        public void LoadPatients()
        {
            try
            {
                var repo = new DB_Manager();
                List<PatientModel> data = repo.GetAllPatients();
                RegisteredPatients = new ObservableCollection<PatientModel>(data);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류 발생: {ex.Message}");
            }
        }

        private void AddPatient()
        {
            var vm = new PatientAddViewModel();
            var result = _dialogService.ShowDialog(vm);
            if (result == true)
            {
                LoadPatients();
            }
        }

        private void EditPatient()
        {
            var vm = new PatientEditViewModel();
            var result = _dialogService.ShowDialog(vm);
        }

        private void SyncButtonClicked()
        {
            MessageBox.Show("당일 예약된 EMR 환자 정보가 최신 상태로 업데이트되었습니다 .");
        }
    }
}
