using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using LSS_prototype.Patient_Page;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype.Dicom_Module
{
    public partial class DicomManager
    {
        /// <summary>
        /// DICOM C-FIND 요청으로 MWL 서버에서 환자 목록을 조회합니다.
        /// 반환된 PatientModel의 AccessionNumber 유무로 EMR/LOCAL을 구분합니다.
        ///   AccessionNumber != "" → EMR 환자 (IsEmrPatient = true)
        ///   AccessionNumber == "" → LOCAL 환자 (IsEmrPatient = false)
        /// </summary>
        /// 

       

        public async Task<List<PatientModel>> GetWorklistPatientsAsync(
            string sourceAET, string targetIP, int targetPort, string targetAET)
        {
            var result = new List<PatientModel>();

            // C-FIND 요청 생성
            var request = BuildWorklistRequest();

            // 응답 수신 콜백: Pending 상태 응답마다 PatientModel 변환 후 리스트에 추가
            request.OnResponseReceived += (_, res) =>
            {
                if (res.Status == DicomStatus.Pending && res.Dataset != null)
                    result.Add(ParsePatientModel(res.Dataset));
            };

            // DICOM 클라이언트 생성 및 요청 전송
            var client = DicomClientFactory.Create(targetIP, targetPort, false, sourceAET, targetAET);
            client.NegotiateAsyncOps();
            await client.AddRequestAsync(request);

            // 5초 타임아웃 적용
            var sendTask = client.SendAsync();
            if (await Task.WhenAny(sendTask, Task.Delay(5000)) == sendTask)
                await sendTask; // 정상 완료 → 내부 예외 전파
            else
                throw new TimeoutException("DICOM 서버가 응답하지 않습니다.");

            return result;
        }

        /// <summary>
        /// 전체 환자 대상 MWL C-FIND 요청 Dataset을 생성합니다.
        /// 값이 "*" → 와일드카드 전체 검색
        /// 값이 "" → 필터 없음, 해당 필드 반환 요청
        /// </summary>
        private static DicomCFindRequest BuildWorklistRequest()
        {
            return new DicomCFindRequest(DicomQueryRetrieveLevel.NotApplicable)
            {
                Dataset = new DicomDataset
                {
                    { DicomTag.PatientName,                   "*" }, // (0010,0010) 전체 검색
                    { DicomTag.PatientID,                     "*" }, // (0010,0020) 전체 검색
                    { DicomTag.StudyInstanceUID,              ""  }, // (0020,000D) 반환 요청
                    { DicomTag.StudyDate,                     ""  }, // (0008,0020) 반환 요청
                    { DicomTag.PatientBirthDate,              ""  }, // (0010,0030) 반환 요청
                    { DicomTag.PatientSex,                    ""  }, // (0010,0040) 반환 요청
                    { DicomTag.AccessionNumber,               ""  }, // (0008,0050) 반환 요청 ★ EMR/LOCAL 구분 기준
                    { DicomTag.RequestedProcedureDescription, ""  }, // (0032,1060) 반환 요청
                }
            };
        }

        /// <summary>
        /// C-FIND 응답 Dataset을 PatientModel로 변환합니다.
        ///
        /// ★ EMR/LOCAL 구분 기준:
        ///   AccessionNumber != "" → EMR 환자 (병원 RIS 접수번호 존재)
        ///   AccessionNumber == "" → LOCAL 환자 (접수번호 없음)
        ///
        /// ★ Dataset 필드:
        ///   Save_Click에서 DicomManager(HID, Serial, Dataset) 생성자에 전달하여
        ///   MWL 원본 태그(AccessionNumber, StudyInstanceUID 등)를 보존하기 위해 보관.
        ///   JSON 직렬화 시에는 [JsonIgnore] 처리 필요.
        /// </summary>
        private static PatientModel ParsePatientModel(DicomDataset ds)
        {
            string rawId = ds.GetSingleValueOrDefault(DicomTag.PatientID, "");
            string rawBirth = ds.GetSingleValueOrDefault(DicomTag.PatientBirthDate, "");
            string accNum = ds.GetSingleValueOrDefault(DicomTag.AccessionNumber, "");

            // AccessionNumber 유무로 EMR/LOCAL 즉시 판정
            bool isEmr = !string.IsNullOrWhiteSpace(accNum);

            return new PatientModel
            {
                PatientCode = int.TryParse(rawId, out int code) ? code : 0,

                PatientName = ds.GetSingleValueOrDefault(DicomTag.PatientName, "")
                                .Replace("^", " "),           // "홍^길동" → "홍 길동"

                BirthDate = DateTime.TryParseExact(
                                rawBirth, "yyyyMMdd", null,
                                System.Globalization.DateTimeStyles.None,
                                out DateTime birth)
                            ? birth
                            : DateTime.MinValue,              // 파싱 실패 시 기본값

                Sex = ds.GetSingleValueOrDefault(DicomTag.PatientSex, ""),

                // ★ EMR/LOCAL 구분의 핵심 컬럼
                AccessionNumber = accNum,                     // EMR: RIS 접수번호 / LOCAL: ""

                // 화면 표시용 (DB 저장 안 함)
                IsEmrPatient = isEmr,
                Source = isEmr
                               ? PatientSource.EmrImported
                               : PatientSource.Local,

                // ★ Save_Click에서 DicomManager(HID, Serial, Dataset) 생성자에 전달용
                // EMR: MWL 원본 태그 전체 보관 → 저장 시 AccessionNumber 등 서버값 유지
                // LOCAL: null (빈 데이터셋으로 새로 생성)
                Dataset = isEmr ? ds : null,
            };
        }
    }
}
