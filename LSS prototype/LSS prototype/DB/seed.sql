
-- ===========================================
-- 새로 생성하는 테이블은 db_init.sql 저장 
-- 새로 생성된 테이블 초기값은 seed.sql 저장
-- ★ 중요 코드 내 Common.cs -> DB_VERSION 무조건 업데이트해주기 
-- ===========================================


-- ===========================================
-- DEVICE TABLE ( 2026.02.05 생성자 : 박한용 )
-- ===========================================

INSERT OR IGNORE INTO DEVICE -- 중복되지 않는 데이터에 대해서만 INSERT를 진행한다. 
(
    DEVICE_ID,
    MANUFACTURE_SERIAL,
    EXPIRE_DATE
)
VALUES
(
    1001,
    'SN-2026-0001',
    datetime('now', '+1 year')
);



-- ===========================================
-- DEVICE TABLE ( 2026.02.05 생성자 : 박한용 ) 
-- ===========================================

INSERT INTO USER
(
    DEVICE_ID,
    LOGIN_ID,
    PASSWORD_HASH,
	PASSWORD_SALT,
    ROLE_CODE,
    USER_NAME,
    USER_ROLE
	
)
VALUES
(
    1001,
    'ADMIN',
    '3TkSPajUsNu0QusDihSVAX2U1Gq7ardCSFuwVnDN1pY=',
	'2jdIYa2pllWiiDgcrDRnrAxA51ECHlroc3xygCUPiuo=',
    'A',
    '시스템관리자',
    'ADMIN'
);



-- ================================================
-- DEFAULT_SET TABLE ( 2026.02.27 생성자 : 박한용 )
-- 프로그램 실제 납부 시 적정값으로 VALUES 수정해야함 
-- ================================================
INSERT INTO DEFAULT_SET
(
    EXPOSURE_TIME,
    GAIN,
    GAMMA,
    FOCUS,
    IRIS,
    ZOOM,
    FILTER
)
VALUES
(
    0.6,   -- EXPOSURE_TIME  (0.2s ~ 1s) 샘플값
    10.0,   -- GAIN           (3dB ~ 45dB) 샘플값
    0.8,    -- GAMMA          (0.3 ~ 1) 샘플값 
    5927,   -- FOCUS          (3545 ~ 8310, Step 100) → 중간값
    328,    -- IRIS           (0 ~ 656,    Step 50)  → 중간값
    2903,   -- ZOOM           (1138 ~ 4669, Step 100) → 중간값
    1       -- FILTER         (0=OFF, 1=ON)
);