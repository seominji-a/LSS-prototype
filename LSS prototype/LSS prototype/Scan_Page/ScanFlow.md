SCAN 페이지 구조 문서
작성일: 2026-03-10
최종 수정일: 2026-03-10
작성자: 서민지

■ 요약
`데이터 출처에 따른 처리 이원화`

1.환자 구분 (Standard): AccessionNumber의 유무로 EMR(병원시스템)과 LOCAL(직접입력)을 판별

2.스캔 흐름: 환자 확인 → 주사 시간 등록 → 촬영 → 이미지 검토 후 저장 순으로 진행

3.저장 방식 (DICOM)
-EMR: 병원 서버에서 받은 원본 태그(Dataset)를 유지하여 병원 시스템과 동기화 진행
 EMR 환자는 병원 서버에서 받은 고유 번호와 태그 정보가 이미 존재
저장할 때 그 정보를 그대로 복사해서 병원 서버(PACS)로 다시 보내는 방식을 취함
-LOCAL: 빈 데이터셋에서 시작하여 시스템이 새로운 규격으로 생성
로컬 환자는 우리가 직접 등록한 환자이므로, 시스템이 새로운 고유 번호를 부여하고 규격에 맞게 DICOM 파일을 새로 생성

4.데이터 관리
DICOM 객체(Dataset)는 저장 시점에만 사용, 
관리는 텍스트 데이터(AccessionNumber)로 대체

촬영이 끝나면 사용자가 이미지를 최종 확인하고 '저장' 버튼을 누르는데, 
시스템이 자동으로 '이 환자가 EMR인지 로컬인지' 판단해서 적절한 태그를 입혀 파일화 진행
저장 완료 후에는 용량이 큰 원본 데이터셋은 메모리에서 비우고, 
DB에는 꼭 필요한 환자 정보와 촬영 횟수 같은 통계값만 남기도록 설계해 성능 최적화 필요

■ 현재 촬영 구조 
환자 선택-> Scan 메뉴 클릭(study 생성)->촬영 시작-> 촬영 설정(series 생성)-> image scan 촬영 버튼(instance 생성)
Study(Setstudy): 각 환자 검사 번호
Series(SetSeries): 각 촬영 세션 번호
Instance(SetContent): 각 이미지가 저장 번호

■PACS 연동 시 가장 중요한 것
StudyInstanceUID 유지
SeriesInstanceUID 유지
SOPInstanceUID 매 이미지 생성

(고려사항)
언제 새 Series를 만들지 결정 필요
-촬영 모드(icg/dual) 변경
-컬러맵/필터 변경
-검사 단계 변경
-장비 조건의 의미 있는 변화(설정 값 변경)
-사용자가 “새 촬영 세트 시작”을 눌렀을 때

같은 촬영 세션에서 여러 장을 찍을 거면 SeriesNumber는 고정 권고
이유: 같은 시리즈에 속한 이미지들은 PACS에서 한 묶음으로 보이는게 필수

SeriesNumber를 고정하는 이유는
같은 촬영 세션에서 찍은 여러 장을 PACS에서 하나의 Series로 묶기 위함

한 촬영 세션 동안은 같은 값 유지 
반대로 단순히 같은 조건으로 여러 장 찍는 건 새 Series가 아니라 같은 Series의 여러 Instance가 맞음 

ex)ICG 모드로 연속 10장 촬영
한 Series

ex)ICG 모드에서 찍다가 화이트 모드로 바꿈 및 줌/필터/촬영 조건이 바뀜
새 Series

■ Dicom 파일 안에 각 환자(이름)별 폴더 생성
PACS 뷰어는 보통 Series 단위로 썸네일/스택/그룹핑-스택 탐색 편리, 같은 촬영 묶음인지 파악 가능
홍길동
 └ 2026-03-11 Study
     └ ICG Series
         ├ 1
         ├ 2
         ├ 3
         ├ 4
         └ 5

■ 규칙
-StudyInstanceUID - 같은 검사에서는 동일
-SeriesInstanceUID - 촬영 세션마다 새로 생성
현재 촬영 진입 시점 별로 새로 생성 
-SOPInstanceUID - 이미지 마다 새로 생성


■ 향후 개발 진행 계획
1. 이미지 코멘트 관련
이미지가 저장되었을 경우 실시간으로 썸네일 저장 방법 고려
(1)실시간으로 저장될 때 로드시키는 방법

2. icg 주입 설정 시간 포함

3. icg led, dual led 별로 이미지 파일명 차이 존재

4. Record dicom, Record video 관련 구현

5. import 
(현재) LOCAL/EMR로 구분 가능, .dcm 파일만 가져옴
(수정) 한자 별 폴더를 가져오는 방식 진행 예정



<LSIS Scan 전체 흐름>
■ Scan 동작 처리 
(Scan 버튼 클릭 -> 주사 시간 등록)

1. Scan_Click() 함수
선택 환자가 미예약(로컬)인지 여부에 따른 처리

(1)yes(선택 환자-로컬)
-> 경고 메시지 표시(예약되지 않은 환자를 촬영하겠습니까?)
1)yes (로컬 환자 촬영하겠다)
-> ScamBtn() 함수 호출

2)no (로컬 환자 촬영하지 않겠다)
-> 종료

(2)no(선택 환자-emr)
->ScanBtn() 함수 호출

2.ScanBtn()
(1)ScanLoad 호출(스캔 탭 이동)

(2)DB_injection 조회(오늘 주사 기록)
1)count =0이 아닐 경우
DB_Injection SELECT (기존 주사시각 조회)
->slt.SelectinjectionTime = DB 기존 값

2)count =0일 경우 
injectionWindow 팝업 표시

1-DB_Patient 존재 확인
존재하지 않을 경우
-DB_Injection INSERT (주사시각 저장)
->slt.SelectinjectionTime = 입력값

■ Scan 이미지 저장 처리
(Confirm_Click → ImageCheck → Save_Click → DICOM 파일 저장)

1.Confirm_Click()
ImageCheck 팝업 표시(촬영된 이미지를 확인하고 DICOM으로 저장하는 팝업 창)

(1)ImageCheck 내부의 Save_Click에서 저장 완료 시 
1) DialogResult = true로 설정
2) logdata.WriteLog("데이터 저장");
데이터 저장" 이벤트를 로그 파일/DB에 기록
3) await dm.LodadAsync(db) 목록 갱신
DICOM 저장 후 로컬 DB에서 환자 목록을 다시 불러와 UI를 갱신.

(2) ShowDialog(): 이 창이 닫힐 때까지 현재 스레드(UI)를 블로킹.
1) 사용자가 그냥 닫거나 취소하면 
DialogResult =false 또는 null.
2) 종료

■Save_Click 내부 상세 흐름

1.Save_Click()

(1)Auto/Manual 이미지 ------> `Auto/Manual 이미지 분리 삭제`
Mat -> Bitmap 변환 ------> `필요한 이유 향후 판단`
Dicomimage[] 구성

(2)seriesNumber, time
루프 공통값 준비

(3)이미지 수만큼 반복 (x=0~Auto+Manual)

(4) slt.Dataset ! = null? ------->`Dataset 의미`
(EMR vs Local 분기)

1) null일 경우-LOCAL
new DicomManager(HID, SerialNum)
->accessionNumber = "" (빈 값 권장)

2) not null일 경우-EMR
new DicomManager(HID, SerialNum, Dataset)
->accessionNumber = Dataset.AccessionNumber

(5)SetPatient / SetStudy
SetSeries / SetContent
SetPrivateDataElement

(6)await SaveImageFile()
.dcm 파일 저장

(7)img.Clear()
DialogResult = true
Window.Close()

■List_ImageReview_SelectionChanged → 이미지 조회 및 렌더링
1.selectionChanged()
선택항목 존재 여부 판단

(1)yes(선택 항목 존재)
1)lastClickedIndex 추출
DB_shotSave SQL
->FileName, Comment 조회

2)DICOM 파일 열기
DicomImage 렌더링

3) Bitmap→BitmapImage 변환
Image_Count()
ImageGrid 표시

(2)no (선택 항목 미존재)
ImageGrid.Clear()
Comment.Clear()

<`LOCAL 환자 저장 흐름`>
■ MWL 서버와 연동 없이 사용자가 직접 입력한 환자.
 slt.Dataset == null,  Reserved == false,  AccessionNumber 없음

1. 사용자가 직접 환자 정보 입력
2.WorklistItemUIModel
Reserved = false
Dataset = null
AccessNum = ""

2.Scan_Click()
"미예약 환자" 경고 표시
Yes → 계속

3.ScanBtn()
DB_Injection 체크
없으면 InjectionWindow

4.DB_Patient INSERT ← 최초 1회
이 시점에 처음 DB에 등록됨

5.Confirm_Click()
→ ImageCheck 팝업

6.Save_Click() 
(1) slt.Dataset == null
→ DicomManager(HID, SerialNum)
(빈 데이터셋으로 초기화)

(2) accessionNumber
현재 코드
accessionNumber = 날짜+"0001" 
같은 날 여러 검사 → 충돌!

권장 수정
accessionNumber = ""

(3) SetPatient / SetStudy("")
SetSeries / SetContent
SetPrivateDataElement

(4) SaveImageFile()
→ dicom\{FileName}.dcm
DB_ShotSave INSERT

(5) 저장 완료
DialogResult = true

<`EMR 환자 저장 흐름`>
■ 병원 RIS/EMR에서 워크리스트(MWL C-FIND)로 조회된 환자.
slt.Dataset != null,  Reserved == true,  AccessionNumber 병원 할당값 존재

1.GetWorklistPatientsAsync()
MWL C-FIND 요청
(예)(IP: 192.168.1.6, Port: 2000)

2.OnResponseReceived 콜백
→ WorklistItemUIModel 생성
Reserved = true
Dataset = 원본 DicomDataset
AccessNum = RIS 할당값

3.Scan_Click()
Reserved=true → 경고 없이
바로 ScanBtn()

4.ScanBtn()
DB_Injection 체크
주사시각 등록/조회

5.Confirm_Click()
→ ImageCheck 팝업

6.Save_Click()
(1)slt.Dataset != null
→ DicomManager(HID, SerialNum, Dataset)
기존 태그 복사하여 초기화

(2)accessionNumber =
Dataset.GetSingleValueOrDefault
(AccessionNumber, "")

(3)SetPatient / SetStudy(accessionNumber)
SetSeries / SetContent
SetPrivateDataElement

(4)SaveImageFile()
→ dicom\{FileName}.dcm
DB_ShotSave INSERT

(5)C-STORE
(예)IP: 192.168.1.6, Port: 4242
AET: ORTHANC ← RMICG

(6)저장 완료
DialogResult = true

<파라미터 의미>
HID: 환자 ID (Hospital ID)
Serial: 장비 시리얼 번호, UID 생성 재료
Dataset: MWL 서버에서 받아온 원본 DICOM 태그 묶음, AccesionNumber, StudyInstanceUID 포함

<DicomManager(HID, Serial) vs DicomManager(HID, Serial, Dataset) 차이>
(1) LOCAL: 빈 데이터셋에서 시작 → 모든 태그를 새로 만듦
DicomManager(HID, Serial)
→ dataset = new DicomDataset()  // 아무것도 없는 상태

(2) EMR: MWL 서버 태그를 복사해서 시작 → 기존 태그 유지
DicomManager(HID, Serial, Dataset)
→ dataset에 MWL 원본 태그 전체 복사
→ AddIfNotExists()로 이미 있는 태그는 덮어쓰지 않음
→ AccessionNumber, StudyInstanceUID 등 서버값 보존

<PatientModel의 역할>
DB 저장 + 화면 표시용 데이터

<DicomDataset의 역할>
수십 개의 DICOM 태그를 담은 객체
DB에 저장 불가, JSON 직렬화 불가(JsonIgnore 처리 필요)
->실제 DICOM 파일을 저장할 때만 필요하고, 그 시점은 `Save_Click` 안에서 `DicomFile.Open()`으로 직접 열면 됩니다.

<결론>
 DicomDataset은 WorklistItemUIModel에만 두고, PatientModel에는 AccessionNumber 문자열만 저장

<`전체 흐름에서 Dataset의 위치`>
[MWL 조회]
    WorklistItemUIModel.Dataset = 원본 DicomDataset  ← 여기에만 보관
          │
          ▼
[Save_Click]
    slt.Dataset != null  →  DicomManager(HID, Serial, Dataset) 생성 시 사용
          │
          ▼
[DICOM 파일 저장 완료]
    Dataset 역할 끝 → 더 이상 필요 없음
          │
          ▼
[DB 저장 / Import]
    PatientModel.AccessionNumber = "" or "RIS번호"
    → Dataset 없이 AccessionNumber만으로 EMR/LOCAL 구분 가능

<AccessionNumber 관련 사항>
DICOM 표준상 AccessionNumber (0008,0050)는 Type 2 태그
Type 2는 태그는 반드시 존재해야 하지만, 값은 비워도(empty string) 가능
즉, 로컬 환자처럼 병원 RIS에서 할당받지 않은 경우 빈 값("")으로 두는 것이 표준

■ LSIS에 날짜+"0001"로 채워준 이유
PACS 서버가 빈 AccessionNumber를 거부하거나 중복 처리 문제가 생길 것을 우려해 임시로 넣어둔 것으로 보입니다. 
같은 날 여러 검사가 생기면 AccessionNumber가 충돌하는 문제가 있습니다.

■ 수정 방향
MWL(워크리스트)에서 로드된 환자 → AccessionNumber 그대로 사용, 
로컬 수동 생성 환자 → "" 빈 값으로 설정하면 됩니다. 
slt.Dataset의 null 여부로 이미 두 케이스를 구분하고 있으니 분기를 활용하면 됩니다.

(수정 부분)
1.Save_Click 내부 (SetStudy 호출 부분):

// ── 변경 전 ──
dm.SetStudy(
    DateTime.Now.ToString("yyyyMMdd") + "0001",  // StudyID
    DateTime.Now.ToString("yyyyMMdd") + "0001",  // AccessionNumber ← 문제 부분
    ...
);

// ── 변경 후 ──
string accessionNumber = (slt.Dataset != null)
    ? slt.Dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, "") // MWL 환자: 서버값 사용-병원 RIS와 연동 필요
    : "";  // 로컬 환자: 빈 값-Type 2 허용, 충돌 방지

dm.SetStudy(
    DateTime.Now.ToString("yyyyMMdd") + "0001",  // StudyID는 내부 관리용이므로 유지 가능
    accessionNumber,                              // AccessionNumber
    DateTime.Now.ToString("yyyyMMdd"),
    time,
    slt.SelectInjectionTime,
    hpt.GetHospitalName(),
    ""
);

2.SetStudy() 내부에서 AddIfNotExists → AddOrUpdate 
// DicomManager.cs - SetStudy() 내부
// 변경 전
AddIfNotExists(DicomTag.AccessionNumber, accessionNumber);

// 변경 후 (빈 값도 명시적으로 기록하고 싶을 때)
dataset.AddOrUpdate(DicomTag.AccessionNumber, accessionNumber);

<Orthanc의 빈 AccessionNumber 처리 방식>
AccessionNumber (0008,0050)는 DICOM Type 2 태그로 정의
Type 2 의미: "태그는 존재해야 하지만, 값은 비워도 된다"

 AccessionNumber, PatientID, StudyInstanceUID, SeriesInstanceUID, SOPInstanceUID 등의 DICOM 식별자 인덱스 
 내부적으로 유지하면서 자체 내부 ID에 매핑

 RIS에서 AccessionNumber를 사전 할당받지 못한 경우에도 나중에 환자 정보를 채워 넣는 reconciliation 프로세스가 가능하도록 설계

 빈 AccessionNumber("")로 C-STORE를 받아도 거부하지 않는다

 ■ 수정할 필요 없는 부분
 DicomManager.SetStudy() 내부 코드
 AddIfNotExists를 쓰고 있으므로 MWL Dataset에 이미 AccessionNumber가 있으면 자동으로 그 값이 유지 가능