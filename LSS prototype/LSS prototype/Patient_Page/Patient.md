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

■ 6. import 데이터 처리 정책

PatientModel.cs

=PatientSource 함수
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

