using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using LSS_prototype.Auth;
using LSS_prototype.DB_CRUD;
using LSS_prototype.Dicom_Module;
using LSS_prototype.Login_Page;
using LSS_prototype.User_Page;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace LSS_prototype.Patient_Page
{
    internal class PatientViewModel : INotifyPropertyChanged
    {
        private readonly SearchDebouncer _searchDebouncer;
        private readonly IDialogService _dialogService;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private ObservableCollection<PatientModel> _Patients = new ObservableCollection<PatientModel>();
        public ObservableCollection<PatientModel> Users
        {
            get { return _Patients; }
            set { _Patients = value; OnPropertyChanged(); }
        }

        private enum PatientCompareResult
        {
            None = 0,
            ExactMatch = 1,
            MergeCandidate = 2,
            Conflict = 3
        }

        private string Normalizing(string value) => (value ?? string.Empty).Trim();

        private PatientCompareResult ComparePatients(PatientModel exist, PatientModel import)
        {
            if (exist == null || import == null) return PatientCompareResult.None;

            bool sameCode = exist.PatientCode == import.PatientCode;
            bool sameBirth = exist.BirthDate.Date == import.BirthDate.Date;
            bool sameSex = string.Equals(Normalizing(exist.Sex), Normalizing(import.Sex), StringComparison.OrdinalIgnoreCase);
            bool sameName = string.Equals(Normalizing(exist.PatientName), Normalizing(import.PatientName), StringComparison.OrdinalIgnoreCase);
            bool sameAccession =
                !string.IsNullOrWhiteSpace(Normalizing(exist.AccessionNumber)) &&
                !string.IsNullOrWhiteSpace(Normalizing(import.AccessionNumber)) &&
                string.Equals(Normalizing(exist.AccessionNumber), Normalizing(import.AccessionNumber), StringComparison.OrdinalIgnoreCase);

            if (sameAccession) return PatientCompareResult.ExactMatch;
            if (sameCode && sameBirth && sameSex && sameName) return PatientCompareResult.ExactMatch;
            if (sameCode && sameBirth && sameSex) return PatientCompareResult.MergeCandidate;
            if (sameCode) return PatientCompareResult.Conflict;
            return PatientCompareResult.None;
        }

        private bool _isMenuOpen;
        private DateTime _menuLastClosed = DateTime.MinValue;
        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set
            {
                if (_isMenuOpen && !value) _menuLastClosed = DateTime.UtcNow;
                _isMenuOpen = value;
                OnPropertyChanged();
            }
        }

        public bool IsAccessionNumberVisible =>
            _selectedPatient != null && !string.IsNullOrWhiteSpace(_selectedPatient.AccessionNumber);

        public Visibility DescriptionVisibility =>
            string.IsNullOrEmpty(Common.MwlDescriptionFilter) ? Visibility.Visible : Visibility.Hidden;

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                _searchDebouncer.OnTextChanged(value);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private ObservableCollection<PatientModel> _patients;
        private List<PatientModel> _emrPatients = new List<PatientModel>();
        private List<PatientModel> _importedEmrPatients = new List<PatientModel>();
        private List<PatientModel> _localPatients = new List<PatientModel>();

        public bool IsPatientSelected => _selectedPatient != null;

        public ObservableCollection<PatientModel> Patients
        {
            get => _patients;
            set { _patients = value; OnPropertyChanged(); }
        }

        private PatientModel _selectedPatient;
        public PatientModel SelectedPatient
        {
            get => _selectedPatient;
            set
            {
                _selectedPatient = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPatientSelected));
                OnPropertyChanged(nameof(IsAccessionNumberVisible));
                OnPropertyChanged(nameof(DescriptionVisibility));
            }
        }

        public string PageTitle => _showAll ? "Integrated Patient" : "EMR Patient";

        // 체크박스 바인딩용 - FALSE: EMR만 / TRUE:  LOCAL

        private bool _showAll = false;
        public bool ShowAll
        {
            get => _showAll;
            set
            {
                if (_showAll == value) return;
                _showAll = value;
                _searchText = string.Empty;
                OnPropertyChanged(nameof(SearchText));
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageTitle));
                RefreshPatients();
            }
        }

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
        public ICommand LockCommand { get; }
        public ICommand ToggleMenuCommand { get; }

        public PatientViewModel()
        {
            _dialogService = new Dialog();

            PatientAddCommand = new RelayCommand(async _ => await AddPatient());
            PatientEditCommand = new RelayCommand(async _ => await EditPatient());
            PatientDeleteCommand = new RelayCommand(async _ => await DeletePatient());
            EmrSyncCommand = new AsyncRelayCommand(async _ => await EmrSync());
            ImportCommand = new RelayCommand(async _ => await ImportPatient());
            LogoutCommand = new AsyncRelayCommand(async _ => await ExecuteLogout());
            ExitCommand = new AsyncRelayCommand(async _ => await ExecuteExit());
            LockCommand = new AsyncRelayCommand(async _ => await ExecuteLock());
            ToggleMenuCommand = new RelayCommand(_ => ToggleMenu());
            NavScanCommand = new RelayCommand(NavScan);
            NavImageReviewCommand = new RelayCommand(NavImageReview);
            NavVideoReviewCommand = new RelayCommand(NavVideoReview);

            _searchDebouncer = new SearchDebouncer(async keyword => await ExecuteSearch(keyword), delayMs: 500);
            _ = EmrSync(_cts.Token);
        }

        public async Task InitializeAsync()
        {
            await LoadPatients();
        }

        #region 메뉴 액션
        private async Task ExecuteLogout()
        {
            IsMenuOpen = false;
            await Common.ExecuteLogout();
        }

        private async Task ExecuteExit()
        {
            IsMenuOpen = false;
            await Common.ExcuteExit();
        }

        private void ToggleMenu()
        {
            if (!IsMenuOpen && (DateTime.UtcNow - _menuLastClosed).TotalMilliseconds < 200)
                return;
            IsMenuOpen = !IsMenuOpen;
        }

        private async Task ExecuteLock()
        {
            IsMenuOpen = false;

            var result = await CustomMessageWindow.ShowAsync(
                "프로그램을 잠금하시겠습니까?",
                CustomMessageWindow.MessageBoxType.YesNo, 0,
                CustomMessageWindow.MessageIconType.Info);

            if (result != CustomMessageWindow.MessageBoxResult.Yes) return;

            App.ActivityMonitor.Stop();
            SessionStateManager.SuspendSession();
            var sessionLoginWindow = new SessionLogin();
            sessionLoginWindow.Show();
            Application.Current.MainWindow = sessionLoginWindow;
        }
        #endregion

        private async void NavScan()
        {
            if (SelectedPatient == null)
            {
                await CustomMessageWindow.ShowAsync("환자를 먼저 선택해주세요.",
                    CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            string emrcheck = string.Empty;
            if (!ShowAll) emrcheck = "EMR"; // EMR화면에서 클릭된 경우
            else emrcheck = "LOCAL";

            // LOCAL 화면에서 EMR 예약 목록에 없는 환자면 경고 (스캔은 계속 진행)
            if (ShowAll && !_emrPatients.Any(x => x.PatientCode == SelectedPatient.PatientCode))
            {
                await CustomMessageWindow.ShowAsync(
                    "예약되지않은 환자입니다.",
                    CustomMessageWindow.MessageBoxType.Ok,
                    0,
                    CustomMessageWindow.MessageIconType.Warning);
            }

            MainPage.Instance.NavigateTo(new Scan_Page.Scan(SelectedPatient, emrcheck, null));

        }

        private async void NavImageReview()
        {
            if (SelectedPatient == null)
            {
                await CustomMessageWindow.ShowAsync("환자를 먼저 선택해주세요.",
                    CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                return;
            }
            MainPage.Instance.NavigateTo(new ImageReview_Page.ImageReview(SelectedPatient, null, null));
        }

        private async void NavVideoReview()
        {
            if (SelectedPatient == null)
            {
                await CustomMessageWindow.ShowAsync("환자를 먼저 선택해주세요.",
                    CustomMessageWindow.MessageBoxType.Ok, 2, CustomMessageWindow.MessageIconType.Warning);
                return;
            }
            MainPage.Instance.NavigateTo(new VideoReview_Page.VideoReview(SelectedPatient, null, null));
        }

        private bool IsMergeCandidatePatient(PatientModel existing, PatientModel incoming)
            => ComparePatients(existing, incoming) == PatientCompareResult.MergeCandidate;

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
                    if (p.AccessionNumber == null) p.AccessionNumber = string.Empty;
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

        private void RefreshPatients()
        {
            IEnumerable<PatientModel> list = _showAll
                ? _importedEmrPatients.Concat(_localPatients)
                : (IEnumerable<PatientModel>)_emrPatients;

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
                        CustomMessageWindow.MessageBoxType.YesNo, 0,
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
                            ShowAll = true;
                            SelectedPatient = Patients.FirstOrDefault(p =>
                                p.PatientCode == model.PatientCode &&
                                p.PatientName == model.PatientName);
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
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task EditPatient()
        {
            if (SelectedPatient == null)
            {
                await CustomMessageWindow.ShowAsync("수정할 환자를 선택해주세요.",
                    CustomMessageWindow.MessageBoxType.Ok, 1, CustomMessageWindow.MessageIconType.Info);
                return;
            }

            if (!string.IsNullOrWhiteSpace(SelectedPatient.AccessionNumber))
            {
                await CustomMessageWindow.ShowAsync("EMR 데이터는 수정이 \n 불가능합니다.",
                    CustomMessageWindow.MessageBoxType.Ok, 1, CustomMessageWindow.MessageIconType.Warning);
                return;
            }

            var repo = new DB_Manager();
            _importedEmrPatients = repo.GetEmrPatients();

            var vm = new PatientEditViewModel(_dialogService, SelectedPatient);
            var result = await _dialogService.ShowDialogAsync(vm);

            if (result == true)
                await LoadPatients();
        }

        private async Task DeletePatient()
        {
            try
            {
                if (SelectedPatient == null)
                {
                    await CustomMessageWindow.ShowAsync("삭제할 환자를 선택해주세요.",
                        CustomMessageWindow.MessageBoxType.Ok, 1, CustomMessageWindow.MessageIconType.Info);
                    return;
                }

                if (await CustomMessageWindow.ShowAsync(
                        $"{SelectedPatient.PatientName} 환자 정보를 삭제하시겠습니까?\n 환자 데이터는 복구가 불가능합니다.",
                        CustomMessageWindow.MessageBoxType.YesNo, 0,
                        CustomMessageWindow.MessageIconType.Warning)
                    == CustomMessageWindow.MessageBoxResult.Yes)
                {
                    var repo = new DB_Manager();

                    if (repo.HardDeletePatientWithLog(
                            SelectedPatient.PatientId,
                            SelectedPatient.PatientCode,
                            SelectedPatient.PatientName))
                    {
                        string folderName = $"{SelectedPatient.PatientName}_{SelectedPatient.PatientCode}";
                        string dicomPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM", folderName);
                        string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VIDEO", folderName);
                        string isfPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ISF", folderName);

                        if (Directory.Exists(dicomPath)) Directory.Delete(dicomPath, recursive: true);
                        if (Directory.Exists(videoPath)) Directory.Delete(videoPath, recursive: true);
                        if (Directory.Exists(isfPath)) Directory.Delete(isfPath, recursive: true);

                        await CustomMessageWindow.ShowAsync("삭제되었습니다.",
                            CustomMessageWindow.MessageBoxType.Ok, 1,
                            CustomMessageWindow.MessageIconType.Info);

                        await LoadPatients();
                    }
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task ImportPatient()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "가져올 DICOM 파일을 선택하세요",
                Filter = "DICOM Files|*.dcm",
                Multiselect = true
            };

            if (dialog.ShowDialog() != true)
                return;

            string[] selectedFiles = dialog.FileNames;
            string importErrorBatchFolder = null;

            try
            {
                if (selectedFiles == null || selectedFiles.Length == 0)
                {
                    await CustomMessageWindow.ShowAsync("가져올 파일을 선택해주세요.",
                        CustomMessageWindow.MessageBoxType.Ok, 0,
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
                    await CustomMessageWindow.ShowAsync("지원되는 파일(.dcm)을 선택해주세요.",
                        CustomMessageWindow.MessageBoxType.Ok, 0,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                importErrorBatchFolder = CreateImportErrorBatchFolder();

                var repo = new DB_Manager();

                _localPatients = repo.GetLocalPatients();
                foreach (var p in _localPatients)
                {
                    p.IsEmrPatient = false;
                    p.Source = PatientSource.Local;
                    if (p.AccessionNumber == null) p.AccessionNumber = string.Empty;
                }

                _importedEmrPatients = repo.GetEmrPatients();
                foreach (var p in _importedEmrPatients)
                {
                    p.IsEmrPatient = true;
                    p.Source = PatientSource.ESync;
                }

                // ── 서비스 인스턴스 생성 ──
                var importService = new PatientImportService(repo, _localPatients, _importedEmrPatients);

                List<PatientModel> patientGroups = null;
                try
                {
                    patientGroups = await BuildPatientImportGroups(supportedFiles, false);
                }
                catch (Exception ex)
                {
                    await Common.WriteLog(ex);
                    int saved = await SaveFilesToImportErrorFolder(
                        supportedFiles, importErrorBatchFolder, "Unknown", 0,
                        $"BuildPatientImportGroups 실패: {ex.Message}");
                    await CustomMessageWindow.ShowAsync(
                        $"가져오기 중 오류가 발생했습니다.\n선택한 파일 {saved}건을 \n ImportError 폴더에 저장했습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 0,
                        CustomMessageWindow.MessageIconType.Danger);
                    return;
                }

                if (patientGroups == null || patientGroups.Count == 0)
                {
                    int saved = await SaveFilesToImportErrorFolder(
                        supportedFiles, importErrorBatchFolder, "Unknown", 0,
                        "가져올 수 있는 DICOM 환자 정보가 없습니다. (invalid DICOM 또는 환자 정보 추출 실패)");
                    await CustomMessageWindow.ShowAsync(
                        $"DICOM 환자 정보가 없습니다.\n선택한 파일 {saved}건을 \n ImportError 폴더에 저장했습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 0,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                int multiFrameCount = 0;
                foreach (var file in supportedFiles)
                {
                    try { if (await IsMultiFrameDicom(file)) multiFrameCount++; }
                    catch (Exception ex) { await Common.WriteLog(ex); }
                }

                // ── BuildImportPlans → 서비스 위임 ──
                var importPlans = await importService.BuildImportPlans(patientGroups);

                int newLocalCountPlan = importPlans.Count(x => x.ActionType == ImportActionType.NewLocalPatient);
                int existingLocalAddStudyCountPlan = importPlans.Count(x => x.ActionType == ImportActionType.ExistingLocalPatientAddStudy);
                int newEmrCountPlan = importPlans.Count(x => x.ActionType == ImportActionType.NewEmrPatient);
                int existingEmrAddStudyCountPlan = importPlans.Count(x => x.ActionType == ImportActionType.ExistingEmrPatientAddStudy);
                int duplicateStudySkipCountPlan = importPlans.Count(x => x.ActionType == ImportActionType.SkipDuplicateStudy);
                int conflictSkipCountPlan = importPlans.Count(x => x.ActionType == ImportActionType.SkipConflictPatient);

                int willImportCount =
                    newLocalCountPlan + existingLocalAddStudyCountPlan +
                    newEmrCountPlan + existingEmrAddStudyCountPlan;

                if (willImportCount == 0)
                {
                    string noImportMessage = "가져올 신규 데이터가 없습니다.";
                    if (duplicateStudySkipCountPlan > 0) noImportMessage += $"\n중복 제외: {duplicateStudySkipCountPlan}건";
                    if (conflictSkipCountPlan > 0) noImportMessage += $"\n충돌 제외: {conflictSkipCountPlan}건";

                    await CustomMessageWindow.ShowAsync(noImportMessage,
                        CustomMessageWindow.MessageBoxType.Ok, 0,
                        CustomMessageWindow.MessageIconType.Info);

                    TryCleanImportErrorFolder(importErrorBatchFolder);
                    return;
                }

                string confirmMessage = $"환자 파일 {willImportCount}건을 가져옵니다.";
                if (duplicateStudySkipCountPlan > 0) confirmMessage += $"\n중복 파일 {duplicateStudySkipCountPlan}건은 제외됩니다.";
                if (conflictSkipCountPlan > 0) confirmMessage += $"\n충돌 환자 {conflictSkipCountPlan}건은 제외됩니다.";
                if (multiFrameCount > 0) confirmMessage += "\n\n영상이 포함되어 시간이 소요됩니다.";
                confirmMessage += "\n계속 진행하시겠습니까?";

                var confirm = await CustomMessageWindow.ShowAsync(confirmMessage,
                    CustomMessageWindow.MessageBoxType.YesNo, 0,
                    CustomMessageWindow.MessageIconType.Info);

                if (confirm != CustomMessageWindow.MessageBoxResult.Yes)
                {
                    TryCleanImportErrorFolder(importErrorBatchFolder);
                    return;
                }

                LoadingWindow.Begin($"환자 파일 import 중... (0/{willImportCount})");

                int processedCount = 0, successCount = 0;
                int newLocalCount = 0, existingLocalAddStudyCount = 0;
                int newEmrCount = 0, existingEmrAddStudyCount = 0;
                int duplicateStudySkipCount = 0, conflictSkipCount = 0;
                int errorPatientCount = 0, errorFileCount = 0;
                var errorPatientCodes = new HashSet<int>();

                await Task.Run(async () =>
                {
                    foreach (var plan in importPlans)
                    {
                        try
                        {
                            // ── ExecuteImportPlan → 서비스 위임 ──
                            bool success = await importService.ExecuteImportPlan(plan);

                            if (success)
                            {
                                successCount++;
                                switch (plan.ActionType)
                                {
                                    case ImportActionType.NewLocalPatient: newLocalCount++; break;
                                    case ImportActionType.ExistingLocalPatientAddStudy: existingLocalAddStudyCount++; break;
                                    case ImportActionType.NewEmrPatient: newEmrCount++; break;
                                    case ImportActionType.ExistingEmrPatientAddStudy: existingEmrAddStudyCount++; break;
                                }
                            }
                            else
                            {
                                switch (plan.ActionType)
                                {
                                    case ImportActionType.SkipDuplicateStudy: duplicateStudySkipCount++; break;
                                    case ImportActionType.SkipConflictPatient: conflictSkipCount++; break;
                                }
                            }

                            processedCount++;
                            Application.Current.Dispatcher.Invoke(() =>
                                LoadingWindow.Update($"환자 파일 import 중... ({processedCount}/{willImportCount})"));
                        }
                        catch (Exception ex)
                        {
                            await Common.WriteLog(ex);

                            if (plan?.Group != null)
                            {
                                if (errorPatientCodes.Add(plan.Group.PatientCode))
                                    errorPatientCount++;

                                int saved = await SaveFilesToImportErrorFolder(
                                    plan.Group.DcmFiles?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                                    importErrorBatchFolder,
                                    plan.Group.PatientName,
                                    plan.Group.PatientCode,
                                    ex.Message);

                                errorFileCount += saved;
                            }
                        }
                    }
                });

                LoadingWindow.End();

                _localPatients = repo.GetLocalPatients();
                foreach (var p in _localPatients)
                {
                    p.IsEmrPatient = false;
                    p.Source = PatientSource.Local;
                    if (p.AccessionNumber == null) p.AccessionNumber = string.Empty;
                }

                _importedEmrPatients = repo.GetEmrPatients();
                foreach (var p in _importedEmrPatients)
                {
                    p.IsEmrPatient = true;
                    p.Source = PatientSource.ESync;
                }

                ShowAll = true;
                RefreshPatients();

                if (newEmrCount > 0)
                {
                    var lastNewEmr = importPlans.Where(p => p.ActionType == ImportActionType.NewEmrPatient).LastOrDefault();
                    if (lastNewEmr != null)
                        SelectedPatient = Patients.FirstOrDefault(p =>
                            p.PatientCode == lastNewEmr.Group.PatientCode &&
                            p.PatientName == lastNewEmr.Group.PatientName &&
                            p.Source == PatientSource.ESync);
                }
                else if (newLocalCount > 0)
                {
                    var lastNewLocal = importPlans.Where(p => p.ActionType == ImportActionType.NewLocalPatient).LastOrDefault();
                    if (lastNewLocal != null)
                        SelectedPatient = Patients.FirstOrDefault(p =>
                            p.PatientCode == lastNewLocal.Group.PatientCode &&
                            p.PatientName == lastNewLocal.Group.PatientName &&
                            p.Source == PatientSource.Local);
                }

                TryCleanImportErrorFolder(importErrorBatchFolder);

                string message = BuildImportSummaryMessage(
                    successCount, duplicateStudySkipCount, conflictSkipCount,
                    errorPatientCount, errorFileCount);

                await CustomMessageWindow.ShowAsync(message,
                    CustomMessageWindow.MessageBoxType.Ok, 0,
                    CustomMessageWindow.MessageIconType.Info);

                // ── 병합 후보 확인 ──
                var importedNewEmrGroups = importPlans
                    .Where(p => p.ActionType == ImportActionType.NewEmrPatient)
                    .Select(p => p.Group)
                    .ToList();

                if (importedNewEmrGroups.Any())
                {
                    var mergeCandidates = new List<(PatientModel Local, PatientModel Emr)>();

                    _localPatients = repo.GetLocalPatients();
                    foreach (var p in _localPatients)
                    {
                        p.IsEmrPatient = false;
                        p.Source = PatientSource.Local;
                        if (p.AccessionNumber == null) p.AccessionNumber = string.Empty;
                    }

                    _importedEmrPatients = repo.GetEmrPatients();
                    foreach (var p in _importedEmrPatients)
                    {
                        p.IsEmrPatient = true;
                        p.Source = PatientSource.ESync;
                    }

                    foreach (var importedGroup in importedNewEmrGroups)
                    {
                        var importedEmr = _importedEmrPatients
                            .Where(x => x.PatientCode == importedGroup.PatientCode)
                            .OrderByDescending(x => x.PatientId)
                            .FirstOrDefault(x =>
                                string.Equals(Normalizing(x.PatientName), Normalizing(importedGroup.PatientName), StringComparison.OrdinalIgnoreCase) &&
                                x.BirthDate.Date == importedGroup.BirthDate.Date &&
                                string.Equals(Normalizing(x.Sex), Normalizing(importedGroup.Sex), StringComparison.OrdinalIgnoreCase));

                        if (importedEmr == null) continue;

                        var matchedLocal = _localPatients.FirstOrDefault(local =>
                            IsMergeCandidatePatient(local, importedEmr));

                        if (matchedLocal != null)
                            mergeCandidates.Add((matchedLocal, importedEmr));
                    }

                    if (mergeCandidates.Any())
                    {
                        string mergeCodeText = string.Join(", ",
                            mergeCandidates.Select(x => x.Local.PatientCode).Distinct().OrderBy(x => x));

                        var popupResult = await CustomMessageWindow.ShowAsync(
                            $"병합 후보가 생성되었습니다.\n환자번호: {mergeCodeText}\n생년월일과 성별이 일치합니다.\n\n병합하시겠습니까?",
                            CustomMessageWindow.MessageBoxType.YesNo, 0,
                            CustomMessageWindow.MessageIconType.Warning);

                        if (popupResult == CustomMessageWindow.MessageBoxResult.Yes)
                        {
                            bool anyMerged = false;

                            // ── MergeEditedLocalToImportedEmr → 서비스 위임 ──
                            var mergeService = new PatientImportService(repo, _localPatients, _importedEmrPatients);

                            foreach (var pair in mergeCandidates)
                            {
                                bool merged = await mergeService.MergeEditedLocalToImportedEmr(
                                    pair.Local, pair.Local, pair.Emr);
                                if (merged) anyMerged = true;
                            }

                            await LoadPatients();

                            await CustomMessageWindow.ShowAsync(
                                anyMerged ? "병합이 완료되었습니다." : "병합 중 오류가 발생했습니다.",
                                anyMerged ? CustomMessageWindow.MessageBoxType.AutoClose : CustomMessageWindow.MessageBoxType.Ok,
                                anyMerged ? 1 : 0,
                                anyMerged ? CustomMessageWindow.MessageIconType.Info : CustomMessageWindow.MessageIconType.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoadingWindow.End();
                await Common.WriteLog(ex);

                try
                {
                    if (!string.IsNullOrWhiteSpace(importErrorBatchFolder) &&
                        Directory.Exists(importErrorBatchFolder))
                    {
                        var recoverFiles = selectedFiles?
                            .Where(f => File.Exists(f) &&
                                        Path.GetExtension(f).Equals(".dcm", StringComparison.OrdinalIgnoreCase))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        if (recoverFiles != null && recoverFiles.Count > 0)
                            await SaveFilesToImportErrorFolder(
                                recoverFiles, importErrorBatchFolder, "Unknown", 0,
                                $"ImportPatient 최상위 예외: {ex.Message}");
                    }
                }
                catch (Exception saveEx) { await Common.WriteLog(saveEx); }

                await CustomMessageWindow.ShowAsync($"오류 발생: {ex.Message}",
                    CustomMessageWindow.MessageBoxType.Ok, 0,
                    CustomMessageWindow.MessageIconType.Danger);
            }
        }

        private async Task EmrSync(CancellationToken ct = default)
        {
            bool loadingEnded = false;
            try
            {
                var db = new DB_Manager();
                var pacsSet = db.GetPacsSet();
                var dicom = new DicomManager();

                LoadingWindow.Begin("MWL 조회 중...");
                var worklistItems = await dicom.GetWorklistPatientsAsync(
                    pacsSet.MwlMyAET, pacsSet.MwlIP, pacsSet.MwlPort, pacsSet.MwlAET);
                await Task.Delay(500);

                _emrPatients = worklistItems;
                foreach (var p in _emrPatients) { p.Source = PatientSource.Emr; p.IsEmrPatient = true; }
                RefreshPatients();

                LoadingWindow.End();
                loadingEnded = true;

                await CustomMessageWindow.ShowAsync("EMR 동기화 완료되었습니다.",
                    CustomMessageWindow.MessageBoxType.Ok, 1,
                    CustomMessageWindow.MessageIconType.Info);

                if (string.IsNullOrEmpty(Common.MwlDescriptionFilter))
                {
                    var distinctCount = worklistItems
                        .Select(p => p.RequestedProcedureDescription).Distinct().Count();

                    if (distinctCount >= 2)
                        await CustomMessageWindow.ShowAsync(
                            "근적외선 림프조영술(ICG) 대상이 아닌 환자가 포함되어 있습니다\nMWL Filter를 설정해 주십시오.",
                            CustomMessageWindow.MessageBoxType.Ok,
                            icon: CustomMessageWindow.MessageIconType.Info);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { await Common.WriteLog(ex); }
            finally { if (!loadingEnded) LoadingWindow.End(); }
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
                        RefreshPatients();
                        SelectedPatient = null;
                        return;
                    }

                    var source = _showAll
                        ? _importedEmrPatients.Concat(_localPatients)
                        : (IEnumerable<PatientModel>)_emrPatients;
                    string kwNoSpace = keyword.Replace(" ", "");

                    Patients = new ObservableCollection<PatientModel>(
                        source.Where(p => MatchesKeyword(p, keyword, kwNoSpace)));

                    if (selectedId.HasValue)
                        SelectedPatient = Patients.FirstOrDefault(p => p.PatientId == selectedId.Value);
                });
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private bool MatchesKeyword(PatientModel p, string keyword, string kwNoSpace)
        {
            string nameNoSpace = (p.DisplayName ?? "").Replace(" ", "");
            return
                nameNoSpace.IndexOf(kwNoSpace, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (p.PatientName ?? "").IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                p.PatientCode.ToString().Contains(keyword);
        }

        // ── BuildPatientImportGroups (PatientViewModel에 남기는 이유: _cts 등 VM 상태 불필요, 순수 DICOM 파싱) ──
        private async Task<List<PatientModel>> BuildPatientImportGroups(
            List<string> dcmFiles, bool forceEsyncImport)
        {
            var groups = new Dictionary<string, PatientModel>(StringComparer.OrdinalIgnoreCase);
            var studyIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var reservedStudyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

                    if (string.IsNullOrWhiteSpace(patientName)) patientName = "Unknown Name";
                    if (!int.TryParse(patientIdText, out int patientCode)) continue;

                    DateTime birthDate;
                    if (!DateTime.TryParseExact(birthText, "yyyyMMdd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out birthDate))
                        birthDate = new DateTime(1900, 1, 1);

                    bool isEmrPatient = forceEsyncImport || !string.IsNullOrWhiteSpace(accession);
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
                            IsEmrPatient = isEmrPatient,
                            Source = isEmrPatient ? PatientSource.ESync : PatientSource.Local,
                            SourceType = isEmrPatient ? (int)PatientSourceType.ESync : (int)PatientSourceType.Local,
                            LastShootDate = importedLastShootDate,
                            ShotNum = 0
                        };
                        shotDateMap[key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                    else if (importedLastShootDate.HasValue)
                    {
                        if (!groups[key].LastShootDate.HasValue ||
                            groups[key].LastShootDate.Value < importedLastShootDate.Value)
                            groups[key].LastShootDate = importedLastShootDate;
                    }

                    if (importedLastShootDate.HasValue)
                    {
                        shotDateMap[key].Add(importedLastShootDate.Value.ToString("yyyyMMdd"));
                        groups[key].ShotNum = shotDateMap[key].Count;
                    }

                    groups[key].DcmFiles.Add(file);

                    string studyId = await ResolveStudyIdForImport(
                        dicomFile, patientName, patientCode, studyIdMap, reservedStudyIds);
                    if (!string.IsNullOrWhiteSpace(studyId))
                        groups[key].StudyIds.Add(studyId);
                }
                catch (Exception ex) { await Common.WriteLog(ex); }
            }

            return groups.Values.ToList();
        }

        // ── DICOM 파싱 헬퍼 (BuildPatientImportGroups에서만 사용) ──
        private async Task<DateTime?> TryGetImportLastShootDate(DicomDataset ds)
        {
            try
            {
                if (ds == null) return null;

                string acquisitionDateTime = ds.GetSingleValueOrDefault(DicomTag.AcquisitionDateTime, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(acquisitionDateTime) &&
                    TryParseDicomDateTime(acquisitionDateTime, out DateTime dt1)) return dt1;

                string contentDate = ds.GetSingleValueOrDefault(DicomTag.ContentDate, string.Empty).Trim();
                string contentTime = ds.GetSingleValueOrDefault(DicomTag.ContentTime, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(contentDate) &&
                    TryParseDicomDateAndTime(contentDate, contentTime, out DateTime dt2)) return dt2;

                string studyDate = ds.GetSingleValueOrDefault(DicomTag.StudyDate, string.Empty).Trim();
                string studyTime = ds.GetSingleValueOrDefault(DicomTag.StudyTime, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(studyDate) &&
                    TryParseDicomDateAndTime(studyDate, studyTime, out DateTime dt3)) return dt3;

                string seriesDate = ds.GetSingleValueOrDefault(DicomTag.SeriesDate, string.Empty).Trim();
                string seriesTime = ds.GetSingleValueOrDefault(DicomTag.SeriesTime, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(seriesDate) &&
                    TryParseDicomDateAndTime(seriesDate, seriesTime, out DateTime dt4)) return dt4;

                return null;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return null; }
        }

        private bool TryParseDicomDateAndTime(string dicomDate, string dicomTime, out DateTime result)
        {
            result = default;
            try
            {
                if (string.IsNullOrWhiteSpace(dicomDate)) return false;
                dicomDate = dicomDate.Trim();
                dicomTime = (dicomTime ?? string.Empty).Trim();
                string timePart = "000000";

                if (!string.IsNullOrWhiteSpace(dicomTime))
                {
                    string pureTime = Regex.Replace(dicomTime.Split('.')[0], @"[^\d]", "");
                    if (pureTime.Length >= 6) timePart = pureTime.Substring(0, 6);
                    else if (pureTime.Length == 4) timePart = pureTime + "00";
                    else if (pureTime.Length == 2) timePart = pureTime + "0000";
                    else if (pureTime.Length > 0) timePart = pureTime.PadRight(6, '0');
                }

                return DateTime.TryParseExact(
                    dicomDate + timePart, "yyyyMMddHHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out result);
            }
            catch { return false; }
        }

        private bool TryParseDicomDateTime(string dicomDateTime, out DateTime result)
        {
            result = default;
            try
            {
                if (string.IsNullOrWhiteSpace(dicomDateTime)) return false;
                string value = Regex.Replace(dicomDateTime.Trim(), @"([+\-]\d{4})$", "").Split('.')[0];
                value = Regex.Replace(value, @"[^\d]", "");
                if (value.Length < 14) value = value.PadRight(14, '0');
                else if (value.Length > 14) value = value.Substring(0, 14);

                return DateTime.TryParseExact(value, "yyyyMMddHHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out result);
            }
            catch { return false; }
        }

        private async Task<string> ResolveStudyIdForImport(
            DicomFile dicomFile, string patientName, int patientCode,
            Dictionary<string, string> studyIdMap, HashSet<string> reservedStudyIds)
        {
            try
            {
                if (dicomFile == null) return DateTime.Now.ToString("yyyyMMdd") + "0001";
                var ds = dicomFile.Dataset;
                string originalKey = await BuildOriginalStudyKey(ds, patientName, patientCode);

                if (studyIdMap.TryGetValue(originalKey, out string cached)) return cached;

                string rawStudyId = ds.GetSingleValueOrDefault(DicomTag.StudyID, "").Trim();
                if (!string.IsNullOrWhiteSpace(rawStudyId) && Regex.IsMatch(rawStudyId, @"^\d{12}$"))
                {
                    studyIdMap[originalKey] = rawStudyId;
                    reservedStudyIds.Add(rawStudyId);
                    return rawStudyId;
                }

                string studyDate = await GetStudyDateFromDataset(ds);
                var usedStudyIds = await GetExistingStudyIds(patientName, patientCode);
                foreach (var r in reservedStudyIds) usedStudyIds.Add(r);

                string newStudyId = await GenerateNextStudyId(studyDate, usedStudyIds);
                studyIdMap[originalKey] = newStudyId;
                reservedStudyIds.Add(newStudyId);
                return newStudyId;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return DateTime.Now.ToString("yyyyMMdd") + "0001"; }
        }

        private async Task<string> BuildOriginalStudyKey(DicomDataset ds, string patientName, int patientCode)
        {
            try
            {
                string studyId = ds.GetSingleValueOrDefault(DicomTag.StudyID, "").Trim();
                if (!string.IsNullOrWhiteSpace(studyId))
                    return $"{patientName}|{patientCode}|SID|{studyId}";

                string studyDate = ds.GetSingleValueOrDefault(DicomTag.StudyDate, "").Trim();
                if (!string.IsNullOrWhiteSpace(studyDate) && Regex.IsMatch(studyDate, @"^\d{8}$"))
                    return $"{patientName}|{patientCode}|SDATE|{studyDate}";

                return $"{patientName}|{patientCode}|SDATE|{DateTime.Now:yyyyMMdd}";
            }
            catch (Exception ex) { await Common.WriteLog(ex); return $"{patientName}|{patientCode}|SDATE|{DateTime.Now:yyyyMMdd}"; }
        }

        private async Task<string> GetStudyDateFromDataset(DicomDataset ds)
        {
            try
            {
                string studyDate = ds.GetSingleValueOrDefault(DicomTag.StudyDate, "").Trim();
                return !string.IsNullOrWhiteSpace(studyDate) && Regex.IsMatch(studyDate, @"^\d{8}$")
                    ? studyDate : DateTime.Now.ToString("yyyyMMdd");
            }
            catch (Exception ex) { await Common.WriteLog(ex); return DateTime.Now.ToString("yyyyMMdd"); }
        }

        private async Task<HashSet<string>> GetExistingStudyIds(string patientName, int patientCode)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string patientFolder = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "DICOM", $"{patientName}_{patientCode}");
                if (!Directory.Exists(patientFolder)) return result;

                foreach (var dateDir in Directory.GetDirectories(patientFolder))
                    foreach (var studyDir in Directory.GetDirectories(dateDir))
                    {
                        string id = Path.GetFileName(studyDir);
                        if (Regex.IsMatch(id, @"^\d{12}$")) result.Add(id);
                    }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
            return result;
        }

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
                    if (!usedStudyIds.Contains(candidate)) return candidate;
                    seq++;
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); return DateTime.Now.ToString("yyyyMMdd") + "0001"; }
        }

        private async Task<bool> IsMultiFrameDicom(string filePath)
        {
            try
            {
                var dicomFile = DicomFile.Open(filePath, FileReadOption.ReadAll);
                return dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.NumberOfFrames, 1) > 1;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return false; }
        }

        // ── ImportError 폴더 관련 ──
        private string GetDesktopImportErrorRootPath()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ImportError");

        private string CreateImportErrorBatchFolder()
        {
            string root = GetDesktopImportErrorRootPath();
            Directory.CreateDirectory(root);
            string batchPath = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(batchPath);
            return batchPath;
        }

        private void TryCleanImportErrorFolder(string batchFolderPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(batchFolderPath) &&
                    Directory.Exists(batchFolderPath) &&
                    !Directory.EnumerateFileSystemEntries(batchFolderPath).Any())
                {
                    Directory.Delete(batchFolderPath, true);
                    string root = GetDesktopImportErrorRootPath();
                    if (Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any())
                        Directory.Delete(root, true);
                }
            }
            catch { }
        }

        private async Task<int> SaveFilesToImportErrorFolder(
            IEnumerable<string> sourceFiles, string batchFolderPath,
            string patientName, int patientCode, string reason)
        {
            int savedCount = 0;
            try
            {
                if (string.IsNullOrWhiteSpace(batchFolderPath)) return 0;

                string safePatientName = string.IsNullOrWhiteSpace(patientName) ? "Unknown" : patientName;
                foreach (char c in Path.GetInvalidFileNameChars())
                    safePatientName = safePatientName.Replace(c, '_');

                string patientFolderPath = Path.Combine(batchFolderPath, $"{safePatientName}_{patientCode}");
                Directory.CreateDirectory(patientFolderPath);

                File.WriteAllText(Path.Combine(patientFolderPath, "reason.txt"),
                    $"Reason: {reason}{Environment.NewLine}" +
                    $"SavedAt: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
                    $"PatientName: {patientName}{Environment.NewLine}" +
                    $"PatientCode: {patientCode}");

                foreach (var file in sourceFiles ?? Enumerable.Empty<string>())
                {
                    try
                    {
                        if (!File.Exists(file)) continue;
                        string fileName = Path.GetFileName(file);
                        string destPath = Path.Combine(patientFolderPath, fileName);

                        if (File.Exists(destPath))
                        {
                            string name = Path.GetFileNameWithoutExtension(fileName);
                            string ext = Path.GetExtension(fileName);
                            destPath = Path.Combine(patientFolderPath, $"{name}_{Guid.NewGuid():N}{ext}");
                        }

                        File.Copy(file, destPath, true);
                        savedCount++;
                    }
                    catch (Exception ex) { await Common.WriteLog(ex); }
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
            return savedCount;
        }

        private string BuildImportSummaryMessage(
            int successCount, int duplicateStudySkipCount, int conflictSkipCount,
            int errorPatientCount, int errorFileCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"가져오기 완료: {successCount}건");
            if (duplicateStudySkipCount > 0) sb.AppendLine($"중복 제외: {duplicateStudySkipCount}건");
            if (conflictSkipCount > 0) sb.AppendLine($"충돌 제외: {conflictSkipCount}건");
            if (errorPatientCount > 0) sb.AppendLine($"실패 환자: {errorPatientCount}건");
            if (errorFileCount > 0) sb.AppendLine($"실패 파일: {errorFileCount}건");
            return sb.ToString().Trim();
        }
    }
}