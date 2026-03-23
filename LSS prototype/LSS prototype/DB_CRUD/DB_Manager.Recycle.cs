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
        /// <summary>
        /// 만료기한이 지난, 데이터 삭제
        /// </summary>
        /// <returns></returns>
        public List<RecoveryModel> GetExpiredLogs()
        {
            var list = new List<RecoveryModel>();
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.SELECT_EXPIRED_LOGS, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new RecoveryModel
                        {
                            DeleteId = Convert.ToInt32(reader["DELETE_ID"]),
                            FileType = reader["FILE_TYPE"].ToString(),
                            ImagePath = reader["IMAGE_PATH"] == DBNull.Value ? null : reader["IMAGE_PATH"].ToString(),
                            AviPath = reader["AVI_PATH"] == DBNull.Value ? null : reader["AVI_PATH"].ToString(),
                            DicomPath = reader["DICOM_PATH"] == DBNull.Value ? null : reader["DICOM_PATH"].ToString(),
                            PatientCode = Convert.ToInt32(reader["PATIENT_CODE"]),
                            PatientName = reader["PATIENT_NAME"].ToString(),
                        });
                    }
                }
            }
            return list;
        }
        public bool InsertNormalVideoDeleteLog(string aviPath, int patientCode, string patientName)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.INSERT_NORMAL_VIDEO_DELETE_LOG, conn))
                {
                    cmd.Parameters.AddWithValue("@DeletedBy", Common.CurrentUserId);
                    cmd.Parameters.AddWithValue("@FileType", "NORMAL_VIDEO");
                    cmd.Parameters.AddWithValue("@AviPath", aviPath);
                    cmd.Parameters.AddWithValue("@PatientCode", patientCode);
                    cmd.Parameters.AddWithValue("@PatientName", patientName);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool InsertDicomVideoDeleteLog(string aviPath, string dcmPath, int patientCode, string patientName)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.INSERT_DICOM_VIDEO_DELETE_LOG, conn))
                {
                    cmd.Parameters.AddWithValue("@DeletedBy", Common.CurrentUserId);
                    cmd.Parameters.AddWithValue("@FileType", "DICOM_VIDEO");
                    cmd.Parameters.AddWithValue("@AviPath", aviPath);
                    cmd.Parameters.AddWithValue("@DicomPath", dcmPath);
                    cmd.Parameters.AddWithValue("@PatientCode", patientCode);
                    cmd.Parameters.AddWithValue("@PatientName", patientName);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        /// <summary>
        /// 환자 강제 삭제 시 delete log 테이블 및 patient 테이블 트랜잭션 처리 추가 0323 박한용
        /// </summary>
        /// <param name="deleteId"></param>
        /// <param name="patientCode"></param>
        /// <param name="patientName"></param>
        /// <returns></returns>
        public bool ForceDeletePatientWithLog(int deleteId, int patientCode, string patientName, string deletedBy = null)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. DELETE_LOG UPDATE_FORCE_DELETED
                        using (var cmd = new SQLiteCommand(Query.UPDATE_FORCE_DELETED, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@DeleteId", deleteId);
                            cmd.Parameters.AddWithValue("@ForceDeletedBy", deletedBy ?? Common.CurrentUserId); // 
                            if (cmd.ExecuteNonQuery() <= 0)
                            {
                                transaction.Rollback();
                                return false;
                            }
                        }

                        // 2. PATIENT 테이블 완전 DELETE
                        using (var cmd = new SQLiteCommand(Query.DELETE_PATIENT_BY_CODE_AND_NAME, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@PatientCode", patientCode);
                            cmd.Parameters.AddWithValue("@PatientName", patientName);
                            if (cmd.ExecuteNonQuery() <= 0)
                            {
                                transaction.Rollback();
                                return false;
                            }
                        }

                        // ✅ 이슈 G 수정: 같은 환자의 IMAGE/VIDEO 행도 IS_FORCE_DELETED = 'Y' 로 업데이트
                        // PATIENT 강제삭제 시 폴더째 삭제되므로 DELETE_LOG 이력도 동일하게 맞춰줌
                        // 관련 행 없어도 괜찮으니 결과 체크 안함
                        using (var cmd = new SQLiteCommand(Query.FORCE_DELETE_RELATED_LOGS, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@PatientCode", patientCode);
                            cmd.Parameters.AddWithValue("@PatientName", patientName);
                            cmd.Parameters.AddWithValue("@ForceDeletedBy", deletedBy ?? Common.CurrentUserId);
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        return false;
                    }
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
                            IsForceDeleted = reader["IS_FORCE_DELETED"].ToString(),
                            ForceDeletedBy = reader["FORCE_DELETED_BY"] == DBNull.Value ? null : reader["FORCE_DELETED_BY"].ToString(), 
                            PatientDeleted = reader["PATIENT_DELETED"].ToString(),
                        }); ;
                    }
                }
            }
            return list;
        }

        // ================================================
        // DELETE_LOG - 복구 처리 UPDATE
        // 복구 버튼 클릭 시 IS_RECOVERED = Y, RECOVERED_AT = 현재시간 ( 환자 복구는 RestorePatient 함수에서 처리 ) 
        // DELETE_ID 기준으로 단 1건만 업데이트
        // ================================================
        /// <summary>
        /// 영상 및 이미지 복구 함수 
        /// </summary>
        /// <param name="deleteId"></param>
        /// <returns></returns>
        public bool UpdateRecovered(int deleteId)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.UPDATE_RECOVERED, conn))
                {
                    cmd.Parameters.AddWithValue("@DeleteId", deleteId);
                    cmd.Parameters.AddWithValue("@RecoveredBy", Common.CurrentUserId); // 누가 복구했는지도 추적가능하도록 추가 
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // ================================================
        // DELETE_LOG - 복구 처리 UPDATE (only Patient,나머지 이미지 및 영상은 UpdateRecovered < 이 함수에서 처리 ) 
        // ================================================
        /// <summary>
        /// 환자 복구 함수 
        /// </summary>
        /// <param name="patientCode"></param>
        /// <param name="patientName"></param>
        /// <returns></returns>
        public bool RestorePatient(int patientCode, string patientName)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.RESTORE_PATIENT, conn))
                {
                    cmd.Parameters.AddWithValue("@PatientCode", patientCode);
                    cmd.Parameters.AddWithValue("@PatientName", patientName);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        /// <summary>
        /// 환자 복구 트랙잭션 함수 why? DELETE_LOG 테이블과 PATIENT 테이블 둘다 성공 OR 실패 보장을 받아야함 0323 박한용
        /// </summary>
        /// <param name="deleteId"></param>
        /// <param name="patientCode"></param>
        /// <param name="patientName"></param>
        /// <returns></returns>
        public bool RecoverPatientWithLog(int deleteId, int patientCode, string patientName)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. PATIENT IS_DELETED = 'N' 복구
                        using (var cmd = new SQLiteCommand(Query.RESTORE_PATIENT, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@PatientCode", patientCode);
                            cmd.Parameters.AddWithValue("@PatientName", patientName);
                            if (cmd.ExecuteNonQuery() <= 0)
                            {
                                transaction.Rollback();
                                return false;
                            }
                        }

                        // 2. DELETE_LOG IS_RECOVERED = 'Y' 업데이트
                        using (var cmd = new SQLiteCommand(Query.UPDATE_RECOVERED, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@DeleteId", deleteId);
                            cmd.Parameters.AddWithValue("@RecoveredBy", Common.CurrentUserId);
                            if (cmd.ExecuteNonQuery() <= 0)
                            {
                                transaction.Rollback();
                                return false;
                            }
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
            }
        }

        // ================================================
        // DELETE_LOG - 강제 삭제 처리 UPDATE
        // IS_FORCE_DELETED = Y, FORCE_DELETED_AT = 현재시간
        // DELETE_ID 기준으로 단 1건만 업데이트
        // ================================================
        public bool UpdateForceDeleted(int deleteId, string deletedBy = null)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.UPDATE_FORCE_DELETED, conn))
                {
                    cmd.Parameters.AddWithValue("@DeleteId", deleteId);
                    cmd.Parameters.AddWithValue("@ForceDeletedBy", deletedBy ?? Common.CurrentUserId); // system으로 삭제된 경우를 커버하기 위해 
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
    }
}