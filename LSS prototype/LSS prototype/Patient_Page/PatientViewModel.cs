using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using LSS_prototype.DB_CRUD;
using LSS_prototype.User_Page;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using LSS_prototype.Auth;
using System.Threading;

namespace LSS_prototype.Patient_Page
{
    /// <summary>
    /// 환자 목록 관리 및 CRUD(생성, 조회, 수정, 삭제) 기능 수행을 위한 로직
    /// 2026-02-09 서민지
    /// </summary>
    internal class PatientViewModel : INotifyPropertyChanged
    {
        private readonly SearchDebouncer _searchDebouncer;
        private readonly IDialogService _dialogService;
        private CancellationTokenSource _cts = new CancellationTokenSource(); 
        // 위 변수 사용이유
        // dicom 이 연결끊킨 상태일때 한번의 task가 실행되고 -> 두번째 Patient 호출 시 -> 에러발생 
        // 첫번째때 연결된 task를 끊어주기위해서 0306 박한용



        private string _searchText;

        private ObservableCollection<PatientModel> _Patients = new ObservableCollection<PatientModel>();
        public ObservableCollection<PatientModel> Users
        {
            get { return _Patients; }
            set
            {
                _Patients = value;
                OnPropertyChanged();
            }
        }
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();

                // 입력 바뀔 때마다 디바운서에 전달 → 0.5초 후 DB 검색
                _searchDebouncer.OnTextChanged(value);
            }
        }

        private UserModel _selectedUser;
        public UserModel SelectedUser
        {
            get { return _selectedUser; }
            set
            {
                _selectedUser = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        // Patients (EMR + LOCAL 담는 리스트)
        private ObservableCollection<PatientModel> _patients;

        // EMR(DICOM)에서 받아온 예약 환자 목록
        private List<PatientModel> _emrPatients = new List<PatientModel>();

        // SQLite에서 받아온 당일 접수 환자 목록
        private List<PatientModel> _localPatients = new List<PatientModel>();
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
        public string PageTitle => _showAll ? "Integrated Patient" : "EMR Patient";

        // 체크박스 바인딩용 - FALSE: EMR만 / TRUE: EMR + LOCAL
        private bool _showAll = false;
        public bool ShowAll
        {
            get => _showAll;
            set
            {
                if (_showAll == value) return;
                _showAll = value;
                _searchText = string.Empty;
                OnPropertyChanged(nameof(SearchText)); // UI 검색창도 같이 초기화
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageTitle)); // 페이지이름 변경 함수호출 
                RefreshPatients(); // 토글될 때마다 화면 즉시 갱신
            }
        }

        // ===== Commands =====
        public ICommand PatientAddCommand { get; }
        public ICommand PatientEditCommand { get; }
        public ICommand PatientDeleteCommand { get; }
        public ICommand EmrSyncCommand { get; }

        public ICommand ImportCommand { get; }
        public ICommand NavScanCommand { get; }
        public ICommand NavImageReviewCommand { get; }
        public ICommand NavVideoReviewCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand ExitCommand { get; }

        public PatientViewModel()
        {
            _dialogService = new Dialog();

            PatientAddCommand = new RelayCommand(AddPatient);
            PatientEditCommand = new RelayCommand(EditPatient);
            PatientDeleteCommand = new RelayCommand(DeletePatient);
            EmrSyncCommand = new AsyncRelayCommand(async _ => await EmrSync());
            ImportCommand = new RelayCommand(ImportPatient);
            LogoutCommand = new RelayCommand(Common.ExecuteLogout);
            ExitCommand = new RelayCommand(Common.ExcuteExit);

            NavScanCommand = new RelayCommand(_ => MainPage.Instance.NavigateTo(new Scan_Page.Scan()));
            // 0227 박한용 아래코드는 데이터 관련 처리 완료 후 주석 풀고 연동 예정 
            //NavImageReviewCommand = new RelayCommand(_ => MainPage.Instance.NavigateTo(new ImageReview_Page.ImageReview()));
            //NavVideoReviewCommand = new RelayCommand(_ => MainPage.Instance.NavigateTo(new VideoReview_Page.VideoReview()));
            _searchDebouncer = new SearchDebouncer(ExecuteSearch, delayMs: 500);
            LoadPatients();
            _ = EmrSync(_cts.Token); // task 무시하기위해 _ = 사용 (별의미 X )

        }

        

        /// <summary>
        /// DB에서 로컬등록된  환자 목록을 불러와 최신순(내림차순)으로 UI에 반영
        /// </summary>
        public void LoadPatients()
        {
            try
            {
                var repo = new DB_Manager();
                List<PatientModel> data = repo.GetAllPatients();
                _localPatients = data;
                RefreshPatients();
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        /// <summary>
        /// ShowAll 상태에 따라 표시할 환자 목록을 갱신
        /// FALSE → EMR만 / TRUE → LOCAL만 ( E-SYNC + LOCAL )
        /// </summary>
        private void RefreshPatients()
        {
            var combined = _showAll
                ? _localPatients
                : _emrPatients;

            Patients = new ObservableCollection<PatientModel>(combined);
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


            if(!string.IsNullOrWhiteSpace(SelectedPatient.AccessionNumber))
            {
                CustomMessageWindow.Show("EMR 데이터는 수정이 \n 불가능합니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Warning);
                return;
            }
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

                if (!string.IsNullOrWhiteSpace(SelectedPatient.AccessionNumber))
                {
                    CustomMessageWindow.Show("EMR 데이터는 삭제가 \n 불가능합니다.",
                            CustomMessageWindow.MessageBoxType.AutoClose, 1,
                            CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                if (CustomMessageWindow.Show(
                        $"{SelectedPatient.PatientName} 환자 정보를 삭제하시겠습니까?",
                        CustomMessageWindow.MessageBoxType.YesNo, 0, CustomMessageWindow.MessageIconType.Info
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
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private async void ImportPatient()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "DICOM files (*.dcm)|*.dcm",
                Title = "DICOM 파일 선택",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // 1. 로딩 표시 시작 (필요 시)
                // LoadingWindow.Begin("파일 가져오기 중...");

                try
                {
                    string targetFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM");
                    if (!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                    }

                    // 2. 파일 복사 작업을 백그라운드 스레드에서 수행 (CPU/IO 바운드 작업)
                    await Task.Run(() =>
                    {
                        var repoInside = new DB_Manager();
                        foreach (string sourcePath in openFileDialog.FileNames)
                        {
                            try
                            {
                                DicomFile dicomFile = DicomFile.Open(sourcePath);

                                // 1. 변수 선언: DICOM 태그에서 문자열 읽기 (이 줄이 반드시 위에 있어야 합니다)
                                string pBirthStr = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, "19000101");

                                string pName = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.PatientName, "Unknown Name");
                                string pSex = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.PatientSex, "U");
                                string pCodeStr = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.PatientID, "0");

                                // 2. 날짜 변환 (pBirthStr 사용)
                                if (!DateTime.TryParseExact(pBirthStr, "yyyyMMdd",
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.None,
                                    out DateTime birthDate))
                                {
                                    birthDate = new DateTime(1900, 1, 1); // 변환 실패 시 기본값
                                }

                                // 파일 복사
                                string fileName = Path.GetFileName(sourcePath);
                                string targetPath = Path.Combine(targetFolder, fileName);
                                File.Copy(sourcePath, targetPath, true);

                                // DB 저장
                                if (int.TryParse(pCodeStr, out int pCode))
                                {
                                    var patientModel = new PatientModel
                                    {
                                        PatientCode = pCode,
                                        PatientName = pName,
                                        Sex = pSex,
                                        BirthDate = birthDate // 변환된 DateTime 객체 사용
                                    };
                                    repoInside.AddPatient(patientModel);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"에러: {ex.Message}");
                            }
                        }
                    });

                    // 3. DB에서 데이터를 다시 불러오는 작업 (비동기 처리 권장)
                    var repo = new DB_Manager();

                    // Task.Run을 사용하여 DB 조회 시 UI 프리징 방지
                    var updatedList = await Task.Run(() => repo.GetAllPatients());

                    // 4. UI 스레드에서 결과 반영
                    _localPatients = updatedList;
                    RefreshPatients();

                    CustomMessageWindow.Show("임포트가 완료되었습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Info);
                }
                catch (Exception ex)
                {
                    Common.WriteLog(ex);
                    CustomMessageWindow.Show($"오류 발생: {ex.Message}",
                        CustomMessageWindow.MessageBoxType.Ok, 0,
                        CustomMessageWindow.MessageIconType.Danger);
                }
                finally
                {
                    // LoadingWindow.End();
                }
            }
        }

        private async Task EmrSync(CancellationToken ct = default)
        {
            try
            {
                var db = new DB_Manager();
                var pacsSet = db.GetPacsSet();

                LoadingWindow.Begin("MWL 조회 중...");
                var worklistItems = await db.GetWorklistPatientsAsync(
                    pacsSet.MwlMyAET, pacsSet.MwlIP, pacsSet.MwlPort, pacsSet.MwlAET);
                await Task.Delay(500); // 로딩바 테스트 차 0.5 delay 추후 배포 시 해당코드 삭제

                // TODO: LS / LSS 간 표시 데이터 차이 확인 후 바인딩 필드 정리 필요 0227 박한용
                _emrPatients = worklistItems;

                RefreshPatients();
                CustomMessageWindow.Show("EMR 동기화 완료되었습니다.",
                            CustomMessageWindow.MessageBoxType.AutoClose, 1,
                            CustomMessageWindow.MessageIconType.Info);
            }
            catch (TimeoutException ex)
            {
                Common.WriteLog(ex);
                await CustomMessageWindow.ShowAsync(
                    "DICOM 서버가 응답하지 않습니다.\n네트워크 또는 서버 상태를 확인해주세요.",
                    CustomMessageWindow.MessageBoxType.Ok, 0,
                    CustomMessageWindow.MessageIconType.Warning);
            }
            catch (OperationCanceledException) { } // task 해제되는 경우 

            catch (Exception ex)
            {
                Common.WriteLog(ex);
                await CustomMessageWindow.ShowAsync(
                    $"MWL 조회 실패:\n{ex.Message}",
                    CustomMessageWindow.MessageBoxType.Ok, 0,
                    CustomMessageWindow.MessageIconType.Warning);
            }
            
            finally
            {
                LoadingWindow.End();
            }
        }


        public void Dispose()
        {
            _searchDebouncer?.Dispose();
        }

        public void OnSearchTextChanged(string text)
        {
            _searchDebouncer.OnTextChanged(text);
        }

        private void ExecuteSearch(string keyword)
        {
            try
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    int? selectedId = SelectedPatient?.PatientId;

                    if (string.IsNullOrWhiteSpace(keyword))
                    {
                        // 검색어 없으면 원래 목록으로 복원
                        RefreshPatients();

                        // 검색어 지울 땐 선택 해제 ( 첫번째 항목 자동선택 방지 )
                        SelectedPatient = null;
                        return;
                    }

                    // 검색 대상 결정: 전체(_showAll) or EMR만
                    var source = _showAll
                        ? _emrPatients.Concat(_localPatients)
                        : _emrPatients;

                    string kwNoSpace = keyword.Replace(" ", "");

                    Patients = new ObservableCollection<PatientModel>(
                        source.Where(p => MatchesKeyword(p, keyword, kwNoSpace))
                    );

                    // 검색 중일 땐 기존 선택 항목 유지
                    if (selectedId.HasValue)
                        SelectedPatient = Patients.FirstOrDefault(p => p.PatientId == selectedId.Value);
                });
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        // 환자 한 명이 검색 조건에 맞는지 판단
        private bool MatchesKeyword(PatientModel p, string keyword, string kwNoSpace)
        {
            // 이름 공백 제거 버전 ( ParkHan으로 Park Hanyong 검색 )
            string nameNoSpace = (p.PatientName ?? "").Replace(" ", "");

            return
                // 이름: 공백제거 후 대소문자 무시
                nameNoSpace.IndexOf(kwNoSpace, StringComparison.OrdinalIgnoreCase) >= 0 ||
                // 이름: 원본 그대로 대소문자 무시 ( park hanyong → Park Hanyong )
                (p.PatientName ?? "").IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                // 환자코드
                p.PatientCode.ToString().Contains(keyword);
            // 접수번호 ( 사용 미지시 일단 주석 )
            // || (p.AccessionNumber ?? "").Contains(keyword);
        }
    }
}
