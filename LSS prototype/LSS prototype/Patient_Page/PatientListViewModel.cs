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

/// <summary>
/// 환자 목록 관리 및 CRUD(생성, 조회, 수정, 삭제) 기능 수행을 위한 로직
/// 2026-02-09 서민지
/// </summary>

namespace LSS_prototype
{
    internal class PatientListViewModel : INotifyPropertyChanged  /// 환자 목록 화면을 관리하는 PatientListViewModel 클래스
    {
        private readonly IDialogService _dialogService; // 다이얼로그 처리를 위한 서비스

        public event PropertyChangedEventHandler PropertyChanged;

 
        protected void OnPropertyChanged([CallerMemberName] string name = null) /// 속성 값이 변경되었을 때 UI에 알리는 메서드
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // --- 데이터 속성 (Properties) ---

        private ObservableCollection<PatientModel> _registeredPatients; /// 등록된 환자 목록 관련 UI의 DataGrid와 바인딩
        public ObservableCollection<PatientModel> RegisteredPatients 
        {
            get => _registeredPatients;
            set { _registeredPatients = value;
                OnPropertyChanged();
            }
        }

        private PatientModel _selectedPatient; //환자 리스트에서 현재 선택된 환자 정보  처리
        public PatientModel SelectedPatient
        {
            get => _selectedPatient;
            set
            {
                _selectedPatient = value;
                OnPropertyChanged(); // 선택 변경 알림
            }
        }

        // --- 커맨드 (Commands: UI 버튼 등과 연결) ---
        public ICommand PatientAddCommand { get; } // 환자 추가
        public ICommand SyncClickCommand { get; } // EMR 동기화

        public ICommand PatientEditCommand { get; } // 환자 수정

        public ICommand PatientDeleteCommand { get; } // 환자 삭제

       
        public PatientListViewModel()  /// 서비스 초기화 및 커맨드 바인딩
        {
            _dialogService = new Dialog();

            // RelayCommand 통한 로직 연결
            PatientAddCommand = new RelayCommand(AddPatient);
            PatientEditCommand = new RelayCommand(EditPatient);
            PatientDeleteCommand = new RelayCommand(DeletePatient);
            SyncClickCommand = new AsyncRelayCommand(async _ => await SyncButtonClicked());
            LoadPatients();
        }

        // --- 로직 처리---
        public void LoadPatients() /// DB에서 전체 환자 목록을 불러와 ObservableCollection에 할당
        {
            try
            {
                var repo = new DB_Manager();
                List<PatientModel> data = repo.GetAllPatients();
                // ObservableCollection으로 변환하여 UI 자동 갱신 지원
                RegisteredPatients = new ObservableCollection<PatientModel>(data);
            }
            catch (Exception ex)
            {
                new CustomMessageWindow($"데이터 로드 중 오류 발생{ex.Message}").Show();
            }
        }

        private void AddPatient() /// 환자 등록 다이얼로그 호출 및 저장 로직
        {
            var vm = new PatientAddViewModel();

            if (_dialogService.ShowDialog(vm) == true) // 등록 form 다이얼로그 표시
            {
                var confirm = CustomMessageWindow.Show(
                    $"{vm.PatientName} 환자 정보를 생성하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo
                    ); // 최종 저장 여부 확인

                if (confirm == CustomMessageWindow.MessageBoxResult.Yes) // 최종 저장 여부에 관하여 수락
                {
                    var repo = new DB_Manager();

                    bool result = repo.AddPatient(vm); //DB에 환자 등록처리 결과 값

                    if (result) //DB에 환자 등록이 정상적으로 이루어 졌을 경우
                    {
                        CustomMessageWindow.Show("환자가 정상적으로 등록되었습니다."); //환자 등록 완료에 관한 확인 메세지 표시
                        LoadPatients(); //DB에 존재하는 환자 데이터에 맞춰 UI의 환자 리스트 갱신
                    }
                    else //DB에 환자 등록이 정상적으로 이루어 지지 않았을 경우
                    {
                        CustomMessageWindow.Show("등록 중 오류가 발생했습니다.");  //환자 등록 중 발생한 오류에 관한 경고 메세지 표시
                    }
                }
            }
        }

        private void EditPatient() //선택된 환자 정보를 수정하는 로직
        {
            //선택된 환자가 있는지 검사
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
                new CustomMessageWindow("삭제할 환자를 선택해주세요.").Show();
                return;
            }

            // 2. 삭제 확인 메시지
            if (CustomMessageWindow.Show( $"{SelectedPatient.Name} 환자 정보를 삭제하시겠습니까?", CustomMessageWindow.MessageBoxType.YesNo)== CustomMessageWindow.MessageBoxResult.Yes)
            {
                var repo = new DB_Manager();

                if (repo.DeletePatient(SelectedPatient.PatientId))
                {
                    CustomMessageWindow.Show("삭제되었습니다.");
                    LoadPatients();
                }
            }
        }

        private async Task SyncButtonClicked()
        {
            await new CustomMessageWindow("EMR 환자 정보가 최신 상태로 업데이트되었습니다.",
                CustomMessageWindow.MessageBoxType.AutoClose,3).ShowAsync();
        }
    }
}
