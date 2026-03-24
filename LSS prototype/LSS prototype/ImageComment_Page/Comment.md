


모든 코멘트는 COMMENT 테이블에 저장하지만, **읽을 때는 파일 타입에 따라 다르게 처리한다.**

---

## COMMENT 테이블 구조

```sql
CREATE TABLE COMMENT (
    FILE_TYPE  TEXT NOT NULL,   -- 'IMAGE' / 'DICOM_VIDEO' / 'NORMAL_VIDEO'
    FILE_NAME  TEXT NOT NULL,   -- 파일명 (확장자 제외)
    COMMENT    TEXT NOT NULL DEFAULT '',
    PRIMARY KEY (FILE_TYPE, FILE_NAME)
);
```

---

## 저장 / 읽기 규칙

### IMAGE
- **저장** → dcm 태그 `(0020,4000) ImageComments` + COMMENT TB UPSERT
- **읽기** → dcm 파일 열 때 태그에서 직접 읽음 (TB 미사용)
- 이유: dcm 파일은 어차피 이미지 로드 시 열어야 하므로 태그 하나 더 읽는 게 DB 여는 것보다 빠름

### DICOM_VIDEO
- **저장** → avi와 쌍을 이루는 dcm 태그 `(0020,4000) ImageComments` + COMMENT TB UPSERT
- **읽기** → dcm 파일 태그에서 직접 읽음 (TB 미사용)
- 이유: PACS 전송 시 dcm 태그에 코멘트가 있어야 수신 측에서 읽을 수 있음

### NORMAL_VIDEO
- **저장** → COMMENT TB UPSERT만
- **읽기** → COMMENT TB에서 읽음 (`WHERE FILE_TYPE='NORMAL_VIDEO' AND FILE_NAME=@fileName`)
- 이유: avi 파일에는 DICOM 태그 구조가 없어 태그 저장 불가

---

## 핵심 요약

```
저장  →  모든 타입 전부 COMMENT TB에 기록
읽기  →  IMAGE / DICOM_VIDEO  → dcm 태그에서 읽음
         NORMAL_VIDEO         → COMMENT TB에서 읽음
```

---

## 주의사항
- 빈 코멘트 저장 시 COMMENT TB 행을 삭제 (빈 문자열 행 남기지 않음)
