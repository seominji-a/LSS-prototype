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
            SaveHospitalCommand = new RelayCommand(async _ => await SaveHospitalAsync());

            CStoreTestCommand = new RelayCommand(async _ => await CStoreTestAsync());
            CStoreApplyCommand = new RelayCommand(async _ => await CStoreApplyAsync());
            CStoreResetCommand = new RelayCommand(_ => LoadSettings(true));

            MwlTestCommand = new RelayCommand(async _ => await MwlTestAsync());
            MwlApplyCommand = new RelayCommand(async _ => await MwlApplyAsync());
            MwlResetCommand = new RelayCommand(_ => LoadSettings(true));

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
                // TODO: C-STORE 연결 테스트 로직
                await CustomMessageWindow.ShowAsync(
                    "C-STORE 연결 테스트 - TODO",
                    CustomMessageWindow.MessageBoxType.Ok,
                    0,
                    CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
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


        private async Task MwlTestAsync()
        {
            try
            {
                // TODO: MWL 연결 테스트 로직
                await CustomMessageWindow.ShowAsync(
                    "MWL 연결 테스트 - TODO",
                    CustomMessageWindow.MessageBoxType.Ok,
                    0,
                    CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
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
