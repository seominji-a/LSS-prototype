using FellowOakDicom;
using LSS_prototype.DB_CRUD;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace LSS_prototype.Patient_Page
{
    internal class PatientImportService
    {
        private readonly DB_Manager _repo;
        private readonly List<PatientModel> _localPatients;
        private readonly List<PatientModel> _importedEmrPatients;

        public PatientImportService(
            DB_Manager repo,
            List<PatientModel> localPatients,
            List<PatientModel> importedEmrPatients)
        {
            _repo = repo;
            _localPatients = localPatients ?? new List<PatientModel>();
            _importedEmrPatients = importedEmrPatients ?? new List<PatientModel>();
        }

        public Task<List<ImportPlan>> BuildImportPlans(List<PatientModel> patientGroups)
        {
            return Task.Run(async () =>
            {
                var plans = new List<ImportPlan>();

                try
                {
                    foreach (var group in patientGroups)
                    {
                        try
                        {
                            if (group == null)
                                continue;

                            var conflictPatient = PatientImportComparer.FindConflictPatientForImport(
                                group, _localPatients, _importedEmrPatients);

                            if (conflictPatient != null)
                            {
                                plans.Add(new ImportPlan
                                {
                                    Group = group,
                                    ExistingPatient = conflictPatient,
                                    ActionType = ImportActionType.SkipConflictPatient,
                                    Reason =
                                        $"[충돌] 동일 환자번호({group.PatientCode})가 이미 존재하지만 생년월일/성별이 일치하지 않습니다."
                                });
                                continue;
                            }

                            var existingPatient = PatientImportComparer.FindExistingPatientForImport(
                                group, _localPatients, _importedEmrPatients);

                            if (existingPatient != null)
                            {
                                var compareResult = PatientImportComparer.ComparePatients(existingPatient, group);

                                bool existingIsEmr =
                                    existingPatient.Source == PatientSource.ESync ||
                                    !string.IsNullOrWhiteSpace(existingPatient.AccessionNumber) ||
                                    existingPatient.IsEmrPatient ||
                                    existingPatient.SourceType == (int)PatientSourceType.ESync;

                                if (compareResult == PatientCompareResult.ExactMatch)
                                {
                                    string[] newFiles = await FilterNewDicomFiles(
                                        existingPatient.PatientName,
                                        existingPatient.PatientCode,
                                        group.DcmFiles);

                                    if (newFiles.Length == 0)
                                    {
                                        plans.Add(new ImportPlan
                                        {
                                            Group = group,
                                            ExistingPatient = existingPatient,
                                            ActionType = ImportActionType.SkipDuplicateStudy,
                                            Reason = $"[중복파일] {group.PatientName}({group.PatientCode})"
                                        });
                                    }
                                    else
                                    {
                                        plans.Add(new ImportPlan
                                        {
                                            Group = group,
                                            ExistingPatient = existingPatient,
                                            ActionType = existingIsEmr
                                                ? ImportActionType.ExistingEmrPatientAddStudy
                                                : ImportActionType.ExistingLocalPatientAddStudy,
                                            Reason = existingIsEmr
                                                ? $"[기존 EMR 동일 환자] 새 파일 추가: {group.PatientName}"
                                                : $"[기존 LOCAL 동일 환자] 새 파일 추가: {group.PatientName}"
                                        });
                                    }

                                    continue;
                                }

                                if (compareResult == PatientCompareResult.MergeCandidate)
                                {
                                    if (!existingIsEmr)
                                    {
                                        plans.Add(new ImportPlan
                                        {
                                            Group = group,
                                            ExistingPatient = existingPatient,
                                            ActionType = ImportActionType.NewEmrPatient,
                                            Reason = $"[병합 후보] LOCAL과 중복 가능성 있음 → 신규 E-SYNC 생성: {group.PatientName}"
                                        });

                                        continue;
                                    }

                                    string[] newFiles = await FilterNewDicomFiles(
                                        existingPatient.PatientName,
                                        existingPatient.PatientCode,
                                        group.DcmFiles);

                                    if (newFiles.Length == 0)
                                    {
                                        plans.Add(new ImportPlan
                                        {
                                            Group = group,
                                            ExistingPatient = existingPatient,
                                            ActionType = ImportActionType.SkipDuplicateStudy,
                                            Reason = $"[중복파일] {group.PatientName}({group.PatientCode})"
                                        });
                                    }
                                    else
                                    {
                                        plans.Add(new ImportPlan
                                        {
                                            Group = group,
                                            ExistingPatient = existingPatient,
                                            ActionType = ImportActionType.ExistingEmrPatientAddStudy,
                                            Reason = $"[병합 후보 EMR] 새 파일 추가: {group.PatientName}"
                                        });
                                    }

                                    continue;
                                }
                            }

                            bool isLocal =
                                string.IsNullOrWhiteSpace(group.AccessionNumber) &&
                                !group.IsEmrPatient &&
                                group.Source != PatientSource.ESync &&
                                group.SourceType != (int)PatientSourceType.ESync;

                            plans.Add(new ImportPlan
                            {
                                Group = group,
                                ActionType = isLocal
                                    ? ImportActionType.NewLocalPatient
                                    : ImportActionType.NewEmrPatient,
                                Reason = isLocal
                                    ? $"[신규 LOCAL 환자] {group.PatientName}"
                                    : $"[신규 EMR 환자] {group.PatientName}"
                            });
                        }
                        catch (Exception ex)
                        {
                            await Common.WriteLog(ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await Common.WriteLog(ex);
                }

                return plans;
            });
        }

        public async Task<bool> ExecuteImportPlan(ImportPlan plan)
        {
            try
            {
                if (plan == null || plan.Group == null)
                    return false;

                var group = plan.Group;

                switch (plan.ActionType)
                {
                    case ImportActionType.NewLocalPatient:
                        {
                            var patientModel = new PatientModel
                            {
                                PatientCode = group.PatientCode,
                                PatientName = group.PatientName,
                                Sex = group.Sex,
                                BirthDate = group.BirthDate,
                                AccessionNumber = string.Empty,
                                Source = PatientSource.Local,
                                IsEmrPatient = false,
                                SourceType = (int)PatientSourceType.Local,
                                LastShootDate = group.LastShootDate,
                                ShotNum = group.ShotNum
                            };

                            if (!_repo.AddPatient(patientModel))
                                return false;

                            await ImportPatientFilesToStructuredFolders(
                                group.DcmFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                                group.PatientName,
                                group.PatientCode,
                                string.Empty);

                            return true;
                        }

                    case ImportActionType.ExistingLocalPatientAddStudy:
                        {
                            var target = plan.ExistingPatient ?? group;

                            var newFiles = await FilterNewDicomFiles(
                                target.PatientName,
                                target.PatientCode,
                                group.DcmFiles);

                            if (newFiles.Length == 0)
                                return false;

                            await ImportPatientFilesToStructuredFolders(
                                newFiles,
                                target.PatientName,
                                target.PatientCode,
                                string.Empty);

                            return true;
                        }

                    case ImportActionType.NewEmrPatient:
                        {
                            var emrPatientModel = new PatientModel
                            {
                                PatientCode = group.PatientCode,
                                PatientName = group.PatientName,
                                Sex = group.Sex,
                                BirthDate = group.BirthDate,
                                AccessionNumber = group.AccessionNumber,
                                IsEmrPatient = true,
                                Source = PatientSource.ESync,
                                SourceType = (int)PatientSourceType.ESync,
                                LastShootDate = group.LastShootDate,
                                ShotNum = group.ShotNum
                            };

                            if (!_repo.UpsertEmrPatient(emrPatientModel))
                                return false;

                            await ImportPatientFilesToStructuredFolders(
                                group.DcmFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                                group.PatientName,
                                group.PatientCode,
                                group.AccessionNumber);

                            return true;
                        }

                    case ImportActionType.ExistingEmrPatientAddStudy:
                        {
                            var target = plan.ExistingPatient ?? group;

                            var emrPatientModel = new PatientModel
                            {
                                PatientCode = target.PatientCode,
                                PatientName = target.PatientName,
                                Sex = target.Sex,
                                BirthDate = target.BirthDate,
                                AccessionNumber = target.AccessionNumber,
                                IsEmrPatient = true,
                                Source = PatientSource.ESync,
                                SourceType = (int)PatientSourceType.ESync,
                                LastShootDate = target.LastShootDate,
                                ShotNum = target.ShotNum
                            };

                            if (!_repo.UpsertEmrPatient(emrPatientModel))
                                return false;

                            var newFiles = await FilterNewDicomFiles(
                                target.PatientName,
                                target.PatientCode,
                                group.DcmFiles);

                            if (newFiles.Length == 0)
                                return false;

                            await ImportPatientFilesToStructuredFolders(
                                newFiles,
                                target.PatientName,
                                target.PatientCode,
                                target.AccessionNumber);

                            return true;
                        }

                    case ImportActionType.SkipDuplicateStudy:
                    case ImportActionType.SkipConflictPatient:
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return false;
            }
        }

        public async Task<bool> MergeEditedLocalToImportedEmr(
            PatientModel originalLocal,
            PatientModel updatedLocal,
            PatientModel importedEmr)
        {
            // 👉 기존 PatientViewModel의 MergeEditedLocalToImportedEmr 그대로 이동
            throw new NotImplementedException();
        }

        private async Task<string[]> FilterNewDicomFiles(string patientName, int patientCode, IEnumerable<string> sourceFiles)
        {
            try
            {
                string dicomRoot = GetDicomRootPath();
                string patientRoot = Path.Combine(dicomRoot, $"{patientName}_{patientCode}");

                var existingKeys = await GetExistingDicomInstanceKeys(patientRoot);
                var result = new List<string>();

                foreach (var file in sourceFiles ?? Enumerable.Empty<string>())
                {
                    try
                    {
                        if (!File.Exists(file))
                            continue;

                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                        string key = await BuildDicomInstanceKey(dicomFile.Dataset);

                        if (string.IsNullOrWhiteSpace(key) || !existingKeys.Contains(key))
                            result.Add(file);
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }

                return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return sourceFiles?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
            }
        }

        private async Task<string> BuildDicomInstanceKey(DicomDataset ds)
        {
            try
            {
                if (ds == null)
                    return string.Empty;

                string sopUid = ds.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(sopUid))
                    return "SOP:" + sopUid;

                string studyUid = ds.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty).Trim();
                string seriesUid = ds.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, string.Empty).Trim();
                string instanceNumber = ds.GetSingleValueOrDefault(DicomTag.InstanceNumber, string.Empty).Trim();
                string numberOfFrames = ds.GetSingleValueOrDefault(DicomTag.NumberOfFrames, 1).ToString();

                string key = $"{studyUid}|{seriesUid}|{instanceNumber}|{numberOfFrames}".Trim('|');

                return string.IsNullOrWhiteSpace(key) ? string.Empty : "FALLBACK:" + key;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return string.Empty;
            }
        }

        private async Task<HashSet<string>> GetExistingDicomInstanceKeys(string patientRootFolder)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (string.IsNullOrWhiteSpace(patientRootFolder) || !Directory.Exists(patientRootFolder))
                    return result;

                foreach (var file in Directory.GetFiles(patientRootFolder, "*.dcm", SearchOption.AllDirectories))
                {
                    try
                    {
                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                        string key = await BuildDicomInstanceKey(dicomFile.Dataset);

                        if (!string.IsNullOrWhiteSpace(key))
                            result.Add(key);
                    }
                    catch (Exception ex)
                    {
                        await Common.WriteLog(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
            }

            return result;
        }

        private void CopyDirectory(string sourceDir, string targetDir, bool overwrite)
        {
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName);
                File.Copy(file, destFile, overwrite);
            }

            foreach (string directory in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(directory);
                string destDir = Path.Combine(targetDir, dirName);
                CopyDirectory(directory, destDir, overwrite);
            }
        }

        private string GetDicomRootPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM");
        }

        private string GetVideoRootPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VIDEO");
        }

        private async Task ImportPatientFilesToStructuredFolders(
            string[] files,
            string patientName,
            int patientCode,
            string accessionNumber)
        {
            // 👉 기존 메서드 그대로 이동
            throw new NotImplementedException();
        }

        private async Task UpdateDicomTagsForMerge(string rootFolder, string patientName, int patientCode, string accessionNumber)
        {
            // 👉 기존 메서드 그대로 이동
            throw new NotImplementedException();
        }

        private async Task MergePatientVideoFolder(string sourceVideoFolder, string targetVideoFolder, string patientName, int patientCode)
        {
            // 👉 기존 메서드 그대로 이동
            throw new NotImplementedException();
        }

        private async Task NormalizeDicomFileNames(string folder, string patientName, int patientCode)
        {
            // 👉 기존 메서드 그대로 이동
            throw new NotImplementedException();
        }

        private async Task NormalizeDicomFileNamesRecursively(string rootFolder, string patientName, int patientCode)
        {
            // 👉 기존 메서드 그대로 이동
            throw new NotImplementedException();
        }

        private void SafeMoveFile(string sourcePath, string destPath)
        {
            Exception lastEx = null;

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (File.Exists(destPath))
                        File.Delete(destPath);

                    File.Move(sourcePath, destPath);
                    return;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    Thread.Sleep(100);
                }
            }

            if (lastEx != null)
                throw lastEx;
        }

        private void SafeDeleteFile(string filePath)
        {
            Exception lastEx = null;

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);

                    return;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    Thread.Sleep(100);
                }
            }

            if (lastEx != null)
                throw lastEx;
        }
    }
}
