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
                    cmd.Parameters.AddWithValue("@SourceType", patient.SourceType);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
        #endregion

        #region [ 환자 로드 담당부 ]
        public List<PatientModel> GetAllPatients()
        {
            List<PatientModel> list = new List<PatientModel>();

            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.SELECT_PATIENTLIST, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(MapPatient(reader));
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
                            list.Add(MapPatient(reader));
                        }
                    }
                }
            }
            return list;
        }
        #endregion

        private PatientModel MapPatient(SQLiteDataReader reader)
        {
            return new PatientModel
            {
                PatientId = reader["PATIENT_ID"] != DBNull.Value ? Convert.ToInt32(reader["PATIENT_ID"]) : 0,
                PatientCode = reader["PATIENT_CODE"] != DBNull.Value ? Convert.ToInt32(reader["PATIENT_CODE"]) : 0,
                PatientName = reader["PATIENT_NAME"]?.ToString() ?? "",
                BirthDate = reader["BIRTH_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["BIRTH_DATE"]) : DateTime.MinValue,
                Sex = reader["SEX"]?.ToString() ?? "",
                Reg_Date = reader["REG_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["REG_DATE"]) : DateTime.Now,
                SourceType = reader["SOURCE_TYPE"] != DBNull.Value ? Convert.ToInt32(reader["SOURCE_TYPE"]) : 0,
                LastShootDate = reader["LASTSHOOTDATE"] != DBNull.Value
                    ? Convert.ToDateTime(reader["LASTSHOOTDATE"])
                    : (DateTime?)null,
                ShotNum = reader["SHOTNUM"] != DBNull.Value
                    ? Convert.ToInt32(reader["SHOTNUM"])
                    : 0
            };
        }

        public List<PatientModel> GetLocalPatients()
        {
            var list = new List<PatientModel>();

            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.SELECT_LOCAL_PATIENTLIST, conn))
                {
                    cmd.Parameters.AddWithValue("@SourceType", 0); // LOCAL

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(MapPatient(reader));
                        }
                    }
                }
            }

            return list;
        }

        public List<PatientModel> GetEmrPatients()
        {
            var list = new List<PatientModel>();

            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.SELECT_EMR_PATIENTLIST, conn))
                {
                    cmd.Parameters.AddWithValue("@SourceType", 1); // EMR / E-SYNC

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(MapPatient(reader));
                        }
                    }
                }
            }

            return list;
        }

        public bool UpsertEmrPatient(PatientModel patient)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();

                using (var checkCmd = new SQLiteCommand(Query.SELECT_PATIENT_BY_CODE_AND_SOURCE, conn))
                {
                    checkCmd.Parameters.AddWithValue("@PatientCode", patient.PatientCode);
                    checkCmd.Parameters.AddWithValue("@SourceType", patient.SourceType);

                    var exists = checkCmd.ExecuteScalar();

                    if (exists != null)
                    {
                        using (var updateCmd = new SQLiteCommand(Query.UPDATE_EMR_PATIENT, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@PatientCode", patient.PatientCode);
                            updateCmd.Parameters.AddWithValue("@PatientName", patient.PatientName);
                            updateCmd.Parameters.AddWithValue("@BirthDate", patient.BirthDate);
                            updateCmd.Parameters.AddWithValue("@Sex", patient.Sex);
                            updateCmd.Parameters.AddWithValue("@LastShootDate",patient.LastShootDate?.ToString("yyyy-MM-dd HH:mm:ss"));
                            updateCmd.Parameters.AddWithValue("@ShotNum", patient.ShotNum);
                            updateCmd.Parameters.AddWithValue("@SourceType", patient.SourceType);

                            return updateCmd.ExecuteNonQuery() > 0;
                        }
                    }
                    else
                    {
                        using (var insertCmd = new SQLiteCommand(Query.INSERT_EMR_PATIENT, conn))
                        {
                            insertCmd.Parameters.AddWithValue("@PatientCode", patient.PatientCode);
                            insertCmd.Parameters.AddWithValue("@PatientName", patient.PatientName);
                            insertCmd.Parameters.AddWithValue("@BirthDate", patient.BirthDate);
                            insertCmd.Parameters.AddWithValue("@Sex", patient.Sex);
                            insertCmd.Parameters.AddWithValue("@SourceType", patient.SourceType);
                            insertCmd.Parameters.AddWithValue("@LastShootDate", patient.LastShootDate?.ToString("yyyy-MM-dd HH:mm:ss"));
                            insertCmd.Parameters.AddWithValue("@ShotNum", patient.ShotNum);

                            return insertCmd.ExecuteNonQuery() > 0;
                        }
                    }
                }
            }
        }

        public bool UpdateLocalPatientAfterScan(PatientModel patient)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(Query.UPDATE_LOCAL_PATIENT_AFTER_SCAN, conn))
                {
                    cmd.Parameters.AddWithValue("@PatientId", patient.PatientId);
                    cmd.Parameters.AddWithValue("@LastShootDate", patient.LastShootDate?.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@ShotNum", patient.ShotNum);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }


    }
}
