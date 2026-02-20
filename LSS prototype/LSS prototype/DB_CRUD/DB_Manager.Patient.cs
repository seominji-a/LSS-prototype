using LSS_prototype.Patient_Page;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype.DB_CRUD
{
    public partial class DB_Manager
    {


        #region [ 환자 생성 담당부 ]
        public bool AddPatient(PatientAddViewModel patient)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.INSERT_PATIENT, conn))
                {
                    cmd.Parameters.AddWithValue("@PatientName", patient.PatientName);
                    cmd.Parameters.AddWithValue("@PatientCode", patient.PatientCode);
                    cmd.Parameters.AddWithValue("@BirthDate", patient.BirthDate);
                    cmd.Parameters.AddWithValue("@Sex", patient.Sex);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
        #endregion

        #region [ 환자 로드 담당부 ]
        public List<PatientModel> GetAllPatients()
        {
            // 리스트 생성 시에도 PatientModel을 사용합니다.
            List<PatientModel> list = new List<PatientModel>();

            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.SELECT_PATIENTLIST, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // 'Patient' 창 객체가 아닌 'PatientModel' 데이터 객체를 생성합니다.
                            list.Add(new PatientModel
                            {
                                PatientId = Convert.ToInt32(reader["PATIENT_ID"]),
                                PatientCode = Convert.ToInt32(reader["PATIENT_CODE"]),
                                Name = reader["PATIENT_NAME"].ToString(),
                                BRITH_DATE = Convert.ToDateTime(reader["BIRTH_DATE"]),
                                Sex = reader["SEX"].ToString()
                            });
                        }
                    }
                }
            }
            return list;
        }
        #endregion

        #region [ 환자 수정 담당부 ]
        public bool UpdatePatient(PatientEditViewModel vm)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(Query.EDIT_PATIENT, conn))
                {
                    cmd.Parameters.AddWithValue("@Patient_id", vm.Patient_id);
                    cmd.Parameters.AddWithValue("@PatientName", vm.PatientName);
                    cmd.Parameters.AddWithValue("@PatientCode", vm.PatientCode);
                    cmd.Parameters.AddWithValue("@BirthDate", vm.BirthDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@Sex", vm.Sex);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
        #endregion

        #region [ 환자 삭제 담당부 ]
        public bool DeletePatient(int patientId)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.DELETE_PATIENT, conn))
                {
                    // 고유 ID만 있으면 삭제가 가능합니다.
                    cmd.Parameters.AddWithValue("@Patient_id", patientId);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
        #endregion
    }
}
