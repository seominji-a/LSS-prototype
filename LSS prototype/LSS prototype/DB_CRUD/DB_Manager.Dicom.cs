using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using LSS_prototype.Patient_Page;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype.DB_CRUD
{
    public partial class DB_Manager
    {
        /// <summary>
        /// DICOM C-FIND 요청으로 MWL 환자 목록을 조회합니다.
        /// </summary>
        public async Task<List<PatientModel>> GetWorklistPatientsAsync(string sourceAET, string targetIP, int targetPort, string targetAET)
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

            var sendTask = client.SendAsync();
            if (await Task.WhenAny(sendTask, Task.Delay(5000)) == sendTask)
                await sendTask;
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
                    { DicomTag.PatientName,                   "*" },
                    { DicomTag.PatientID,                     "*" },
                    { DicomTag.StudyInstanceUID,              ""  },
                    { DicomTag.StudyDate,                     ""  },
                    { DicomTag.PatientBirthDate,              ""  },
                    { DicomTag.PatientSex,                    ""  },
                    { DicomTag.AccessionNumber,               ""  },
                    { DicomTag.RequestedProcedureDescription, ""  },
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
                AccessionNumber = ds.GetSingleValueOrDefault(DicomTag.AccessionNumber, ""), 
            };
        }
    
}
}
