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
        ///
        /// ★ descriptionFilter 가 비어있으면 전체 조회
        /// ★ descriptionFilter 에 값이 있으면 해당 값과 일치하는 환자만 반환
        /// </summary>
        public async Task<List<PatientModel>> GetWorklistPatientsAsync(string sourceAET, string targetIP, int targetPort, string targetAET)  // 기본값 ICG - 비우면 전체 조회
        {
            var result = new List<PatientModel>();

            // C-FIND 요청 생성
            var request = BuildWorklistRequest();

            // 응답 수신 콜백: Pending 상태 응답마다 PatientModel 변환 후 리스트에 추가
            request.OnResponseReceived += (_, res) =>
            {
                if (res.Status == DicomStatus.Pending && res.Dataset != null)
                {
                    var patient = ParsePatientModel(res.Dataset);

                    // 필터값 비어있으면 전체, 값 있으면 일치하는 환자만 추가
                    if (!string.IsNullOrEmpty(Common.MwlDescriptionFilter) &&
                        !patient.RequestedProcedureDescription.Contains(Common.MwlDescriptionFilter))
                        return;

                    result.Add(patient);
                }
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
                    { DicomTag.SpecificCharacterSet,          ""  }, // 인코딩 정보 반환 요청
                    { DicomTag.PatientName,                   "*" }, // (0010,0010) 전체 검색
                    { DicomTag.PatientID,                     "*" }, // (0010,0020) 전체 검색
                    { DicomTag.StudyInstanceUID,              ""  }, // (0020,000D) 반환 요청
                    { DicomTag.StudyDate,                     ""  }, // (0008,0020) 반환 요청
                    { DicomTag.PatientBirthDate,              ""  }, // (0010,0030) 반환 요청
                    { DicomTag.PatientSex,                    ""  }, // (0010,0040) 반환 요청
                    { DicomTag.AccessionNumber,               ""  }, // (0008,0050) 반환 요청 EMR/LOCAL 구분 기준
                    { DicomTag.RequestedProcedureDescription, ""  }, // (0032,1060) 반환 요청
                }
            };
        }

        /// <summary>
        /// C-FIND 응답 Dataset을 PatientModel로 변환합니다.
        ///
        /// EMR/LOCAL 구분 기준:
        ///   AccessionNumber != "" → EMR 환자 (병원 RIS 접수번호 존재)
        ///   AccessionNumber == "" → LOCAL 환자 (접수번호 없음)
        ///
        /// Dataset 필드:
        ///   Save_Click에서 DicomManager(HID, Serial, Dataset) 생성자에 전달하여
        ///   MWL 원본 태그(AccessionNumber, StudyInstanceUID 등)를 보존하기 위해 보관.
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

                // 코드 방식 - 바이트 직접 꺼내서 EUC-KR 디코딩 (한글 깨짐 방지)
                PatientName = DecodeEucKr(ds, DicomTag.PatientName),

                BirthDate = DateTime.TryParseExact(
                                rawBirth, "yyyyMMdd", null,
                                System.Globalization.DateTimeStyles.None,
                                out DateTime birth)
                            ? birth
                            : DateTime.MinValue,              // 파싱 실패 시 기본값

                Sex = ds.GetSingleValueOrDefault(DicomTag.PatientSex, ""),

                // EMR/LOCAL 구분의 핵심 컬럼
                AccessionNumber = accNum,                     // EMR: RIS 접수번호 / LOCAL: ""

                // 화면 표시용 (DB 저장 안 함)
                IsEmrPatient = isEmr,

                //MWL 환자는 E-SYNC가 아닌 촬영 후보이고, E-SYNC는 촬영 완료 후 DB 저장 시점에만 된다.
                Source = PatientSource.Local,

                // Save_Click에서 DicomManager(HID, Serial, Dataset) 생성자에 전달용
                // EMR: MWL 원본 태그 전체 보관 → 저장 시 AccessionNumber 등 서버값 유지
                // LOCAL: null (빈 데이터셋으로 새로 생성)
                Dataset = isEmr ? ds : null,

                // 선배 코드 방식 - 바이트 직접 꺼내서 EUC-KR 디코딩 (한글 깨짐 방지)
                // LS 코드 방식 - 바이트 직접 꺼내서 EUC-KR 디코딩 (한글 깨짐 방지)
                RequestedProcedureDescription = DecodeEucKr(ds, DicomTag.RequestedProcedureDescription),
            };
        }

        /// <summary>
        /// DICOM 태그 값을 바이트로 직접 꺼내서 EUC-KR 디코딩
        /// fo-dicom이 문자열로 꺼내면 이미 깨진 상태라 바이트 직접 접근 필요
        /// </summary>
        private static string DecodeEucKr(DicomDataset dataset, DicomTag tag)
        {
            var bytes = dataset.GetDicomItem<DicomElement>(tag)?.Buffer?.Data;
            if (bytes == null || bytes.Length == 0) return "";

            // 0x80 미만 = ASCII 범위 → 그냥 ASCII로 디코딩
            if (bytes.All(b => b < 0x80))
                return Encoding.ASCII.GetString(bytes).Trim();

            // 0x80 이상 바이트 있으면 EUC-KR로 디코딩
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding("EUC-KR").GetString(bytes).Trim();
        }
    }
}