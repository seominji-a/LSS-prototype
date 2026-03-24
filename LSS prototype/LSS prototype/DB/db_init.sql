PRAGMA foreign_keys = ON;



-- ================================================
-- DB 버전 관리 테이블  ( 2026.02.05 생성자 : 박한용 ) 
-- ================================================
CREATE TABLE IF NOT EXISTS DB_VERSION (
    version INTEGER NOT NULL,
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

-- ================================================
-- DEVICE TABLE ( 2026.02.05 생성자 : 박한용 ) 
-- ================================================
CREATE TABLE IF NOT EXISTS DEVICE
(
    DEVICE_ID            BIGINT PRIMARY KEY,
    MANUFACTURE_SERIAL   VARCHAR(50) NOT NULL UNIQUE,
    REG_DATE             TIMESTAMP   NOT NULL DEFAULT (datetime('now', 'localtime')),
    EXPIRE_DATE          TIMESTAMP   NULL
);

-- ================================================
-- USER TABLE ( 2026.02.05 생성자 : 박한용 )
-- ================================================
CREATE TABLE IF NOT EXISTS USER
(
    USER_ID        			INTEGER PRIMARY KEY AUTOINCREMENT,
    DEVICE_ID      			BIGINT        NOT NULL,
    LOGIN_ID       			VARCHAR(50)   NOT NULL UNIQUE,
    PASSWORD_HASH  			VARCHAR(255)  NOT NULL,
	PASSWORD_SALT  			VARCHAR(255)  NOT NULL,
    ROLE_CODE      			CHAR(1)       NOT NULL,
    USER_NAME      			VARCHAR(100)  NOT NULL,
    USER_ROLE      			VARCHAR(50)   NOT NULL,
    REG_DATE       			TIMESTAMP     NOT NULL DEFAULT (datetime('now', 'localtime')),
	PASSWORD_CHANGED_AT 	TEXT,

    FOREIGN KEY (DEVICE_ID)
    REFERENCES DEVICE (DEVICE_ID)
);

-- ================================================
-- Patient TABLE ( 2026.02.09 생성자 : 서민지 )
-- Patient TABLE ( 2026.03.17 수정일 : 서민지 )
-- ================================================
CREATE TABLE IF NOT EXISTS PATIENT (
    PATIENT_ID INTEGER PRIMARY KEY AUTOINCREMENT,
    PATIENT_CODE INTEGER NOT NULL,
    PATIENT_NAME VARCHAR(50) NOT NULL,
    BIRTH_DATE DATE NOT NULL,
    SEX CHAR(1) NOT NULL,
    REG_DATE TIMESTAMP NOT NULL DEFAULT (datetime('now', 'localtime')),
    LASTSHOOTDATE DATE,
    SHOTNUM INTEGER NOT NULL DEFAULT 0,
    --문자열 보다 enum/코드화
    SOURCE_TYPE INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS STUDY (
    STUDY_ID INTEGER PRIMARY KEY AUTOINCREMENT,
    PATIENT_ID INTEGER NOT NULL,
    STUDY_INSTANCE_UID   VARCHAR(128) NOT NULL UNIQUE,
    
    -- 실제 촬영이 일어난 시간(의료 이벤트 시간)
    --DICOM 기준;STUDYDATE +STUDYTIME
    --PACS/EMR 기준 시간
    --환자가 촬영된 시점
    SCAN_DATE            TIMESTAMP,

    ICG_INJECTION_TIME   TIMESTAMP,
    ACCESSION_NUMBER     VARCHAR(50),

    --DB에 데이터가 저장된 시간(시스템 이벤트 시간)
    --로컬 DB INSERT 시간
    --IMPORT 시점/SYNC 시점
    CREATED_AT           TIMESTAMP DEFAULT (datetime('now', 'localtime')),

    FOREIGN KEY (PATIENT_ID) 
    REFERENCES PATIENT (PATIENT_ID)
);
-- ================================================
-- DEFAULT_SET TABLE ( 2026.02.27 생성자 : 박한용 )
-- 관리자 페이지 -> 카메라 기본 셋팅값 변경 관련 테이블
-- ================================================
CREATE TABLE CAMERA_DEFAULT_SET (
    EXPOSURE_TIME   DOUBLE  NOT NULL,
    GAIN            DOUBLE  NOT NULL,
    GAMMA           DOUBLE  NOT NULL,
	FOCUS		    DOUBLE  NOT NULL,
    IRIS            DOUBLE  NOT NULL,
	ZOOM		    INTEGER NOT NULL,
	FILTER		    INTEGER NOT NULL
);

-- ================================================
-- PACS 관련 저장 TABLE ( 2026.02.27 생성자 : 박한용 )
-- ================================================
CREATE TABLE PACS_SET (
    -- 병원 정보
    HOSPITAL_NAME                   TEXT    NOT NULL,

    -- C-STORE
    CSTORE_AET                      TEXT    NOT NULL,
    CSTORE_IP                       TEXT    NOT NULL,
    CSTORE_PORT                     INTEGER NOT NULL,
    CSTORE_MY_AET                   TEXT    NOT NULL,

    -- MWL
    MWL_AET                         TEXT    NOT NULL,
    MWL_IP                          TEXT    NOT NULL,
    MWL_PORT                        INTEGER NOT NULL,
    MWL_MY_AET                      TEXT    NOT NULL,
    MWL_DESCRIPTION_FILTER         TEXT
);

-- ================================================
-- DELETE_LOG TABLE ( 최종수정 2026.03.23  : 박한용 )
-- 삭제된 파일 이력 관리 테이블
--
-- FILE_TYPE 종류
--   IMAGE        : 이미지 촬영 파일 (.dcm)
--   DICOM_VIDEO  : DICOM 영상 촬영 파일 (.avi + .dcm 한 쌍)
--   NORMAL_VIDEO : 일반 영상 촬영 파일 (.avi 단독)
--
-- 경로 사용 규칙
--   IMAGE        → IMAGE_PATH = dcm경로  / AVI_PATH = NULL      / DICOM_PATH = NULL
--   NORMAL_VIDEO → IMAGE_PATH = NULL     / AVI_PATH = avi경로   / DICOM_PATH = NULL
--   DICOM_VIDEO  → IMAGE_PATH = NULL     / AVI_PATH = avi경로   / DICOM_PATH = dcm경로
--   PATIENT -> 삭제 할 때, 환자코드와 환자이름을 이용하여 폴더명을 만들고 VIDEO와 DICOM 폴더 2개를 확인해서 안에있는 환자폴더 삭제 처리 
-- IS_RECOVERED : 복구 여부 Y / N  (기본값 N)
-- RECOVERED_AT : 복구한 시간      (복구 안했으면 NULL)
-- ================================================
CREATE TABLE DELETE_LOG (
    DELETE_ID           INTEGER  PRIMARY KEY AUTOINCREMENT,
    DELETED_BY          TEXT     NOT NULL,
    DELETED_AT          DATETIME NOT NULL DEFAULT (datetime('now', 'localtime')),
    FILE_TYPE           TEXT     NOT NULL,
    IMAGE_PATH          TEXT     NULL,
    AVI_PATH            TEXT     NULL,
    DICOM_PATH          TEXT     NULL,
    PATIENT_CODE        INTEGER  NOT NULL,
    PATIENT_NAME        TEXT     NOT NULL,
    IS_RECOVERED        TEXT     NOT NULL DEFAULT 'N',
    RECOVERED_AT        DATETIME NULL,
    RECOVERED_BY        TEXT     NULL,       
    IS_FORCE_DELETED    TEXT     NOT NULL DEFAULT 'N',
    FORCE_DELETED_AT    DATETIME NULL,
    FORCE_DELETED_BY    TEXT     NULL
);