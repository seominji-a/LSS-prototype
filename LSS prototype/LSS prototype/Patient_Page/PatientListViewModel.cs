using LSS_prototype.DB_CRUD;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;

namespace LSS_prototype.Patient_Page
{
    /// <summary>
    /// 환자 목록 관리 및 CRUD(생성, 조회, 수정, 삭제) 기능 수행을 위한 로직
    /// 2026-02-09 서민지
    /// </summary>
    internal class PatientListViewModel : INotifyPropertyChanged
    {
        private string _searchText;
        public string SearchText
        {
            get { return _searchText; }
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                //OnPropertyChanged(); 검색로직 추가 시 해당 부분 주석 해제 0223 박한용
            }
        }

        private readonly IDialogService _dialogService;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // ===== Patients (단일 리스트) =====
        private ObservableCollection<PatientModel> _patients;
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

        // ===== Commands =====
        public ICommand PatientAddCommand { get; }
        public ICommand PatientEditCommand { get; }
        public ICommand PatientDeleteCommand { get; }
        public ICommand EmrSyncCommand { get; }
        public ICommand NavScanCommand { get; }
        public ICommand NavImageReviewCommand { get; }
        public ICommand NavVideoReviewCommand { get; }

        public PatientListViewModel()
        {
            _dialogService = new Dialog();

            PatientAddCommand = new RelayCommand(_ => AddPatient());
            PatientEditCommand = new RelayCommand(_ => EditPatient());
            PatientDeleteCommand = new RelayCommand(_ => DeletePatient());
            EmrSyncCommand = new AsyncRelayCommand(async _ => await EmrSync());

            NavScanCommand = new RelayCommand(_ => MainPage.Instance.NavigateTo(new Scan_Page.Scan()));
            // 0227 박한용 아래코드는 데이터 관련 처리 완료 후 주석 풀고 연동 예정 
            //NavImageReviewCommand = new RelayCommand(_ => MainPage.Instance.NavigateTo(new ImageReview_Page.ImageReview()));
            //NavVideoReviewCommand = new RelayCommand(_ => MainPage.Instance.NavigateTo(new VideoReview_Page.VideoReview()));

            LoadPatients();

        }

        /// <summary>
        /// DB에서 전체 환자 목록을 불러와 최신순(내림차순)으로 UI에 반영
        /// </summary>
        public void LoadPatients()
        {
            try
            {
                var repo = new DB_Manager();
                List<PatientModel> data = repo.GetAllPatients(); // 최신순으로 보장하는 쿼리문 수정해야함 ( 2월19일 기준 ) 

                Patients = new ObservableCollection<PatientModel>(data);
            }
            catch (Exception ex)
            {
                Common.WriteLog(ex);
            }
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

            // ✅ 생성자에 _dialogService를 첫 번째 인자로 추가하여 전달합니다.
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

                if (CustomMessageWindow.Show(
                        $"{SelectedPatient.PatientName} 환자 정보를 삭제하시겠습니까?",
                        CustomMessageWindow.MessageBoxType.YesNo,0,CustomMessageWindow.MessageIconType.Danger
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
            catch(Exception ex)
            {
                Common.WriteLog(ex);
            }
        }

        private async Task EmrSync()
        {
            try
            {
                var pacsSet = new DB_Manager().GetPacsSet();

                LoadingWindow.Begin("MWL 조회 중...");
                var worklistItems = await GetWorklistPatientsAsync(
                    pacsSet.MwlMyAET, pacsSet.MwlIP, pacsSet.MwlPort, pacsSet.MwlAET);
                await Task.Delay(2000);

                // TODO: LS / LSS 간 표시 데이터 차이 확인 후 바인딩 필드 정리 필요 0227 박한용
                Patients = new ObservableCollection<PatientModel>(
                    worklistItems.Select(w => new PatientModel
                    {
                        PatientId = w.PatientId,
                        PatientCode = w.PatientId,
                        PatientName = w.PatientName,
                        BirthDate = w.BirthDate,
                        Sex = w.Sex,
                        Reg_Date = DateTime.Now
                    }));
            }
            catch (TimeoutException ex)
            {
                Common.WriteLog(ex);
                await CustomMessageWindow.ShowAsync(
                    "DICOM 서버가 응답하지 않습니다.\n네트워크 또는 서버 상태를 확인해주세요.",
                    CustomMessageWindow.MessageBoxType.Ok, 0,
                    CustomMessageWindow.MessageIconType.Warning);
            }
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

         /// <summary>
         /// DICOM C-FIND 요청으로 MWL(Modality Worklist) 환자 목록을 조회합니다.
         /// </summary>
         /// <param name="sourceAET">로컬 AE Title</param>
         /// <param name="targetIP">MWL 서버 IP</param>
         /// <param name="targetPort">MWL 서버 Port</param>
         /// <param name="targetAET">MWL 서버 AE Title</param>
         private async Task<List<PatientModel>> GetWorklistPatientsAsync( string sourceAET, string targetIP, int targetPort, string targetAET)
        {
            var result = new List<PatientModel>();

         var request = BuildWorklistRequest();
         request.OnResponseReceived += (_, res) =>
         {
             if (res.Status == DicomStatus.Pending && res.Dataset != null)
                 result.Add(ParsePatientModel(res.Dataset));
         };

         var client = DicomClientFactory.Create(targetIP, targetPort, false, sourceAET, targetAET);
         client.NegotiateAsyncOps();
         await client.AddRequestAsync(request);

         // 5초 내 응답 없으면 TimeoutException
         var sendTask = client.SendAsync();
         if (await Task.WhenAny(sendTask, Task.Delay(5000)) == sendTask)
             await sendTask; // 전송 중 발생한 예외 전파
         else
             throw new TimeoutException("DICOM 서버가 응답하지 않습니다.");

         return result;
         }

         /// <summary>
         /// 전체 환자 대상 MWL C-FIND 요청 Dataset을 생성합니다.
         /// </summary>
         private static DicomCFindRequest BuildWorklistRequest()
        {
            return new DicomCFindRequest(DicomQueryRetrieveLevel.NotApplicable)
            {
                Dataset = new DicomDataset
                {
                    { DicomTag.PatientName,                    "*" },
                    { DicomTag.PatientID,                      "*" },
                    { DicomTag.StudyInstanceUID,               ""  },
                    { DicomTag.StudyDate,                      ""  },
                    { DicomTag.PatientBirthDate,               ""  },
                    { DicomTag.PatientSex,                     ""  },
                    { DicomTag.AccessionNumber,                ""  },
                    { DicomTag.RequestedProcedureDescription,  ""  },
                }
            };
        }

         /// <summary>
         /// C-FIND 응답 Dataset을 PatientModel로 변환합니다.
         /// </summary>
         private static PatientModel ParsePatientModel(DicomDataset ds)
        {
            string rawId = ds.GetSingleValueOrDefault(DicomTag.PatientID, "");
            string rawBirth = ds.GetSingleValueOrDefault(DicomTag.PatientBirthDate, "");

            return new PatientModel
            {
                PatientCode = int.TryParse(rawId, out int code) ? code : 0,
                PatientName = ds.GetSingleValueOrDefault(DicomTag.PatientName, "").Replace("^", " "),
                BirthDate = DateTime.TryParseExact(rawBirth, "yyyyMMdd", null,
                                  System.Globalization.DateTimeStyles.None, out DateTime birth)
                                  ? birth : DateTime.MinValue,
                Sex = ds.GetSingleValueOrDefault(DicomTag.PatientSex, ""),
            };
         }
    }
}
