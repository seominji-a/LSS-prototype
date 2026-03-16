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
using Forms = System.Windows.Forms;

using LSS_prototype.Auth;
using System.Threading;
using LSS_prototype.Dicom_Module;

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

        // DICOM import된 EMR 환자
        private List<PatientModel> _importedEmrPatients = new List<PatientModel>();

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

            NavScanCommand = new RelayCommand(NavScan);
            // 0227 박한용 아래코드는 데이터 관련 처리 완료 후 주석 풀고 연동 예정 
            //NavImageReviewCommand = new RelayCommand(_ => MainPage.Instance.NavigateTo(new ImageReview_Page.ImageReview()));
            //NavVideoReviewCommand = new RelayCommand(_ => MainPage.Instance.NavigateTo(new VideoReview_Page.VideoReview()));
            _searchDebouncer = new SearchDebouncer(ExecuteSearch, delayMs: 500);
            LoadPatients();
            _ = EmrSync(_cts.Token); // task 무시하기위해 _ = 사용 (별의미 X )

        }

        private void NavScan()
        {

            if(SelectedPatient == null)
            {
                CustomMessageWindow.Show("환자를 먼저 선택해주세요.",
                      CustomMessageWindow.MessageBoxType.AutoClose, 2,
                      CustomMessageWindow.MessageIconType.Warning);

                return;
            }
            MainPage.Instance.NavigateTo(new Scan_Page.Scan(SelectedPatient));

        }



        /// <summary>
        /// DB에서 로컬등록된  환자 목록을 불러와 최신순(내림차순)으로 UI에 반영
        /// </summary>
        public void LoadPatients()
        {
            try
            {
                var repo = new DB_Manager();
                _localPatients = repo.GetAllPatients();

                // DB에서 읽은 건 전부 LOCAL로 간주
                foreach (var p in _localPatients)
                {
                    p.IsEmrPatient = false;
                    p.Source = PatientSource.ImportLocal;
                    p.AccessionNumber = string.Empty;
                }

                // DICOM 폴더에서 EMR 환자 따로 로드
                _importedEmrPatients = LoadImportedEmrPatientsFromDicomFolder();

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
            IEnumerable<PatientModel> list;

            if (_showAll)
            {
                // Integrated = LOCAL + import된 EMR만
                list = _importedEmrPatients.Concat(_localPatients);
            }
            else
            {
                // EMR만
                list = _emrPatients;
            }

            Patients = new ObservableCollection<PatientModel>(list);
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
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = "가져올 환자 폴더를 선택하세요";

                if (dialog.ShowDialog() != Forms.DialogResult.OK)
                    return;

                string sourcePatientFolder = dialog.SelectedPath;

                try
                {
                    string sourceFolderName = Path.GetFileName(sourcePatientFolder);

                    if (string.IsNullOrWhiteSpace(sourceFolderName) || !sourceFolderName.Contains("_"))
                    {
                        CustomMessageWindow.Show(
                            "환자 폴더 형식이 올바르지 않습니다.\n'환자이름_환자번호' 폴더를 선택해주세요.",
                            CustomMessageWindow.MessageBoxType.Ok,
                            0,
                            CustomMessageWindow.MessageIconType.Warning);
                        return;
                    }

                    // 대표 DICOM 1개만 읽어서 환자 정보 파악
                    string firstDcm = Directory.EnumerateFiles(
                        sourcePatientFolder,
                        "*.dcm",
                        SearchOption.AllDirectories).FirstOrDefault();

                    if (string.IsNullOrEmpty(firstDcm))
                    {
                        CustomMessageWindow.Show(
                            "선택한 폴더 안에 DICOM 파일이 없습니다.",
                            CustomMessageWindow.MessageBoxType.Ok,
                            0,
                            CustomMessageWindow.MessageIconType.Warning);
                        return;
                    }

                    var dcm = DicomFile.Open(firstDcm);

                    string pBirthStr = dcm.Dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, "19000101");
                    string pName = dcm.Dataset.GetSingleValueOrDefault(DicomTag.PatientName, "Unknown Name");
                    string pSex = dcm.Dataset.GetSingleValueOrDefault(DicomTag.PatientSex, "U");
                    string pCodeStr = dcm.Dataset.GetSingleValueOrDefault(DicomTag.PatientID, "0");
                    string pAccess = dcm.Dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);

                    if (!DateTime.TryParseExact(
                            pBirthStr,
                            "yyyyMMdd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out DateTime birthDate))
                    {
                        birthDate = new DateTime(1900, 1, 1);
                    }

                    if (!int.TryParse(pCodeStr, out int pCode))
                    {
                        CustomMessageWindow.Show(
                            "환자 번호를 읽을 수 없습니다.",
                            CustomMessageWindow.MessageBoxType.Ok,
                            0,
                            CustomMessageWindow.MessageIconType.Warning);
                        return;
                    }

                    bool isEmrImport = !string.IsNullOrWhiteSpace(pAccess);

                    string dicomRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM");
                    Directory.CreateDirectory(dicomRoot);

                    // E-SYNC 기준 폴더명 = import된 EMR 환자 이름/번호 기준
                    string emrTargetFolder = Path.Combine(dicomRoot, $"{pName}_{pCode}");
                    string finalTargetFolder = emrTargetFolder;

                    var repo = new DB_Manager();
                    var localPatient = _localPatients.FirstOrDefault(x => x.PatientCode == pCode);

                    // 1. EMR import + 동일 LOCAL 환자 존재 → 병합 여부 확인
                    // 1. EMR import + 동일 LOCAL 환자 존재 → 병합 여부 확인
                    if (isEmrImport && localPatient != null)
                    {
                        var result = CustomMessageWindow.Show(
                            $"환자 번호 {pCode}와 동일한 LOCAL 환자가 존재합니다.\nE-SYNC로 병합하시겠습니까?",
                            CustomMessageWindow.MessageBoxType.YesNo,
                            0,
                            CustomMessageWindow.MessageIconType.Warning);

                        if (result == CustomMessageWindow.MessageBoxResult.Yes)
                        {
                            string localFolder = FindPatientFolder(localPatient);

                            await Task.Run(() =>
                            {
                                if (!string.IsNullOrWhiteSpace(localFolder) &&
                                    Directory.Exists(localFolder) &&
                                    !string.Equals(
                                        Path.GetFullPath(localFolder).TrimEnd('\\'),
                                        Path.GetFullPath(emrTargetFolder).TrimEnd('\\'),
                                        StringComparison.OrdinalIgnoreCase))
                                {
                                    CopyDirectory(localFolder, emrTargetFolder, overwrite: true);

                                    try
                                    {
                                        Directory.Delete(localFolder, true);
                                    }
                                    catch (Exception ex)
                                    {
                                        Common.WriteLog(ex);
                                    }
                                }

                                CopyDirectory(sourcePatientFolder, emrTargetFolder, overwrite: true);
                                NormalizeDicomFileNamesRecursively(emrTargetFolder, pName, pCode);
                            });

                            // 병합했으므로 LOCAL 삭제
                            repo.DeletePatient(localPatient.PatientId);
                        }
                        else
                        {
                            CustomMessageWindow.Show(
                                "LOCAL 환자의 환자 번호를 반드시 수정하여 병합해주십시오.",
                                CustomMessageWindow.MessageBoxType.Ok,
                                0,
                                CustomMessageWindow.MessageIconType.Warning);

                            emrTargetFolder = Path.Combine(dicomRoot, $"{pName}_{pCode}");

                            if (Directory.Exists(emrTargetFolder))
                            {
                                var overwrite = CustomMessageWindow.Show(
                                    $"동일한 E-SYNC 폴더가 이미 존재합니다.\n덮어쓰시겠습니까?",
                                    CustomMessageWindow.MessageBoxType.YesNo,
                                    0,
                                    CustomMessageWindow.MessageIconType.Warning);

                                if (overwrite != CustomMessageWindow.MessageBoxResult.Yes)
                                    return;
                            }

                            await Task.Run(() =>
                            {
                                CopyDirectory(sourcePatientFolder, emrTargetFolder, overwrite: true);
                                NormalizeDicomFileNamesRecursively(emrTargetFolder, pName, pCode);
                            });

                            // LOCAL은 유지, EMR은 폴더 스캔으로만 표시
                        }
                    }
                    // 2. EMR import + 같은 LOCAL 없음
                    else if (isEmrImport)
                    {
                        if (Directory.Exists(emrTargetFolder))
                        {
                            var overwrite = CustomMessageWindow.Show(
                                $"동일한 E-SYNC 폴더가 이미 존재합니다.\n덮어쓰시겠습니까?",
                                CustomMessageWindow.MessageBoxType.YesNo,
                                0,
                                CustomMessageWindow.MessageIconType.Warning);

                            if (overwrite != CustomMessageWindow.MessageBoxResult.Yes)
                                return;
                        }

                        await Task.Run(() =>
                        {
                            CopyDirectory(sourcePatientFolder, emrTargetFolder, overwrite: true);
                            NormalizeDicomFileNamesRecursively(emrTargetFolder, pName, pCode);
                        });

                        // 여기서는 DB 저장 절대 하면 안 됨
                    }
                    // 3. LOCAL import
                    else
                    {
                        if (localPatient != null)
                        {
                            CustomMessageWindow.Show(
                                $"동일한 환자 번호({pCode})의 LOCAL 환자가 이미 존재합니다.",
                                CustomMessageWindow.MessageBoxType.Ok,
                                0,
                                CustomMessageWindow.MessageIconType.Warning);
                            return;
                        }

                        string localTargetFolder = Path.Combine(dicomRoot, $"{pName}_{pCode}");

                        if (Directory.Exists(localTargetFolder))
                        {
                            var overwrite = CustomMessageWindow.Show(
                                $"동일한 환자 폴더가 이미 존재합니다.\n덮어쓰시겠습니까?",
                                CustomMessageWindow.MessageBoxType.YesNo,
                                0,
                                CustomMessageWindow.MessageIconType.Warning);

                            if (overwrite != CustomMessageWindow.MessageBoxResult.Yes)
                                return;
                        }

                        await Task.Run(() =>
                        {
                            CopyDirectory(sourcePatientFolder, localTargetFolder, overwrite: true);
                            NormalizeDicomFileNamesRecursively(localTargetFolder, pName, pCode);
                        });

                        var patientModel = new PatientModel
                        {
                            PatientCode = pCode,
                            PatientName = pName,
                            Sex = pSex,
                            BirthDate = birthDate,
                            AccessionNumber = string.Empty,
                            Source = PatientSource.ImportLocal,
                            IsEmrPatient = false
                        };

                        repo.AddPatient(patientModel);
                    }

                    // 화면 갱신
                    _localPatients = repo.GetAllPatients();
                    foreach (var p in _localPatients)
                    {
                        p.IsEmrPatient = false;
                        p.Source = PatientSource.ImportLocal;
                        p.AccessionNumber = string.Empty;
                    }

                    _importedEmrPatients = LoadImportedEmrPatientsFromDicomFolder();
                    RefreshPatients();

                    CustomMessageWindow.Show(
                        "환자 폴더 임포트가 완료되었습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose,
                        1,
                        CustomMessageWindow.MessageIconType.Info);
                }
                catch (Exception ex)
                {
                    Common.WriteLog(ex);
                    CustomMessageWindow.Show(
                        $"오류 발생: {ex.Message}",
                        CustomMessageWindow.MessageBoxType.Ok,
                        0,
                        CustomMessageWindow.MessageIconType.Danger);
                }
            }
        }

        private async Task EmrSync(CancellationToken ct = default)
        {
            try
            {
                var db = new DB_Manager();
                var pacsSet = db.GetPacsSet();

                var dicom = new DicomManager();
                

                LoadingWindow.Begin("MWL 조회 중...");
                var worklistItems = await dicom.GetWorklistPatientsAsync(pacsSet.MwlMyAET, pacsSet.MwlIP, pacsSet.MwlPort, pacsSet.MwlAET);
                await Task.Delay(500); // 로딩바 테스트 차 0.5 delay 추후 배포 시 해당코드 삭제

                // TODO: LS / LSS 간 표시 데이터 차이 확인 후 바인딩 필드 정리 필요 0227 박한용
                _emrPatients = worklistItems;

                foreach (var p in _emrPatients)
                {
                    p.Source = PatientSource.EmrImported;
                    p.IsEmrPatient = true; 
                }
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
                        ? _importedEmrPatients.Concat(_localPatients)
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

       

        private List<PatientModel> LoadImportedEmrPatientsFromDicomFolder()
        {
            var list = new List<PatientModel>();
            string dicomFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM");

            if (!Directory.Exists(dicomFolder))
                return list;

            foreach (var file in Directory.EnumerateFiles(dicomFolder, "*.dcm", SearchOption.AllDirectories))
            {
                try
                {
                    var dcm = DicomFile.Open(file);

                    string acc = dcm.Dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);
                    if (string.IsNullOrWhiteSpace(acc))
                        continue; // accession 없으면 EMR로 안 봄

                    string pid = dcm.Dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty);
                    string pname = dcm.Dataset.GetSingleValueOrDefault(DicomTag.PatientName, string.Empty);
                    string sex = dcm.Dataset.GetSingleValueOrDefault(DicomTag.PatientSex, "U");
                    string birth = dcm.Dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, "19000101");

                    if (!int.TryParse(pid, out int patientCode))
                        continue;

                    DateTime birthDate;
                    if (!DateTime.TryParseExact(
                            birth,
                            "yyyyMMdd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out birthDate))
                    {
                        birthDate = new DateTime(1900, 1, 1);
                    }

                    // accession 기준 중복 제거
                    bool exists = list.Any(x => x.AccessionNumber == acc);
                    if (exists)
                        continue;

                    list.Add(new PatientModel
                    {
                        PatientId = -1, // DB 환자가 아님
                        PatientCode = patientCode,
                        PatientName = pname,
                        BirthDate = birthDate,
                        Sex = sex,
                        AccessionNumber = acc,
                        IsEmrPatient = true,
                        Source = PatientSource.ImportEmr
                    });
                }
                catch (Exception ex)
                {
                    Common.WriteLog(ex);
                }
            }

            return list;
        }

       

        

        private void CopyDirectory(string sourceDir, string targetDir, bool overwrite)
        {
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName);
                File.Copy(file, destFile, overwrite);
            }

            foreach (string directory in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(directory);
                string destDir = Path.Combine(targetDir, dirName);
                CopyDirectory(directory, destDir, overwrite);
            }
        }

        private string FindPatientFolder(PatientModel patient)
        {
            try
            {
                string dicomRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM");
                if (!Directory.Exists(dicomRoot))
                    return null;

                string expectedFolderName = $"{patient.PatientName}_{patient.PatientCode}";
                string directPath = Path.Combine(dicomRoot, expectedFolderName);

                if (Directory.Exists(directPath))
                    return directPath;

                return Directory.GetDirectories(dicomRoot)
                    .FirstOrDefault(x =>
                        string.Equals(
                            Path.GetFileName(x),
                            expectedFolderName,
                            StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                return null;
            }
        }

        private void NormalizeDicomFileNames(string folder, string patientName, int patientCode)
        {
            try
            {
                var files = Directory.GetFiles(folder, "*.dcm")
                    .OrderBy(f => f)
                    .ToList();

                if (files.Count == 0)
                    return;

                // 폴더 구조 예:
                // DICOM/환자명_번호/20260316/202603160001/Image
                // studyId = 202603160001
                string studyId = new DirectoryInfo(folder).Parent?.Name ?? "000000000000";

                int index = 1;

                foreach (var file in files)
                {
                    string newName = $"{patientName}_{patientCode}_{studyId}_{index}.dcm";
                    string newPath = Path.Combine(folder, newName);

                    if (string.Equals(file, newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        index++;
                        continue;
                    }

                    while (File.Exists(newPath))
                    {
                        index++;
                        newName = $"{patientName}_{patientCode}_{studyId}_{index}.dcm";
                        newPath = Path.Combine(folder, newName);
                    }

                    File.Move(file, newPath);
                    index++;
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private void NormalizeDicomFileNamesRecursively(string rootFolder, string patientName, int patientCode)
        {
            try
            {
                if (!Directory.Exists(rootFolder))
                    return;

                var allDirs = Directory.GetDirectories(rootFolder, "*", SearchOption.AllDirectories)
                    .Prepend(rootFolder);

                foreach (var dir in allDirs)
                {
                    if (Directory.GetFiles(dir, "*.dcm").Any())
                    {
                        NormalizeDicomFileNames(dir, patientName, patientCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }
    }
}
