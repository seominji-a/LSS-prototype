using System;
using System.Collections.Generic;

namespace LSS_prototype.Patient_Page
{
    /// <summary>
    /// 화면에 간단하게 DB 값 로드하는건 DB_CRUD 에서 진행 
    /// Dicom과 관련된 복잡한 데이터 처리는 Model에서 진행 
    /// </summary>

    /// 화면 표시용
    public enum PatientSource
    {
        Local = 0,   // Integrated 화면의 LOCAL
        Emr = 1,     // EMR 화면의 MWL 조회 환자
        ESync = 2    // Integrated 화면의 촬영 완료 E-SYNC
    }

    //DB 저장용
    public enum PatientSourceType
    {
        Local = 0,   // DB 저장용
        ESync = 1    // DB 저장용
    }

    public class PatientModel
    {
        public int PatientId { get; set; }
        public int PatientCode { get; set; }
        public string PatientName { get; set; }

        // 화면 표시용 - ^ 제거 (원본 PatientName은 그대로 유지)
        public string DisplayName => PatientName?.Replace("^", " ") ?? "";

        public DateTime BirthDate { get; set; }

        public string Sex { get; set; }

        public DateTime Reg_Date { get; set; }

        //DB 저장용
        public int SourceType { get; set; }
        public DateTime? LastShootDate { get; set; }
        public int ShotNum { get; set; }

        public string AccessionNumber { get; set; }//★ 중요 EMR과 LOCAL 데이터를 나누는 기준 컬럼 

        public bool IsEmrPatient { get; set; }   // 화면 표시용(저장 안 함)

        //화면 및 로직용
        public PatientSource Source { get; set; }

        public FellowOakDicom.DicomDataset Dataset { get; set; }

        public string RequestedProcedureDescription { get; set; }

        public List<string> DcmFiles { get; set; } = new List<string>();
        public List<string> AviFiles { get; set; } = new List<string>();

        public HashSet<string> StudyIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    }


}
