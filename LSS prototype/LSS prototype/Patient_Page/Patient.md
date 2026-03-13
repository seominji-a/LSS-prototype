PATIENT 페이지 구조 문서
작성일: 2026-03-03
최종 수정일: 2026-03-03
작성자: 박한용

────────────────────────────────────

■ 1. 개요

본 페이지는 EMR(DICOM MWL) 데이터와
LOCAL(SQLite) 데이터를 조합하여 환자 목록을 표시한다.

데이터 구분

EMR 환자

데이터 소스: DICOM MWL (병원 서버)

설명: 예약 환자 목록

LOCAL 환자

데이터 소스: SQLite (로컬 DB)

설명: 당일 접수 환자 목록

────────────────────────────────────

■ 2. 데이터 흐름

앱 시작 시

PatientViewModel 생성자 실행

① LoadPatients()
→ SQLite에서 당일 접수 환자 조회
→ _localPatients에 저장

② EmrSync()
→ DICOM MWL에서 예약 환자 조회
→ _emrPatients에 저장
→ RefreshPatients() 호출
→ Patients 갱신
→ 화면 표시

체크박스 (ShowAll) 토글 시

ShowAll setter 실행

→ RefreshPatients() 호출

- ShowAll = false  
  → Patients = _emrPatients  
  → EMR 환자만 표시

- ShowAll = true  
  → Patients = _emrPatients + _localPatients  
  → 전체 환자 표시

3) Sync 버튼 클릭 시

EmrSync() 실행

→ DICOM MWL 재조회
→ _emrPatients 갱신
→ RefreshPatients() 호출
→ 화면 갱신

────────────────────────────────────

■ 3. ViewModel 핵심 구조

내부 데이터 저장용 리스트 (화면 미표시)

_emrPatients
DICOM(MWL)에서 받아온 예약 환자 목록 저장

_localPatients
SQLite에서 받아온 당일 접수 환자 목록 저장

화면 바인딩용 컬렉션

Patients (ObservableCollection<PatientModel>)

ListBox의 ItemsSource에 바인딩됨
RefreshPatients()에서 이 값을 갱신

RefreshPatients()

ShowAll 상태에 따라 Patients를 조합하여 화면 갱신

ShowAll = false → EMR만 표시

ShowAll = true → EMR + LOCAL 합쳐서 표시

Patients에 새 컬렉션이 대입되면
OnPropertyChanged()가 발생하고
XAML의 ItemsSource가 이를 감지하여 화면이 자동 갱신된다.

ShowAll 프로퍼티

기본값: false (EMR만 표시)

true: EMR + LOCAL 전체 표시

값 변경 시 수행 동작:

OnPropertyChanged()

PageTitle 갱신

RefreshPatients() 호출

PageTitle 프로퍼티

ShowAll 상태에 따라 상단 타이틀 변경

ShowAll = false → "Integrated Patient"

ShowAll = true → "EMR Patient"

※ 현재 로직 기준

────────────────────────────────────

■ 4. XAML 바인딩 구조

ListBox.ItemsSource
→ Patients

CheckBox.IsChecked
→ ShowAll (TwoWay)

상단 TextBlock.Text
→ PageTitle

────────────────────────────────────

■ 5. 중복 데이터 처리 정책

현재 상태:

EMR과 LOCAL에 동일 환자가 존재할 경우
중복 표시됨

작성일: 2026-03-03
작성자: 박한용

향후 내부 회의를 통해
중복 제거 또는 병합 정책 결정 예정 (TODO)

────────────────────────────────────
작성일: 2026-03-10
작성자: 서민지

■ 6. import 데이터 처리 정책

-PatientModel.cs

-PatientSource 함수
Local 수동 등록 데이터
EmrImported DICOM EMR
ImportLocal import 되어진 local 데이터
ImpotEmr Import 되어진 emr 데이터

PatientViewModel.cs

-RefreshPatient 함수
EMR-EmrImported
Intergrated-Local, ImportLocal, ImportEmr

-ApplyEmrFlagsFromDicomFolder 함수
DB에서 환자 리스트를 읽은 다음, DICOM 폴더를 다시 스캔해서 ISEmrPatient/Source를 재계산해서 UI에 배치

DICOM 폴더를 스캔해서 AccessionNumber가 있는 환자(PatientID) 목록 생성,
DB에서 불러온 환자 리스트에 IsEmrPatient/Source를 다시 붙여서 EMR/LOCAL 배지 표시가 가능

1. DB에 AccessionNumber를 저장하지 않으면, GetAllPatients()로 다시 로드할 때 EMR/LOCAL 구분 정보가 사라짐 방지 목적으로
DICOM 파일 자체를 근거로 매번 EMR/LOCAL 표시용 플래그를 재생성

2. EMRImported 환자와 importEmr 환자 모두 따로 DB에 AccessNumber(접수 번호)를 저장하지 않으므로
환자를 구분하기 위해 다른 '공통 키'로  DICOM 파일과 DB 환자 레코드 연결

3. DICOM 파일에서 Patient id를 꺼내고 그 값을 int로 변경해서 code로 만든다. 
DB의 paientcode(int)와 동일한지 여부를 판단한다

-Patient.xaml
PatientModel의 PatientModel.Source 기준(데이터 상태)에 따라 배지(SourceBadge) UI를 자동으로 바꿔주는 조건부 스타일 규칙

1. Source == ImportLocal
SourceBadge 배경/테두리 + TxtSourceLabel 글자색/텍스트
파란 배지 + “LOCAL”

2. Source == ImportEmr	
SourceBadge 배경/테두리 + TxtSourceLabel 글자색/텍스트
초록 배지 + “E-Sync”


<현재 import 전체 흐름 요약>
      [DICOM 파일 선택]
             │
             ▼
  파일 열기 → AccessionNumber 읽기
            │
         비어있나?
  ┌────┴────┐
 YES                  NO
LOCAL                 EMR
  │        │        │
  └────┬────┘
            ▼
    PatientModel 생성
Source / IsEmrPatient 설정
           │
           ▼
DB 저장 (Source/IsEmrPatient는 저장 안 됨)
          │
          ▼
DB에서 전체 재조회 (Source 날아간 상태)
          │
          ▼
ApplyEmrFlagsFromDicomFolder()
  DICOM 폴더 재스캔
  AccessionNumber 있음 → EMR
  AccessionNumber 없음 → LOCAL
  Source / IsEmrPatient 재복원
         │
         ▼
UI 반영 (RefreshPatients)


[현재 상황]
LOCAL dcm 파일에 AccessionNumber = "202503100001" 존재
→ import 시 EMR로 잘못 분류됨 

[해결]
① 기존 LSIS의 Save_Click 수정 → LOCAL은 AccessionNumber = "" 로 저장
② 기존 잘못된 dcm 파일 삭제
③ 재촬영 → 재저장 → 재import
→ LOCAL/EMR 정확히 분류됨 

────────────────────────────────────
작성일: 2026-03-10
작성자: 서민지

① dcm 이미지 LOCAL은 AccessionNumber = "" 로 저장되게 변경 완료

② 재촬영 → 재저장 → 재import
AccessionNumber 유무로
→ LOCAL/E-sync 구분 확인 가능

③ Local 환자 먼저 생성 , import emr환자 중복 자동 덮어쒸움-확인

1. 덮어쒸워지는 거 없애도록 해결
LOCAL 먼저 생성, E-SYNC 나중에 생성, E-SYNC로 덮어쓰여짐
E-SYNC 환자 삭제하고 다시 같은 번호로 LOCAL 환자 생성하면 E-SYNC로 인식하는 문제

2. 환자 화면에서 e-sync랑 local로 동일한 환자가 2개 존재해야함

3. 중복 등록 방지 

(문제 상황)
local에 이미 동일한 환자가 존재하는데
import로 e-sync 화면에 등록/ emr환자가 당일 촬영헤서 e-sync로 integrated patient 화면에 나중에 등록할 경우
->동일한 2명의 환자가 integrated patient 화면에 존재
->병합 과정 필요

(개선 사항)
1.local 환자 선택 
2. edit diaglog에서 환자 번호 변경( e-sync환자와 동일한 환자 번호 입력)

3. 병합하시겠습니까?라는 팝업창 
예- e-sync 로 병합 , 이미지도 같이 묶임
아니요- 따로 계속 존재 

(문제X 상황)
import로 e-sync 화면에 등록/ emr환자가 당일 촬영헤서 e-sync로 integrated patient 화면에 이미 등록된 경우
local 환자를 나중에 생성할 경우에는 동일한 환자가 이미 존재해서 아예 안만들어지도록 처리되고 있는 상황
->동일한 2명의 환자가 integrated patient 화면에 미존재
->병합 과정 불필요

■ 문제 발생 원인

1. DB에서 환자 번호 기준으로 같은 환자로 처리 중일 가능성
ImportPatient()에서 EMR DICOM을 읽어서
repoInside.AddPatient(patientModel)호출 

-DB_Manager.AddPatient() 내부가 아래 3가지 경우에 해당- 같은 환자번호의 LCCAL 환자 행이 EMR 정보로 덮어써짐
PatientCode가 UNIQUE / INSERT OR REPLACE / ON CONFLICT(PATIENT_CODE) DO UPDATE

DB 저장 로직이 환자번호를 동일 환자 키로 인식함
=> LOCAL 환자 미리 생성, 같은 번호의 EMR 촬영본 import, 기존 LOCAL이 EMR 처럼 변경

2. ApplyEmrFlagsFromDicomFolder()가 PatientCode만 보고 EMR로 재판정함
AccessionNumber도 안 보고, DB에 저장된 구분값도 안 보고, 오직 PatientCode만 확인

(1) 예전에 EMR DICOM 파일이 한번이라도 존재했던 환자번호 존재
(2) 나중에 EMR 환자 삭제
(3) 다시 같은 번호로 LOCAL 환자 생성
(4) DICOM 폴더에 예전 EMR 파일 남아 있으면
(5) ApplyEmrFlagsFromDicomFolder()가 다시 EMR로 인식

3. E-SYNC 삭제 후 다시 LOCAL 생성하면 EMR로 띄워짐
DB에서 E-SYNC 환자를 삭제해도, DICOM 폴더에 예전 E-SYNC 파일 존재- 같은 번호는 계속 E-SYNC 취급됨 
현재 DICOM 폴더에 예전 E-SYNC 파일 존재하지 않아도 생성됨
==>나중에 E-SYNC환자를 삭제하면, DICOM 폴더에 예전 E-SYNC 파일 삭제 될거니까 문제 없지 않을까?

ApplyEmrFlagsFromDicomFolder(updatedList);
emrPatientCodes.Contains(p.PatientCode)

■ 향후 개발 관련
같은 환자 코드여도 LOCAL 환자 1명, E-SYNC 환자 1명 
->Integrated 화면에서 2개가 따로 존재

*PatientCode로 동일 환자라고 합치지 않는것 권장*
-확인 사항(중복 허용으로 변경)
(1)AddPatient()의 sql 존재 여부 판단 -삭제 필요
INSERT OR REPLACE INTO PATIENT
ON CONFLICT(PATIENT_CODE) DO UPDATE

(2)테이블 존재 여부 판단-삭제 필요
PATIENT_CODE UNIQUE 존재할 가능성 높음 ===> 유지하면서 시스템 상으로 환자 코드를 중복 허용하는 방식 고려

-저장 방식
LOCAL 등록 → 새 row insert
EMR import → 같은 번호라도 또 새 row insert

*AddPatient()는 항상 insert*

*ApplyEmrFlagsFromDicomFolder()는 제거 및 변경 권장*
같은 환자번호를 전부 EMR로 덮어버리는 로직 존재

환자 구분은 DICOM 폴더 스캔으로 하지 말고,
DB에 저장된 값으로만 판단
=====>우리는 그 반대이므로 반대 방식 유지할 경우 고려

결론
DB_Manager.AddPatient()에서 PatientCode 중복 허용 ->db가 patientcode를 같은 환자로 보고 덮어쓰고 있을 가능성
ApplyEmrFlagsFromDicomFolder() 제거 ->같은 번호를 전부 emr로 재분류
EMR/LOCAL 구분은 DB 컬럼으로 저장하고 읽기
환자 삭제 후 재생성 시 DICOM 폴더 기반 재판정하지 않기