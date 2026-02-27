using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using LSS_prototype.DB_CRUD;
using System;
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


        // Commands - Hospital
        public ICommand SaveHospitalCommand { get; }

        // Commands - C-STORE
        public ICommand CStoreTestCommand { get; }
        public ICommand CStoreApplyCommand { get; }
        public ICommand CStoreResetCommand { get; }

        // Commands - MWL
        public ICommand MwlTestCommand { get; }
        public ICommand MwlApplyCommand { get; }
        public ICommand MwlResetCommand { get; }


        // Constructor
        public SettingViewModel()
        {
            SaveHospitalCommand = new AsyncRelayCommand(async _ => await SaveHospitalAsync());

            CStoreTestCommand = new AsyncRelayCommand(async _ => await CStoreTestAsync());
            CStoreApplyCommand = new AsyncRelayCommand(async _ => await CStoreApplyAsync());
            CStoreResetCommand = new AsyncRelayCommand(_ => { LoadSettings(true); return Task.CompletedTask; });

            MwlTestCommand = new AsyncRelayCommand(async _ => await MwlTestAsync());
            MwlApplyCommand = new AsyncRelayCommand(async _ => await MwlApplyAsync());
            MwlResetCommand = new AsyncRelayCommand(_ => { LoadSettings(true); return Task.CompletedTask; });

            // DB에서 초기값 로드
            LoadSettings();
        }


        // DB 로드
        private void LoadSettings(bool showMessage = false)
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


                if (showMessage)
                {
                    CustomMessageWindow.Show("리셋되었습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose, 1,
                        CustomMessageWindow.MessageIconType.Info);
                }

            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }


        private async Task SaveHospitalAsync()
        {
            try
            {
                var confirm = CustomMessageWindow.Show(
                  "병원명을 변경하시겠습니까?",
                  CustomMessageWindow.MessageBoxType.YesNo,
                  0,
                  CustomMessageWindow.MessageIconType.Warning);

                if (confirm != CustomMessageWindow.MessageBoxResult.Yes) return;

                var db = new DB_Manager();
                bool success = db.UpdateHospitalName(HospitalName);

                if (success)
                {
                    await CustomMessageWindow.ShowAsync(
                        "병원명이 저장되었습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose,
                        1,
                        CustomMessageWindow.MessageIconType.Info);
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }


        private async Task CStoreTestAsync()
        {
            try
            { 
                if (string.IsNullOrWhiteSpace(CStoreIP))
                {
                    await CustomMessageWindow.ShowAsync(
                        "IP 주소를 입력해주세요.",
                        CustomMessageWindow.MessageBoxType.Ok,
                        0,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;

                }
                if (string.IsNullOrWhiteSpace(CStorePort))
                {
                    await CustomMessageWindow.ShowAsync(
                        "포트 번호가 올바르지 않습니다.",
                        CustomMessageWindow.MessageBoxType.Ok,
                        0,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                string testDcmPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestDicom.dcm");

                LoadingWindow.Begin("PACS 연결 중...");
                await SendToPacsAsync(testDcmPath, CStoreMyAET, CStoreIP, Convert.ToInt32(CStorePort), CStoreAET);
                await Task.Delay(3000); // ← 테스트용 3초 딜레이 (나중에 제거)
                LoadingWindow.End();

                await CustomMessageWindow.ShowAsync(
                    "PACS 전송 테스트 성공",
                    CustomMessageWindow.MessageBoxType.AutoClose,
                    1,
                    CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
            finally
            {
                LoadingWindow.End();
            }
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

            if (!success)
                throw new Exception($"PACS 응답 오류: {statusMessage}");
        }

        private async Task CStoreApplyAsync()
        {
            try
            {

                var confirm = CustomMessageWindow.Show(
                  "C-STROE의 설정값을 \n변경하시겠습니까?",
                  CustomMessageWindow.MessageBoxType.YesNo,
                  0,
                  CustomMessageWindow.MessageIconType.Warning);

                if (confirm != CustomMessageWindow.MessageBoxResult.Yes) return;

                var db = new DB_Manager();
                var data = new SettingModel
                {
                    CStoreAET = CStoreAET,
                    CStoreIP = CStoreIP,
                    CStorePort = int.TryParse(CStorePort, out int cp) ? cp : 0,
                    CStoreMyAET = CStoreMyAET
                };

                bool success = db.UpdateCStore(data);

                if (success)
                {
                    await CustomMessageWindow.ShowAsync(
                        "C-STORE 설정이 적용되었습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose,
                        1,
                        CustomMessageWindow.MessageIconType.Info);
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        // MWL C-FIND 요청 — 응답 성공 여부만 확인
        private async Task MwlTestAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(MwlIP))
                {
                    await CustomMessageWindow.ShowAsync(
                        "IP 주소를 입력해주세요.",
                        CustomMessageWindow.MessageBoxType.Ok,
                        0,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(MwlPort))
                {
                    await CustomMessageWindow.ShowAsync(
                        "포트 번호를 입력해주세요.",
                        CustomMessageWindow.MessageBoxType.Ok,
                        0,
                        CustomMessageWindow.MessageIconType.Warning);
                    return;
                }

                LoadingWindow.Begin("MWL 연결 중...");
                await QueryWorklistAsync(MwlMyAET, MwlIP, Convert.ToInt32(MwlPort), MwlAET);
                await Task.Delay(3000); // TODO: 테스트용 딜레이 — 실사용 전 제거
                LoadingWindow.End();

                await CustomMessageWindow.ShowAsync(
                    "MWL 연결 테스트 성공",
                    CustomMessageWindow.MessageBoxType.AutoClose,
                    1,
                    CustomMessageWindow.MessageIconType.Info);
            }
            catch (TimeoutException ex)
            {
                Common.WriteLog(ex);
                await CustomMessageWindow.ShowAsync(
                    "DICOM 서버가 응답하지 않습니다.\n네트워크 또는 서버 상태를 확인해주세요.",
                    CustomMessageWindow.MessageBoxType.Ok,
                    0,
                    CustomMessageWindow.MessageIconType.Warning);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
                await CustomMessageWindow.ShowAsync(
                    $"MWL 연결 실패:\n{ex.Message}",
                    CustomMessageWindow.MessageBoxType.Ok,
                    0,
                    CustomMessageWindow.MessageIconType.Warning);
            }
            finally
            {
                LoadingWindow.End();
            }
        }

        private async Task QueryWorklistAsync(string sourceAET, string targetIP, int targetPort, string targetAET)
        {
            var client = DicomClientFactory.Create(targetIP, targetPort, false, sourceAET, targetAET);

            bool hasResponse = false;

            var request = new DicomCFindRequest(DicomQueryRetrieveLevel.NotApplicable)
            {
                Dataset = new DicomDataset
                {
                    { DicomTag.PatientName,      "*" },
                    { DicomTag.PatientID,        "*" },
                    { DicomTag.StudyInstanceUID, ""  },
                    { DicomTag.StudyDate,        ""  }
                }
                    };

            request.OnResponseReceived += (req, response) =>
            {
                if (response.Status == DicomStatus.Pending ||
                    response.Status == DicomStatus.Success)
                    hasResponse = true;
            };

            await client.AddRequestAsync(request);

            // 5초 안에 응답 없으면 타임아웃
            // why ? 연결이 됐어도 서버측에서 데이터가 없으면 무한대기상태로 빠지니
            // 방어코드로 5초동안 1건의 데이터도 안들어오면, thorw처리 
            var sendTask = client.SendAsync();
            var timeout = Task.Delay(5000);
            if (await Task.WhenAny(sendTask, timeout) == timeout)
                throw new TimeoutException("DICOM 서버가 응답하지 않습니다.");
            await sendTask;

            if (!hasResponse)
                throw new Exception("5초동안 서버 응답이 없습니다.");
        }

        private async Task MwlApplyAsync()
        {
            try
            {
                var confirm = CustomMessageWindow.Show(
                  "MWD의을 설정값을 \n변경하시겠습니까?",
                  CustomMessageWindow.MessageBoxType.YesNo,
                  0,
                  CustomMessageWindow.MessageIconType.Warning);

                if (confirm != CustomMessageWindow.MessageBoxResult.Yes) return;

                var db = new DB_Manager();
                var data = new SettingModel
                {
                    MwlAET = MwlAET,
                    MwlIP = MwlIP,
                    MwlPort = int.TryParse(MwlPort, out int mp) ? mp : 0,
                    MwlMyAET = MwlMyAET
                };

                bool success = db.UpdateMwl(data);

                if (success)
                {
                    await CustomMessageWindow.ShowAsync(
                        "MWL 설정이 적용되었습니다.",
                        CustomMessageWindow.MessageBoxType.AutoClose,
                        1,
                        CustomMessageWindow.MessageIconType.Info);
                }
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }
    }
}
