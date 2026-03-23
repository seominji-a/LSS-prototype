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

────────────────────────────────────
작성일: 2026-03-16
작성자: 서민지

import/local 병합

local이 먼저 존재, emr 환자 촬영 / import emr 실행 경우

같은 환자 번호 존재
-> emr 환자 촬영 및 import emr 실행 데이터가 로드되자마자 병합하겠냐고 팝업창으로 여부를 확인함

예

Image
 ├ Cho Hyunwoo_10012_202603160001_...
 ├ 조현우_10012_202603160001_1.dcm (기존 local)
 ├ 조현우_10012_202603160001_2.dcm (기존 local)

Image
 ├ Cho^Hyunwoo_10012_001.dcm 
 ├ Cho^Hyunwoo_10012_002.dcm
 ├ Cho^Hyunwoo_10012_003.dcm (기존 local->e-sync 기준으로 맞춤)
 ├ Cho^Hyunwoo_10012_004.dcm (기존 local->e-sync 기준으로 맞춤)

 //현재
 파일명만 변경, DICOM 태그 자체는 변경 이전

-파일명: Cho^Hyunwoo_10012_001.dcm
-내부 PatientName 태그: 여전히 조현우일 수도 있음

=>뷰어나 리스트에서 표시 이름도 완전히 통일하고 싶으면 DICOM 태그까지 수정

(현재 사항)
LOCAL 환자 정보는 DB에서만 관리
E-SYNC 여부는 DICOM 파일의 AccessionNumber로 판단
화면에서 LOCAL은 항상 AccessionNumber = ""
E-SYNC는 _importedEmrPatients에서만 표시

(문제)
병합될 경우, local 파일
그냥 복사만 되고 dicom 태그는 그대로
=>파일명만 e-sync처럼 보여도 내부 AccessionNumber는 공백 

폴더/파일명-E-SYNC 기준으로 통일
LOCAL에서 가져운 .dcm 내부 태그는 그대로
내부 AccessionNumber는 여전히 빈 값

(향후 계획)
병합 시점에 local 쪽 dicom 파일도 같은 AccessionNumber를 가짐
병합 직후 Dicom 태그를 수정하는 메서드 필요

────────────────────────────────────
1. 병합 관련 팝업창 수정

환자 이름:pname
생년 월일:bithdate
성별: sex 

모두 같은 환자가 두명 존재합니다
병합하시겠습니까?

────────────────────────────────────
2. 생년월일, 성별 같을 경우 (한글이름/ 영어이름-사용자가 직접 판단)
Edit dialog 화면에 환자번호를 변경하여 병합하기

3. 환자 카드  
병원 방문 횟수(일자)-> 총 촬영 횟수
마지막에 찍은 날짜 기준->마지막 촬영 일자

────────────────────────────────────
dcm.frame 갯수
1개일 경우-이미지 판단
1개 이상일 경우-비디오 판단


────────────────────────────────────
작성일: 2026-03-17
작성자: 서민지

-Dicom.avi 와 Dicom.dcm 은 같은 인덱스로 1쌍
-DICOM 이름 생성 기준은 영상 쪽 Dicom 인덱스여야 함
-DICOM 독립 정렬이 아니라 VIDEO 기준 동기화가 맞음

1. LOCAL 환자 선택
2. EditDialog 열림
3. 생년월일/성별 같은 E-SYNC 후보가 있으면 안내 표시
예:Cho^Hyunwoo / 10012
4. 사용자가 LOCAL 환자 번호를 10012로 수정
5. 저장
6. HandleLocalEditConflictAfterSave()가 같은 번호의 E-SYNC를 찾음
7. 병합 여부 팝업
8. 확인 시 E-SYNC 기준으로 통합


────────────────────────────────────
작성일: 2026-03-18
작성자: 서민지

1. 환자 테이블에 마지막 촬영 일자, 촬영 횟수, esync/local인지 상태값 칼럼 추가, patient code에 unique 삭제

1. 2. e-sync 가 추가될 때, patient db에 e-sync에 대한 데이터 추가 
-scan 화면에서 촬영이 한번이라도 완료 되었을 때 patient db에 데이터 추가
-import 되었을 때 patient db에 추가

3. e-sync가 삭제 될 때, db에 e-sync에 대한 데이터 삭제

4. 병합 되었을 때-> patient db에는 e-sync 데이터만 존재

────────────────────────────────────
작성일: 2026-03-17
작성자: 서민지

(향후 계획)
//emr 환자 관련-> 촬영 했을 때 ui 반영
scan에서 sourcetype을 고려해야함

//마지막 촬영 일자-시분초까지만 저장

1.현재, DICOM, VIDEO 파일 별도 존재 
IMPORT 할경우, 둘다 반영되게 변경해야함

2. import된 데이터 관련해서 마지막 촬영일자랑 총촬영 횟수도 같이 저장

3. 병합처리할 경우
-lastshootDate 비교처리 해서, 제일 마지막에 촬영할 것으로 변경
-shootnum은 서로 합쳐져서 표시되어야함

4. 중복된 데이터 관련
edit dialog 처리-환자 번호, 생년월일, 성별 같아야지만 중복이라 판단하게 변경


5. 마지막 촬영 일자, 총촬영 횟수-일자로 통일 => 환자 카드에 로드

6. 영상 관련 마지막 촬영 일자-> dcm.frame 마지막으로 저장된 날짜? 시간을 가지고 와서 뛰워주기

7. 외부에서 촬영한 환자 데이터 import
dcm.frame 갯수
1개-이미지 판단
1개 이상-비디오 판단

//avi는 모두 import 되지 않게 적용

import 파일은 .dcm만 찾도록

import할 경우 dicom.dcm (멀티 프레임)이 존재하면 Dicom.avi를 생성해주는 코드 들고와서 적용하기

────────────────────────────────────
작성일: 2026-03-18
작성자: 서민지

(현재)파일명 규칙에 의존하고 있음
-외부 데이터 import에서 금방 깨짐 
(별도 처리를 생각해 봤었는데, 공통되게 해야한다고 판단되어 변경 필요)

외부 import는 내부 규칙과 다름
_Dicom, _Avi, 환자명_번호_study_index가 존재하지 않음
=>이름 차이 존재, 확장자만 .dcm, avi인 경우 존재

(결론) frame 수로 이미지인지 영상인지 판단
파일명 기반 분류는 보조수단/실제 파일 내용 기반으로 분류 진행

.dcm: 열어서 멀티 프레임인지 단일 프레임인지 판단
.avi: 열어서 프레임 수로 판단
Dicom.dcm <-> Dicom.avi 는 파일명보다 영상 속성 기반 매칭이 우선
일반 이미지 .dcm과 일반 영상 .avi는 별도 처리 가능

(문제사항의 경우) 
파일명을 신뢰하기 어려움
=>프레임 수/DICOM 태그/ 재생 가능 여부로 판단

IMG0001.dcm → 사실 단일 이미지
US_CINE_01.dcm → 사실 멀티프레임 영상 DICOM
capture01.avi → 일반 AVI
study_video.avi → DICOM 변환용 원본 AVI일 수도 있음
movie_final.avi → 그냥 별도 저장용 AVI일 수도 있음

(향후 계획)
1. .dcm 분리

(1)이미지 DICOM
-단일 프레임
-NumberOfFrames 없음 또는 1
-Image 폴더로

(2)비디오 DICOM
-멀티프레임
-NumberofFrames > 1
-Video 폴더로

//핵심
.dcm은 _Dicom.dcm 같은 이름이 없어도
멀티프레임이면 Video DICOM으로 판단


2. .avi 분리 ->,avi는 기본적으로 VIDEO 자산으로 분류 후, DICOM과 매칭 시도

(1)일반 AVI
-단독 AVI
-대응되는 DICOM Video 없음
-_Avi_avi로 취급 가능

(2)DICOM 대응 AVI
-같은 study 내 멀티프레임 DICOM과 길이/프레임 수/해상도가 유사
-_DICOM.avi로 취급 가능

//핵심
.avi는 파일명보다 DICOM video와 짝을 이룰 수 있는지로 _Dicom_avi 를 판단해야함


3. Dicom.dcm, Dicom.avi가 한쌍이 존재하도록 가져올 때 문제사항

-Dicom.dcm만 있고 대응 AVI가 없을 수 있음
-avi만 있고 대응 Dicom.dcm이 없을 수 있음
-둘 다 있는데 개수가 다를 수 있음

=>가능하면 쌍으로 매칭하고, 안 되면 단독 자산으로 수용
(Dicom.dcm과 Dicom.avi는 “반드시 한 쌍”이라고 강제하면 안 됨)
만약에 한쌍이 존재하지 않으면, import할 때 "import 불가합니다"라고 팝업을 뛰우는게 맞는지? --->물어보기


(향후 진행 과정) -파일을 내용 기준으로 분류해서 원하는 폴더로 재배치한 뒤,  이름 정리
1. 파일 스캔
가져온 폴더 전체에서 모두 수집
.dcm
.avi

2. .dcm 분석:.dcm은 frame 수뿐 아니라 DICOM 태그도 같이 보는 게 더 안전함
PatientName
PatientID
StudyInstanceUID 또는 StudyDate/StudyID
NumberOfFrames
Rows/Columns
SOPClassUID

//분류
NumberOfFrames 
1 → video DICOM
아니면 image DICOM

3. .avi 분석
frame count
fps
width/height
duration

4. 매칭
video DICOM과 AVI를 같은 study 안에서 매칭(점수 기반) -매칭 실패 시에도 각각 보존해야 함

같은 환자
같은 study date 또는 study id
width/height 유사
frame count 유사
duration 유사
생성 시각 유사

5. 저장
(1) DICOM

.dcm => NumberOfFrames 기반으로 Image / Video 분류

단일프레임 .dcm → Image
멀티프레임 .dcm → Video

(2)VIDEO

.avi
=>일단 VIDEO에 저장
=>video DICOM과 매칭되면 _Dicom.avi, 아니면 _Avi.avi

매칭된 .avi → _Dicom.avi
매칭 실패한 .avi → _Avi.avi

(반영 사항)-모든 환자의 dcm/avi가 섞여 있어도 dcm 기준으로 환자를 나누고 import
ImportPatient()가 이제 단일 환자 기준이 아니라 여러 환자 기준으로 동작함

BuildPatientImportGroups()가 PatientName + PatientID 기준으로 환자 묶음 생성

AssignAviFilesToPatientGroups()가 StudyID로 AVI 연결

ImportPatientFilesToStructuredFolders()가 환자별로 파일을 구조화해서 넣음


//AVI는 파일명/경로에서 StudyID를 찾을 수 있고, 그 StudyID가 어떤 환자의 DCM에서 이미 발견된 경우에만 자동 연결 가능

AVI 경로나 파일명에서 202603180001 같은 값을 찾고,
그걸 가진 환자 그룹이 정확히 1개일 때만 붙여

(자동 연결 가능한 상황)
AVI 파일명/경로에 studyId 있음
그 studyId가 DCM 쪽 그룹 중 한 환자에만 존재

(동작 과정)
.dcm은 환자 식별용 기준 파일
.avi는 보조 파일
.avi 단독 import 불가
.avi는 StudyID가 명확할 때만 자동 연결
연결 안 된 .avi는 건너뛰고 개수만 알려주기

---------------------------------------------------------------

(현재 문제 사항)
일반 AVI 자체에는 환자명, 환자번호, accession, study_id 같은 DICOM 메타데이터가 존재하지 않음
=> 완전히 섞인 AVI를 “자동으로 정확히” 환자에 붙이는 건 원천적으로 한계

(결론)
완전히 섞여 있는 외부 .avi를 파일명도 못 믿고, study_id도 없고, 
메타데이터도 없는 상태에서 정확하게 자동 분류하는 방법은 없음

---------------------------------------------------------------
작성일: 2026-03-18
작성자: 서민지

(향후 계획)
//emr 환자 관련-> 촬영 했을 때 ui 반영
scan에서 sourcetype을 고려해야함

//마지막 촬영 일자-시분초까지만 저장

//1.현재, DICOM, VIDEO 파일 별도 존재 
IMPORT 할경우, 둘다 반영되게 변경해야함

(1)avi는 모두 import 되지 않게 적용
import 파일은 .dcm만 찾도록
import할 경우 dicom.dcm (멀티 프레임)이 존재하면 Dicom.avi를 생성해주는 코드 들고와서 적용하기

(2) 촬영 여부-팝업창 생성, 촬영 진행 사항-로딩바 추가

//2. 마지막 촬영 일자, 총 촬영 횟수-일자로 맞추기

3. import된 데이터 관련해서 마지막 촬영일자랑 총촬영 횟수도 같이 저장

(1)촬영할 경우, DICOM 내부에 LastShootDate, ShotNum 저장
(2)나중에 import 할 경우, 다시 읽어서 복원 가능
(3)값이 없으면 fallback으로
-ShotNum = 촬영 날짜 수
-LastShootDate = 가장 최근 StudyDate로 처리

(현재) 문제점-저장 시점과 계산 시점 차이 발생
1.촬영 버튼 누름
2.저장 전 계산 아직 파일 없음-> 0
3.보정해서 1저장

(문제사항)
날짜 수 기준 계산, 증가 연산이 섞이면, 중복 증가 발생

(권장) 예상값 저장 대신, 저장 완료 후 확정값 계산하고 저장
1.촬영
2.파일 저장 완료
3.저장된 실제 폴더 기준으로 날짜 수 재계산
4. 그 값을 DB에 그대로 저장

(이유)
저장 전에는 상태 불확실(예상값)
-아직 폴더 생선 전일 확률
-저장 실패 가능성
-예외로 중간 종료 확률
-메모리 값과 실제 디스크 상태 차이 존재

저장 후에는 확실(확정값)
-파일이 실제로 존재
-날짜 폴더가 실제로 존재함
-GetShotDateCount가 진짜 상태를 읽음

=>emr 환자
seletedpatient랑 실제 db의 esyncpatient 차이 존재

//문제
화면에 있는 selectedpatient.shotnum = 1 
db의 esyncpatient.shotnum=1
오늘 또 촬영
->날짜 기준이면, 여전히 1이여야함

esyncPatient.ShotNum += 1; 사용
->무조건 2가 존재 (날짜 기준 로직과 충돌 발생)

(결론)
ShotNUM은 파일 개수x, 촬영 버튼 누른 횟수x, 날짜 수!
절대 +=1 처리 불가, 재계산 결과 그대로 반영

GetShotDateCount()를 한 번만 계산해서 공통으로 쓰고,
EMR/E-SYNC도 += 1 없이 계산값 그대로 저장 , 반드시 파일 저장이 끝난 뒤 호출-GetShotDateCount()가 실제 저장된 폴더를 보고 정확한 값 계산

=>
이미지: SaveImageFile(...) 후 UpdatePatientAfterScan()
동영상: SaveVideoFile(...) 후 UpdatePatientAfterScan()

(결과)
같은 날짜에 여러 번 찍어도 ShotNum 그대로
다른 날짜에 새로 찍으면 그때만 ShotNum 증가
EMR 환자도 2, 3처럼 이상하게 중복 증가되는 문제 방지

<<import 용 lastshootdate>>
1)Acquisition DateTime
DicomTag.AcquisitionDateTime

2)Content Date + Content Time
DicomTag.ContentDate
DicomTag.ContentTime

3)Study Date + Study Time
DicomTag.StudyDate
DicomTag.StudyTime

4)Series Date + Series Time

5)없으면 null

(결론)
각 dcm 파일마다 import 촬영일시 추출
같은 환자 그룹이면 가장 최신값으로 LastShootDate 반영

4. 병합처리할 경우
-lastshootDate 비교처리 해서, 제일 마지막에 촬영할 것으로 변경
-shootnum은 서로 합쳐져서 표시되어야함

5. 중복된 데이터 관련
edit dialog 처리-환자 번호, 생년월일, 성별 같아야지만 중복이라 판단하게 변경


6. 마지막 촬영 일자, 총촬영 횟수-일자로 통일 => 환자 카드에 로드

---------------------------------------------------------------
2026.03.19

(현재)
1️. Dicom Record 시작 //녹화 시작
StartDicomRecord()

_Dicom.avi 먼저 생성됨
GenerateDicomAviPath(...) → "_Dicom.avi"

2. 녹화 종료
StopDicomRecord()

dm.SaveVideoFile(dcmPath, _aviSavePath); //Dicom 변환
이미 존재하는 _Dicom.avi를 사용해서 _Dicom.dcm 생성

(향후) 역방향 변환 필요
Dicom.dcm 존재 ->Dicom.avi 생성

import된 멀티 프레임 DICOM (.dcm) 존재 
->자동으로 _Dicom.avi 생성
---------------------------------------------------------------
파일명 기반 StudyID 추출 삭제

DICOM 태그 기반으로만 판단

내부 저장용 StudyID는 12자리 규칙으로 재생성

같은 import 내에서는 캐시로 같은 스터디 유지

기존 폴더와 충돌 없게 sequence 증가

(규칙)
내부 저장용 폴더 StudyID는 항상 12자리 규칙으로 통일

외부 DICOM의 원본 Study 관련 태그는 읽기만 하고

내부 저장용 StudyID는 아래 규칙으로 생성

(결론)
원본 DICOM의 StudyID가 12자리 숫자면 그대로 사용

아니면 StudyDate + sequence(0001~) 방식으로 새로 생성

StudyDate도 없으면 오늘 날짜 기준으로 생성

(내부 저장용 StudyID 생성 규칙)-같은 날 여러 건도 출돌 없음

DICOM StudyID가 ^\d{12}$ 이면 그대로 사용

아니면 StudyDate(yyyyMMdd)를 가져옴

그 날짜 기준으로 환자 폴더 안에서 이미 존재하는 StudyID들을 조회

yyyyMMdd0001, yyyyMMdd0002 ... 식으로 안 겹치는 값 생성

==>외부 import는 내부 장비처럼 완벽한 메타를 믿지 않는다
==>같은 환자 + 같은 StudyID 또는 같은 StudyDate 는 한 묶음으로 본다

---------------------------------------------------------------
(현재) import 진행이 오래 걸리는 이유
//import 과정
-DICOM 파일 오픈
-태그 읽기
-파일 복사
-멀티프레임이면 AVI 생성
-DICOM 태그 수정
-파일명 재정렬
-다시 멀티프레임 DICOM 읽어서 AVI 생성

파일 전체를 메모리로 다 읽기에 파일수 증가할 경우-지연 발생
복사한 뒤 전체 .dcm를 또 한 번 전부 열고 다시 저장함(읽고, 복사하고, 열고, 다시 저장) -I/O 증가, 지연 발생 
멀티프레임 DICOM 하나당 프레임 전체를 다시 렌더링해서 AVI로 사용-지연 발생

---------------------------------------------------------------
2026.03.20

3.import할때 팝업창에 실패한 환자 수 알려주기

4. 병합처리할 경우
-lastshootDate 비교처리 해서, 제일 마지막에 촬영할 것으로 변경
-shootnum은 서로 합쳐져서 표시되어야함

5.기존 환자와 똑같은 환자가 import되는 경우 처리

6. 중복된 데이터 관련
edit dialog 처리-환자 번호, 생년월일, 성별 같아야지만 중복이라 판단하게 변경

7. 마지막 촬영 일자, 총촬영 횟수-일자로 통일 => 환자 카드에 로드

8. 더블클릭 불가하게 변경, 시리얼 넘버-환경변수로 로드할 때 들고 있다가 Dicom 저장 

---------------------------------------------------------------
(import 실패)
-실패는 환자 단위로 본다
-루프 중간에 실패 팝업을 계속 띄우지 않는다
-failedPatientCount, failedpatients 리스트를 관리한다
-마지막 완료 메시지에서 성공/실패 수를 함께 보여준다
-가능하면 파일/DB 작업의 롤백 정책을 포함한다. (파일 복사 후 DB 저장 실패하면, 복사한 파일/폴더를 지워준다)
한 환자에 파일이 여러 개 존재, 한 개라도 실패하면 환자 전체 실패로 판단한다.

환자 성공=해당 환자 그룹의 모든 파일, DB 작업 성공
환자 실패=그 외 전부

(결론)
-환자 단위 성공/실패
-파일 하나라도 실패하면 환자 전체 실패 
-중간 팝업 없음
-파일 복사 후 DB 저장 실패 시 롤백
-ImportPatientFilesToStructhredFolders까지 포함
-실패가 많아도 팝업에는 요약 위주
-실패 상세는 텍스트 파일로 저장

>>팝업: 요약만 표시

실패 환자 상세: 로그 + txt/csv 파일 저장 

실패 수가 적을 때만 팝업 하단에 일부 표시

예: 5명 이하까지만 이름 노출

-----------------------------------------------------------------
(롤백 정책)

현재는 파일 저장 성공 후 DB 실패 시 폴더 전체 삭제입니다.

-케이스
기존 환자 폴더가 이미 있었는데 이번 import에서 덮어쓴 경우

지금 예시는:

if (Directory.Exists(patientFolderPath))
{
    Directory.Delete(patientFolderPath, true);
}
Directory.CreateDirectory(patientFolderPath);

기존 폴더를 지우고 새로 생성합니다.
기존 데이터가 이미 있었던 경우 위험할 수 있습니다.

환자별 하위에 import timestamp 폴더 생성
임시 폴더에 먼저 복사 후 성공 시 rename/move


(안전한 실무형 권장 개선)

지금 코드도 동작은 괜찮지만, 더 안전하게 하려면
ImportPatientFilesToStructuredFolders()를 임시 폴더 기반으로 바꾸는 걸 추천합니다.

-현재 방식
최종 폴더에 바로 복사
DB 실패 시 폴더 삭제

//문제사항
import가 실패해도 기존 정상 데이터를 되돌릴 수 없게 만든다

if.기존에 환자 폴더에 정상 데이터가 이미 존재
1)기존 폴더 삭제
2)새 폴더 생성
3)파일 몇 개 복사
4)중간에 예외 발생
5)롤백으로 새 폴더 삭제

result
1)기존 데이터 상실
2)새 데이터 미존재
=>환자 데이터가 상실될 가능성 존재

(1) 복사 중 일부 파일 실패
-원본 파일 하나가 잠겨 있음
-디스크 공간 부족
-권한 문제
-네트워크 드라이브 끊김
=> 시작 전에 기존 폴더를 지우면, 실패 순간 기존 데이터 복구 불가

(2) DB 저장 실패
-파일 저장
-DB 저장
-DB 실패 시 파일 롤백
=> 기존 폴더를 먼저 지운 상태면, DB 실패 후 롤백할 때 지워지는 건 새 폴더/원래 있던 기존 데이터는 이미 삭제되서 없음

(3) 같은 환자 재 import
같은 환자를 다시 가져오는 경우( 기존 폴더를 무조건 삭제)
-과거 영상까지 함게 저장되어 있음
-이번에 일부 파일만 다시 import
-전체 폴더 삭제 후 새 파일만 복사
=>예전 파일 유실, 사용자는 추가 import라고 생각했는데, 실제로는 전체 교체인 상황\

(4)폴더명 충돌
{환자명}_{환자코드}_{AccessionNumber}
=>같은 환자/같은 accession으로 다시 들어오는 경우, 기본 폴더와 충돌 가능
삭제 후, 재생성 정책이면 데이터 덮어쓰기 위험 발생

//특이 위험 사항
기존 데이터가 임상적으로 의미 있음

import가 “추가” 개념일 수 있음

파일 개수가 많음

네트워크/외장 저장소 사용

같은 환자를 여러 번 import 할 수 있음

장애가 발생해도 기존 데이터는 반드시 보존해야 함

-안전한 방식
1.patientFolderPath_temp_GUID 같은 임시 폴더에 복사
DB 성공
임시 폴더를 최종 폴더로 이동
실패 시 임시 폴더만 삭제
이 방식이면 기존 폴더를 잘못 날릴 위험이 줄어듭니다.

-기존 데이터 보존
-실패 시 롤백 명확
-최종 상태가 깔끔함
-운영상 가장 안정적

2.기존 폴더 삭제 대신 timestamp 폴더 생성
-기존 폴더 안 지움
-실패 시 이번 폴더만 삭제
-기존 데이터 보존

//기존 폴더 삭제 허용된는 경우
이 import는 “갱신”이고 기존 데이터는 반드시 폐기하는 정책
import 실패 시 복구 가능한 별도 백업이 있음
폴더 자체가 캐시 성격임
원본 데이터가 항상 외부 시스템에 안전하게 남아 있음

(결론)
기존 폴더 삭제 방식은 피하고
새 폴더 생성, 임시 폴더, 백업 후 교체

데이터 안전성
실패 시 복구 가능성
기존 데이터 보존
운영 중 사고 방지
=>임시 폴더에 먼저 저장 → DB 성공 → 마지막에 교체

1.기존 환자 폴더는 그대로 둠
2.새 데이터를 임시 폴더에 먼저 복사
3.DB 작업 수행
4.전부 성공했을 때만 최종 교체
5.중간 실패 시 임시 폴더만 삭제

commit 직전까지 기존 데이터를 보존-파일 시스템에서 DB 트랜잭션 비슷하게 흉내 내는 방법

DB는 트랜잭션이 있는데 파일 복사는 보통 트랜잭션이 없으니,안정성을 확보(실패해도 기존 데이터는 존재)

(최종)
1)기존 폴더 경로 계산
2)임시 폴더 생성
3)임시 폴더에 파일 모두 복사
4)임시 폴더 검증
-파일 수
-필요하면 DICOM 읽기 가능 여부
5)DB 저장 성공
6)기존 폴더가 있으면 백업 이름으로 변경
7)임시 폴더를 최종 폴더명으로 이동
8)최종 성공 후 백업 폴더 삭제
9)실패 시
-임시 폴더 삭제
-필요하면 백업 복원

//삭제는 마지막
기존 폴더를 먼저 지우지 말고
새 데이터 준비 완료/ DB 완료/ 최종 교체 직전
=>이 시점에만 기존 폴더를 백업/교체

임시 폴더 → 검증 → DB → 기존 폴더 백업 rename → 임시 폴더 rename → 백업 삭제
-Directory.Move
같은 볼륨 안에서는 비교적 안전하고 빠름

예)
Hong_1234 ← 현재 운영 폴더
Hong_1234_temp_a1b2c3
Hong_1234_backup_20260320_101010

성공 시:
Hong_1234 → Hong_1234_backup_...
Hong_1234_temp_a1b2c3 → Hong_1234
문제 없으면 backup 삭제

실패 시:
temp 삭제
backup 있으면 원복

파일 수가 매우 많음
DICOM 메타데이터 읽기 반복
IsMultiFrameDicom() 검사 비용
DB insert/upsert 반복
UI 업데이트를 너무 자주 함
네트워크 드라이브 사용
=> 병목은 보통 폴더 교체 방식보다 파일 I/O와 DICOM 처리 자체

1. temp 폴더는 최종 폴더와 같은 부모 경로에 생성
D:\Patients\Hong_1234
D:\Patients\Hong_1234_temp_abcd
D:\Patients\Hong_1234_backup_20260320

2. backup은 copy 말고 rename
Directory.Move(currentPath, backupPath);
복사가 아니라 이름 변경만 하도록.

3. 마지막 교체 직전까지만 temp 사용
즉, 복사는 temp에만 하고 최종 교체는 마지막 한 번만.

4. UI 업데이트 빈도 줄이기
환자 수가 많으면 매 환자마다 Dispatcher.Invoke 하는 것도 조금 부담일 수 있습니다.
필요하면 5명 단위, 10명 단위 업데이트로 줄일 수 있습니다.

5. 실패 보고서는 마지막에 한 번만 저장

(결론)
데이터 보존이 가장 중요하다
→ 임시 폴더 방식 추천

속도가 절대적으로 중요하고, 기존 데이터 날아가도 복구 가능하다
→ 단순 교체도 가능

(방안)
1. 가장 안전
temp 생성
기존 폴더 backup rename
temp를 최종 폴더로 rename
성공 후 backup 삭제

장점:
복구가 가장 안전

단점:
잠깐이라도 폴더가 2~3개 존재
디스크 여유가 필요

2. 공간 절약형 안전 방식(임시 폴더만 사용하고 backup 폴더는 만들지 않는 방식)
temp 생성
DB 성공
기존 폴더 즉시 삭제
temp를 최종 폴더로 rename

장점:
backup 폴더가 없어서 공간 부담 적음
temp만 추가로 필요

단점:
기존 폴더 삭제 후 temp rename 사이에 실패하면 복구 어려움
그래도 “처음부터 기존 폴더 삭제”보다는 훨씬 안전

>>기존 데이터는 함부로 날리면 안 됨

하지만 temp + backup까지 두면 용량이 부담됨

파일 수가 많아서 관리 포인트 늘어나는 것도 싫음

3. 더 단순한 방식
기존 폴더 유지
새 폴더 timestamp로 생성
최신 것만 사용

장점:
구현 간단
기존 데이터 안전

단점:
폴더가 계속 쌓임
장기적으로 용량 관리 더 안 좋아짐

----------------------------------------
(차이)
기존 위험한 방식은:
시작하자마자 기존 삭제
새 복사 실패 시 끝

(1) 복사 중 일부 파일 실패
-원본 파일 하나가 잠겨 있음
-디스크 공간 부족
-권한 문제
-네트워크 드라이브 끊김

(2) DB 저장 실패
-파일 저장
-DB 저장
-DB 실패 시 파일 롤백
=> 기존 폴더를 먼저 지운 상태면, DB 실패 후 롤백할 때 지워지는 건 새 폴더/원래 있던 기존 데이터는 이미 삭제되서 없음

(3) 같은 환자 재 import
같은 환자를 다시 가져오는 경우( 기존 폴더를 무조건 삭제)
-과거 영상까지 함게 저장되어 있음
-이번에 일부 파일만 다시 import
-전체 폴더 삭제 후 새 파일만 복사
=>예전 파일 유실, 사용자는 추가 import라고 생각했는데, 실제로는 전체 교체인 상황

(4)폴더명 충돌
{환자명}_{환자코드}_{AccessionNumber}
=>같은 환자/같은 accession으로 다시 들어오는 경우, 기본 폴더와 충돌 가능
삭제 후, 재생성 정책이면 데이터 덮어쓰기 위험 발생

(현재)덮어쓰기 방식
시작하자마자 기존 삭제
새 복사 실패 시 끝

파일 개수가 많음
네트워크/외장 저장소 사용
같은 환자를 여러 번 import 할 수 있음
장애가 발생해도 기존 데이터는 반드시 보존해야 함(기존 데이터가 임상적으로 의미 있음)

--------------------------------------------------------------------------------------------
임시 폴더만 사용하고 backup 폴더는 만들지 않는 방식
새 복사가 다 끝날 때까지 기존 보존
성공 직전에만 기존 삭제


---------------------------------------------------------------------------------------------
1.기존 폴더 그대로 둠
2.temp 폴더에 새 데이터 전부 복사
3.DB 작업 성공
4.기존 폴더 삭제
5.temp 폴더를 최종 폴더명으로 변경

(정점)
import 중 실패하면 기존 폴더 그대로 살아 있음
backup 폴더를 따로 안 만들어도 됨
공간은 기존 + temp 정도만 잠깐 필요
파일 관리도 비교적 단순
=>기존 방식보다 훨씬 안전하고, full backup 방식보다 가볍습니다.


>>임시 폴더만 사용하고 backup 폴더는 만들지 않는 방식
환자별 temp 폴더에 먼저 저장
temp 안에서 구조 생성 / 태그 수정 / AVI 생성 / 파일명 정리
temp 전체 검증
DB 저장
마지막에만 기존 폴더 삭제 후 temp를 최종 폴더로 이동
실패 시 temp만 삭제
환자 단위 성공/실패 집계
마지막에 요약 팝업
실패 상세는 txt 저장

-------------------------------------------------
(현재 사항)
DB는 patientcode가 같으면 추가 방지,
파일은 여전히 import 처리

-DB 중복은 일부 방지
-파일 중복은 방지 안됨
-같은 촬영은 재수입하면 중복 STUDY/FOLDER/FILE 생성 가능
-SHOTNUM, LASTSHOOTDATE 덮어쓰기/누락 가능

(경우의 수)
1.완전히 같은 기존 환자+같은 검사/같은 STUDY
-같은 환자를 같은 DICOM 파일로 다시 IMPORT
-같은 SUTDYID, 같은 SOPINSTANCEUID들이 이미 존재

=> SKIP 처리
DB 갱신 안함
파일 복사 안함
사용자에게 "이미 가져온 검사입니다"알림

2.같은 환자 + 새로운 검사(STUDY 추가)
-환자는 같지만 다른 날짜 촬영
-혹은 같은 날짜라도 다른 SUTDYID

=>MERGE/IMPORT 처리
환자 재생성 안함, 
기존 환자에 검사만 추가, 
파일만 새 SUTDY 폴더에 IMPORT
LASTSHOOTDATE, SHOTNUM 갱신

3.PatientCode는 같지만, 사람이 다른 경우(이름/생년월일 다름)
-자동 병합하면 안 됨
-사용자 선택

=>같은 환자번호의 기존 환자가 존재하지만 이름/생년월일이 다릅니다. 
새 환자로 등록할지, 기존 환자에 병합할지 선택해주세요


1)EMR(import with accession)
기존 EMR 환자 존재 시:
-AccessionNumber 같으면 동일 검사
같은 study/file이면 skip
새 study만 있으면 추가 import

-AccessionNumber 다르면 같은 환자의 다른 검사
기존 환자 유지 + 새 검사 추가

2)Local(import without accession)
PatientCode 같은 local 환자 존재 시:
-이름/생년월일도 같으면 같은 환자
새 study만 추가

-다르면 충돌
사용자 확인 필요

(결론)
기존 환자가 존재할 때 동일 환자를 import하면 “새 환자 생성”이 아니라 
“기존 환자에 검사 추가”로 처리하고, 같은 검사/같은 파일은 SOPInstanceUID 또는 
StudyInstanceUID 기준으로 중복 import를 막는 방식이 가장 적절합니다.

-중복 환자
EMR: AccessionNumber 우선
Local: PatientCode + PatientName + BirthDate

-중복 검사
StudyInstanceUID 또는 StudyID 기준
같은 검사면 skip

-충돌 환자
PatientCode는 같은데 이름/생년월일이 다르면 충돌
자동 병합하지 않고 건너뜀 + 마지막에 요약 알림


-------------------------------------------------------------------------------
1. 기존 환자 + 완전히 같은 검사

StudyInstanceUID / StudyID 기준으로 이미 있으면

SkipDuplicateStudy

2. 기존 환자 + 새로운 검사

환자는 새로 만들지 않고

기존 환자 폴더에 새 검사만 import

3. 같은 PatientCode인데 이름/생년월일 다름

자동 병합 안 함

SkipConflictPatient

4. 같은 검사인데 일부 DICOM만 다시 들어온 경우

SOPInstanceUID 기준으로 기존 파일 제외 후

신규 파일만 import


------------------------------------
문제 원인:

Study 단위 중복 판정

해결 방법:

DICOM 파일 단위(SOPInstanceUID/fallback key) 중복 판정으로 변경



----------------------------------------------
import 시
동일 identity면 기존 환자에 검사 추가
동일 code지만 다른 identity면 신규 환자로 생성
import 완료 후 동일 code 환자 2명 이상이면 안내 팝업 표시

------------------------------------------------
기존 동일 환자 판단: 코드 + 이름 + 생년월일
병합 후보 판단: 코드 + 생년월일 + 성별
같은 코드만 같음: 안내만


----------------------------------------------------
3.import할때 팝업창에 실패한 환자 수 알려주기

4. 병합처리할 경우
-lastshootDate 비교처리 해서, 제일 마지막에 촬영할 것으로 변경
-shootnum은 서로 합쳐져서 표시되어야함

7. 마지막 촬영 일자, 총촬영 횟수-일자로 통일 => 환자 카드에 로드

----------------------------------------------------------------
import 실패 원인

1.파일 자체가 손상
파일 손상, .dcm 확장자지만 실제 DICOM 아님
DicomFile.open() 자체 실패, 읽는 도중 예외 발생

2.환자 식별 정보(DICOM tag가 비정상/누락)
patientID가 비어있음
patientID가 숫자가 아님(int.TryParse 실패)
patientName이 비어있음
patientBirthDate 형식 이상
sex 값이 비정상

(현재) 완전 실패하는 경우는 없음, 정상 import로 보기 애매한 파일로 간주
patientName 미존재 ->unknow name
birthdate 실패-> 1900-01-01

3.검사/폴더 구성 정보 문제
studyid, studydate가 이상해서 폴더 구조 생성이 꼬임
같은 환자인데 태그 조합이 너무 이상해서 의도치 않은 새 study로 분리됨
numberoffrate값 이상
멀티프레임 판별 중 예외 발생

4.중복 판정/기존 데이터 비교 중 문제 (비교 과정에서 기존, 신규 판정 불안정 문제)
기존 폴더 내 dcm 읽다가 예외
sopinstanceuid / studyinstanceuid 읽기 실패
파일 키 생성 실패

5.파일 시스템 문제
폴더 생성 실패, 파일 복사 실패, 파일 이동 실패, 같은 이름 파일 존재, 파일 잠금, 권한 문제, 경로 길이 문제, 디스크 공간 문제

6. 후처리 실패 (읽기 성공, 저장/정리 단계 실패)
태그 수정, rename, video/dicom pair 정리, avi 생성, 병합 후 정리

-----------------------------------------------------
같은 patientcode라도 이름/생년월일/성별이 다른 경우 존재
자동 충돌 제외하지 않고 둘다 존재하게 정함
나중에 병합 여부는 사용자 판단
=>정상 import 대상으로 간주하여 실패한 import 폴더에 포함하지 않음

error 간주 사항(사용자가 직접 확인, 원본 파일 다시 받아와야함-보관에 포함하는 의미 있다고 간주)
-DicomFile.Open() 실패
-필수 태그 읽기 실패
-PatientID 숫자 변환 실패
-파일 복사 실패
-폴더 생성 실패
-rename 실패
-태그 수정 실패
-저장 중 예외 발생

-------------------------------------
ImportError
 └─ 20260323_091530
     ├─ 파일들...
     └─ error_log.txt

가져오기 완료
완료: 5명
중복 제외: 2건
오류 파일: 1건
오류 파일은 ImportError 폴더에 저장되었습니다.

------------------------------------------
import 로직
파일 단위처럼 보여도 최종 의미는 환자 단위에 가까움

사용자 입장에서 궁금한 점
-몇 명이 등록됐는지
-몇 명이 제외됐는지
-몇 명이 실패했는지

파일 수로 보여주면 문제 사항
-한 환자에 dcm가 30개면 완료:30건 처럼 보여서 과하게 커 보임
-한 명 실패했는데 파일이 50개면 오류 파일:50 건이라 사용자 입장에서는 "환자 50명이 실패한 건가?"라고 헷갈림
-환자 파일 import, 총 n명의 환자 파일 - 환자 중심에 해당

(1)메인 요약-환자 기준
팝업 요약=환자 기준

(2)보조 정보- 파일 기준
eroor 폴더/txt 로그 = 파일 기준 상세

가져오기 완료
완료: 5명
중복 제외: 2명
오류: 1명
오류 파일 12건은 ImportError 폴더에 저장되었습니다.

(결론)
사용자에게 중요 - 환자가 처리됐는지
개발자, 상세 확인용 정보-파일의 갯수

완료 → 환자 수
중복 제외 → 환자 수
오류 → 환자 수
상세 로그/보관 → 파일 수

----------------------------------------------------
병합 성공 완료 팝업은 한 군데에서만 띄운다
Import와 Edit 후 병합 제안 문구를 분리한다
아니오를 눌러도 나중에 다시 병합 가능하다는 문구를 명확히 한다

-------------------------------------------------------
3.import할때 팝업창에 실패한 환자 수 알려주기
실패 폴더 생성

4. 병합처리할 경우
-lastshootDate 비교처리 해서, 제일 마지막에 촬영할 것으로 변경
-shootnum은 서로 합쳐져서 표시되어야함

카드에 마지막 촬영 일자, 촬영 횟수 넣기
try~catch 잘되어 있는지 확인
테스트 많이 이행
import 부분 따로 클래스 생성
플로우차트 생성
환자 관련 코드 정리

-------------------------------------------------------
에러 없음:importError 폴더 아예 생성 안됨
일부 에러 있음: 해당 batch 폴더만 유지
batch 폴더만 있고 파일 없음: 자동 삭제
importError 전체 비움: 루트까지 삭제

DICOM 태그 이상해도 무조건 import되는 안전 구조

------------------------------------------------------
(현재)
import 실패 발생
importerror 폴더 만드는 지점 전에 종료되는 상황 발생

(1)importpatient()에서 buildpatientimportgroups()호출, 결과 0개
1)가져올 수 있는 dicom 환자 정보가 없습니다.라는 팝업 띄우고 return 실행
2)importerrorbatch폴더는 뒤쪽에서 만들어짐

(2)BuildPatientImportGroups(...) 내부나 그 전에 DICOM 읽는 부분에서 예외 발생
대개 catch에서 로그만 쓰고 계속 진행하는 형태
invalid DICOM이 들어와도 “실패 환자”로 수집되지 않고, 그룹이 안 만들어진 채 종료

(결과)
완전히 깨진 dicom은 초반에 탈락해서 에러 폴더 생성 로직에 도달하지 못함
실패는 했는데 import 후반부 실패 처리 파이프라인에 포함되지 않음

BuildPatientImportGroups(...) 내부나 그 전에 DICOM 읽는 부분에서 예외 발생
대개 catch에서 로그만 쓰고 계속 진행하는 형태
invalid DICOM이 들어와도 “실패 환자”로 수집되지 않고, 그룹이 안 만들어진 채 종료

(해결방안)
1)CreateImportErrorBatchFolder() 먼저 실행될 필요 존재
최소한 supportedFiles 확인 직후, BuildPatientImportGroups(...) 호출 전

2)patientGroups.Count == 0 인 경우에도 원본 파일을 ImportError 폴더로 옮기거나 복사 필요
팝업만 띄우고 종료, 폴더가 생성 안됨


-------------------------------------------------------------------
BuildPatientImportGroups(...) 내부에서 DICOM 읽기 실패 시 
단순 로그만 쓰고 continue 하지 말고, 실패 파일 경로를 별도 리스트에 모아서 
ImportPatient()에서 같이 SaveFilesToImportErrorFolder() 하도록 바꾸기

------------------------------------------------------------------
ShotNum = 로컬 ShotNum + EMR ShotNum
LastShootDate = 둘 중 더 늦은 날짜
DB에는 EMR 환자 레코드가 최종값으로 저장
그 다음 로컬 환자 삭제