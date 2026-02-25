using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LSS_prototype.User_Page
{
    public class SettingViewModel : INotifyPropertyChanged
    {
        // ══════════════════════════════════════════
        // INotifyPropertyChanged
        // ══════════════════════════════════════════
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));



        // ══════════════════════════════════════════
        // Properties - Hospital
        // ══════════════════════════════════════════
        private string _hospitalName;
        public string HospitalName
        {
            get => _hospitalName;
            set { _hospitalName = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════
        // Properties - C-STORE
        // ══════════════════════════════════════════
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

        private string _cStoreMyAET = "RMICG";
        public string CStoreMyAET
        {
            get => _cStoreMyAET;
            set
            {
                _cStoreMyAET = value;
                OnPropertyChanged();
            }

        }

        // ══════════════════════════════════════════
        // Properties - MWL
        // ══════════════════════════════════════════
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

        private string _mwlMyAET = "RMICG";
        public string MwlMyAET
        {
            get => _mwlMyAET;
            set { _mwlMyAET = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════
        // Commands - Hospital
        // ══════════════════════════════════════════
        public ICommand SaveHospitalCommand { get; }

        // ══════════════════════════════════════════
        // Commands - C-STORE
        // ══════════════════════════════════════════
        public ICommand CStoreTestCommand { get; }
        public ICommand CStoreApplyCommand { get; }
        public ICommand CStoreCancelCommand { get; }

        // ══════════════════════════════════════════
        // Commands - MWL
        // ══════════════════════════════════════════
        public ICommand MwlTestCommand { get; }
        public ICommand MwlApplyCommand { get; }
        public ICommand MwlCancelCommand { get; }

        // ══════════════════════════════════════════
        // Constructor
        // ══════════════════════════════════════════
        public SettingViewModel()
        {
            SaveHospitalCommand = new RelayCommand(async _ => await SaveHospitalAsync());

            CStoreTestCommand = new RelayCommand(async _ => await CStoreTestAsync());
            CStoreApplyCommand = new RelayCommand(async _ => await CStoreApplyAsync());
            CStoreCancelCommand = new RelayCommand(_ => CStoreCancel());

            MwlTestCommand = new RelayCommand(async _ => await MwlTestAsync());
            MwlApplyCommand = new RelayCommand(async _ => await MwlApplyAsync());
            MwlCancelCommand = new RelayCommand(_ => MwlCancel());

            // MyAET 기본값
            CStoreMyAET = "RMICG";
            MwlMyAET = "RMICG";

            // TODO: 초기값 DB에서 로드 (DB값이 있으면 위 기본값을 덮어씀)
            // LoadSettings();
        }

        // ══════════════════════════════════════════
        // Methods - Hospital
        // ══════════════════════════════════════════
        private async Task SaveHospitalAsync()
        {
            try
            {
                // TODO: DB 저장
                // var db = new DB_Manager();
                // db.SaveHospitalName(HospitalName);
                await CustomMessageWindow.ShowAsync(
                    "병원명이 저장되었습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose,
                    1,
                    CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        // ══════════════════════════════════════════
        // Methods - C-STORE
        // ══════════════════════════════════════════
        private async Task CStoreTestAsync()
        {
            try
            {
                // TODO: C-STORE 연결 테스트 로직
                // bool result = await DicomService.TestConnection(CStoreIP, CStorePort, CStoreAET);
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
                // TODO: C-STORE 설정 DB 저장
                // var db = new DB_Manager();
                // db.SaveCStoreSetting(CStoreAET, CStoreIP, CStorePort, CStoreMyAET);
                await CustomMessageWindow.ShowAsync(
                    "C-STORE 설정이 적용되었습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose,
                    1,
                    CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private void CStoreCancel()
        {
            try
            {
                // TODO: 기존 저장값으로 되돌리기
                // LoadCStoreSettings();
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        // ══════════════════════════════════════════
        // Methods - MWL
        // ══════════════════════════════════════════
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
                // TODO: MWL 설정 DB 저장
                await CustomMessageWindow.ShowAsync(
                    "MWL 설정이 적용되었습니다.",
                    CustomMessageWindow.MessageBoxType.AutoClose,
                    1,
                    CustomMessageWindow.MessageIconType.Info);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private void MwlCancel()
        {
            try
            {
                // TODO: 기존 저장값으로 되돌리기
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
        }
    }

    
}