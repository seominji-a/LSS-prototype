using System;

namespace LSS_prototype.Patient_Page
{
    /// <summary>
    /// 화면에 간단하게 DB 값 로드하는건 DB_CRUD 에서 진행 
    /// Dicom과 관련된 복잡한 데이터 처리는 Model에서 진행 
    /// </summary>
    public class PatientModel
    {
        public int PatientId { get; set; }
        public int PatientCode { get; set; }
        public string PatientName { get; set; }

        public DateTime BirthDate { get; set; }

        public string Sex { get; set; }

        public DateTime Reg_Date { get; set; }
    }

    
}
