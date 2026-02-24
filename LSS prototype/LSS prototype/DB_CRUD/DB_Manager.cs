using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Threading;
using System.Security.Cryptography;

/// <summary>
//  작성자 : 박한용
/// 목적 : SQLite DB 테이블 구조 공유
/// 설명 : init.sql ( CREATE 구문 ), SEED.sql( INSERT 구문 )
/// 주의 : 테이블 내 값들은 공유안됨. 
/// </summary>
namespace LSS_prototype.DB_CRUD
{
    public partial class DB_Manager
    {
        #region [ 전역변수 선언부 ]
        private readonly string _dbPath = Common.DB_PATH; // .db 파일 위치 
        private readonly string _db_init_Path = Common.DB_INIT_PATH; // .init 파일 위치 
        private readonly string _db_seed_Path = Common.DB_SEED_PATH; // .seed 파일 위치
        private readonly int _db_version = Common.DB_VERSION; // 로컬에 저장된 db 버전
        #endregion

        #region [ DB 생성 및 버전확인 ] 
        public void InitDB()
        {
            bool first_create = false;

            try
            {
                bool dbExists = File.Exists(_dbPath);

                if (!dbExists) // DB 파일 미존재 시, DB 및 테이블 생성 
                {
                    SQLiteConnection.CreateFile(_dbPath);
                    first_create = true;
                }
                else // DB파일 존재 시 버전 확인 -> 사용자에게 업데이트 YES/NO 물어보고 진행 
                {
                    bool needRecreate = false;

                    using (var conn_ = new SQLiteConnection($"Data Source={Common.DB_PATH};"))
                    {
                        conn_.Open();

                        using (var cmd = new SQLiteCommand(Query.SELECT_VERSION, conn_))
                        {
                            int dbVersion = Convert.ToInt32(cmd.ExecuteScalar());

                            if (dbVersion < this._db_version)
                            {
                                var result = CustomMessageWindow.Show(
                                         "DB 버전이 다릅니다.\n 기존 로컬 DB가 삭제되고 신규 DB가 생성됩니다.\n진행하시겠습니까?",
                                         CustomMessageWindow.MessageBoxType.YesNo,
                                         autoCloseSeconds: 30,
                                         icon: CustomMessageWindow.MessageIconType.Danger);

                                if (result == CustomMessageWindow.MessageBoxResult.Yes)
                                    needRecreate = true;
                                else
                                    return;
                            }
                        }
                    }

                    if (needRecreate)
                    {
                        SQLiteConnection.ClearAllPools();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();

                        Thread.Sleep(300); // 자원이 정리될 최소한의 시간 확보

                        if (File.Exists(Common.DB_PATH))
                            File.Delete(Common.DB_PATH);

                        InitDB(); // 자원 해제 및 기존 db 삭제 후 재귀방식으로 InitDB 함수 재호출
                        return;
                    }
                }

                if (first_create)
                {
                    using (var conn = new SQLiteConnection($"Data Source={_dbPath};"))
                    {
                        conn.Open();

                        string initSql = File.ReadAllText(_db_init_Path);
                        using (var initCmd = new SQLiteCommand(initSql, conn))
                            initCmd.ExecuteNonQuery();

                        string seedSql = File.ReadAllText(_db_seed_Path);
                        using (var seedCmd = new SQLiteCommand(seedSql, conn))
                            seedCmd.ExecuteNonQuery();

                        using (var verCmd = new SQLiteCommand(Query.INSERT_DB_VERSION, conn))
                        {
                            verCmd.Parameters.AddWithValue("@version", this._db_version);
                            verCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB Init Error : " + ex.Message);

                if (first_create && File.Exists(_dbPath))
                    File.Delete(_dbPath);

                throw;
            }
        }
        #endregion

        #region [ ADMIN 권한 ID 조회 ]
        public List<string> SelectAdminLoginIds()
        {
            var list = new List<string>();

            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(Query.ADMIN_ID_SEARCH, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(reader["LOGIN_ID"].ToString());
                    }
                }
            }

            return list;
        }
        #endregion
    }
}
