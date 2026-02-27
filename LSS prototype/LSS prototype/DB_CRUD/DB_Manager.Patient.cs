using LSS_prototype.Patient_Page;
using LSS_prototype.User_Page;
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
        public bool AddPatient(PatientModel patient)
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
                                PatientName = reader["PATIENT_NAME"].ToString(),
                                BirthDate = Convert.ToDateTime(reader["BIRTH_DATE"]),
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
        public bool UpdatePatient(PatientModel vm)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(Query.EDIT_PATIENT, conn))
                {
                    cmd.Parameters.AddWithValue("@Patient_id", vm.PatientId);
                    cmd.Parameters.AddWithValue("@PatientName", vm.PatientName);
                    cmd.Parameters.AddWithValue("@PatientCode", vm.PatientCode);
                    cmd.Parameters.AddWithValue("@BirthDate", vm.BirthDate);
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

        #region [ 환자 번호 존재 여부 판단 담당부 ]
        public bool ExistsPatientCode(int patientCode)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.PATIENT_CODE_SEARCH, conn))
                {
                    cmd.Parameters.AddWithValue("@PatientCode", patientCode);
                    long count = (long)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }
        #endregion

        #region [ 자기 자신 환자 번호 존재 여부 판단 담당부 ]
        public bool ExistsPatientCodeExceptSelf(int code, int id)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(Query.PATIENT_CODE_SEARCHSELF, conn))
                {
                    cmd.Parameters.AddWithValue("@PatientCode", code);
                    cmd.Parameters.AddWithValue("@Patient_id", id);

                    // 기본 ExecuteScalar는 object를 반환하므로 형변환 필요
                    var result = cmd.ExecuteScalar();
                    return result != null && Convert.ToInt32(result) > 0;
                }
            }
        }
        #endregion

        #region [ 환자 이름 및 코드 검색 ]
        public List<PatientModel> SearchPatients(string keyword)
        {
            var list = new List<PatientModel>();
            string pattern = "%" + keyword.Trim() + "%";

            using (var conn = new SQLiteConnection("Data Source=" + Common.DB_PATH))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.SEARCH_PATIENT, conn))
                {
                    cmd.Parameters.AddWithValue("@keyword", pattern);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new PatientModel
                            {
                                // 쿼리에서 SELECT 한 컬럼명과 정확히 일치해야 합니다.
                                PatientId = reader["PATIENT_ID"] != DBNull.Value ? Convert.ToInt32(reader["PATIENT_ID"]) : 0,
                                PatientCode = reader["PATIENT_CODE"] != DBNull.Value ? Convert.ToInt32(reader["PATIENT_CODE"]) : 0,
                                PatientName = reader["PATIENT_NAME"]?.ToString() ?? "",
                                BirthDate = reader["BIRTH_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["BIRTH_DATE"]) : DateTime.MinValue,
                                Sex = reader["SEX"]?.ToString() ?? "",
                                Reg_Date = reader["REG_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["REG_DATE"]) : DateTime.Now
                            });
                        }
                    }
                }
            }
            return list;
        }
        #endregion
    }
}
