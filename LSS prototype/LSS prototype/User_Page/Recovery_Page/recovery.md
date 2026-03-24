================================================================
Recovery 페이지 로직 
작성일: 2026-03-23
작성자: 박한용
================================================================

----------------------------------------------------------------
1. 개요
----------------------------------------------------------------
Recovery 페이지는 삭제된 파일(이미지/영상) 및 환자 데이터에 대한
이력 관리, 복구, 강제삭제 기능을 제공하는 페이지입니다.

모든 삭제 작업은 즉시 완전 삭제가 아닌 DELETE_LOG 테이블에
이력을 남기고, 일정 기간(운영: 72시간 / 테스트: 3분) 내에
복구 가능하도록 설계되어 있습니다.

----------------------------------------------------------------
2. DELETE_LOG 테이블 구조
----------------------------------------------------------------
FILE_TYPE 종류:
  - IMAGE        : 이미지 파일 (.dcm)
  - DICOM_VIDEO  : DICOM 영상 파일 (.avi + .dcm 한 쌍)
  - NORMAL_VIDEO : 일반 영상 파일 (.avi 단독)
  - PATIENT      : 환자 자체 삭제

주요 컬럼:
  - DELETED_AT      : 삭제 시각 (datetime('now', 'localtime') 기준)
  - IS_RECOVERED    : 복구 여부 (Y/N)
  - RECOVERED_AT    : 복구 시각
  - RECOVERED_BY    : 복구한 사용자 ID
  - IS_FORCE_DELETED: 강제삭제 여부 (Y/N)
  - FORCE_DELETED_AT: 강제삭제 시각
  - FORCE_DELETED_BY: 강제삭제 주체
                      (사용자 직접 삭제 시 → 사용자 ID)
                      (만료 자동 삭제 시   → "SYSTEM")

----------------------------------------------------------------
3. SELECT_DELETE_LOGS 쿼리 (핵심 조회 로직)
----------------------------------------------------------------
SELECT d.*,
  CASE WHEN p.DELETE_ID IS NOT NULL THEN 'Y' ELSE 'N' END AS PATIENT_DELETED
FROM DELETE_LOG d
LEFT JOIN DELETE_LOG p
  ON d.PATIENT_CODE = p.PATIENT_CODE
  AND d.PATIENT_NAME = p.PATIENT_NAME
  AND p.FILE_TYPE = 'PATIENT'
  AND p.IS_RECOVERED = 'N'
ORDER BY d.DELETED_AT DESC

PATIENT_DELETED 계산 로직:
  - 같은 PATIENT_CODE + PATIENT_NAME 을 가진 행 중
    FILE_TYPE = 'PATIENT' 이고 IS_RECOVERED = 'N' 인 행이 존재하면
    해당 환자의 모든 IMAGE/VIDEO 행 PATIENT_DELETED = 'Y'
  - 이유: 환자 자체가 삭제(미복구) 상태면
          하위 이미지/영상 복구 및 강제삭제가 무의미하기 때문

----------------------------------------------------------------
4. RecoveryModel - IsCheckable 조건
----------------------------------------------------------------
아래 조건 중 하나라도 해당되면 체크박스 비활성화:
  - IsExpired = true          (만료 기한 초과)
  - IsRecovered = "Y"         (이미 복구됨)
  - IsForceDeleted = "Y"      (이미 강제삭제됨)
  - PatientDeleted = "Y"      (환자 자체가 삭제된 상태)
    단, FileType = "PATIENT" 인 행 자신은 제외

----------------------------------------------------------------
5. RemainText 우선순위
----------------------------------------------------------------
1순위: PatientDeleted = "Y" && FileType != "PATIENT" → "환자 삭제"
2순위: IsForceDeleted = "Y" && ForceDeletedBy = "SYSTEM" → "기한만료"
3순위: IsForceDeleted = "Y" → "강제삭제"
4순위: IsRecovered = "Y" → "복구처리"
5순위: IsExpired = true → "만료"
기본값: N시간 M분 (남은 시간 표시)

----------------------------------------------------------------
6. 복구 로직 (ExecuteRecover)
----------------------------------------------------------------
처리 순서:
  1. 선택된 항목 중 PATIENT 를 맨 마지막으로 정렬
     (다중선택 시 IMAGE/VIDEO 먼저 복구 후 PATIENT 처리)

FILE_TYPE 별 처리:
  - IMAGE
    → Del_ 접두사 제거 (파일명 복원)
    → 연결된 .isf 파일도 복원
    → UpdateRecovered() 호출

  - DICOM_VIDEO
    → Del_ 접두사 제거 (.avi + .dcm)
    → UpdateRecovered() 호출

  - NORMAL_VIDEO
    → Del_ 접두사 제거 (.avi)
    → UpdateRecovered() 호출

  - PATIENT (트랜잭션 처리) --> 추후 n개 이상 쿼리보장이 받아야될 시 무조건 트랙잭션 처리할 것
    → RecoverPatientWithLog() 호출
      [트랜잭션]
      1. PATIENT 테이블 IS_DELETED = 'N'
      2. DELETE_LOG IS_RECOVERED = 'Y', RECOVERED_BY = 현재유저
    → LoadLogs() 전체 새로고침
      (같은 환자의 IMAGE/VIDEO 행 PATIENT_DELETED = 'N' 반영)

실패 시: renamedFiles 역순으로 롤백 (파일명 원복) --> 과도한 엔지니어링인거같은데 테스트 후 이부분 삭제 예정 0323

----------------------------------------------------------------
7. 강제삭제 로직 (ExecuteForceDelete)
----------------------------------------------------------------
전제 조건: OTP 검증 통과 필요

처리 순서:
  1. 선택된 항목 중 PATIENT 를 맨 마지막으로 정렬
     (다중선택 시 IMAGE/VIDEO 먼저 삭제 후 PATIENT 폴더 삭제)

FILE_TYPE 별 처리:
  - IMAGE
    → 파일 완전 삭제
    → 연결된 .isf 파일 삭제
    → UpdateForceDeleted() 호출

  - DICOM_VIDEO
    → .avi + .dcm 파일 완전 삭제
    → UpdateForceDeleted() 호출

  - NORMAL_VIDEO
    → .avi 파일 완전 삭제
    → UpdateForceDeleted() 호출

  - PATIENT (트랜잭션 처리)
    → ForceDeletePatientWithLog() 호출
      [트랜잭션]
      1. DELETE_LOG PATIENT 행 IS_FORCE_DELETED = 'Y'
      2. PATIENT 테이블 완전 DELETE
         (PATIENT_CODE + PATIENT_NAME 100% 일치 조건)
      3. 같은 환자의 IMAGE/VIDEO 행도 IS_FORCE_DELETED = 'Y'
         (FORCE_DELETE_RELATED_LOGS 쿼리)
    → 환자명_환자코드 폴더 탐색 후 완전 삭제
      경로: ./DICOM/환자명_환자코드/
            ./VIDEO/환자명_환자코드/
    → LoadLogs() 전체 새로고침

----------------------------------------------------------------
8. 만료 자동 처리 (AutoCleanupService)
----------------------------------------------------------------
실행 시점: 로그인 성공 후 백그라운드 Task.Run 으로 실행
           (메인 스레드 성능 영향 없음)

대상: DELETED_AT 기준 EXPIRE_MINUTES 이상 경과
      IS_RECOVERED = 'N' AND IS_FORCE_DELETED = 'N' 인 행

처리 방식: ExecuteForceDelete 와 동일한 로직
           단, FORCE_DELETED_BY = "SYSTEM" 으로 기록
           (사용자 직접 삭제와 자동 만료 삭제 구분)

처리 순서: PATIENT 맨 마지막 (IMAGE/VIDEO 먼저 처리)

----------------------------------------------------------------
9. 미리보기 로직
----------------------------------------------------------------
아래 조건 해당 시 미리보기 로드 안함:
  - IsExpired = true
  - IsRecovered = "Y"
  - IsForceDeleted = "Y"
  - PatientDeleted = "Y" && FileType != "PATIENT"

FILE_TYPE 별:
  - IMAGE       → DICOM 파일 렌더링 + ISF 스트로크 오버레이
  - DICOM_VIDEO → MediaElement 재생
  - NORMAL_VIDEO → MediaElement 재생
  - PATIENT     → 미리보기 없음

----------------------------------------------------------------
10. 상수 관리 (Common.cs)
----------------------------------------------------------------
EXPIRE_HOURS   = 72   (운영: 복구 가능 시간)
EXPIRE_MINUTES = 3    (테스트: 자동 만료 처리 기준)

* 만료 시간 변경 시 두 값 모두 수정 필요:
  1. Common.EXPIRE_HOURS   → RecoveryViewModel LoadLogs() 만료 계산
  2. Common.EXPIRE_MINUTES → SELECT_EXPIRED_LOGS 쿼리 (분 단위)

================================================================

----------------------------------------------------------------
11. DELETE_LOG 오래된 데이터 정리 
----------------------------------------------------------------

현재는 프로그램 상 쿼리를 이용하여 3년 이상 됐을 시점 자동으로 삭제 하도록 처리 
DELETE_OLD_LOGS << 쿼리참조 
