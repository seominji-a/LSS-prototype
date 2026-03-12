<촬영 데이터 흐름>
1.환자 선택
2.Scan 진입
3.오늘 기존 Study를 이어갈지 / 새 Study 만들지 결정
4. 새 study 또는 선택된 study 확정
5. 그 시점에 STUDY 테이블에 저장
6. 이후 촬영 버튼 클릭할 때마다 IMAGE 테이블에 한장씩 저장

■ 추천 관계 구조
PATIENT
  1 └── N STUDY
           1 └── N SERIES
                    1 └── N IMAGE

환자 1명은 여러 검사(STUDY) 가능
검사 1건은 여러 시리즈(SERIES) 가능
시리즈 1개는 여러 이미지(IMAGE) 가능

■ STUDY 테이블 역할
-어떤 환자의 검사인지
-언제 촬영 시작했는지
-ICG 주입 시간이 언제인지
-Accession Number가 뭔지 (필요성 고려)
-Study Instance UID가 뭔지(필요성 고려)

■ STUDY 테이블(검사 세션 단위)
-한 번 Scan 들어와서 오늘 검사 시작하면 1건 생성
-사진을 처음 찍기 전 또는 Scan 세션 시작 시

STUDY_ID
PATIENT_ID
STUDY_INSTANCE_UID
SCAN_DATE
ICG_INJECTION_TIME
ACCESSION_NUMBER

■ SERIES 테이블(촬영 설정/촬영 묶음 단위) : _currentSeriesNumber
-같은 촬영 세션 안에서 설정 묶음 하나가 Series
만약 촬영 세션 안에서 설정을 변경했는데 촬영 버튼을 누르지 않았을 경우에는 테이블에 굳이 저장될 필요가 없지 않나? 
-촬영 설정(시리즈) 생성 시

SERIES_ID
STUDY_ID
SERIES_INSTANCE_UID
SERIES_NUMBER
BODY_PART
SERIES_DATE
SERIES_TIME
GAIN
EXPOSURE
GAMMA
FILTER
COLORMAP

■ IMAGE 테이블(실제 이미지 파일 단위)
촬영 버튼 누를 때마다 1건 저장
촬영 설정(시리즈) 생성 시

IMAGE_ID
SERIES_ID
SOP_INSTANCE_UID
INSTANCE_NUBER
CAPTURED_AT
FILE_PATH_

■ SERIES 테이블 역할
-이미지 저장 경로
(현재) 환자 폴더 / STUDY 폴더/ SERIES 폴더/ 이미지.dcm
(수정) 환자 폴더 / STUDY 날짜 / STUDY 폴더/ SERIES 폴더/ 이미지.dcm

(필요 이유)
특정 시리즈만 조회
특정 시리즈의 모든 이미지 보기
같은 STUDY 안의 다른 촬영 설정 비교
Image Review에서 시리즈별 그룹핑

■ 결론-현재 추가 필요 테이블 설계

1. Scan 진입 시
(Study 결정)
-기존 Study 이어쓰기, 새 Study 생성
-STUDY 테이블 insert 또는 기존 row 선택
-STUDY_INSTANCE_UID 저장: 검사 세션 식별자

//STUDY
CREATE TABLE STUDY (
    STUDY_ID             INTEGER PRIMARY KEY AUTOINCREMENT,
    PATIENT_ID           INTEGER NOT NULL,
    STUDY_INSTANCE_UID   VARCHAR(128) NOT NULL UNIQUE,
    SCAN_DATE            TIMESTAMP,
    ICG_INJECTION_TIME   TIMESTAMP,
    ACCESSION_NUMBER     VARCHAR(50),
    CREATED_AT           TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (PATIENT_ID) REFERENCES PATIENT(PATIENT_ID)
);

2. 촬영 설정 확정 시
(Series 생성)
-SERIES_INSTANCE_UID 저장:촬영 묶음 식별자
-gain/exposure/gamma/filter/color map 저장

//SERIES
CREATE TABLE SERIES (
    SERIES_ID              INTEGER PRIMARY KEY AUTOINCREMENT,
    STUDY_ID               INTEGER NOT NULL,
    SERIES_INSTANCE_UID    VARCHAR(128) NOT NULL UNIQUE,
    SERIES_NUMBER          VARCHAR(50),
    SERIES_DATE            TIMESTAMP,
    BODY_PART              VARCHAR(50),
    GAIN                   REAL,
    EXPOSURE               REAL,
    GAMMA                  REAL,
    FILTER_VALUE           INTEGER,
    COLOR_MAP              VARCHAR(50),
    CREATED_AT             TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (STUDY_ID) REFERENCES STUDY(STUDY_ID)
);

3. Image Scan 버튼 클릭 시
(Image 생성)
-DICOM 파일 저장
-IMAGE 테이블 insert
-SOP_INSTANCE_UID 저장: 각 이미지 파일 식별자
-INSTANCE_NUMBER, FILE_PATH 저장

//IMAGE
CREATE TABLE IMAGE (
    IMAGE_ID              INTEGER PRIMARY KEY AUTOINCREMENT,
    SERIES_ID             INTEGER NOT NULL,
    SOP_INSTANCE_UID      VARCHAR(128) NOT NULL UNIQUE,
    INSTANCE_NUMBER       INTEGER,
    CAPTURED_AT           TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FILE_PATH             VARCHAR(255) NOT NULL,
    FOREIGN KEY (SERIES_ID) REFERENCES SERIES(SERIES_ID)
);


■ 현재 시점의 개발 진행 사항(파일 시스템만 쓰고 있고, DB는 안 쓰는 상태)
ScanViewModel.CS
-DICOM 파일은 저장, 폴더도 생성, StudyId / SeriesNumber / InstanceIndex도 관리

DB
- (Study 저장, Series 저장, Image 저장) 미존재

■ CalculateAge
-나이는 시간이 지나면 바뀜
-DOB(생년월일)만 저장, 필요 시 계산
DB에는 BirthDate 저장
DICOM 태그의 PatientAge를 넣고 싶으면 촬영 시점에 계산해서 DICOM에만 반영

■ 최종 결론
방법1, 정식으로 갈 거면 DB_Injection 칼럼 생성

-오늘 기록 유지
-날짜 지나면 새로 생성
-이전 기록 재사용 여부 선택

방법2, 임시로 갈 거면 DB 조회 없이 세션 내 입력만 사용
-오늘 기록 재사용 불가

■ 최종 구조
1. STUDY 테이블 추가, ICG_INJECTION_TIME 칼럼 생성
2. SERIES 테이블 추가
3. IMAGE 테이블 추가
4. 촬영 시 DB 저장 순서:
-Study 확정
-Series 확정
-Image 저장


■ 고려사항
(문제 사항) 
전부 STUDY row가 생기면 DB에 실제 촬영이 없는 빈 세션 저장->저장 불필요

1.SCAN 화면만 들어왔다가 바로 나감

2.ICG 주사시간만 입력하고 촬영 안 함
->ICG INJECTION TIME 설정 여부로 STUDY 테이블에 데이터 생성 판단 부족
-주사시간 설정, 촬영 안함
-주사시간 없이도 촬영
-나중에 주사시간 수정

3.설정만 바꾸고 실제 촬영 안 함

4.카메라 확인만 하고 종료, Series 바꿨지만 이미지 저장 안 함

■ 권장 사항 1
첫 촬영 버튼을 눌러 실제 이미지가 저장되는 순간 처리

1. 메모리 상에서 _currentStudyId, _currentSeriesNumber, InjectionTime 저장

2. 실제 첫 번째 이미지 저장 시점(파일이 생긴 순가 DB row도 생기게 맞춤)
환자 선택 완료, scan 진입 완료, 카메라 준비 완료, 촬영 버튼 클릭 완료, 실제 DICOM 이미지 저장 시도

DB insert
-STUDY 테이블에 데이터 생성(INSERT)
-SERIES 테이블에 데이터 생성(INSERT)
-IMAGE 테이블에 데이터 생성(INSERT)

■ 권장 사항 2
Study / Series / SOP Instance UID
-파일만 저장할 거면 DB에 없어도 동작은 함
-REVIEW / 검색 / PACS 연동 / 중복 방지 / 추적성 때문에 저장하는 게 좋음

(이유)
1.특정 이미지가 어떤 DICOM 파일인지 정확히 찾기
2.중복 import 방지
3.PACS에 보낸 것과 로컬 기록 매칭
4.Image Review에서 Study/Series 정확히 그룹핑
5.파일명이 바뀌어도 동일 객체 추적

(결론) 
파일 구조 보다 uid 기반이 안전

■ 권장 사항 3
DICOM/
 └─ 환자폴더/
     └─ 20260312/ -STUDY 날짜 폴더 (하루 단위 정리 가능, 직접 탐색 가능, 날짜별 백업/이관 편리)
         └─ 202603120001/ -실제 STUDY ID 폴더
             └─ SERIES_01/ -SERIES 폴더
                 ├─ image_001.dcm - 이미지 파일
                 ├─ image_002.dcm

■ 파일만으로 REVIEW 만들면 생기는 문제
-환자별 정렬 느림
-날짜/Study/Series 기준 검색 어려움
-주사시간, gain, exposure 등 메타 정보 조회 어려움
-썸네일 목록 빠르게 띄우기 어려움
-특정 환자의 특정 날짜 촬영 내역 찾기 번거로움
-DB와 달리 필터/ 정렬/페이징 어려움

■ DB가 있으면 장점(REVIEW 화면 생성 필수라고 판단됨)
-환자별 촬영 이력 조회 가능 
-날짜별 검사 목록 조회 가능
-Study별 Series 목록 조회 가능
-Series별 Image 목록 조회 가능
-촬영 시간순 정렬
-주사 시간 표시
-EMR/LOCAL 구분
-accession number 검색
-리뷰 화면 빠른 로딩

■저장 트리거
(1)첫 이미지 저장 시, 아직 DB에 현재 세션 정보가 없으면:
-STUDY insert
-SERIES insert
-IMAGE insert

(2) 이후 같은 series에서 추가 촬영 시
-IMAGE만 insert

(3) 설정을 바꿔 새 series로 판단되면
-SERIES insert
-이후 IMAGE insert