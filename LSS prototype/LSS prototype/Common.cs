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

        public const int DB_VERSION = 29; // DB Version 

    }

    /// <summary>
    /// 모든 쿼리는 해당 클래스 내에서 관리 ( 코드상 직접 기입 지양 )
    /// </summary>
    public static class Query
    {
        public const string INSERT_DB_VERSION = "INSERT INTO DB_VERSION(version) VALUES (@version)"; // DB VERSION INSERT QUERY 
        public const string SELECT_VERSION = "SELECT version FROM DB_VERSION"; // DB 버전 SELECT QUERY
        public const string LOGIN_HASH_CHECK = "SELECT PASSWORD_HASH, PASSWORD_SALT,ROLE_CODE FROM USER WHERE LOGIN_ID = @loginId"; // 로그인 시 해싱및 솔트값 확인 쿼리문
        public const string INSERT_PATIENT = "INSERT INTO PATIENT (PATIENT_NAME,PATIENT_CODE, BIRTH_DATE, SEX ) VALUES (@PatientName, @PatientCode, @BirthDate, @Sex)";
        public const string SELECT_PATIENT_LIST = "SELECT * FROM PATIENT";
        public const string INSERT_ADD_USER = "INSERT INTO USER(LOGIN_ID, PASSWORD_HASH,PASSWORD_SALT, USER_NAME, USER_ROLE, DEVICE_ID, ROLE_CODE)" +
                                              " VALUES (@loginId, @hash, @salt, @userName, @userRole, @device_id, @role_code)"; // 사용자 추가
        public const string EDIT_PATIENT = "UPDATE PATIENT SET PATIENT_NAME = @PatientName, PATIENT_CODE= @PatientCode, BIRTH_DATE = @BirthDate, SEX = @Sex WHERE PATIENT_ID = @Patient_id";
        public const string SELECT_PATIENTLIST = "SELECT * FROM PATIENT";
        public const string DELETE_PATIENT = "DELETE FROM PATIENT WHERE PATIENT_ID = @Patient_id";


    }
}