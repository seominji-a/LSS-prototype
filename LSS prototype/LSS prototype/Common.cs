using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype
{
    /// <summary>
    //  작성자 : 박한용
    /// 목적 : 경로, 파일명등 공용변수를 선언하기 위함
    /// 주의 : Const 사용 ( 변경 절대 불가 )
    /// </summary>
    public static class Common
    {
        public const string DB_PATH = "./LSS_TEST.db"; // .db 경로 
        public const string DB_INIT_PATH = "../../../DB/db_init.sql"; // 초기 DB 테이블 생성 파일 경로 
        public const string DB_SEED_PATH = "../../../DB/seed.sql"; // 초기 DB 테이블 데이터 생성 경로 
        public const int DB_VERSION = 3; // DB Version 
    }

    /// <summary>
    /// 모든 쿼리는 해당 클래스 내에서 관리 ( 코드상 직접 기입 지양 )
    /// </summary>
    public static class Query
    {
        public const string INSERT_DB_VERSION = "INSERT INTO DB_VERSION(version) VALUES (@version)"; // DB VERSION INSERT QUERY 
    }

}