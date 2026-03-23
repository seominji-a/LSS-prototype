using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using LSS_prototype.Auth;
using LSS_prototype.DB_CRUD;
using LSS_prototype.Dicom_Module;
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Runtime.InteropServices;

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

        private enum ImportActionType
        {
            NewLocalPatient,
            ExistingLocalPatientAddStudy,
            NewEmrPatient,
            ExistingEmrPatientAddStudy,
            SkipDuplicateStudy,
            SkipConflictPatient
        }

        private class ImportPlan
        {
            public PatientModel Group { get; set; }
            public ImportActionType ActionType { get; set; }
            public string Reason { get; set; }

            // 실제 반영 대상 환자 정보
            public PatientModel ExistingPatient { get; set; }
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

            PatientAddCommand = new RelayCommand(async _ => await AddPatient());
            PatientEditCommand = new RelayCommand(async _ => await EditPatient());
            PatientDeleteCommand = new RelayCommand(async _ => await DeletePatient());
            EmrSyncCommand = new AsyncRelayCommand(async _ => await EmrSync());
            ImportCommand = new RelayCommand(async _ => await ImportPatient());
            LogoutCommand = new RelayCommand(async _ => await Common.ExecuteLogout());
            ExitCommand = new RelayCommand(async _ => await Common.ExcuteExit());

            NavScanCommand = new RelayCommand(NavScan);
            // 0227 박한용 아래코드는 데이터 관련 처리 완료 후 주석 풀고 연동 예정 
            //NavImageReviewCommand = new RelayCommand(_ => MainPage.Instance.NavigateTo(new ImageReview_Page.ImageReview()));
            //NavVideoReviewCommand = new RelayCommand(_ => MainPage.Instance.NavigateTo(new VideoReview_Page.VideoReview()));
            _searchDebouncer = new SearchDebouncer(async keyword => await ExecuteSearch(keyword), delayMs: 500);
            _ = EmrSync(_cts.Token); // task 무시하기위해 _ = 사용 (별의미 X )

        }

        public async Task InitializeAsync()
        {
            await LoadPatients();
        }

        private async void NavScan()
        {

            if (SelectedPatient == null)
            {
                await CustomMessageWindow.ShowAsync("환자를 먼저 선택해주세요.",
                      CustomMessageWindow.MessageBoxType.Ok, 2,
                      CustomMessageWindow.MessageIconType.Warning);

                return;
            }
            MainPage.Instance.NavigateTo(new Scan_Page.Scan(SelectedPatient));

        }

        //중복/충돌 판정용 helper 추가
        //환자 동일성 판단
        private bool IsSameLocalPatient(PatientModel existing, PatientModel incoming)
        {
            if (existing == null || incoming == null)
                return false;

            return existing.PatientCode == incoming.PatientCode
                && string.Equals((existing.PatientName ?? "").Trim(), (incoming.PatientName ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
                && existing.BirthDate.Date == incoming.BirthDate.Date;
        }

        //환자 충돌 판단
        //patientcode는 같은데 이름/생년월일이 다른 경우
        private bool IsConflictPatient(PatientModel existing, PatientModel incoming)
        {
            if (existing == null || incoming == null)
                return false;

            if (existing.PatientCode != incoming.PatientCode)
                return false;

            bool sameName = string.Equals((existing.PatientName ?? "").Trim(), (incoming.PatientName ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
            bool sameBirth = existing.BirthDate.Date == incoming.BirthDate.Date;

            return !(sameName && sameBirth);
        }

        //Dicom에서 study 키 추출
        //가능하면 studyinstanceUID, 없으면 studyID, 그것도 없으면 studyDate
        private async Task<string> BuildStudyKey(DicomDataset ds)
        {
            try
            {
                if (ds == null)
                    return string.Empty;

                string studyInstanceUid = ds.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(studyInstanceUid))
                    return "SUI:" + studyInstanceUid;

                string studyId = ds.GetSingleValueOrDefault(DicomTag.StudyID, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(studyId))
                    return "SID:" + studyId;

                string studyDate = ds.GetSingleValueOrDefault(DicomTag.StudyDate, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(studyDate))
                    return "SDATE:" + studyDate;

                return string.Empty;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return string.Empty;
            }
        }

        //import 그룹의 검사 키 집합 구하기
        private async Task<HashSet<string>> GetIncomingStudyKeys(IEnumerable<string> dcmFiles)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var file in dcmFiles ?? Enumerable.Empty<string>())
                {
                    try
                    {
                        if (!File.Exists(file))
                            continue;

                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                        string key = await BuildStudyKey(dicomFile.Dataset);

                        if (!string.IsNullOrWhiteSpace(key))
                            result.Add(key);
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }

            return result;
        }

        //기존 환자 폴더에서 검사 키 집합 구하기
        private async Task<HashSet<string>> GetExistingStudyKeys(string patientRootFolder)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (string.IsNullOrWhiteSpace(patientRootFolder) || !Directory.Exists(patientRootFolder))
                    return result;

                foreach (var file in Directory.GetFiles(patientRootFolder, "*.dcm", SearchOption.AllDirectories))
                {
                    try
                    {
                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                        string key = await BuildStudyKey(dicomFile.Dataset);

                        if (!string.IsNullOrWhiteSpace(key))
                            result.Add(key);
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }

            return result;
        }

        //SOPInstanceUID 기준 파일 중복 제거용
        //같은 검사라도 일부 파일만 중복 가능하니 파일 단위도 거름
        private async Task<HashSet<string>> GetExistingSopInstanceUids(string patientRootFolder)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (string.IsNullOrWhiteSpace(patientRootFolder) || !Directory.Exists(patientRootFolder))
                    return result;

                foreach (var file in Directory.GetFiles(patientRootFolder, "*.dcm", SearchOption.AllDirectories))
                {
                    try
                    {
                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                        string sopUid = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, string.Empty).Trim();

                        if (!string.IsNullOrWhiteSpace(sopUid))
                            result.Add(sopUid);
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }

            return result;
        }

        //import 대상 파일 중 기존 SOPInstanceUID 제외
        private async Task<string[]> FilterNewDicomFiles(string patientName, int patientCode, IEnumerable<string> sourceFiles)
        {
            try
            {
                string dicomRoot = GetDicomRootPath();
                string patientRoot = Path.Combine(dicomRoot, $"{patientName}_{patientCode}");

                var existingKeys = await GetExistingDicomInstanceKeys(patientRoot);
                var result = new List<string>();

                foreach (var file in sourceFiles ?? Enumerable.Empty<string>())
                {
                    try
                    {
                        if (!File.Exists(file))
                            continue;

                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                        string key = await BuildDicomInstanceKey(dicomFile.Dataset);

                        // 키를 못 만들면 일단 신규로 허용
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            result.Add(file);
                            continue;
                        }

                        if (!existingKeys.Contains(key))
                            result.Add(file);
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }

                return result
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return sourceFiles?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
            }
        }

        //import 계획 수립 메서드 추가
        //메서드-그룹별
        //(신규 환자인지, 기존 환자에 새 검사 추가인지,완전 중복 검사인지, 충돌 환자인지) 판단
        private async Task<List<ImportPlan>> BuildImportPlans(List<PatientModel> patientGroups)
        {
            var plans = new List<ImportPlan>();

            try
            {
                foreach (var group in patientGroups)
                {
                    try
                    {
                        if (group == null)
                            continue;

                        var existingPatient = FindExistingPatientForImport(group);
                        var conflictPatient = existingPatient == null ? FindConflictPatientForImport(group) : null;

                        // 1) 충돌 환자
                        if (conflictPatient != null)
                        {
                            plans.Add(new ImportPlan
                            {
                                Group = group,
                                ExistingPatient = conflictPatient,
                                ActionType = ImportActionType.SkipConflictPatient,
                                Reason = $"[충돌] 같은 PatientCode, 다른 환자 정보: {group.PatientName}({group.PatientCode})"
                            });
                            continue;
                        }

                        // 2) 기존 환자 존재 → 파일 단위 중복 여부 판단
                        if (existingPatient != null)
                        {
                            string[] newFiles = await FilterNewDicomFiles(
                                existingPatient.PatientName,
                                existingPatient.PatientCode,
                                group.DcmFiles);

                            bool hasNewFiles = newFiles.Length > 0;

                            bool existingIsEmr =
                                !string.IsNullOrWhiteSpace(existingPatient.AccessionNumber) ||
                                existingPatient.Source == PatientSource.ESync;

                            if (!hasNewFiles)
                            {
                                plans.Add(new ImportPlan
                                {
                                    Group = group,
                                    ExistingPatient = existingPatient,
                                    ActionType = ImportActionType.SkipDuplicateStudy,
                                    Reason = $"[중복파일] {group.PatientName}({group.PatientCode})"
                                });
                            }
                            else
                            {
                                plans.Add(new ImportPlan
                                {
                                    Group = group,
                                    ExistingPatient = existingPatient,
                                    ActionType = existingIsEmr
                                        ? ImportActionType.ExistingEmrPatientAddStudy
                                        : ImportActionType.ExistingLocalPatientAddStudy,
                                    Reason = existingIsEmr
                                        ? $"[기존 EMR 환자] 새 파일 추가: {group.PatientName}"
                                        : $"[기존 LOCAL 환자] 새 파일 추가: {group.PatientName}"
                                });
                            }

                            continue;
                        }

                        // 3) 신규 환자
                        bool isEmr = !string.IsNullOrWhiteSpace(group.AccessionNumber);

                        plans.Add(new ImportPlan
                        {
                            Group = group,
                            ActionType = isEmr
                                ? ImportActionType.NewEmrPatient
                                : ImportActionType.NewLocalPatient,
                            Reason = isEmr
                                ? $"[신규 EMR 환자] {group.PatientName}"
                                : $"[신규 LOCAL 환자] {group.PatientName}"
                        });
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }

            return plans;
        }

        //실제 반영 메서드 추가
        private async Task<bool> ExecuteImportPlan(ImportPlan plan, DB_Manager repo)
        {
            try
            {
                if (plan == null || plan.Group == null)
                    return false;

                var group = plan.Group;

                switch (plan.ActionType)
                {
                    case ImportActionType.NewLocalPatient:
                        {
                            var patientModel = new PatientModel
                            {
                                PatientCode = group.PatientCode,
                                PatientName = group.PatientName,
                                Sex = group.Sex,
                                BirthDate = group.BirthDate,
                                AccessionNumber = string.Empty,
                                Source = PatientSource.Local,
                                IsEmrPatient = false,
                                SourceType = (int)PatientSourceType.Local,
                                LastShootDate = group.LastShootDate,
                                ShotNum = group.ShotNum
                            };

                            repo.AddPatient(patientModel);

                            var newFiles = await FilterNewDicomFiles(group.PatientName, group.PatientCode, group.DcmFiles);
                            if (newFiles.Length > 0)
                            {
                                await ImportPatientFilesToStructuredFolders(
                                    newFiles,
                                    group.PatientName,
                                    group.PatientCode,
                                    string.Empty);
                            }

                            return true;
                        }

                    case ImportActionType.ExistingLocalPatientAddStudy:
                        {
                            var newFiles = await FilterNewDicomFiles(group.PatientName, group.PatientCode, group.DcmFiles);
                            if (newFiles.Length == 0)
                                return false;

                            await ImportPatientFilesToStructuredFolders(
                                newFiles,
                                group.PatientName,
                                group.PatientCode,
                                string.Empty);

                            return true;
                        }

                    case ImportActionType.NewEmrPatient:
                        {
                            var emrPatientModel = new PatientModel
                            {
                                PatientCode = group.PatientCode,
                                PatientName = group.PatientName,
                                Sex = group.Sex,
                                BirthDate = group.BirthDate,
                                AccessionNumber = group.AccessionNumber,
                                IsEmrPatient = true,
                                Source = PatientSource.ESync,
                                SourceType = (int)PatientSourceType.ESync,
                                LastShootDate = group.LastShootDate,
                                ShotNum = group.ShotNum
                            };

                            repo.UpsertEmrPatient(emrPatientModel);

                            var newFiles = await FilterNewDicomFiles(group.PatientName, group.PatientCode, group.DcmFiles);
                            if (newFiles.Length > 0)
                            {
                                await ImportPatientFilesToStructuredFolders(
                                    newFiles,
                                    group.PatientName,
                                    group.PatientCode,
                                    group.AccessionNumber);
                            }

                            return true;
                        }

                    case ImportActionType.ExistingEmrPatientAddStudy:
                        {
                            var emrPatientModel = new PatientModel
                            {
                                PatientCode = group.PatientCode,
                                PatientName = group.PatientName,
                                Sex = group.Sex,
                                BirthDate = group.BirthDate,
                                AccessionNumber = group.AccessionNumber,
                                IsEmrPatient = true,
                                Source = PatientSource.ESync,
                                SourceType = (int)PatientSourceType.ESync,
                                LastShootDate = group.LastShootDate,
                                ShotNum = group.ShotNum
                            };

                            repo.UpsertEmrPatient(emrPatientModel);

                            var newFiles = await FilterNewDicomFiles(group.PatientName, group.PatientCode, group.DcmFiles);
                            if (newFiles.Length == 0)
                                return false;

                            await ImportPatientFilesToStructuredFolders(
                                newFiles,
                                group.PatientName,
                                group.PatientCode,
                                group.AccessionNumber);

                            return true;
                        }

                    case ImportActionType.SkipDuplicateStudy:
                    case ImportActionType.SkipConflictPatient:
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return false;
            }
        }

        private string BuildImportSummaryMessage(
                int successCount,
                int newLocalCount,
                int existingLocalAddStudyCount,
                int newEmrCount,
                int existingEmrAddStudyCount,
                int duplicateStudySkipCount,
                int conflictSkipCount,
                List<string> conflictMessages)
        {
            int skippedCount = duplicateStudySkipCount + conflictSkipCount;

            string message =
                $"환자 파일 가져오기가 완료되었습니다.\n" +
                $"완료: {successCount}명";

            if (skippedCount > 0)
                message += $"\n제외: {skippedCount}명";

            if (skippedCount == 0)
                message += "\n\n모든 데이터가 정상 처리되었습니다.";

            return message;
        }

        //같은 환자 판정 helper 추가

        private bool IsSamePatientIdentity(PatientModel existing, PatientModel incoming)
        {
            if (existing == null || incoming == null)
                return false;

            bool sameCode = existing.PatientCode == incoming.PatientCode;
            bool sameName = string.Equals(
                (existing.PatientName ?? "").Trim(),
                (incoming.PatientName ?? "").Trim(),
                StringComparison.OrdinalIgnoreCase);

            bool sameBirth = existing.BirthDate.Date == incoming.BirthDate.Date;

            return sameCode && sameName && sameBirth;
        }

        //LOCAL / import된 EMR 둘 다 뒤져서 같은 환자를 찾습니다.
        private PatientModel FindExistingPatientForImport(PatientModel group)
        {
            if (group == null)
                return null;

            // 1. accession 있으면 import된 EMR에서 accession 우선
            if (!string.IsNullOrWhiteSpace(group.AccessionNumber))
            {
                var emrByAcc = _importedEmrPatients.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x.AccessionNumber) &&
                    string.Equals(x.AccessionNumber, group.AccessionNumber, StringComparison.OrdinalIgnoreCase));

                if (emrByAcc != null)
                    return emrByAcc;
            }

            // 2. LOCAL에서 환자 동일성 확인
            var localMatch = _localPatients.FirstOrDefault(x => IsSamePatientIdentity(x, group));
            if (localMatch != null)
                return localMatch;

            // 3. import된 EMR에서도 환자 동일성 확인
            var emrMatch = _importedEmrPatients.FirstOrDefault(x => IsSamePatientIdentity(x, group));
            if (emrMatch != null)
                return emrMatch;

            return null;
        }

        //충돌 환자 찾기 helper 추가
        private PatientModel FindConflictPatientForImport(PatientModel group)
        {
            if (group == null)
                return null;

            var localConflict = _localPatients.FirstOrDefault(x => IsConflictPatient(x, group));
            if (localConflict != null)
                return localConflict;

            var emrConflict = _importedEmrPatients.FirstOrDefault(x => IsConflictPatient(x, group));
            if (emrConflict != null)
                return emrConflict;

            return null;
        }

        //BuildStudyKey() 기반 판정은 유지 불가함에 따라 파일 단위 키를 새로 생성.
        private async Task<string> BuildDicomInstanceKey(DicomDataset ds)
        {
            try
            {
                if (ds == null)
                    return string.Empty;

                // 1순위: SOPInstanceUID
                string sopUid = ds.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(sopUid))
                    return "SOP:" + sopUid;

                // 2순위: fallback 조합
                string studyUid = ds.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty).Trim();
                string seriesUid = ds.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, string.Empty).Trim();
                string instanceNumber = ds.GetSingleValueOrDefault(DicomTag.InstanceNumber, string.Empty).Trim();
                string numberOfFrames = ds.GetSingleValueOrDefault(DicomTag.NumberOfFrames, 1).ToString();

                string key = $"{studyUid}|{seriesUid}|{instanceNumber}|{numberOfFrames}";
                key = key.Trim('|');

                if (!string.IsNullOrWhiteSpace(key))
                    return "FALLBACK:" + key;

                return string.Empty;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return string.Empty;
            }
        }

        //기존 폴더의 “파일 단위 키” 수집 메서드
        private async Task<HashSet<string>> GetExistingDicomInstanceKeys(string patientRootFolder)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (string.IsNullOrWhiteSpace(patientRootFolder) || !Directory.Exists(patientRootFolder))
                    return result;

                foreach (var file in Directory.GetFiles(patientRootFolder, "*.dcm", SearchOption.AllDirectories))
                {
                    try
                    {
                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                        string key = await BuildDicomInstanceKey(dicomFile.Dataset);

                        if (!string.IsNullOrWhiteSpace(key))
                            result.Add(key);
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }

            return result;
        }

        /// <summary>
        /// DB에서 로컬등록된  환자 목록을 불러와 최신순(내림차순)으로 UI에 반영
        /// </summary>
        public async Task LoadPatients()
        {
            try
            {
                var repo = new DB_Manager();

                _localPatients = repo.GetLocalPatients();
                foreach (var p in _localPatients)
                {
                    p.IsEmrPatient = false;
                    p.Source = PatientSource.Local;
                    if (p.AccessionNumber == null)
                        p.AccessionNumber = string.Empty;
                }

                _importedEmrPatients = repo.GetEmrPatients();
                foreach (var p in _importedEmrPatients)
                {
                    p.IsEmrPatient = true;
                    p.Source = PatientSource.ESync;
                }

                RefreshPatients();
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
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

        private async Task AddPatient()
        {
            try
            {
                var vm = new PatientAddViewModel(_dialogService);

                if (await _dialogService.ShowDialogAsync(vm) == true)
                {
                    var confirm = await CustomMessageWindow.ShowAsync(
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
                            Sex = vm.Sex,
                            SourceType = (int)PatientSourceType.Local
                        };


                        var repo = new DB_Manager();

                        bool result = repo.AddPatient(model);

                        if (result)
                        {
                            await CustomMessageWindow.ShowAsync("환자가 정상적으로 등록되었습니다.",
                                CustomMessageWindow.MessageBoxType.Ok, 1,
                                CustomMessageWindow.MessageIconType.Info);
                            await LoadPatients();
                        }
                        else
                        {
                            await CustomMessageWindow.ShowAsync("등록 중 오류가 발생했습니다.",
                                CustomMessageWindow.MessageBoxType.Ok, 1,
                                CustomMessageWindow.MessageIconType.Danger);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task EditPatient()
        {
            if (SelectedPatient == null)
            {
                await CustomMessageWindow.ShowAsync("수정할 환자를 선택해주세요.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Info);
                return;
            }

            if (!string.IsNullOrWhiteSpace(SelectedPatient.AccessionNumber))
            {
                await CustomMessageWindow.ShowAsync("EMR 데이터는 수정이 \n 불가능합니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            var originalLocal = new PatientModel
            {
                PatientId = SelectedPatient.PatientId,
                PatientCode = SelectedPatient.PatientCode,
                PatientName = SelectedPatient.PatientName,
                BirthDate = SelectedPatient.BirthDate,
                Sex = SelectedPatient.Sex,
                AccessionNumber = SelectedPatient.AccessionNumber,
                IsEmrPatient = SelectedPatient.IsEmrPatient,
                Source = SelectedPatient.Source
            };

            // 같은 코드의 E-SYNC 존재 여부
            bool canMergeWithoutEdit = _importedEmrPatients.Any(x => x.PatientCode == SelectedPatient.PatientCode);

            var vm = new PatientEditViewModel(_dialogService, SelectedPatient, canMergeWithoutEdit);

            var result = await _dialogService.ShowDialogAsync(vm);

            if (result == true)
            {
                await HandleLocalEditConflictAfterSave(originalLocal);
            }
        }

        private async Task DeletePatient()
        {
            try
            {
                if (SelectedPatient == null)
                {
                    await CustomMessageWindow.ShowAsync("삭제할 환자를 선택해주세요.",
                            CustomMessageWindow.MessageBoxType.Ok, 1,
                            CustomMessageWindow.MessageIconType.Info);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(SelectedPatient.AccessionNumber))
                {
                    await CustomMessageWindow.ShowAsync("EMR 데이터는 삭제가 \n 불가능합니다.",
                            CustomMessageWindow.MessageBoxType.Ok, 1,
                            CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                if (await CustomMessageWindow.ShowAsync(
                        $"{SelectedPatient.PatientName} 환자 정보를 삭제하시겠습니까?",
                        CustomMessageWindow.MessageBoxType.YesNo, 0, CustomMessageWindow.MessageIconType.Info
                    ) == CustomMessageWindow.MessageBoxResult.Yes)
                {
                    var repo = new DB_Manager();

                    if (repo.SoftDeletePatientWithLog(SelectedPatient.PatientId,SelectedPatient.PatientCode,SelectedPatient.PatientName))
                    {
                        await CustomMessageWindow.ShowAsync("삭제되었습니다.",
                            CustomMessageWindow.MessageBoxType.Ok, 1,
                            CustomMessageWindow.MessageIconType.Info);
                        await LoadPatients();
                    }

                   
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task ImportPatient()
        {
            var dialog = new OpenFileDialog
            {
                Title = "가져올 DICOM 파일을 선택하세요",
                Filter = "DICOM Files|*.dcm",
                Multiselect = true
            };

            if (dialog.ShowDialog() != true)
                return;

            string[] selectedFiles = dialog.FileNames;

            try
            {
                if (selectedFiles == null || selectedFiles.Length == 0)
                {
                    await CustomMessageWindow.ShowAsync(
                        "가져올 파일을 선택해주세요.",
                        CustomMessageWindow.MessageBoxType.Ok,
                        0,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                var supportedFiles = selectedFiles
                    .Where(f => File.Exists(f) &&
                                Path.GetExtension(f).Equals(".dcm", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (supportedFiles.Count == 0)
                {
                    await CustomMessageWindow.ShowAsync(
                        "지원되는 파일(.dcm)을 선택해주세요.",
                        CustomMessageWindow.MessageBoxType.Ok,
                        0,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                var repo = new DB_Manager();

                // 최신 상태 재조회
                _localPatients = repo.GetLocalPatients();
                foreach (var p in _localPatients)
                {
                    p.IsEmrPatient = false;
                    p.Source = PatientSource.Local;
                    if (p.AccessionNumber == null)
                        p.AccessionNumber = string.Empty;
                }

                _importedEmrPatients = repo.GetEmrPatients();
                foreach (var p in _importedEmrPatients)
                {
                    p.IsEmrPatient = true;
                    p.Source = PatientSource.ESync;
                }

                // DCM 기준 환자 그룹 생성
                var patientGroups = await BuildPatientImportGroups(supportedFiles);

                if (patientGroups.Count == 0)
                {
                    await CustomMessageWindow.ShowAsync(
                        "가져올 수 있는 DICOM 환자 정보가 없습니다.",
                        CustomMessageWindow.MessageBoxType.Ok,
                        0,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                int multiFrameCount = 0;
                foreach (var file in supportedFiles)
                {
                    try
                    {
                        if (await IsMultiFrameDicom(file))
                            multiFrameCount++;
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }

                // import 계획 수립
                var importPlans = await BuildImportPlans(patientGroups);

                int newLocalCountPlan = importPlans.Count(x => x.ActionType == ImportActionType.NewLocalPatient);
                int existingLocalAddStudyCountPlan = importPlans.Count(x => x.ActionType == ImportActionType.ExistingLocalPatientAddStudy);
                int newEmrCountPlan = importPlans.Count(x => x.ActionType == ImportActionType.NewEmrPatient);
                int existingEmrAddStudyCountPlan = importPlans.Count(x => x.ActionType == ImportActionType.ExistingEmrPatientAddStudy);
                int duplicateStudySkipCountPlan = importPlans.Count(x => x.ActionType == ImportActionType.SkipDuplicateStudy);
                int conflictSkipCountPlan = importPlans.Count(x => x.ActionType == ImportActionType.SkipConflictPatient);

                int willImportCount = newLocalCountPlan + existingLocalAddStudyCountPlan + newEmrCountPlan + existingEmrAddStudyCountPlan;
                int willSkipCount = duplicateStudySkipCountPlan + conflictSkipCountPlan;

                if (willImportCount == 0)
                {
                    string noImportMessage = "가져올 신규 데이터가 없습니다.";

                    if (willSkipCount > 0)
                        noImportMessage += $"\n제외: {willSkipCount}건";

                    await CustomMessageWindow.ShowAsync(
                        noImportMessage,
                        CustomMessageWindow.MessageBoxType.Ok,
                        0,
                        CustomMessageWindow.MessageIconType.Info);

                    return;
                }

                string confirmMessage = $"환자 파일 {willImportCount}건을 가져옵니다.";

                if (willSkipCount > 0)
                    confirmMessage += $"\n{willSkipCount}건은 중복 또는 충돌로 제외됩니다.";

                if (multiFrameCount > 0)
                    confirmMessage += "\n\n영상이 포함되어 시간이 소요됩니다.";

                confirmMessage += "\n계속 진행하시겠습니까?";

                var confirm = await CustomMessageWindow.ShowAsync(
                    confirmMessage,
                    CustomMessageWindow.MessageBoxType.YesNo,
                    0,
                    CustomMessageWindow.MessageIconType.Info);

                if (confirm != CustomMessageWindow.MessageBoxResult.Yes)
                    return;

                //실제 import 대상만 count
                LoadingWindow.Begin($"환자 파일 import 중... (0/{willImportCount})");

                int processedCount = 0;
                int successCount = 0;
                int newLocalCount = 0;
                int existingLocalAddStudyCount = 0;
                int newEmrCount = 0;
                int existingEmrAddStudyCount = 0;
                int duplicateStudySkipCount = 0;
                int conflictSkipCount = 0;
                var conflictMessages = new List<string>();

                await Task.Run(async () =>
                {
                    foreach (var plan in importPlans)
                    {
                        try
                        {
                            bool success = await ExecuteImportPlan(plan, repo);

                            if (success)
                            {
                                successCount++;

                                switch (plan.ActionType)
                                {
                                    case ImportActionType.NewLocalPatient:
                                        newLocalCount++;
                                        break;
                                    case ImportActionType.ExistingLocalPatientAddStudy:
                                        existingLocalAddStudyCount++;
                                        break;
                                    case ImportActionType.NewEmrPatient:
                                        newEmrCount++;
                                        break;
                                    case ImportActionType.ExistingEmrPatientAddStudy:
                                        existingEmrAddStudyCount++;
                                        break;
                                }
                            }
                            else
                            {
                                switch (plan.ActionType)
                                {
                                    case ImportActionType.SkipDuplicateStudy:
                                        duplicateStudySkipCount++;
                                        break;
                                    case ImportActionType.SkipConflictPatient:
                                        conflictSkipCount++;
                                        if (!string.IsNullOrWhiteSpace(plan.Reason))
                                            conflictMessages.Add(plan.Reason);
                                        break;
                                }
                            }

                            processedCount++;

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                LoadingWindow.Update($"환자 파일 import 중... ({processedCount}/{willImportCount})");
                            });
                        }
                        catch (Exception ex)
                        {
                            await Common.WriteLog(ex);
                        }
                    }
                });

                LoadingWindow.End();

                // 최신 목록 다시 로드
                _localPatients = repo.GetLocalPatients();
                foreach (var p in _localPatients)
                {
                    p.IsEmrPatient = false;
                    p.Source = PatientSource.Local;
                    if (p.AccessionNumber == null)
                        p.AccessionNumber = string.Empty;
                }

                _importedEmrPatients = repo.GetEmrPatients();
                foreach (var p in _importedEmrPatients)
                {
                    p.IsEmrPatient = true;
                    p.Source = PatientSource.ESync;
                }

                ShowAll = true;
                RefreshPatients();

                string message = BuildImportSummaryMessage(
                    successCount,
                    newLocalCount,
                    existingLocalAddStudyCount,
                    newEmrCount,
                    existingEmrAddStudyCount,
                    duplicateStudySkipCount,
                    conflictSkipCount,
                    conflictMessages);

                await CustomMessageWindow.ShowAsync(
                    message,
                    CustomMessageWindow.MessageBoxType.Ok,
                    0,
                    CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex)
            {
                LoadingWindow.End();
                await Common.WriteLog(ex);

                await CustomMessageWindow.ShowAsync(
                    $"오류 발생: {ex.Message}",
                    CustomMessageWindow.MessageBoxType.Ok,
                    0,
                    CustomMessageWindow.MessageIconType.Danger);
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
                    p.Source = PatientSource.Emr;
                    p.IsEmrPatient = true;
                }
                RefreshPatients();
                await CustomMessageWindow.ShowAsync("EMR 동기화 완료되었습니다.",
                            CustomMessageWindow.MessageBoxType.Ok, 1,
                            CustomMessageWindow.MessageIconType.Info);
            }
            catch (OperationCanceledException) { } // task 해제되는 경우

            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }

            finally
            {
                LoadingWindow.End();
            }
        }


        public void Dispose()
        {
            _searchDebouncer?.Dispose();
            _cts?.Cancel();   
            _cts?.Dispose();  
        }

        public void OnSearchTextChanged(string text)
        {
            _searchDebouncer.OnTextChanged(text);
        }

        private async Task ExecuteSearch(string keyword)
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
                await Common.WriteLog(ex);
            }
        }

        // 환자 한 명이 검색 조건에 맞는지 판단
        private bool MatchesKeyword(PatientModel p, string keyword, string kwNoSpace)
        {
            // 이름 공백 제거 버전 ( ParkHan으로 Park Hanyong 검색 )
            string nameNoSpace = (p.DisplayName ?? "").Replace(" ", "");

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



        /*private List<PatientModel> LoadImportedEmrPatientsFromDicomFolder()
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
                    await Common.WriteLog(ex);
                }
            }

            return list;
        }*/


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

        private async Task<string> FindPatientFolder(PatientModel patient)
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
                await Common.WriteLog(ex);
                return null;
            }
        }

        private string GetDicomRootPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM");
        }

        private string GetVideoRootPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VIDEO");
        }

        private async Task<string> FindPatientFolderByRoot(PatientModel patient, string rootPath)
        {
            try
            {
                if (!Directory.Exists(rootPath))
                    return null;

                string expectedFolderName = $"{patient.PatientName}_{patient.PatientCode}";
                string directPath = Path.Combine(rootPath, expectedFolderName);

                if (Directory.Exists(directPath))
                    return directPath;

                return Directory.GetDirectories(rootPath)
                    .FirstOrDefault(x =>
                        string.Equals(
                            Path.GetFileName(x),
                            expectedFolderName,
                            StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return null;
            }
        }

        private async Task<string> FindPatientVideoFolder(PatientModel patient)
        {
            return await FindPatientFolderByRoot(patient, GetVideoRootPath());
        }

        private async Task MergePatientVideoFolder(string sourceVideoFolder, string targetVideoFolder, string patientName, int patientCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourceVideoFolder) || !Directory.Exists(sourceVideoFolder))
                    return;

                Directory.CreateDirectory(targetVideoFolder);

                if (!string.Equals(
                        Path.GetFullPath(sourceVideoFolder).TrimEnd('\\'),
                        Path.GetFullPath(targetVideoFolder).TrimEnd('\\'),
                        StringComparison.OrdinalIgnoreCase))
                {
                    CopyDirectory(sourceVideoFolder, targetVideoFolder, overwrite: true);

                    try
                    {
                        Directory.Delete(sourceVideoFolder, true);
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }

                // 복사/병합 후 파일명 정리
                await NormalizeVideoFileNamesRecursively(targetVideoFolder, patientName, patientCode);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }


        //파일 이름 정리
        private async Task NormalizeDicomFileNames(string folder, string patientName, int patientCode)
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
                // Parent = 202603160001
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

                    SafeMoveFile(file, newPath);
                    index++;
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        //일반 import나 fallback , 이미지에 관해서는 병합 후 단순 순번으로 처리
        private async Task NormalizeDicomFileNamesRecursively(string rootFolder, string patientName, int patientCode)
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
                        await NormalizeDicomFileNames(dir, patientName, patientCode);
                    }
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        //병합할 때, lcoal에 관한 다이콤 태그 수정
        private async Task UpdateDicomTagsForMerge(string rootFolder, string patientName, int patientCode, string accessionNumber)
        {
            try
            {
                if (!Directory.Exists(rootFolder))
                    return;

                var dcmFiles = Directory.GetFiles(rootFolder, "*.dcm", SearchOption.AllDirectories);

                foreach (var file in dcmFiles)
                {
                    try
                    {
                        // 메모리로 읽어서 파일 잠금 최소화
                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                        var ds = dicomFile.Dataset;

                        ds.AddOrUpdate(DicomTag.PatientName, patientName);
                        ds.AddOrUpdate(DicomTag.PatientID, patientCode.ToString());
                        ds.AddOrUpdate(DicomTag.AccessionNumber, accessionNumber);

                        string tempFile = file + ".tmp";

                        // temp 파일로 먼저 저장
                        dicomFile.Save(tempFile);

                        // 원본 교체
                        SafeDeleteFile(file);
                        SafeMoveFile(tempFile, file);
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private void SafeMoveFile(string sourcePath, string destPath)
        {
            Exception lastEx = null;

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (File.Exists(destPath))
                        File.Delete(destPath);

                    File.Move(sourcePath, destPath);
                    return;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    Thread.Sleep(100);
                }
            }

            if (lastEx != null)
                throw lastEx;
        }

        private void SafeDeleteFile(string filePath)
        {
            Exception lastEx = null;

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);

                    return;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    Thread.Sleep(100);
                }
            }

            if (lastEx != null)
                throw lastEx;
        }

        //수정 후 E-SYNC 충돌 검사
        private async Task HandleLocalEditConflictAfterSave(PatientModel originalLocal)
        {
            try
            {
                var repo = new DB_Manager();

                // 수정된 LOCAL 환자 다시 조회
                var updatedLocal = repo.GetAllPatients()
                    .FirstOrDefault(x => x.PatientId == originalLocal.PatientId);

                if (updatedLocal == null)
                {
                    await LoadPatients();
                    return;
                }

                // 현재 import된 E-SYNC 환자 목록 다시 로드
                _importedEmrPatients = repo.GetEmrPatients();

                // 같은 환자번호를 가진 E-SYNC 환자 찾기
                var matchedEmr = _importedEmrPatients
                    .FirstOrDefault(x => x.PatientCode == updatedLocal.PatientCode);

                // 없으면 그냥 갱신만
                if (matchedEmr == null)
                {
                    await LoadPatients();
                    return;
                }

                var popupResult = await CustomMessageWindow.ShowAsync(
                    $"번호가 같은 2명의 환자가 존재합니다.\n병합하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo,
                    0,
                    CustomMessageWindow.MessageIconType.Warning);

                if (popupResult == CustomMessageWindow.MessageBoxResult.Yes)
                {
                    await MergeEditedLocalToImportedEmr(originalLocal, updatedLocal, matchedEmr);
                }
                else
                {
                    // 아니오면 그냥 닫고 끝
                    await LoadPatients();
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                await LoadPatients();
            }
        }

        //LOCAL → E-SYNC 병합
        private async Task MergeEditedLocalToImportedEmr(PatientModel originalLocal, PatientModel updatedLocal, PatientModel importedEmr)
        {
            try
            {
                string dicomRoot = GetDicomRootPath();
                string videoRoot = GetVideoRootPath();

                // E-SYNC 기준 폴더
                string emrTargetFolder = Path.Combine(dicomRoot, $"{importedEmr.PatientName}_{importedEmr.PatientCode}");
                string emrVideoTargetFolder = Path.Combine(videoRoot, $"{importedEmr.PatientName}_{importedEmr.PatientCode}");

                // LOCAL 원래 폴더를 먼저 찾음
                string localFolder =
                    await FindPatientFolder(originalLocal) ??
                    await FindPatientFolder(updatedLocal);

                string localVideoFolder =
                    await FindPatientVideoFolder(originalLocal) ??
                    await FindPatientVideoFolder(updatedLocal);

                if (!Directory.Exists(emrTargetFolder))
                    Directory.CreateDirectory(emrTargetFolder);

                if (!Directory.Exists(emrVideoTargetFolder))
                    Directory.CreateDirectory(emrVideoTargetFolder);

                // LOCAL DICOM 폴더가 있으면 E-SYNC DICOM 폴더로 복사
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
                        await Common.WriteLog(ex);
                    }
                }

                // 병합 후 DICOM 태그를 E-SYNC 기준으로 통일
                await UpdateDicomTagsForMerge(emrTargetFolder, importedEmr.PatientName, importedEmr.PatientCode, importedEmr.AccessionNumber);

                // 파일명도 E-SYNC 기준으로 통일
                //NormalizeDicomFileNamesRecursively(emrTargetFolder,importedEmr.PatientName,importedEmr.PatientCode);

                // VIDEO 병합 추가
                await MergePatientVideoFolder(localVideoFolder, emrVideoTargetFolder, importedEmr.PatientName, importedEmr.PatientCode);

                // VIDEO의 Dicom 인덱스에 맞춰 DICOM 파일명 동기화
                await SyncDicomFileNamesWithVideoDicomIndices(emrTargetFolder, emrVideoTargetFolder, importedEmr.PatientName, importedEmr.PatientCode);

                // Image 폴더의 dcm은 E-SYNC 기준 일반 이름으로 정리
                await NormalizeImageDicomFileNames(emrTargetFolder, importedEmr.PatientName, importedEmr.PatientCode);

                // DB에서 LOCAL 환자 삭제
                var repo = new DB_Manager();
                repo.DeletePatient(updatedLocal.PatientId);

                await CustomMessageWindow.ShowAsync(
                    "E-SYNC 환자로 병합되었습니다.",
                    CustomMessageWindow.MessageBoxType.Ok,
                    1,
                    CustomMessageWindow.MessageIconType.Info);

                await LoadPatients();
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                await LoadPatients();
            }
        }

        private async Task NormalizeVideoFileNames(string folder, string patientName, int patientCode)
        {
            try
            {
                var videoExtensions = new[] { ".avi", ".mp4", ".mov", ".wmv", ".mpeg", ".mpg" };

                var files = Directory.GetFiles(folder)
                    .Where(f => videoExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .OrderBy(f => f)
                    .ToList();

                if (files.Count == 0)
                    return;

                // 구조 예:
                // VIDEO/환자명_번호/20260317/202603170001/*.avi
                string folderName = new DirectoryInfo(folder).Name;
                string parentName = new DirectoryInfo(folder).Parent?.Name ?? folderName;

                string studyId = Regex.IsMatch(folderName, @"^\d{8,}$")
                    ? folderName
                    : parentName;

                int index = 1;

                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file);

                    // 원본 파일명에서 _Avi / _Dicom 유지
                    string typeSuffix = await ExtractVideoTypeSuffix(file);

                    string newName = $"{patientName}_{patientCode}_{studyId}_{index}{typeSuffix}{ext}";
                    string newPath = Path.Combine(folder, newName);

                    if (string.Equals(file, newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        index++;
                        continue;
                    }

                    while (File.Exists(newPath))
                    {
                        index++;
                        newName = $"{patientName}_{patientCode}_{studyId}_{index}{ext}";
                        newPath = Path.Combine(folder, newName);
                    }

                    SafeMoveFile(file, newPath);
                    index++;
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task NormalizeVideoFileNamesRecursively(string rootFolder, string patientName, int patientCode)
        {
            try
            {
                if (!Directory.Exists(rootFolder))
                    return;

                var videoExtensions = new[] { ".avi", ".mp4", ".mov", ".wmv", ".mpeg", ".mpg" };

                var allDirs = Directory.GetDirectories(rootFolder, "*", SearchOption.AllDirectories)
                    .Prepend(rootFolder);

                foreach (var dir in allDirs)
                {
                    bool hasVideo = Directory.GetFiles(dir)
                        .Any(f => videoExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));

                    if (hasVideo)
                    {
                        await NormalizeVideoFileNames(dir, patientName, patientCode);
                    }
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        //병합해도 video 타입 표시는 유지 ->DICOM.avi/AVI.avi
        private async Task<string> ExtractVideoTypeSuffix(string filePath)
        {
            try
            {
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

                // 파일명 끝부분이 _Avi 또는 _Dicom 형태면 유지
                var match = Regex.Match(fileNameWithoutExt, @"_(Avi|AVI|Dicom|DICOM)$", RegexOptions.IgnoreCase);

                if (!match.Success)
                    return string.Empty;

                string value = match.Groups[1].Value;

                // 표기 통일하고 싶으면 여기서 고정
                if (value.Equals("avi", StringComparison.OrdinalIgnoreCase))
                    return "_Avi";

                if (value.Equals("dicom", StringComparison.OrdinalIgnoreCase))
                    return "_Dicom";

                return "_" + value;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return string.Empty;
            }
        }

        //DICOM 이름을 VIDEO 인덱스에 맞추는 메서드
        private async Task SyncDicomFileNamesWithVideoDicomIndices(string dicomPatientRoot, string videoPatientRoot, string patientName, int patientCode)
        {
            try
            {
                if (!Directory.Exists(dicomPatientRoot) || !Directory.Exists(videoPatientRoot))
                    return;

                // DICOM 쪽에서도 "Video" 폴더만 대상
                var dicomVideoDirs = Directory.GetDirectories(dicomPatientRoot, "*", SearchOption.AllDirectories)
                    .Where(dir =>
                        string.Equals(
                            new DirectoryInfo(dir).Name,
                            "Video",
                            StringComparison.OrdinalIgnoreCase) &&
                        Directory.GetFiles(dir, "*.dcm").Any())
                    .ToList();

                foreach (var dicomDir in dicomVideoDirs)
                {
                    string studyId = await GetStudyIdFromDicomFolder(dicomDir);
                    if (string.IsNullOrWhiteSpace(studyId))
                        continue;

                    string matchingVideoDir = await FindVideoStudyFolder(videoPatientRoot, studyId);
                    if (string.IsNullOrWhiteSpace(matchingVideoDir))
                        continue;

                    await RenameDicomFilesByVideoIndices(dicomDir, matchingVideoDir, patientName, patientCode, studyId);
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task<string> GetStudyIdFromDicomFolder(string dicomFolder)
        {
            try
            {
                var dirInfo = new DirectoryInfo(dicomFolder);

                // 예:
                // DICOM/환자/20260317/202603170001/Video
                // DICOM/환자/20260317/202603170001/Image
                // Parent = 202603170001
                string folderName = dirInfo.Name;
                string parentName = dirInfo.Parent?.Name ?? string.Empty;

                if (Regex.IsMatch(folderName, @"^\d{8,}$"))
                    return folderName;

                if (Regex.IsMatch(parentName, @"^\d{8,}$"))
                    return parentName;

                return string.Empty;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return string.Empty;
            }
        }
        private async Task<string> FindVideoStudyFolder(string videoPatientRoot, string studyId)
        {
            try
            {
                return Directory.GetDirectories(videoPatientRoot, "*", SearchOption.AllDirectories)
                    .FirstOrDefault(dir =>
                        string.Equals(
                            new DirectoryInfo(dir).Name,
                            studyId,
                            StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return null;
            }
        }
        private async Task RenameDicomFilesByVideoIndices(string dicomDir, string videoDir, string patientName, int patientCode, string studyId)
        {
            try
            {
                var dicomFiles = Directory.GetFiles(dicomDir, "*.dcm")
                    .OrderBy(f => f)
                    .ToList();

                if (dicomFiles.Count == 0)
                    return;

                var dicomVideoFiles = Directory.GetFiles(videoDir)
                    .Where(IsDicomVideoFileSync)
                    .OrderBy(f => ExtractIndexFromMergedFileNameSync(f))
                    .ThenBy(f => f)
                    .ToList();

                if (dicomVideoFiles.Count == 0)
                {
                    // video 쪽 Dicom avi가 없으면 기존 방식으로 fallback
                    await NormalizeDicomFileNamesWithDicomSuffix(dicomDir, patientName, patientCode, studyId);
                    return;
                }

                var targetIndices = dicomVideoFiles
                    .Select(ExtractIndexFromMergedFileNameSync)
                    .Where(i => i > 0)
                    .Distinct()
                    .OrderBy(i => i)
                    .ToList();

                if (targetIndices.Count == 0)
                {
                    await NormalizeDicomFileNamesWithDicomSuffix(dicomDir, patientName, patientCode, studyId);
                    return;
                }

                // dcm 파일 수와 video-dicom 인덱스 수가 다를 수 있으므로 가능한 만큼만 매핑
                int pairCount = Math.Min(dicomFiles.Count, targetIndices.Count);

                // 1단계: temp 이름으로 먼저 변경
                var tempMappings = new List<(string TempPath, int TargetIndex)>();

                for (int i = 0; i < pairCount; i++)
                {
                    string source = dicomFiles[i];
                    string tempPath = source + ".renametmp";

                    SafeMoveFile(source, tempPath);
                    tempMappings.Add((tempPath, targetIndices[i]));
                }

                // 남는 dcm 파일은 뒤 번호로 이어서 부여
                // 정상 데이터에서는 Dicom.Avi개수와 대응되는 .dcm 개수가 같기 때문에 남는 인덱스 계산
                // 예외 상황 대비용-rename 터질 경우 방지 목적
                // dcm 파일이 더 많을 경우/VIDEO 파일 일부 누락/병합 중 중복/잔여 파일 발생/이전 테스트 파일 폴더에 남아 있음
                int nextIndex = 2;
                if (targetIndices.Any())
                {
                    int maxIndex = targetIndices.Max();
                    nextIndex = (maxIndex % 2 == 0) ? maxIndex + 2 : maxIndex + 1;
                }

                for (int i = pairCount; i < dicomFiles.Count; i++)
                {
                    string source = dicomFiles[i];
                    string tempPath = source + ".renametmp";

                    SafeMoveFile(source, tempPath);
                    tempMappings.Add((tempPath, nextIndex));
                    nextIndex += 2; // Dicom 쌍 기준이면 2,4,6... 구조 유지
                }

                // 2단계: 최종 이름으로 변경
                foreach (var item in tempMappings)
                {
                    string finalName = $"{patientName}_{patientCode}_{studyId}_{item.TargetIndex}_Dicom.dcm";
                    string finalPath = Path.Combine(dicomDir, finalName);

                    if (File.Exists(finalPath))
                        SafeDeleteFile(finalPath);

                    SafeMoveFile(item.TempPath, finalPath);
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }
        //DICOM fallback 정리 함수-VIDEO에서 DICOM.avi 인덱스를 찾지 못했을 경우, 파일명에 Dicom.dcm 유지 --> 필요한지 고려
        private async Task NormalizeDicomFileNamesWithDicomSuffix(string folder, string patientName, int patientCode, string studyId)
        {
            try
            {
                var files = Directory.GetFiles(folder, "*.dcm")
                    .OrderBy(f => f)
                    .ToList();

                if (files.Count == 0)
                    return;

                var tempFiles = new List<string>();

                foreach (var file in files)
                {
                    string tempPath = file + ".renametmp";
                    SafeMoveFile(file, tempPath);
                    tempFiles.Add(tempPath);
                }

                int index = 2; // Dicom 쪽은 짝 인덱스 기준 시작
                foreach (var tempFile in tempFiles)
                {
                    string newName = $"{patientName}_{patientCode}_{studyId}_{index}_Dicom.dcm";
                    string newPath = Path.Combine(folder, newName);

                    while (File.Exists(newPath))
                    {
                        index += 2;
                        newName = $"{patientName}_{patientCode}_{studyId}_{index}_Dicom.dcm";
                        newPath = Path.Combine(folder, newName);
                    }

                    SafeMoveFile(tempFile, newPath);
                    index += 2;
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        //VIDEO가 DICOM 파일인지 판단하는 함수
        private async Task<bool> IsDicomVideoFile(string filePath)
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(filePath);
                return Regex.IsMatch(name, @"_Dicom$", RegexOptions.IgnoreCase);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return false;
            }
        }

        // OrderBy 람다 안에서 await 불가 → 동기 헬퍼 (순수 문자열 처리라 async 불필요)
        private bool IsDicomVideoFileSync(string filePath)
        {
            try { return Regex.IsMatch(Path.GetFileNameWithoutExtension(filePath), @"_Dicom$", RegexOptions.IgnoreCase); }
            catch { return false; }
        }

        //병합된 파일명에서 인덱스 추출 함수-> DICOM.AVI의 인덱스를 추출,
        //병합 후 DICOM이름 단순 순번이 아닌 VIDEO DICOM 인덱스 종속
        private async Task<int> ExtractIndexFromMergedFileName(string filePath)
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(filePath);

                var match = Regex.Match(name, @"_(\d+)_(Avi|AVI|Dicom|DICOM)$", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
                    return index;

                return -1;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        // OrderBy 람다 안에서 await 불가 → 동기 헬퍼
        private int ExtractIndexFromMergedFileNameSync(string filePath)
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(filePath);
                var match = Regex.Match(name, @"_(\d+)_(Avi|AVI|Dicom|DICOM)$", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int index)) return index;
                return -1;
            }
            catch { return -1; }
        }

        private async Task NormalizeImageDicomFileNames(string dicomPatientRoot, string patientName, int patientCode)
        {
            try
            {
                if (!Directory.Exists(dicomPatientRoot))
                    return;

                var imageDirs = Directory.GetDirectories(dicomPatientRoot, "*", SearchOption.AllDirectories)
                    .Where(dir =>
                        string.Equals(
                            new DirectoryInfo(dir).Name,
                            "Image",
                            StringComparison.OrdinalIgnoreCase) &&
                        Directory.GetFiles(dir, "*.dcm").Any())
                    .ToList();

                foreach (var imageDir in imageDirs)
                {
                    string studyId = await GetStudyIdFromDicomFolder(imageDir);
                    if (string.IsNullOrWhiteSpace(studyId))
                        continue;

                    await RenameImageDicomFiles(imageDir, patientName, patientCode, studyId);
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task RenameImageDicomFiles(string imageDir, string patientName, int patientCode, string studyId)
        {
            try
            {
                var files = Directory.GetFiles(imageDir, "*.dcm")
                    .OrderBy(f => f)
                    .ToList();

                if (files.Count == 0)
                    return;

                var tempFiles = new List<string>();

                // 1단계: temp로 피신
                foreach (var file in files)
                {
                    string tempPath = file + ".renametmp";
                    SafeMoveFile(file, tempPath);
                    tempFiles.Add(tempPath);
                }

                // 2단계: E-SYNC 기준 이름으로 재부여
                int index = 1;
                foreach (var tempFile in tempFiles)
                {
                    string newName = $"{patientName}_{patientCode}_{studyId}_{index}.dcm";
                    string newPath = Path.Combine(imageDir, newName);

                    while (File.Exists(newPath))
                    {
                        index++;
                        newName = $"{patientName}_{patientCode}_{studyId}_{index}.dcm";
                        newPath = Path.Combine(imageDir, newName);
                    }

                    SafeMoveFile(tempFile, newPath);
                    index++;
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        //DICOM이 멀티프레임(비디오)인지 판별
        private async Task<bool> IsMultiFrameDicom(string filePath)
        {
            try
            {
                var dicomFile = DicomFile.Open(filePath, FileReadOption.ReadAll);
                int frames = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.NumberOfFrames, 1);
                return frames > 1;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return false;
            }
        }

        //파일명/경로/DICOM 태그에서 StudyID 추출
        /*private string ResolveStudyIdForImport(string filePath, DicomFile dicomFile = null, string fallbackStudyId = null)
        {
            try
            {
                // 1. 파일명/경로에 12자리 StudyID 있으면 우선 사용
                string full = filePath.Replace("\\", "/");
                var match = Regex.Match(full, @"(\d{12})");
                if (match.Success)
                    return match.Groups[1].Value;

                // 2. DICOM 태그의 StudyID 사용
                if (dicomFile != null)
                {
                    string studyId = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.StudyID, "");
                    if (!string.IsNullOrWhiteSpace(studyId) && Regex.IsMatch(studyId, @"^\d{12}$"))
                        return studyId;

                    string studyDate = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.StudyDate, "");
                    if (!string.IsNullOrWhiteSpace(studyDate) && Regex.IsMatch(studyDate, @"^\d{8}$"))
                        return studyDate + "0001";
                }

                // 3. fallback
                if (!string.IsNullOrWhiteSpace(fallbackStudyId))
                    return fallbackStudyId;

                return DateTime.Now.ToString("yyyyMMdd") + "0001";
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return DateTime.Now.ToString("yyyyMMdd") + "0001";
            }
        }*/

        //AVI 파일을 import용 임시 이름으로 복사
        private void CopyFileWithUniqueName(string sourcePath, string targetDir, string extension)
        {
            Directory.CreateDirectory(targetDir);

            string tempName = $"__import__{Guid.NewGuid():N}{extension}";
            string destPath = Path.Combine(targetDir, tempName);

            File.Copy(sourcePath, destPath, true);
        }

        //import 파일들을 원하는 구조로 재배치
        private async Task ImportPatientFilesToStructuredFolders(string[] sourceFiles, string patientName, int patientCode, string accessionNumber)
        {
            try
            {
                string patientFolderName = $"{patientName}_{patientCode}";
                string dicomRoot = GetDicomRootPath();
                string videoRoot = GetVideoRootPath();

                Directory.CreateDirectory(dicomRoot);
                Directory.CreateDirectory(videoRoot);

                var allFiles = sourceFiles.Where(f =>
                        File.Exists(f) &&
                        Path.GetExtension(f).Equals(".dcm", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (allFiles.Count == 0)
                    return;

                var discoveredStudyIds = new List<string>();

                // 같은 import 작업 안에서 StudyID를 일관되게 유지
                var studyIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var reservedStudyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. DCM 파일 분류
                foreach (var file in allFiles)
                {
                    try
                    {
                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);

                        string filePatientName = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.PatientName, "").Trim();
                        string filePatientId = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.PatientID, "").Trim();

                        // 대표 환자와 다른 파일은 제외
                        if (!string.Equals(filePatientName, patientName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (filePatientId != patientCode.ToString())
                            continue;

                        string studyId = await ResolveStudyIdForImport(
                            dicomFile,
                            patientName,
                            patientCode,
                            studyIdMap,
                            reservedStudyIds);

                        if (string.IsNullOrWhiteSpace(studyId))
                            studyId = DateTime.Now.ToString("yyyyMMdd") + "0001";

                        if (!discoveredStudyIds.Contains(studyId))
                            discoveredStudyIds.Add(studyId);

                        string studyDate = studyId.Substring(0, 8);

                        if (await IsMultiFrameDicom(file))
                        {
                            // 멀티프레임 => Video 폴더
                            string dicomVideoDir = Path.Combine(
                                dicomRoot,
                                patientFolderName,
                                studyDate,
                                studyId,
                                "Video");

                            CopyFileWithUniqueName(file, dicomVideoDir, ".dcm");
                        }
                        else
                        {
                            // 싱글프레임 => Image 폴더
                            string imageDir = Path.Combine(
                                dicomRoot,
                                patientFolderName,
                                studyDate,
                                studyId,
                                "Image");

                            CopyFileWithUniqueName(file, imageDir, ".dcm");
                        }
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }

                // 2. DICOM 태그 보정
                string dicomPatientRoot = Path.Combine(dicomRoot, patientFolderName);
                string videoPatientRoot = Path.Combine(videoRoot, patientFolderName);

                if (Directory.Exists(dicomPatientRoot))
                {
                    await UpdateDicomTagsForMerge(dicomPatientRoot, patientName, patientCode, accessionNumber);
                }

                // 3. 파일명 정리
                foreach (var studyId in discoveredStudyIds.Distinct().OrderBy(x => x))
                {
                    try
                    {
                        string studyDate = studyId.Substring(0, 8);

                        string imageDir = Path.Combine(dicomPatientRoot, studyDate, studyId, "Image");
                        string dicomVideoDir = Path.Combine(dicomPatientRoot, studyDate, studyId, "Video");
                        string videoDir = Path.Combine(videoPatientRoot, studyDate, studyId);

                        // Image DCM 정리
                        if (Directory.Exists(imageDir))
                        {
                            await RenameImageDicomFiles(imageDir, patientName, patientCode, studyId);
                        }

                        // Video AVI 정리
                        if (Directory.Exists(videoDir))
                        {
                            int dicomVideoCount = 0;

                            if (Directory.Exists(dicomVideoDir))
                                dicomVideoCount = Directory.GetFiles(dicomVideoDir, "*.dcm").Length;

                            await RenameImportedVideoFiles(videoDir, patientName, patientCode, studyId, dicomVideoCount);
                        }

                        // Video DCM 정리
                        if (Directory.Exists(dicomVideoDir))
                        {
                            await NormalizeDicomVideoPairs(dicomVideoDir, patientName, patientCode, studyId);
                        }
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                throw;
            }
        }

        //AVI를 _Avi / _Dicom 구조로 정리
        //멀티프레임 DICOM 개수만큼 AVI를 _Dicom.avi로 매칭하고, 나머지는 _Avi.avi로 매칭
        private async Task RenameImportedVideoFiles(string videoDir, string patientName, int patientCode, string studyId, int dicomVideoCount)
        {
            try
            {
                var aviFiles = Directory.GetFiles(videoDir, "*.avi")
                    .OrderBy(f => File.GetLastWriteTime(f))
                    .ThenBy(f => f)
                    .ToList();

                if (aviFiles.Count == 0)
                    return;

                var tempFiles = new List<string>();

                foreach (var file in aviFiles)
                {
                    string tempPath = file + ".renametmp";
                    SafeMoveFile(file, tempPath);
                    tempFiles.Add(tempPath);
                }

                // 앞쪽 dicomVideoCount개는 _Dicom, 나머지는 _Avi
                var dicomTargets = tempFiles.Take(Math.Min(dicomVideoCount, tempFiles.Count)).ToList();
                var aviTargets = tempFiles.Skip(Math.Min(dicomVideoCount, tempFiles.Count)).ToList();

                int aviIndex = 1;
                foreach (var tempFile in aviTargets)
                {
                    while (aviIndex % 2 == 0) aviIndex++;

                    string newName = $"{patientName}_{patientCode}_{studyId}_{aviIndex}_Avi.avi";
                    string newPath = Path.Combine(videoDir, newName);

                    while (File.Exists(newPath))
                    {
                        aviIndex += 2;
                        newName = $"{patientName}_{patientCode}_{studyId}_{aviIndex}_Avi.avi";
                        newPath = Path.Combine(videoDir, newName);
                    }

                    SafeMoveFile(tempFile, newPath);
                    aviIndex += 2;
                }

                int dicomIndex = 2;
                foreach (var tempFile in dicomTargets)
                {
                    while (dicomIndex % 2 != 0) dicomIndex++;

                    string newName = $"{patientName}_{patientCode}_{studyId}_{dicomIndex}_Dicom.avi";
                    string newPath = Path.Combine(videoDir, newName);

                    while (File.Exists(newPath))
                    {
                        dicomIndex += 2;
                        newName = $"{patientName}_{patientCode}_{studyId}_{dicomIndex}_Dicom.avi";
                        newPath = Path.Combine(videoDir, newName);
                    }

                    SafeMoveFile(tempFile, newPath);
                    dicomIndex += 2;
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task<List<PatientModel>> BuildPatientImportGroups(List<string> dcmFiles)
        {
            var groups = new Dictionary<string, PatientModel>(StringComparer.OrdinalIgnoreCase);

            var studyIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var reservedStudyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 환자별 촬영일(yyyyMMdd) 모음
            var shotDateMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in dcmFiles)
            {
                try
                {
                    var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                    var ds = dicomFile.Dataset;

                    string patientName = ds.GetSingleValueOrDefault(DicomTag.PatientName, "").Trim();
                    string patientIdText = ds.GetSingleValueOrDefault(DicomTag.PatientID, "").Trim();
                    string sex = ds.GetSingleValueOrDefault(DicomTag.PatientSex, "U").Trim();
                    string accession = ds.GetSingleValueOrDefault(DicomTag.AccessionNumber, "").Trim();
                    string birthText = ds.GetSingleValueOrDefault(DicomTag.PatientBirthDate, "19000101").Trim();

                    DateTime? importedLastShootDate = await TryGetImportLastShootDate(ds);

                    if (string.IsNullOrWhiteSpace(patientName))
                        patientName = "Unknown Name";

                    if (!int.TryParse(patientIdText, out int patientCode))
                        continue;

                    DateTime birthDate;
                    if (!DateTime.TryParseExact(
                            birthText,
                            "yyyyMMdd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out birthDate))
                    {
                        birthDate = new DateTime(1900, 1, 1);
                    }

                    string key = patientName + "|" + patientCode;

                    if (!groups.ContainsKey(key))
                    {
                        groups[key] = new PatientModel
                        {
                            PatientName = patientName,
                            PatientCode = patientCode,
                            Sex = sex,
                            BirthDate = birthDate,
                            AccessionNumber = accession,
                            IsEmrPatient = !string.IsNullOrWhiteSpace(accession),
                            Source = !string.IsNullOrWhiteSpace(accession)
                                ? PatientSource.ESync
                                : PatientSource.Local,
                            SourceType = !string.IsNullOrWhiteSpace(accession)
                                ? (int)PatientSourceType.ESync
                                : (int)PatientSourceType.Local,
                            LastShootDate = importedLastShootDate,
                            ShotNum = 0
                        };

                        shotDateMap[key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                    else
                    {
                        // 가장 최신 촬영일시 유지
                        if (importedLastShootDate.HasValue)
                        {
                            if (!groups[key].LastShootDate.HasValue ||
                                groups[key].LastShootDate.Value < importedLastShootDate.Value)
                            {
                                groups[key].LastShootDate = importedLastShootDate;
                            }
                        }
                    }

                    // 날짜 수집
                    if (importedLastShootDate.HasValue)
                    {
                        string shotDate = importedLastShootDate.Value.ToString("yyyyMMdd");
                        shotDateMap[key].Add(shotDate);
                        groups[key].ShotNum = shotDateMap[key].Count;
                    }

                    groups[key].DcmFiles.Add(file);

                    string studyId = await ResolveStudyIdForImport(
                        dicomFile,
                        patientName,
                        patientCode,
                        studyIdMap,
                        reservedStudyIds);

                    if (!string.IsNullOrWhiteSpace(studyId))
                        groups[key].StudyIds.Add(studyId);
                }
                catch (Exception ex)
                {
                    await Common.WriteLog(ex);
                }
            }

            return groups.Values.ToList();
        }

        /*private void AssignAviFilesToPatientGroups(List<PatientModel> groups, List<string> aviFiles)
        {
            foreach (var aviFile in aviFiles)
            {
                try
                {
                    string studyId = await ResolveStudyIdForImport(aviFile, null, null);
                    if (string.IsNullOrWhiteSpace(studyId))
                        continue;

                    var matchedGroups = groups
                        .Where(g => g.StudyIds.Contains(studyId))
                        .ToList();

                    // 같은 StudyID를 가진 환자가 정확히 1명일 때만 AVI 연결
                    if (matchedGroups.Count == 1)
                    {
                        matchedGroups[0].AviFiles.Add(aviFile);
                    }
                }
                catch (Exception ex)
                {
                    await Common.WriteLog(ex);
                }
            }
        }*/

        private async Task CreateAviFromDicom(string dcmPath, string dicomVideoDir, string patientName, int patientCode, string studyId)
        {
            try
            {
                var dicomFile = DicomFile.Open(dcmPath, FileReadOption.ReadAll);
                int frames = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.NumberOfFrames, 1);

                if (frames <= 1)
                    return;

                string dicomRoot = GetDicomRootPath();
                string videoRoot = GetVideoRootPath();

                string dicomPatientRoot = Path.Combine(dicomRoot, $"{patientName}_{patientCode}");
                string videoPatientRoot = Path.Combine(videoRoot, $"{patientName}_{patientCode}");

                string videoDir = dicomVideoDir.Replace(GetDicomRootPath(), GetVideoRootPath());
                Directory.CreateDirectory(videoDir);

                string aviPath = Path.Combine(videoDir, $"{patientName}_{patientCode}_{studyId}_1_Dicom.avi");

                var firstDicomImage = new DicomImage(dicomFile.Dataset, 0);
                var firstRendered = firstDicomImage.RenderImage();
                byte[] firstPixels = firstRendered.As<byte[]>();

                int width = firstRendered.Width;
                int height = firstRendered.Height;

                using (var bgraMat = new Mat(height, width, MatType.CV_8UC4))
                using (var bgrMat = new Mat())
                using (var writer = new VideoWriter(
                    aviPath,
                    FourCC.MJPG,
                    30,
                    new OpenCvSharp.Size(width, height)))
                {
                    if (!writer.IsOpened())
                        return;

                    Marshal.Copy(firstPixels, 0, bgraMat.Data, firstPixels.Length);
                    Cv2.CvtColor(bgraMat, bgrMat, ColorConversionCodes.BGRA2BGR);
                    writer.Write(bgrMat);

                    for (int i = 1; i < frames; i++)
                    {
                        var dicomImage = new DicomImage(dicomFile.Dataset, i);
                        var rendered = dicomImage.RenderImage();
                        byte[] pixels = rendered.As<byte[]>();

                        Marshal.Copy(pixels, 0, bgraMat.Data, pixels.Length);
                        Cv2.CvtColor(bgraMat, bgrMat, ColorConversionCodes.BGRA2BGR);
                        writer.Write(bgrMat);
                    }

                    writer.Release();
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        //폴더 및 파일에 사용할 studyid 생성
        private async Task<string> GetStudyDateFromDataset(DicomDataset ds)
        {
            try
            {
                string studyDate = ds.GetSingleValueOrDefault(DicomTag.StudyDate, "").Trim();

                if (!string.IsNullOrWhiteSpace(studyDate) && Regex.IsMatch(studyDate, @"^\d{8}$"))
                    return studyDate;

                return DateTime.Now.ToString("yyyyMMdd");
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return DateTime.Now.ToString("yyyyMMdd");
            }
        }

        private async Task<HashSet<string>> GetExistingStudyIds(string patientName, int patientCode)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string patientFolder = Path.Combine(GetDicomRootPath(), $"{patientName}_{patientCode}");

                if (!Directory.Exists(patientFolder))
                    return result;

                foreach (var dateDir in Directory.GetDirectories(patientFolder))
                {
                    foreach (var studyDir in Directory.GetDirectories(dateDir))
                    {
                        string studyId = Path.GetFileName(studyDir);

                        if (Regex.IsMatch(studyId, @"^\d{12}$"))
                            result.Add(studyId);
                    }
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }

            return result;
        }


        //StudyID 없으면 StudyDate 기준으로만 묶음
        //같은 날짜의 같은 환자 파일은 같은 StudyID로 묶일 가능성이 높아짐
        private async Task<string> GenerateNextStudyId(string studyDate, HashSet<string> usedStudyIds)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(studyDate) || !Regex.IsMatch(studyDate, @"^\d{8}$"))
                    studyDate = DateTime.Now.ToString("yyyyMMdd");

                int seq = 1;

                while (true)
                {
                    string candidate = studyDate + seq.ToString("D4");

                    if (!usedStudyIds.Contains(candidate))
                        return candidate;

                    seq++;
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return DateTime.Now.ToString("yyyyMMdd") + "0001";
            }
        }

        private async Task<string> ResolveStudyIdForImport(DicomFile dicomFile, string patientName, int patientCode, Dictionary<string, string> studyIdMap, HashSet<string> reservedStudyIds)
        {
            try
            {
                if (dicomFile == null)
                    return DateTime.Now.ToString("yyyyMMdd") + "0001";

                var ds = dicomFile.Dataset;

                string originalStudyKey = await BuildOriginalStudyKey(ds, patientName, patientCode);

                if (studyIdMap.TryGetValue(originalStudyKey, out string cachedStudyId))
                    return cachedStudyId;

                string rawStudyId = ds.GetSingleValueOrDefault(DicomTag.StudyID, "").Trim();

                if (!string.IsNullOrWhiteSpace(rawStudyId) &&
                    Regex.IsMatch(rawStudyId, @"^\d{12}$"))
                {
                    studyIdMap[originalStudyKey] = rawStudyId;
                    reservedStudyIds.Add(rawStudyId);
                    return rawStudyId;
                }

                string studyDate = await GetStudyDateFromDataset(ds);

                var usedStudyIds = await GetExistingStudyIds(patientName, patientCode);
                foreach (var reserved in reservedStudyIds)
                    usedStudyIds.Add(reserved);

                string newStudyId = await GenerateNextStudyId(studyDate, usedStudyIds);

                studyIdMap[originalStudyKey] = newStudyId;
                reservedStudyIds.Add(newStudyId);

                return newStudyId;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return DateTime.Now.ToString("yyyyMMdd") + "0001";
            }
        }

        private async Task<string> BuildOriginalStudyKey(DicomDataset ds, string patientName, int patientCode)
        {
            try
            {
                // 1. StudyID 우선 (있으면 가장 정확)
                string studyId = ds.GetSingleValueOrDefault(DicomTag.StudyID, "").Trim();
                if (!string.IsNullOrWhiteSpace(studyId))
                    return $"{patientName}|{patientCode}|SID|{studyId}";

                // 2. 없으면 StudyDate 기준으로 묶기
                string studyDate = ds.GetSingleValueOrDefault(DicomTag.StudyDate, "").Trim();
                if (!string.IsNullOrWhiteSpace(studyDate) && Regex.IsMatch(studyDate, @"^\d{8}$"))
                    return $"{patientName}|{patientCode}|SDATE|{studyDate}";

                // 3. fallback
                return $"{patientName}|{patientCode}|SDATE|{DateTime.Now:yyyyMMdd}";
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return $"{patientName}|{patientCode}|SDATE|{DateTime.Now:yyyyMMdd}";
            }
        }

        //Dicom.dcm과 Dicom.avi와 쌍 맞춤
        private async Task NormalizeDicomVideoPairs(string dicomVideoDir, string patientName, int patientCode, string studyId)
        {
            try
            {
                if (!Directory.Exists(dicomVideoDir))
                    return;

                // 1. DCM 파일명을 먼저 최종 규칙으로 정리
                await NormalizeDicomFileNamesWithDicomSuffix(dicomVideoDir, patientName, patientCode, studyId);

                // 2. VIDEO 폴더 경로 계산
                string videoDir = dicomVideoDir.Replace(GetDicomRootPath(), GetVideoRootPath());
                Directory.CreateDirectory(videoDir);

                // 3. 기존 Dicom.avi 삭제 후 다시 생성
                foreach (var oldAvi in Directory.GetFiles(videoDir, "*_Dicom.avi"))
                {
                    try
                    {
                        SafeDeleteFile(oldAvi);
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }

                // 4. 최종 DCM 파일 기준으로 같은 인덱스의 AVI 생성
                var finalDicomFiles = Directory.GetFiles(dicomVideoDir, "*_Dicom.dcm")
                    .OrderBy(f => ExtractDicomIndexFromFileNameSync(f))
                    .ToList();

                foreach (var finalDcmPath in finalDicomFiles)
                {
                    int dicomIndex = await ExtractDicomIndexFromFileName(finalDcmPath);
                    if (dicomIndex < 0)
                        continue;

                    await CreateAviFromFinalDicom(finalDcmPath, videoDir, patientName, patientCode, studyId, dicomIndex);
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        private async Task CreateAviFromFinalDicom(string finalDcmPath, string videoDir, string patientName, int patientCode, string studyId, int dicomIndex)
        {
            try
            {
                var dicomFile = DicomFile.Open(finalDcmPath, FileReadOption.ReadAll);
                int frames = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.NumberOfFrames, 1);

                if (frames <= 1)
                    return;

                Directory.CreateDirectory(videoDir);

                string aviPath = Path.Combine(
                    videoDir,
                    $"{patientName}_{patientCode}_{studyId}_{dicomIndex}_Dicom.avi");

                var firstDicomImage = new DicomImage(dicomFile.Dataset, 0);
                var firstRendered = firstDicomImage.RenderImage();
                byte[] firstPixels = firstRendered.As<byte[]>();

                int width = firstRendered.Width;
                int height = firstRendered.Height;

                using (var bgraMat = new Mat(height, width, MatType.CV_8UC4))
                using (var bgrMat = new Mat())
                using (var writer = new VideoWriter(
                    aviPath,
                    FourCC.MJPG,
                    30,
                    new OpenCvSharp.Size(width, height)))
                {
                    if (!writer.IsOpened())
                        return;

                    Marshal.Copy(firstPixels, 0, bgraMat.Data, firstPixels.Length);
                    Cv2.CvtColor(bgraMat, bgrMat, ColorConversionCodes.BGRA2BGR);
                    writer.Write(bgrMat);

                    for (int i = 1; i < frames; i++)
                    {
                        var dicomImage = new DicomImage(dicomFile.Dataset, i);
                        var rendered = dicomImage.RenderImage();
                        byte[] pixels = rendered.As<byte[]>();

                        Marshal.Copy(pixels, 0, bgraMat.Data, pixels.Length);
                        Cv2.CvtColor(bgraMat, bgrMat, ColorConversionCodes.BGRA2BGR);
                        writer.Write(bgrMat);
                    }

                    writer.Release();
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }
        }

        //인덱스 추출 함수 추가
        private async Task<int> ExtractDicomIndexFromFileName(string filePath)
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(filePath);

                var match = Regex.Match(name, @"_(\d+)_Dicom$", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
                    return index;

                return -1;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return -1;
            }
        }

        // OrderBy 람다 안에서 await 불가 → 동기 헬퍼
        private int ExtractDicomIndexFromFileNameSync(string filePath)
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(filePath);
                var match = Regex.Match(name, @"_(\d+)_Dicom$", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int index)) return index;
                return -1;
            }
            catch { return -1; }
        }

        //dcm 파일마다 import 촬영 일시 추출
        private async Task<DateTime?> TryGetImportLastShootDate(DicomDataset ds)
        {
            try
            {
                if (ds == null)
                    return null;

                // 1) AcquisitionDateTime 우선
                string acquisitionDateTime = ds.GetSingleValueOrDefault(DicomTag.AcquisitionDateTime, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(acquisitionDateTime))
                {
                    if (TryParseDicomDateTime(acquisitionDateTime, out DateTime dt1))
                        return dt1;
                }

                // 2) ContentDate + ContentTime
                string contentDate = ds.GetSingleValueOrDefault(DicomTag.ContentDate, string.Empty).Trim();
                string contentTime = ds.GetSingleValueOrDefault(DicomTag.ContentTime, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(contentDate))
                {
                    if (TryParseDicomDateAndTime(contentDate, contentTime, out DateTime dt2))
                        return dt2;
                }

                // 3) StudyDate + StudyTime
                string studyDate = ds.GetSingleValueOrDefault(DicomTag.StudyDate, string.Empty).Trim();
                string studyTime = ds.GetSingleValueOrDefault(DicomTag.StudyTime, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(studyDate))
                {
                    if (TryParseDicomDateAndTime(studyDate, studyTime, out DateTime dt3))
                        return dt3;
                }

                // 4) SeriesDate + SeriesTime
                string seriesDate = ds.GetSingleValueOrDefault(DicomTag.SeriesDate, string.Empty).Trim();
                string seriesTime = ds.GetSingleValueOrDefault(DicomTag.SeriesTime, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(seriesDate))
                {
                    if (TryParseDicomDateAndTime(seriesDate, seriesTime, out DateTime dt4))
                        return dt4;
                }

                return null;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return null;
            }
        }

        private bool TryParseDicomDateAndTime(string dicomDate, string dicomTime, out DateTime result)
        {
            result = default;

            try
            {
                if (string.IsNullOrWhiteSpace(dicomDate))
                    return false;

                dicomDate = dicomDate.Trim();
                dicomTime = (dicomTime ?? string.Empty).Trim();

                // DICOM time은 HH, HHmm, HHmmss, HHmmss.ffffff 형태 가능
                string timePart = "000000";

                if (!string.IsNullOrWhiteSpace(dicomTime))
                {
                    string pureTime = dicomTime.Split('.')[0];
                    pureTime = Regex.Replace(pureTime, @"[^\d]", "");

                    if (pureTime.Length >= 6)
                        timePart = pureTime.Substring(0, 6);
                    else if (pureTime.Length == 4)
                        timePart = pureTime + "00";
                    else if (pureTime.Length == 2)
                        timePart = pureTime + "0000";
                    else if (pureTime.Length > 0)
                        timePart = pureTime.PadRight(6, '0');
                }

                string combined = dicomDate + timePart;

                return DateTime.TryParseExact(
                    combined,
                    "yyyyMMddHHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out result);
            }
            catch
            {
                return false;
            }
        }

        private bool TryParseDicomDateTime(string dicomDateTime, out DateTime result)
        {
            result = default;

            try
            {
                if (string.IsNullOrWhiteSpace(dicomDateTime))
                    return false;

                string value = dicomDateTime.Trim();

                // timezone(+0900 등) 제거
                value = Regex.Replace(value, @"([+\-]\d{4})$", "");

                // 소수점 이하 제거
                value = value.Split('.')[0];

                value = Regex.Replace(value, @"[^\d]", "");

                if (value.Length < 14)
                    value = value.PadRight(14, '0');
                else if (value.Length > 14)
                    value = value.Substring(0, 14);

                return DateTime.TryParseExact(
                    value,
                    "yyyyMMddHHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out result);
            }
            catch
            {
                return false;
            }
        }
    }
}
