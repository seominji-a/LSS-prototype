using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.IO;
using System.Windows;

/// <summary>
//  작성자 : 박한용
/// 목적 : SQLite DB 테이블 구조 공유
/// 설명 : init.sql ( CREATE 구문 ), SEED.sql( INSERT 구문 )
/// 주의 : 테이블 내 값들은 공유안됨. 
/// </summary>
namespace LSS_prototype
{
    class DB_Manager
    {
        private readonly string _dbPath = Common.DB_PATH; // .db 파일 위치 
        private readonly string _db_init_Path = Common.DB_INIT_PATH; // .init 파일 위치 
        private readonly string _db_seed_Path = Common.DB_SEED_PATH; // .seed 파일 위치
        private readonly int _db_version = Common.DB_VERSION; // 로컬에 저장된 db 버전                                                              

        public void InitDB()
        {
            bool first_create = false; // DB 최초 생성 판별 변수
            
            try
            {           
                bool dbExists = File.Exists(_dbPath);

                if (dbExists == false)   // .db 파일이 존재하는가 ? 
                {
                    SQLiteConnection.CreateFile(_dbPath);
                    first_create = true; 
                }

                else // 존재했을땐, DB상의 버전과, 코드상의 버전을 확인
                {
                    SQLiteConnection conn_ = new SQLiteConnection("Data Source=" + Common.DB_PATH);
                    conn_.Open();

                    SQLiteCommand cmd = new SQLiteCommand("SELECT version FROM DB_VERSION", conn_);
                    int dbVersion = Convert.ToInt32(cmd.ExecuteScalar());

                    conn_.Close();

                    if (dbVersion < this._db_version)
                    {
                        MessageBoxResult result = MessageBox.Show(
                            "DB 버전이 다릅니다.\n기존 로컬 DB가 삭제되고 신규 DB가 생성됩니다.\n진행하시겠습니까? ( 기존 데이터 삭제 )",
                            "DB 업데이트 확인",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            File.Delete(Common.DB_PATH);
                            InitDB();
                            return;
                        }
                        else return;
                    }
                }

                if (first_create) // CREATE 및 INSERT ( 기초 데이터 셋팅 ) 
                {
                    var conn = new SQLiteConnection($"Data Source={_dbPath};");
                    conn.Open();

                    string initSql = File.ReadAllText(_db_init_Path);
                    new SQLiteCommand(initSql, conn).ExecuteNonQuery();

                    string seedSql = File.ReadAllText(_db_seed_Path);
                    new SQLiteCommand(seedSql, conn).ExecuteNonQuery();

                    SQLiteCommand verCmd = new SQLiteCommand(Query.INSERT_DB_VERSION, conn);
                    verCmd.Parameters.AddWithValue("@version", this._db_version);
                    verCmd.ExecuteNonQuery();

                    conn.Close();
                }
            }
            catch (IOException)
            {
                MessageBox.Show(
                    "DB 파일이 다른 프로그램에서 열려 있습니다.\n\n" +
                    "DB를 닫으신 후 프로그램을 재실행하세요.",
                    "DB 파일 업데이트 실패",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB Init Error : " + ex.Message);
                if(first_create) File.Delete(_dbPath); // db생성은 정상이지만 테이블 생성에서 에러난 경우 db자체 삭제
                throw; //이 부분이 제대로 처리안되면 프로그램 실행 자체 STOP 
            }
        }

    }
}
