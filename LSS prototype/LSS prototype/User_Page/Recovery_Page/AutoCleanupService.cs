using LSS_prototype.DB_CRUD;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LSS_prototype
{
    /// <summary>
    /// 만료된 삭제 로그 자동 처리 서비스
    /// 로그인 성공 후 백그라운드에서 실행
    /// 5분(테스트) 지난 미처리 항목 자동 강제삭제
    /// </summary>
    public static class AutoCleanupService
    {
        public static async Task RunAsync()
        {
            await Task.Run(async () =>
            {
                try
                {
                    var db = new DB_Manager();

                    // 5분 지났고 복구/강제삭제 안된 행 조회
                    var expiredLogs = db.GetExpiredLogs();

                    if (expiredLogs.Count == 0) return;

                    // PATIENT 맨 마지막 처리
                    // IMAGE/VIDEO 파일 먼저 삭제 후 PATIENT 폴더 삭제해야
                    // 폴더 삭제 전에 개별 파일 삭제 시도하는 충돌 방지
                    var sorted = expiredLogs
                        .OrderBy(x => x.FileType == "PATIENT" ? 1 : 0)
                        .ToList();

                    foreach (var log in sorted)
                    {
                        try
                        {
                            switch (log.FileType)
                            {
                                case "IMAGE":
                                    // Del_ 붙은 dcm 파일 삭제
                                    DeleteFileIfExists(log.ImagePath);
                                    // 연결된 isf 파일도 삭제
                                    DeleteIsfFile(log.ImagePath);
                                    // DELETE_LOG IS_FORCE_DELETED = 'Y' 업데이트
                                    db.UpdateForceDeleted(log.DeleteId);
                                    break;

                                case "NORMAL_VIDEO":
                                    // Del_ 붙은 avi 파일 삭제
                                    DeleteFileIfExists(log.AviPath);
                                    // DELETE_LOG IS_FORCE_DELETED = 'Y' 업데이트
                                    db.UpdateForceDeleted(log.DeleteId);
                                    break;

                                case "DICOM_VIDEO":
                                    // Del_ 붙은 avi + dcm 파일 삭제
                                    DeleteFileIfExists(log.AviPath);
                                    DeleteFileIfExists(log.DicomPath);
                                    // DELETE_LOG IS_FORCE_DELETED = 'Y' 업데이트
                                    db.UpdateForceDeleted(log.DeleteId);
                                    break;

                                case "PATIENT":
                                    // 트랜잭션으로 3가지 동시 처리
                                    // 1. DELETE_LOG PATIENT 행 IS_FORCE_DELETED = 'Y'
                                    // 2. PATIENT 테이블 완전 DELETE
                                    // 3. 관련 IMAGE/VIDEO 행도 IS_FORCE_DELETED = 'Y'
                                    if (!db.ForceDeletePatientWithLog(
                                        log.DeleteId,
                                        log.PatientCode,
                                        log.PatientName))
                                        break; // 실패 시 세션로그 스킵하고 다음 항목으로

                                    // 환자명_환자코드 조합으로 폴더 찾아서 완전 삭제
                                    string folderName = $"{log.PatientName}_{log.PatientCode}";
                                    string dicomPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM", folderName);
                                    string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VIDEO", folderName);

                                    if (Directory.Exists(dicomPath))
                                        Directory.Delete(dicomPath, recursive: true);
                                    if (Directory.Exists(videoPath))
                                        Directory.Delete(videoPath, recursive: true);
                                    break;
                            }

                            // 세션 로그 기록
                            // PATIENT 실패 시 break 로 여기 못옴 → 실패 로그 안남음 ✅
                            Common.WriteSessionLog(
                                $"[AUTO CLEANUP] User:{Common.CurrentUserId} " +
                                $"Patient:{log.PatientName}({log.PatientCode}) " +
                                $"Type:{log.FileType} DeleteId:{log.DeleteId}");
                        }
                        catch (Exception ex)
                        {
                            // 개별 항목 실패해도 다음 항목 계속 처리
                            // UI 팝업으로 사용자에게 에러 알림
                            await Common.WriteLog(ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 전체 실패 시 에러 알림
                    await Common.WriteLog(ex);
                }
            });
        }

        // 파일 존재 시 삭제 (없으면 무시)
        private static void DeleteFileIfExists(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            File.Delete(path);
        }

        // dcm 경로 기반으로 연결된 isf 파일 경로 계산 후 삭제
        // DICOM 폴더 구조 기준으로 ISF 폴더에서 동일한 상대경로로 찾음
        private static void DeleteIsfFile(string dcmPath)
        {
            try
            {
                if (string.IsNullOrEmpty(dcmPath)) return;

                string dicomDir = Path.Combine(Common.executablePath, "DICOM");
                string isfDir = Path.Combine(Common.executablePath, "ISF");

                string fileName = Path.GetFileNameWithoutExtension(dcmPath);
                // Del_ 붙은 이름에서 원본 파일명 추출
                string cleanName = fileName.StartsWith("Del_") ? fileName.Substring(4) : fileName;
                string studyDir = Path.GetDirectoryName(Path.GetDirectoryName(dcmPath));
                string relative = studyDir.Substring(dicomDir.Length)
                                          .TrimStart(Path.DirectorySeparatorChar);
                // ISF 경로: ISF/환자폴더/스터디폴더/Del_파일명.isf
                string isfPath = Path.Combine(isfDir, relative, "Del_" + cleanName + ".isf");
                DeleteFileIfExists(isfPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ISF 삭제 실패] {ex.Message}");
            }
        }
    }
}