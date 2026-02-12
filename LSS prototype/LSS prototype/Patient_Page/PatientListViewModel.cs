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

        //로드에 관한 처리
        private ObservableCollection<PatientModel> _registeredPatients;
        public ObservableCollection<PatientModel> RegisteredPatients
        {
            get => _registeredPatients;
            set { _registeredPatients = value;
                OnPropertyChanged();
            }
        }

        //환자 리스트에서 환자 데이터 선택에 관한 처리
        private PatientModel _selectedPatient;
        public PatientModel SelectedPatient
        {
            get => _selectedPatient;
            set
            {
                _selectedPatient = value;
                OnPropertyChanged(); // 선택 변경 알림
            }
        }

        public ICommand PatientAddCommand { get; }
        public ICommand SyncClickCommand { get; }

        public ICommand PatientEditCommand { get; }

        public ICommand PatientDeleteCommand { get; }

        public PatientListViewModel()
        {
            _dialogService = new Dialog();
            PatientAddCommand = new RelayCommand(AddPatient);
            PatientEditCommand = new RelayCommand(EditPatient);
            PatientDeleteCommand = new RelayCommand(DeletePatient);
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
                new CustomMessageWindow($"데이터 로드 중 오류 발생{ex.Message}").Show();
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
            // 1. 선택된 환자가 있는지 검사
            if (SelectedPatient == null)
            {
                new CustomMessageWindow("수정할 환자를 선택해주세요.").Show();
                return;
            }

            // 2. 수정용 ViewModel 생성 및 선택된 데이터 전달
            // 생성자를 통해 데이터를 넘기거나 프로퍼티로 복사해줍니다.
            var vm = new PatientEditViewModel(SelectedPatient);

            // 3. 다이얼로그 표시 및 결과 확인
            var result = _dialogService.ShowDialog(vm);

            // 4. 수정 성공(true) 시 리스트 다시 불러오기
            if (result == true)
            {
                LoadPatients();
            }
        }

        private void DeletePatient()
        {
            // 1. 선택된 환자가 있는지 확인
            if (SelectedPatient == null)
            {
                MessageBox.Show("삭제할 환자를 선택해주세요.");
                return;
            }

            // 2. 삭제 확인 메시지
            if (MessageBox.Show($"{SelectedPatient.Name} 환자 정보를 삭제하시겠습니까?", "삭제 확인",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                var repo = new DB_Manager();
                // 3. 모델에 담긴 PatientId를 넘겨줍니다.
                if (repo.DeletePatient(SelectedPatient.PatientId))
                {
                    MessageBox.Show("삭제되었습니다.");
                    LoadPatients(); // 목록 새로고침
                }
            }
        }

        private void SyncButtonClicked()
        {
            new CustomMessageWindow("당일 예약된 EMR 환자 정보가 최신 상태로 업데이트되었습니다.").Show();
        }
    }
}
