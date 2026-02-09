
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
    'VnmB8HboXt9R//YZzRk+sXWMzhK7HSfknslWgxVp2Qk=',
	'tUZndrX59Uep+Ml7uIjRHrUJXyUE6CNMwjFuB/ZVG6o=',
    'S',
    '시스템관리자',
    'ADMIN'
);


