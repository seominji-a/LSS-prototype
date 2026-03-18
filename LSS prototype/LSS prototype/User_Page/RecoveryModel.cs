
namespace LSS_prototype.User_Page
{
    public class RecoveryModel
    {
        public bool IsCheckable
        {
            get
            {
                if (IsExpired) return false; // 만료된 항목
                if (IsRecovered == "Y") return false; // 복구완료된 항목
                if (IsForceDeleted == "Y") return false; // 강제삭제된 항목
                return true;
            }
        }

        // DB 컬럼과 1:1 매핑
        public int DeleteId { get; set; }   // DELETE_ID
        public string DeletedBy { get; set; }   // DELETED_BY
        public string DeletedAt { get; set; }   // DELETED_AT
        public string FileType { get; set; }   // FILE_TYPE
        public string ImagePath { get; set; }   // IMAGE_PATH
        public string AviPath { get; set; }   // AVI_PATH
        public string DicomPath { get; set; }   // DICOM_PATH
        public int PatientCode { get; set; }   // PATIENT_CODE
        public string IsRecovered { get; set; }   // IS_RECOVERED
        public string RecoveredAt { get; set; }   // RECOVERED_AT

        // 화면용 - ViewModel에서 계산해서 채워줌
        public string PatientName { get; set; }   // PATIENT_CODE로 DB 조회
                                                  
        public string DisplayName => PatientName?.Replace("^", " ") ?? "";// 화면 표시용 - ^ 제거
        public string RemainText { get; set; }   // 예) "64시간 32분"
        public bool IsExpired { get; set; }   // 만료 여부
        public string IsForceDeleted { get; set; }
        // ★ 추가 - 체크박스 바인딩용
        // 강제 삭제 선택 여부 (UI 전용, DB 컬럼 아님)
        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                OnPropertyChanged();
            }
        }

        // INotifyPropertyChanged (체크박스 바인딩에 필요해서 추가했음 ) 
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}


