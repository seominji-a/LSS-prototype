PRAGMA foreign_keys = ON;



-- ================================================
-- DB 버전 관리 테이블  ( 2026.02.05 생성자 : 박한용 ) 
-- ================================================
CREATE TABLE IF NOT EXISTS DB_VERSION (
    version INTEGER NOT NULL,
    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- ================================================
-- DEVICE TABLE ( 2026.02.05 생성자 : 박한용 ) 
-- ================================================
CREATE TABLE IF NOT EXISTS DEVICE
(
    DEVICE_ID            BIGINT PRIMARY KEY,
    MANUFACTURE_SERIAL   VARCHAR(50) NOT NULL UNIQUE,
    REG_DATE             TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP,
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
    REG_DATE       			TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
	PASSWORD_CHANGED_AT 	TEXT,

    FOREIGN KEY (DEVICE_ID)
    REFERENCES DEVICE (DEVICE_ID)
);

-- ================================================
-- Patient TABLE ( 2026.02.09 생성자 : 서민지 )
-- ================================================
CREATE TABLE IF NOT EXISTS PATIENT (
    PATIENT_ID INTEGER PRIMARY KEY AUTOINCREMENT,
    PATIENT_CODE INTEGER NOT NULL UNIQUE,
    PATIENT_NAME VARCHAR(50) NOT NULL,
    BIRTH_DATE DATE NOT NULL,
    SEX CHAR(1) NOT NULL,
    REG_DATE TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
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