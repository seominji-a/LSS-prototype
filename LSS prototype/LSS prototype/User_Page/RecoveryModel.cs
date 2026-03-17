
namespace LSS_prototype.User_Page
{
    public class RecoveryModel
    {
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
    }
}