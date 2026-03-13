<환자별 촬영 통계(누적 횟수 및 최종일)를 산출하고 UI에 표시하는 개발 플로우 차트>

PatientViewModel 내에서 데이터를 로드하거나 업데이트할 때의 핵심 로직을 포함

■ 환자 촬영 정보 집계 및 표시 플로우 차트

1. 전체 프로세스 흐름도
(1) 시작 (Start): 환자 목록 로드 또는 특정 환자 선택 시 트리거.

(2) 데이터 소스 판별: 해당 환자가 MWL(서버) 환자인지 Local(로컬 DB) 환자인지 판별.

Local 기준: PatientModel.Source 또는 IsLocal 플래그 확인.

(3) DICOM 데이터 쿼리:

Scan_TABLE에서 해당 PatientID를 조건으로 데이터 조회.

(4) 통계 연산 (Business Logic):

-마지막 촬영일: MAX(StudyDate) + MAX(StudyTime) 추출.

-총 촬영 횟수: COUNT(DISTINCT StudyInstanceUID) 수행 (중복 날짜/자정 촬영 방어 로직).

(5) UI 모델 갱신: 계산된 값을 WorklistItemUIModel의 필드에 바인딩.

(6) 종료 (End): 화면에 최종 검사일과 횟수 표시.

2. 세부 단계별 구현 전략
Step 1: SQL 기반 집계 (Repository 레벨)
파일 개수를 카운트하는 것이 아닌, SQL 쿼리를 활용하여 '검사 단위'를 정확히 카운트

-- SQLite 기준: 특정 환자의 통계 정보를 가져오는 쿼리
SELECT 
    PatientID,
    MAX(StudyDate) AS LastDate,
    COUNT(DISTINCT StudyInstanceUID) AS TotalCount
FROM Scan_TABLE
WHERE PatientID = @pid
GROUP BY PatientID;

Step 2: ViewModel 로직
데이터베이스에서 가져온 값을 ObservableCollection에 담기 전에 가공

(1)자정 촬영 예외 처리 
쿼리에서 StudyInstanceUID를 기준으로 DISTINCT 카운트를 했으므로, 
날짜가 바뀌더라도 동일 UID면 1회로 자동 집계됩니다.

(2)날짜 포맷팅: 20260310 형태의 문자열을 UI용 2026-03-10으로 변환.

Step 3: UI 바인딩 (View 레벨)
WorklistItemUIModel에 아래 필드를 추가하여 XAML과 연결합니다.

(1) LastStudyDate: string 타입,"마지막 검사일" 섹션에 바인딩
(2) TotalStudyCount: int 타입, "누적 검사 횟수" 섹션에 바인딩

(주의사항)
1. 데이터 정렬: 환자 상세 이력 리스트 출력 시 반드시 StudyDate와 StudyTime 기준 내림차순(DESC) 정렬 적용 확인

마지막 촬영 날짜
존재- 환자 최신 순 정렬
존재X- 환자 등록 시점 정렬

2. 실시간성: 새로운 촬영(Import 또는 Scan)이 완료된 직후 LoadPatients()가 호출되어 통계가 즉시 갱신되는지 확인
3. 비동기 처리: 환자 수가 많을 경우 COUNT(DISTINCT) 연산이 UI 스레드를 차단하지 않도록 Task.Run 내에서 수행되는지 확인

4. 의료 영상 표준의 비즈니스 로직 수립
날짜가 같더라도 UID가 같으면 1회/ 날짜가 다르더라도 UID가 같으면(자정 촬영) 1회
