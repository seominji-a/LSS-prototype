using System;
using System.Data.SQLite;

namespace LSS_prototype.DB_CRUD
{
    public partial class DB_Manager
    {
        // ================================================
        // COMMENT 저장 (UPSERT)
        // 없으면 INSERT, 있으면 UPDATE
        // 빈 코멘트면 행 삭제 (깔끔하게 유지)
        // ================================================
        public bool UpsertComment(string fileType, string fileName, string comment)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();

                if (string.IsNullOrWhiteSpace(comment))
                {
                    using (var cmd = new SQLiteCommand(Query.DELETE_COMMENT, conn))
                    {
                        cmd.Parameters.AddWithValue("@FileType", fileType);
                        cmd.Parameters.AddWithValue("@FileName", fileName);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }

                using (var cmd = new SQLiteCommand(Query.UPSERT_COMMENT, conn))
                {
                    cmd.Parameters.AddWithValue("@FileType", fileType);
                    cmd.Parameters.AddWithValue("@FileName", fileName);
                    cmd.Parameters.AddWithValue("@Comment", comment);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // ================================================
        // COMMENT 조회
        // 없으면 string.Empty 반환
        // NORMAL_VIDEO 로드 시에만 호출
        // ================================================
        public string SelectComment(string fileType, string fileName)
        {
            using (var conn = new SQLiteConnection($"Data Source={Common.DB_PATH}"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.SELECT_COMMENT, conn))
                {
                    cmd.Parameters.AddWithValue("@FileType", fileType);
                    cmd.Parameters.AddWithValue("@FileName", fileName);
                    var result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value
                        ? string.Empty
                        : result.ToString();
                }
            }
        }
    }
}