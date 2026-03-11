using System;

namespace LSS_prototype.Patient_Page
{
    /// <summary>
    /// 화면에 간단하게 DB 값 로드하는건 DB_CRUD 에서 진행 
    /// Dicom과 관련된 복잡한 데이터 처리는 Model에서 진행 
    /// </summary>
    /// 
    public enum PatientSource
    {
        Local = 0,        // AccessionNumber 없음
        EmrImported = 1,   // AccessionNumber 있음 (파일로 들어온 EMR성 데이터)
        ImportLocal,   // Import 했는데 AccessionNumber 없음
        ImportEmr,     // Import 했는데 AccessionNumber 있음
    }

    public class PatientModel
    {
        public int PatientId { get; set; }
        public int PatientCode { get; set; }
        public string PatientName { get; set; }

        public DateTime BirthDate { get; set; }

        public string Sex { get; set; }

        public DateTime Reg_Date { get; set; }
        public string AccessionNumber { get; set; }//★ 중요 EMR과 LOCAL 데이터를 나누는 기준 컬럼 

        public bool IsEmrPatient { get; set; }   // 화면 표시용(저장 안 함)
        public PatientSource Source { get; set; }

        public FellowOakDicom.DicomDataset Dataset { get; set; }
    }
}
