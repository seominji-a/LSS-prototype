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
    USER_ID        INTEGER PRIMARY KEY AUTOINCREMENT,
    DEVICE_ID      BIGINT        NOT NULL,
    LOGIN_ID       VARCHAR(50)   NOT NULL UNIQUE,
    PASSWORD_HASH  VARCHAR(255)  NOT NULL,
    ROLE_CODE      CHAR(1)       NOT NULL,
    USER_NAME      VARCHAR(100)  NOT NULL,
    USER_ROLE      VARCHAR(50)   NOT NULL,
    REG_DATE       TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (DEVICE_ID)
    REFERENCES DEVICE (DEVICE_ID)
);