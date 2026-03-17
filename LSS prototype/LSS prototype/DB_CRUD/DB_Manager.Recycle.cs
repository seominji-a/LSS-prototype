using LSS_prototype.User_Page;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace LSS_prototype.DB_CRUD
{
    public partial class DB_Manager
    {
        // ================================================
        // DELETE_LOG - IMAGE 삭제 INSERT
        // 삭제 시 IMAGE_PATH 에 Del_ 붙은 경로 저장
        // AVI_PATH, DICOM_PATH 는 IMAGE 타입이므로 NULL
        // ================================================
        public bool InsertImageDeleteLog(string imagePath, int patientCode, string patientName)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.INSERT_IMAGE_DELETE_LOG, conn))
                {
                    cmd.Parameters.AddWithValue("@DeletedBy", Common.CurrentUserId);
                    cmd.Parameters.AddWithValue("@FileType", "IMAGE");
                    cmd.Parameters.AddWithValue("@ImagePath", imagePath);
                    cmd.Parameters.AddWithValue("@PatientCode", patientCode);
                    cmd.Parameters.AddWithValue("@PatientName", patientName);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public List<RecoveryModel> GetDeleteLogs()
        {
            var list = new List<RecoveryModel>();

            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.SELECT_DELETE_LOGS, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new RecoveryModel
                        {
                            DeleteId = Convert.ToInt32(reader["DELETE_ID"]),
                            DeletedBy = reader["DELETED_BY"].ToString(),
                            DeletedAt = reader["DELETED_AT"].ToString(),
                            FileType = reader["FILE_TYPE"].ToString(),
                            ImagePath = reader["IMAGE_PATH"] == DBNull.Value ? null : reader["IMAGE_PATH"].ToString(),
                            AviPath = reader["AVI_PATH"] == DBNull.Value ? null : reader["AVI_PATH"].ToString(),
                            DicomPath = reader["DICOM_PATH"] == DBNull.Value ? null : reader["DICOM_PATH"].ToString(),
                            PatientCode = Convert.ToInt32(reader["PATIENT_CODE"]),
                            PatientName = reader["PATIENT_NAME"].ToString(),
                            IsRecovered = reader["IS_RECOVERED"].ToString(),
                            RecoveredAt = reader["RECOVERED_AT"] == DBNull.Value ? null : reader["RECOVERED_AT"].ToString(),
                        }); ;
                    }
                }
            }
            return list;
        }

        // ================================================
        // DELETE_LOG - 복구 처리 UPDATE
        // 복구 버튼 클릭 시 IS_RECOVERED = Y, RECOVERED_AT = 현재시간
        // DELETE_ID 기준으로 단 1건만 업데이트
        // ================================================
        public bool UpdateRecovered(int deleteId)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.UPDATE_RECOVERED, conn))
                {
                    cmd.Parameters.AddWithValue("@DeleteId", deleteId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
    }
}