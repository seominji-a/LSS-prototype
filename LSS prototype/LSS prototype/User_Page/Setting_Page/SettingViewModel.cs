using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using LSS_prototype.DB_CRUD;
using LSS_prototype.Dicom_Module;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{
    public class SettingViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string _hospitalName;
        public string HospitalName
        {
            get => _hospitalName;
            set { _hospitalName = value; OnPropertyChanged(); }
        }

        private string _cStoreAET;
        public string CStoreAET
        {
            get => _cStoreAET;
            set { _cStoreAET = value; OnPropertyChanged(); }
        }

        private string _cStoreIP;
        public string CStoreIP
        {
            get => _cStoreIP;
            set { _cStoreIP = value; OnPropertyChanged(); }
        }

        private string _cStorePort;
        public string CStorePort
        {
            get => _cStorePort;
            set { _cStorePort = value; OnPropertyChanged(); }
        }

        private string _cStoreMyAET;
        public string CStoreMyAET
        {
            get => _cStoreMyAET;
            set { _cStoreMyAET = value; OnPropertyChanged(); }
        }

        private string _mwlAET;
        public string MwlAET
        {
            get => _mwlAET;
            set { _mwlAET = value; OnPropertyChanged(); }
        }

        private string _mwlIP;
        public string MwlIP
        {
            get => _mwlIP;
            set { _mwlIP = value; OnPropertyChanged(); }
        }

        private string _mwlPort;
        public string MwlPort
        {
            get => _mwlPort;
            set { _mwlPort = value; OnPropertyChanged(); }
        }

        private string _mwlMyAET;
        public string MwlMyAET
        {
            get => _mwlMyAET;
            set { _mwlMyAET = value; OnPropertyChanged(); }
        }

        // ═══════════════════════════════════════════
        //  MWL Filter 콤보박스
        //
        //  _isLoadingItems 플래그:
        //  Clear() 시 WPF 바인딩이 SelectedItem=null 로 만들어
        //  setter가 의도치 않게 DB 저장 / Common 세팅하는 것을 방지
        // ═══════════════════════════════════════════
        private bool _isLoadingItems = false;

        private ObservableCollection<string> _mwlDescriptionItems = new ObservableCollection<string>();
        public ObservableCollection<string> MwlDescriptionItems
        {
            get => _mwlDescriptionItems;
            set { _mwlDescriptionItems = value; OnPropertyChanged(); }
        }

        private string _selectedMwlDescription;
        public string SelectedMwlDescription
        {
            get => _selectedMwlDescription;
            set
            {
                _selectedMwlDescription = value;
                OnPropertyChanged();

                // 목록 로딩 중에는 DB 저장 / Common 세팅 차단
                if (_isLoadingItems) return;

                Common.MwlDescriptionFilter = value == "ALL" ? string.Empty : (value ?? string.Empty);
                new DB_Manager().UpdateMwlFilter(
                    value == "ALL" ? string.Empty : (value ?? string.Empty));
            }
        }

        // ═══════════════════════════════════════════
        //  커맨드
        // ═══════════════════════════════════════════
        public ICommand SaveHospitalCommand { get; }
        public ICommand CStoreTestCommand { get; }
        public ICommand CStoreApplyCommand { get; }
        public ICommand CStoreResetCommand { get; }
        public ICommand MwlTestCommand { get; }
        public ICommand MwlApplyCommand { get; }
        public ICommand MwlResetCommand { get; }

        // ═══════════════════════════════════════════
        //  생성자
        // ═══════════════════════════════════════════
        public SettingViewModel()
        {
            SaveHospitalCommand = new RelayCommand(async _ => await SaveHospital());
            CStoreTestCommand = new AsyncRelayCommand(async _ => await CStoreTestAsync());
            CStoreApplyCommand = new RelayCommand(async _ => await CStoreApply());
            CStoreResetCommand = new AsyncRelayCommand(async _ => await LoadSettings(true));
            MwlTestCommand = new AsyncRelayCommand(async _ => await MwlTestAsync());
            MwlApplyCommand = new RelayCommand(async _ => await MwlApply());
            MwlResetCommand = new AsyncRelayCommand(async _ => await LoadSettings(true));
        }

        public async Task InitializeAsync()
        {
            await LoadSettings();
        }

        // ═══════════════════════════════════════════
        //  DB 로드
        // ═══════════════════════════════════════════
        private async Task LoadSettings(bool showMessage = false)
        {
            try
            {
                var db = new DB_Manager();
                var data = db.GetPacsSet();

                HospitalName = data.HospitalName;
                CStoreAET = data.CStoreAET;
                CStoreIP = data.CStoreIP;
                CStorePort = data.CStorePort.ToString();
                CStoreMyAET = data.CStoreMyAET;
                MwlAET = data.MwlAET;
                MwlIP = data.MwlIP;
                MwlPort = data.MwlPort.ToString();
                MwlMyAET = data.MwlMyAET;

                // ── MWL Filter 콤보박스 초기 세팅 ──
                // _isLoadingItems=true → Clear() 시 setter 호출돼도 DB 저장 차단
                _isLoadingItems = true;
                MwlDescriptionItems.Clear();
                MwlDescriptionItems.Add("ALL");

                string savedFilter = data.MwlDescriptionFilter;
                if (!string.IsNullOrWhiteSpace(savedFilter) && savedFilter != "ALL")
                    MwlDescriptionItems.Add(savedFilter);

                _isLoadingItems = false;

                // 저장된 필터값 있으면 선택, 없으면 ALL
                SelectedMwlDescription = string.IsNullOrWhiteSpace(savedFilter)
                    ? "ALL"
                    : savedFilter;

                if (showMessage)
                    await CustomMessageWindow.ShowAsync("리셋되었습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task SaveHospital()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(HospitalName))
                {
                    await CustomMessageWindow.ShowAsync("병원명을 입력해주세요.");
                    return;
                }

                var confirm = await CustomMessageWindow.ShowAsync(
                    "병원명을 변경하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo, 0,
                    CustomMessageWindow.MessageIconType.Warning);
                if (confirm != CustomMessageWindow.MessageBoxResult.Yes) return;

                bool success = new DB_Manager().UpdateHospitalName(HospitalName);
                if (success)
                    await CustomMessageWindow.ShowAsync("병원명이 저장되었습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task CStoreTestAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CStoreIP))
                {
                    await CustomMessageWindow.ShowAsync("IP 주소를 입력해주세요.", CustomMessageWindow.MessageBoxType.Ok, 0, CustomMessageWindow.MessageIconType.Warning);
                    return;
                }
                if (!int.TryParse(CStorePort, out int port))
                {
                    await CustomMessageWindow.ShowAsync("포트 번호가 올바르지 않습니다.");
                    return;
                }
                string testDcmPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestDicom.dcm");
                LoadingWindow.Begin("PACS 연결 중...");
                await SendToPacsAsync(testDcmPath, CStoreMyAET, CStoreIP, Convert.ToInt32(CStorePort), CStoreAET);
                await Task.Delay(3000);
                LoadingWindow.End();
                await CustomMessageWindow.ShowAsync("PACS 전송 테스트 성공",
                    CustomMessageWindow.MessageBoxType.Ok, 1,
                    CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
            finally { LoadingWindow.End(); }
        }

        private async Task SendToPacsAsync(string dcmPath, string sourceAET, string targetIP, int targetPort, string targetAET)
        {
            var dicomFile = DicomFile.Open(dcmPath);
            var client = DicomClientFactory.Create(targetIP, targetPort, false, sourceAET, targetAET);
            bool success = false;
            string statusMessage = string.Empty;

            var request = new DicomCStoreRequest(dicomFile);
            request.OnResponseReceived += (req, response) =>
            {
                success = response.Status == DicomStatus.Success;
                statusMessage = response.Status.ToString();
            };
            await client.AddRequestAsync(request);
            await client.SendAsync();
            if (!success) throw new Exception($"PACS 응답 오류: {statusMessage}");
        }

        private async Task CStoreApply()
        {
            try
            {
                var confirm = await CustomMessageWindow.ShowAsync(
                    "C-STORE의 설정값을 \n변경하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo, 0,
                    CustomMessageWindow.MessageIconType.Warning);
                if (confirm != CustomMessageWindow.MessageBoxResult.Yes) return;

                bool success = new DB_Manager().UpdateCStore(new SettingModel
                {
                    CStoreAET = CStoreAET,
                    CStoreIP = CStoreIP,
                    CStorePort = int.TryParse(CStorePort, out int cp) ? cp : 0,
                    CStoreMyAET = CStoreMyAET
                });
                if (success)
                    await CustomMessageWindow.ShowAsync("C-STORE 설정이 적용되었습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        // ═══════════════════════════════════════════
        //  MWL TEST SEND
        //  흐름:
        //  1. 서버에서 descriptions 조회
        //  2. Clear 전에 _selectedMwlDescription 필드 직접 백업
        //     (setter 거치면 DB 저장되므로 필드 직접 읽기)
        //  3. _isLoadingItems=true → Clear + 목록 채움
        //  4. _isLoadingItems=false
        //  5. 백업값이 새 목록에 있으면 유지, 없으면 ALL
        // ═══════════════════════════════════════════
        private async Task MwlTestAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(MwlIP))
                {
                    await CustomMessageWindow.ShowAsync("IP 주소를 입력해주세요.", CustomMessageWindow.MessageBoxType.Ok, 0, CustomMessageWindow.MessageIconType.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(MwlPort))
                {
                    await CustomMessageWindow.ShowAsync("포트 번호를 입력해주세요.", CustomMessageWindow.MessageBoxType.Ok, 0, CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                LoadingWindow.Begin("MWL 연결 중...");

                var dicom = new DicomManager();
                var descriptions = await dicom.GetWorklistDescriptionsAsync(
                    MwlMyAET, MwlIP, Convert.ToInt32(MwlPort), MwlAET);

                //   Clear 전 현재 선택값을 필드에서 직접 백업
                string backup = _selectedMwlDescription;

                //   _isLoadingItems=true → Clear() 시 setter → DB 저장 차단
                _isLoadingItems = true;
                MwlDescriptionItems.Clear();
                MwlDescriptionItems.Add("ALL");
                foreach (var d in descriptions)
                    MwlDescriptionItems.Add(d);
                _isLoadingItems = false;

                //   백업값이 새 목록에 있으면 유지, 없으면 ALL
                SelectedMwlDescription = MwlDescriptionItems.Contains(backup)
                    ? backup
                    : "ALL";

                LoadingWindow.End();

                await CustomMessageWindow.ShowAsync("MWL 연결 테스트 성공",
                    CustomMessageWindow.MessageBoxType.Ok, 1,
                    CustomMessageWindow.MessageIconType.Info);
            }
            catch (TimeoutException ex)
            {
                await Common.WriteLog(ex);
                await CustomMessageWindow.ShowAsync(
                    "DICOM 서버가 응답하지 않습니다.\n네트워크 또는 서버 상태를 확인해주세요.",
                    CustomMessageWindow.MessageBoxType.Ok, 0,
                    CustomMessageWindow.MessageIconType.Warning);
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                await CustomMessageWindow.ShowAsync($"MWL 연결 실패:\n{ex.Message}",
                    CustomMessageWindow.MessageBoxType.Ok, 0,
                    CustomMessageWindow.MessageIconType.Warning);
            }
            finally { LoadingWindow.End(); }
        }

        private async Task MwlApply()
        {
            try
            {
                var confirm = await CustomMessageWindow.ShowAsync(
                    "MWL의 설정값을 \n변경하시겠습니까?",
                    CustomMessageWindow.MessageBoxType.YesNo, 0,
                    CustomMessageWindow.MessageIconType.Warning);
                if (confirm != CustomMessageWindow.MessageBoxResult.Yes) return;

                bool success = new DB_Manager().UpdateMwl(new SettingModel
                {
                    MwlAET = MwlAET,
                    MwlIP = MwlIP,
                    MwlPort = int.TryParse(MwlPort, out int mp) ? mp : 0,
                    MwlMyAET = MwlMyAET
                });
                if (success)
                    await CustomMessageWindow.ShowAsync("MWL 설정이 적용되었습니다.",
                        CustomMessageWindow.MessageBoxType.Ok, 1,
                        CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }
    }
}
