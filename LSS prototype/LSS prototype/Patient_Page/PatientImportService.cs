using FellowOakDicom;
using FellowOakDicom.Imaging;
using LSS_prototype.DB_CRUD;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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

        // ───────────────────────────────────────────
        // BuildImportPlans
        // ───────────────────────────────────────────
        public async Task<List<ImportPlan>> BuildImportPlans(List<PatientModel> patientGroups)
        {
            var plans = new List<ImportPlan>();

            try
            {
                foreach (var group in patientGroups)
                {
                    try
                    {
                        if (group == null) continue;

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
                                    $"[충돌] 동일 환자번호({group.PatientCode})가 이미 존재하지만 " +
                                    $"생년월일/성별이 일치하지 않습니다."
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
                                        Reason =
                                            $"[병합 후보] LOCAL과 중복 가능성 있음 → 신규 E-SYNC 생성: {group.PatientName}"
                                    });
                                    continue;
                                }

                                string[] newFiles = await FilterNewDicomFiles(
                                    existingPatient.PatientName,
                                    existingPatient.PatientCode,
                                    group.DcmFiles);

                                plans.Add(new ImportPlan
                                {
                                    Group = group,
                                    ExistingPatient = existingPatient,
                                    ActionType = newFiles.Length == 0
                                        ? ImportActionType.SkipDuplicateStudy
                                        : ImportActionType.ExistingEmrPatientAddStudy,
                                    Reason = newFiles.Length == 0
                                        ? $"[중복파일] {group.PatientName}({group.PatientCode})"
                                        : $"[병합 후보 EMR] 새 파일 추가: {group.PatientName}"
                                });
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
                    catch (Exception ex) { await Common.WriteLog(ex); }
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }

            return plans;
        }

        // ───────────────────────────────────────────
        // ExecuteImportPlan
        // ───────────────────────────────────────────
        public async Task<bool> ExecuteImportPlan(ImportPlan plan)
        {
            try
            {
                if (plan?.Group == null) return false;

                var group = plan.Group;

                switch (plan.ActionType)
                {
                    case ImportActionType.NewLocalPatient:
                        {
                            var model = new PatientModel
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

                            if (!_repo.AddPatient(model)) return false;

                            await ImportPatientFilesToStructuredFolders(
                                group.DcmFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                                group.PatientName, group.PatientCode, string.Empty);

                            return true;
                        }

                    case ImportActionType.ExistingLocalPatientAddStudy:
                        {
                            var target = plan.ExistingPatient ?? group;
                            var newFiles = await FilterNewDicomFiles(target.PatientName, target.PatientCode, group.DcmFiles);
                            if (newFiles.Length == 0) return false;

                            await ImportPatientFilesToStructuredFolders(
                                newFiles, target.PatientName, target.PatientCode, string.Empty);

                            return true;
                        }

                    case ImportActionType.NewEmrPatient:
                        {
                            var model = new PatientModel
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

                            if (!_repo.UpsertEmrPatient(model)) return false;

                            await ImportPatientFilesToStructuredFolders(
                                group.DcmFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                                group.PatientName, group.PatientCode, group.AccessionNumber);

                            return true;
                        }

                    case ImportActionType.ExistingEmrPatientAddStudy:
                        {
                            var target = plan.ExistingPatient ?? group;
                            var model = new PatientModel
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

                            if (!_repo.UpsertEmrPatient(model)) return false;

                            var newFiles = await FilterNewDicomFiles(target.PatientName, target.PatientCode, group.DcmFiles);
                            if (newFiles.Length == 0) return false;

                            await ImportPatientFilesToStructuredFolders(
                                newFiles, target.PatientName, target.PatientCode, target.AccessionNumber);

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

        // ───────────────────────────────────────────
        // MergeEditedLocalToImportedEmr
        // ───────────────────────────────────────────
        public async Task<bool> MergeEditedLocalToImportedEmr(
            PatientModel originalLocal,
            PatientModel updatedLocal,
            PatientModel importedEmr)
        {
            try
            {
                string dicomRoot = GetDicomRootPath();
                string videoRoot = GetVideoRootPath();

                string emrDicomFolder = Path.Combine(dicomRoot, $"{importedEmr.PatientName}_{importedEmr.PatientCode}");
                string emrVideoFolder = Path.Combine(videoRoot, $"{importedEmr.PatientName}_{importedEmr.PatientCode}");

                string localDicomFolder =
                    await FindPatientFolder(originalLocal) ??
                    await FindPatientFolder(updatedLocal);

                string localVideoFolder =
                    await FindPatientFolderByRoot(originalLocal, videoRoot) ??
                    await FindPatientFolderByRoot(updatedLocal, videoRoot);

                Directory.CreateDirectory(emrDicomFolder);
                Directory.CreateDirectory(emrVideoFolder);

                if (!string.IsNullOrWhiteSpace(localDicomFolder) &&
                    Directory.Exists(localDicomFolder) &&
                    !string.Equals(
                        Path.GetFullPath(localDicomFolder).TrimEnd('\\'),
                        Path.GetFullPath(emrDicomFolder).TrimEnd('\\'),
                        StringComparison.OrdinalIgnoreCase))
                {
                    CopyDirectory(localDicomFolder, emrDicomFolder, overwrite: true);
                    try { Directory.Delete(localDicomFolder, true); }
                    catch (Exception ex) { await Common.WriteLog(ex); }
                }

                await UpdateDicomTagsForMerge(
                    emrDicomFolder,
                    importedEmr.PatientName,
                    importedEmr.PatientCode,
                    importedEmr.AccessionNumber);

                await MergePatientVideoFolder(
                    localVideoFolder,
                    emrVideoFolder,
                    importedEmr.PatientName,
                    importedEmr.PatientCode);

                await SyncDicomFileNamesWithVideoDicomIndices(
                    emrDicomFolder, emrVideoFolder,
                    importedEmr.PatientName, importedEmr.PatientCode);

                await NormalizeImageDicomFileNames(
                    emrDicomFolder,
                    importedEmr.PatientName,
                    importedEmr.PatientCode);

                int localShotNum = updatedLocal?.ShotNum ?? 0;
                int emrShotNum = importedEmr?.ShotNum ?? 0;
                int mergedShotNum = localShotNum + emrShotNum;

                DateTime? localDate = updatedLocal?.LastShootDate;
                DateTime? emrDate = importedEmr?.LastShootDate;
                DateTime? mergedDate = null;

                if (localDate.HasValue && emrDate.HasValue)
                    mergedDate = localDate.Value >= emrDate.Value ? localDate.Value : emrDate.Value;
                else
                    mergedDate = localDate ?? emrDate;

                var mergedModel = new PatientModel
                {
                    PatientId = importedEmr.PatientId,
                    PatientCode = importedEmr.PatientCode,
                    PatientName = importedEmr.PatientName,
                    BirthDate = importedEmr.BirthDate,
                    Sex = importedEmr.Sex,
                    AccessionNumber = importedEmr.AccessionNumber,
                    IsEmrPatient = true,
                    Source = PatientSource.ESync,
                    SourceType = (int)PatientSourceType.ESync,
                    LastShootDate = mergedDate,
                    ShotNum = mergedShotNum
                };

                if (!_repo.UpsertEmrPatient(mergedModel)) return false;

                _repo.DeletePatient(updatedLocal.PatientId);
                return true;
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return false;
            }
        }

        // ───────────────────────────────────────────
        // 파일 중복 필터
        // ───────────────────────────────────────────
        public async Task<string[]> FilterNewDicomFiles(
            string patientName, int patientCode, IEnumerable<string> sourceFiles)
        {
            try
            {
                string patientRoot = Path.Combine(GetDicomRootPath(), $"{patientName}_{patientCode}");
                var existingKeys = await GetExistingDicomInstanceKeys(patientRoot);
                var result = new List<string>();

                foreach (var file in sourceFiles ?? Enumerable.Empty<string>())
                {
                    try
                    {
                        if (!File.Exists(file)) continue;

                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                        string key = await BuildDicomInstanceKey(dicomFile.Dataset);

                        if (string.IsNullOrWhiteSpace(key) || !existingKeys.Contains(key))
                            result.Add(file);
                    }
                    catch (Exception ex) { await Common.WriteLog(ex); }
                }

                return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                return sourceFiles?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
            }
        }

        // ───────────────────────────────────────────
        // 파일 구조화 저장
        // ───────────────────────────────────────────
        public async Task ImportPatientFilesToStructuredFolders(
            string[] sourceFiles, string patientName, int patientCode, string accessionNumber)
        {
            try
            {
                string patientFolderName = $"{patientName}_{patientCode}";
                string dicomRoot = GetDicomRootPath();
                string videoRoot = GetVideoRootPath();

                Directory.CreateDirectory(dicomRoot);
                Directory.CreateDirectory(videoRoot);

                var allFiles = sourceFiles
                    .Where(f => File.Exists(f) &&
                                Path.GetExtension(f).Equals(".dcm", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (allFiles.Count == 0)
                    throw new Exception("가져올 DICOM 파일이 없습니다.");

                var discoveredStudyIds = new List<string>();
                var studyIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var reservedStudyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int savedFileCount = 0;

                foreach (var file in allFiles)
                {
                    try
                    {
                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                        string filePatId = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.PatientID, "").Trim();

                        if (!string.IsNullOrWhiteSpace(filePatId) && filePatId != patientCode.ToString())
                            continue;

                        string studyId = await ResolveStudyIdForImport(
                            dicomFile, patientName, patientCode, studyIdMap, reservedStudyIds);

                        if (string.IsNullOrWhiteSpace(studyId))
                            studyId = DateTime.Now.ToString("yyyyMMdd") + "0001";

                        if (!discoveredStudyIds.Contains(studyId))
                            discoveredStudyIds.Add(studyId);

                        string studyDate = studyId.Substring(0, 8);

                        if (await IsMultiFrameDicom(file))
                        {
                            string dir = Path.Combine(dicomRoot, patientFolderName, studyDate, studyId, "Video");
                            CopyFileWithUniqueName(file, dir, ".dcm");
                        }
                        else
                        {
                            string dir = Path.Combine(dicomRoot, patientFolderName, studyDate, studyId, "Image");
                            CopyFileWithUniqueName(file, dir, ".dcm");
                        }

                        savedFileCount++;
                    }
                    catch (Exception ex) { await Common.WriteLog(ex); }
                }

                if (savedFileCount == 0)
                    throw new Exception("유효한 DICOM 파일 없음");

                string dicomPatientRoot = Path.Combine(dicomRoot, patientFolderName);
                string videoPatientRoot = Path.Combine(videoRoot, patientFolderName);

                if (Directory.Exists(dicomPatientRoot))
                    await UpdateDicomTagsForMerge(dicomPatientRoot, patientName, patientCode, accessionNumber);

                foreach (var studyId in discoveredStudyIds.Distinct().OrderBy(x => x))
                {
                    try
                    {
                        string studyDate = studyId.Substring(0, 8);
                        string imageDir = Path.Combine(dicomPatientRoot, studyDate, studyId, "Image");
                        string dicomVideoDir = Path.Combine(dicomPatientRoot, studyDate, studyId, "Video");
                        string videoDir = Path.Combine(videoPatientRoot, studyDate, studyId);

                        if (Directory.Exists(imageDir))
                            await RenameImageDicomFiles(imageDir, patientName, patientCode, studyId);

                        if (Directory.Exists(videoDir))
                        {
                            int dicomVideoCount = Directory.Exists(dicomVideoDir)
                                ? Directory.GetFiles(dicomVideoDir, "*.dcm").Length : 0;
                            await RenameImportedVideoFiles(videoDir, patientName, patientCode, studyId, dicomVideoCount);
                        }

                        if (Directory.Exists(dicomVideoDir))
                            await NormalizeDicomVideoPairs(dicomVideoDir, patientName, patientCode, studyId);
                    }
                    catch (Exception ex) { await Common.WriteLog(ex); }
                }
            }
            catch (Exception ex)
            {
                await Common.WriteLog(ex);
                throw;
            }
        }

        // ───────────────────────────────────────────
        // 내부 헬퍼 메서드들 (PatientViewModel에서 이동)
        // ───────────────────────────────────────────
        private async Task<string> BuildDicomInstanceKey(DicomDataset ds)
        {
            try
            {
                if (ds == null) return string.Empty;

                string sopUid = ds.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(sopUid)) return "SOP:" + sopUid;

                string studyUid = ds.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty).Trim();
                string seriesUid = ds.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, string.Empty).Trim();
                string instanceNumber = ds.GetSingleValueOrDefault(DicomTag.InstanceNumber, string.Empty).Trim();
                string numFrames = ds.GetSingleValueOrDefault(DicomTag.NumberOfFrames, 1).ToString();

                string key = $"{studyUid}|{seriesUid}|{instanceNumber}|{numFrames}".Trim('|');
                return string.IsNullOrWhiteSpace(key) ? string.Empty : "FALLBACK:" + key;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return string.Empty; }
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
                        if (!string.IsNullOrWhiteSpace(key)) result.Add(key);
                    }
                    catch (Exception ex) { await Common.WriteLog(ex); }
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
            return result;
        }

        private async Task UpdateDicomTagsForMerge(
            string rootFolder, string patientName, int patientCode, string accessionNumber)
        {
            try
            {
                if (!Directory.Exists(rootFolder)) return;

                foreach (var file in Directory.GetFiles(rootFolder, "*.dcm", SearchOption.AllDirectories))
                {
                    try
                    {
                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                        var ds = dicomFile.Dataset;

                        ds.AddOrUpdate(DicomTag.PatientName, patientName);
                        ds.AddOrUpdate(DicomTag.PatientID, patientCode.ToString());
                        ds.AddOrUpdate(DicomTag.AccessionNumber, accessionNumber);

                        string temp = file + ".tmp";
                        dicomFile.Save(temp);
                        SafeDeleteFile(file);
                        SafeMoveFile(temp, file);
                    }
                    catch (Exception ex) { await Common.WriteLog(ex); }
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task MergePatientVideoFolder(
            string sourceVideoFolder, string targetVideoFolder, string patientName, int patientCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourceVideoFolder) || !Directory.Exists(sourceVideoFolder))
                    return;

                Directory.CreateDirectory(targetVideoFolder);

                if (!string.Equals(
                        Path.GetFullPath(sourceVideoFolder).TrimEnd('\\'),
                        Path.GetFullPath(targetVideoFolder).TrimEnd('\\'),
                        StringComparison.OrdinalIgnoreCase))
                {
                    CopyDirectory(sourceVideoFolder, targetVideoFolder, overwrite: true);
                    try { Directory.Delete(sourceVideoFolder, true); }
                    catch (Exception ex) { await Common.WriteLog(ex); }
                }

                await NormalizeVideoFileNamesRecursively(targetVideoFolder, patientName, patientCode);
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task SyncDicomFileNamesWithVideoDicomIndices(
            string dicomPatientRoot, string videoPatientRoot, string patientName, int patientCode)
        {
            try
            {
                if (!Directory.Exists(dicomPatientRoot) || !Directory.Exists(videoPatientRoot)) return;

                var dicomVideoDirs = Directory.GetDirectories(dicomPatientRoot, "*", SearchOption.AllDirectories)
                    .Where(dir =>
                        string.Equals(new System.IO.DirectoryInfo(dir).Name, "Video", StringComparison.OrdinalIgnoreCase) &&
                        Directory.GetFiles(dir, "*.dcm").Any())
                    .ToList();

                foreach (var dicomDir in dicomVideoDirs)
                {
                    string studyId = await GetStudyIdFromFolder(dicomDir);
                    if (string.IsNullOrWhiteSpace(studyId)) continue;

                    string videoDir = Directory.GetDirectories(videoPatientRoot, "*", SearchOption.AllDirectories)
                        .FirstOrDefault(d => string.Equals(
                            new System.IO.DirectoryInfo(d).Name, studyId, StringComparison.OrdinalIgnoreCase));

                    if (string.IsNullOrWhiteSpace(videoDir)) continue;

                    await RenameDicomFilesByVideoIndices(dicomDir, videoDir, patientName, patientCode, studyId);
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task<string> GetStudyIdFromFolder(string folder)
        {
            try
            {
                var di = new System.IO.DirectoryInfo(folder);
                string name = di.Name;
                string parent = di.Parent?.Name ?? string.Empty;

                if (Regex.IsMatch(name, @"^\d{8,}$")) return name;
                if (Regex.IsMatch(parent, @"^\d{8,}$")) return parent;
                return string.Empty;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return string.Empty; }
        }

        private async Task RenameDicomFilesByVideoIndices(
            string dicomDir, string videoDir, string patientName, int patientCode, string studyId)
        {
            try
            {
                var dicomFiles = Directory.GetFiles(dicomDir, "*.dcm").OrderBy(f => f).ToList();
                if (dicomFiles.Count == 0) return;

                var dicomVideoFiles = Directory.GetFiles(videoDir)
                    .Where(f => Regex.IsMatch(Path.GetFileNameWithoutExtension(f), @"_Dicom$", RegexOptions.IgnoreCase))
                    .OrderBy(ExtractIndexSync).ThenBy(f => f).ToList();

                if (dicomVideoFiles.Count == 0)
                {
                    await NormalizeDicomFileNamesWithDicomSuffix(dicomDir, patientName, patientCode, studyId);
                    return;
                }

                var targetIndices = dicomVideoFiles
                    .Select(ExtractIndexSync).Where(i => i > 0).Distinct().OrderBy(i => i).ToList();

                if (targetIndices.Count == 0)
                {
                    await NormalizeDicomFileNamesWithDicomSuffix(dicomDir, patientName, patientCode, studyId);
                    return;
                }

                int pairCount = Math.Min(dicomFiles.Count, targetIndices.Count);
                var tempMappings = new List<(string TempPath, int TargetIndex)>();

                for (int i = 0; i < pairCount; i++)
                {
                    string temp = dicomFiles[i] + ".renametmp";
                    SafeMoveFile(dicomFiles[i], temp);
                    tempMappings.Add((temp, targetIndices[i]));
                }

                int nextIndex = targetIndices.Any()
                    ? (targetIndices.Max() % 2 == 0 ? targetIndices.Max() + 2 : targetIndices.Max() + 1)
                    : 2;

                for (int i = pairCount; i < dicomFiles.Count; i++)
                {
                    string temp = dicomFiles[i] + ".renametmp";
                    SafeMoveFile(dicomFiles[i], temp);
                    tempMappings.Add((temp, nextIndex));
                    nextIndex += 2;
                }

                foreach (var item in tempMappings)
                {
                    string finalName = $"{patientName}_{patientCode}_{studyId}_{item.TargetIndex}_Dicom.dcm";
                    string finalPath = Path.Combine(dicomDir, finalName);
                    if (File.Exists(finalPath)) SafeDeleteFile(finalPath);
                    SafeMoveFile(item.TempPath, finalPath);
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private int ExtractIndexSync(string filePath)
        {
            try
            {
                var match = Regex.Match(
                    Path.GetFileNameWithoutExtension(filePath),
                    @"_(\d+)_(Avi|AVI|Dicom|DICOM)$", RegexOptions.IgnoreCase);
                return match.Success && int.TryParse(match.Groups[1].Value, out int idx) ? idx : -1;
            }
            catch { return -1; }
        }

        private async Task NormalizeDicomFileNamesWithDicomSuffix(
            string folder, string patientName, int patientCode, string studyId)
        {
            try
            {
                var files = Directory.GetFiles(folder, "*.dcm").OrderBy(f => f).ToList();
                if (files.Count == 0) return;

                var temps = new List<string>();
                foreach (var file in files)
                {
                    string temp = file + ".renametmp";
                    SafeMoveFile(file, temp);
                    temps.Add(temp);
                }

                int index = 2;
                foreach (var temp in temps)
                {
                    string newName = $"{patientName}_{patientCode}_{studyId}_{index}_Dicom.dcm";
                    string newPath = Path.Combine(folder, newName);
                    while (File.Exists(newPath)) { index += 2; newName = $"{patientName}_{patientCode}_{studyId}_{index}_Dicom.dcm"; newPath = Path.Combine(folder, newName); }
                    SafeMoveFile(temp, newPath);
                    index += 2;
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task NormalizeImageDicomFileNames(
            string dicomPatientRoot, string patientName, int patientCode)
        {
            try
            {
                if (!Directory.Exists(dicomPatientRoot)) return;

                var imageDirs = Directory.GetDirectories(dicomPatientRoot, "*", SearchOption.AllDirectories)
                    .Where(dir =>
                        string.Equals(new System.IO.DirectoryInfo(dir).Name, "Image", StringComparison.OrdinalIgnoreCase) &&
                        Directory.GetFiles(dir, "*.dcm").Any())
                    .ToList();

                foreach (var imageDir in imageDirs)
                {
                    string studyId = await GetStudyIdFromFolder(imageDir);
                    if (string.IsNullOrWhiteSpace(studyId)) continue;
                    await RenameImageDicomFiles(imageDir, patientName, patientCode, studyId);
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task RenameImageDicomFiles(
            string imageDir, string patientName, int patientCode, string studyId)
        {
            try
            {
                var files = Directory.GetFiles(imageDir, "*.dcm").OrderBy(f => f).ToList();
                if (files.Count == 0) return;

                var temps = new List<string>();
                foreach (var file in files) { string t = file + ".renametmp"; SafeMoveFile(file, t); temps.Add(t); }

                int index = 1;
                foreach (var temp in temps)
                {
                    string newName = $"{patientName}_{patientCode}_{studyId}_{index}.dcm";
                    string newPath = Path.Combine(imageDir, newName);
                    while (File.Exists(newPath)) { index++; newName = $"{patientName}_{patientCode}_{studyId}_{index}.dcm"; newPath = Path.Combine(imageDir, newName); }
                    SafeMoveFile(temp, newPath);
                    index++;
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task NormalizeDicomVideoPairs(
            string dicomVideoDir, string patientName, int patientCode, string studyId)
        {
            try
            {
                if (!Directory.Exists(dicomVideoDir)) return;

                await NormalizeDicomFileNamesWithDicomSuffix(dicomVideoDir, patientName, patientCode, studyId);

                string videoDir = dicomVideoDir.Replace(GetDicomRootPath(), GetVideoRootPath());
                Directory.CreateDirectory(videoDir);

                foreach (var oldAvi in Directory.GetFiles(videoDir, "*_Dicom.avi"))
                {
                    try { SafeDeleteFile(oldAvi); } catch (Exception ex) { await Common.WriteLog(ex); }
                }

                var finalDcmFiles = Directory.GetFiles(dicomVideoDir, "*_Dicom.dcm")
                    .OrderBy(f =>
                    {
                        var m = Regex.Match(Path.GetFileNameWithoutExtension(f), @"_(\d+)_Dicom$", RegexOptions.IgnoreCase);
                        return m.Success && int.TryParse(m.Groups[1].Value, out int i) ? i : -1;
                    }).ToList();

                foreach (var dcmPath in finalDcmFiles)
                {
                    var m = Regex.Match(Path.GetFileNameWithoutExtension(dcmPath), @"_(\d+)_Dicom$", RegexOptions.IgnoreCase);
                    if (!m.Success || !int.TryParse(m.Groups[1].Value, out int dicomIndex) || dicomIndex < 0) continue;
                    await CreateAviFromFinalDicom(dcmPath, videoDir, patientName, patientCode, studyId, dicomIndex);
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task CreateAviFromFinalDicom(
    string finalDcmPath, string videoDir,
    string patientName, int patientCode, string studyId, int dicomIndex)
        {
            try
            {
                var dicomFile = DicomFile.Open(finalDcmPath, FileReadOption.ReadAll);
                int frames = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.NumberOfFrames, 1);
                if (frames <= 1) return;

                Directory.CreateDirectory(videoDir);
                string aviPath = Path.Combine(videoDir, $"{patientName}_{patientCode}_{studyId}_{dicomIndex}_Dicom.avi");

                var first = new DicomImage(dicomFile.Dataset, 0).RenderImage();
                byte[] firstPix = first.As<byte[]>();
                int width = first.Width, height = first.Height;

                using (var bgraMat = new Mat(height, width, MatType.CV_8UC4))
                using (var bgrMat = new Mat())
                using (var writer = new VideoWriter(aviPath, FourCC.MJPG, 30, new OpenCvSharp.Size(width, height)))
                {
                    if (!writer.IsOpened()) return;

                    Marshal.Copy(firstPix, 0, bgraMat.Data, firstPix.Length);
                    Cv2.CvtColor(bgraMat, bgrMat, ColorConversionCodes.BGRA2BGR);
                    writer.Write(bgrMat);

                    for (int i = 1; i < frames; i++)
                    {
                        byte[] pix = new DicomImage(dicomFile.Dataset, i).RenderImage().As<byte[]>();
                        Marshal.Copy(pix, 0, bgraMat.Data, pix.Length);
                        Cv2.CvtColor(bgraMat, bgrMat, ColorConversionCodes.BGRA2BGR);
                        writer.Write(bgrMat);
                    }

                    writer.Release();
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task RenameImportedVideoFiles(
            string videoDir, string patientName, int patientCode, string studyId, int dicomVideoCount)
        {
            try
            {
                var aviFiles = Directory.GetFiles(videoDir, "*.avi")
                    .OrderBy(File.GetLastWriteTime).ThenBy(f => f).ToList();
                if (aviFiles.Count == 0) return;

                var temps = new List<string>();
                foreach (var f in aviFiles) { string t = f + ".renametmp"; SafeMoveFile(f, t); temps.Add(t); }

                var dicomTargets = temps.Take(Math.Min(dicomVideoCount, temps.Count)).ToList();
                var aviTargets = temps.Skip(Math.Min(dicomVideoCount, temps.Count)).ToList();

                int aviIndex = 1;
                foreach (var temp in aviTargets)
                {
                    while (aviIndex % 2 == 0) aviIndex++;
                    string newName = $"{patientName}_{patientCode}_{studyId}_{aviIndex}_Avi.avi";
                    string newPath = Path.Combine(videoDir, newName);
                    while (File.Exists(newPath)) { aviIndex += 2; newName = $"{patientName}_{patientCode}_{studyId}_{aviIndex}_Avi.avi"; newPath = Path.Combine(videoDir, newName); }
                    SafeMoveFile(temp, newPath);
                    aviIndex += 2;
                }

                int dicomIndex = 2;
                foreach (var temp in dicomTargets)
                {
                    while (dicomIndex % 2 != 0) dicomIndex++;
                    string newName = $"{patientName}_{patientCode}_{studyId}_{dicomIndex}_Dicom.avi";
                    string newPath = Path.Combine(videoDir, newName);
                    while (File.Exists(newPath)) { dicomIndex += 2; newName = $"{patientName}_{patientCode}_{studyId}_{dicomIndex}_Dicom.avi"; newPath = Path.Combine(videoDir, newName); }
                    SafeMoveFile(temp, newPath);
                    dicomIndex += 2;
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task NormalizeVideoFileNamesRecursively(
            string rootFolder, string patientName, int patientCode)
        {
            try
            {
                if (!Directory.Exists(rootFolder)) return;

                var videoExts = new[] { ".avi", ".mp4", ".mov", ".wmv", ".mpeg", ".mpg" };

                foreach (var dir in Directory.GetDirectories(rootFolder, "*", SearchOption.AllDirectories).Prepend(rootFolder))
                {
                    if (Directory.GetFiles(dir).Any(f => videoExts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
                        await NormalizeVideoFileNames(dir, patientName, patientCode);
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private async Task NormalizeVideoFileNames(
            string folder, string patientName, int patientCode)
        {
            try
            {
                var videoExts = new[] { ".avi", ".mp4", ".mov", ".wmv", ".mpeg", ".mpg" };
                var files = Directory.GetFiles(folder)
                    .Where(f => videoExts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .OrderBy(f => f).ToList();
                if (files.Count == 0) return;

                var di = new System.IO.DirectoryInfo(folder);
                string folderName = di.Name;
                string parentName = di.Parent?.Name ?? folderName;

                string studyId = Regex.IsMatch(folderName, @"^\d{8,}$") ? folderName : parentName;
                int index = 1;

                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file);
                    string typeSuffix = ExtractVideoTypeSuffix(file);
                    string newName = $"{patientName}_{patientCode}_{studyId}_{index}{typeSuffix}{ext}";
                    string newPath = Path.Combine(folder, newName);

                    if (string.Equals(file, newPath, StringComparison.OrdinalIgnoreCase)) { index++; continue; }
                    while (File.Exists(newPath)) { index++; newName = $"{patientName}_{patientCode}_{studyId}_{index}{ext}"; newPath = Path.Combine(folder, newName); }
                    SafeMoveFile(file, newPath);
                    index++;
                }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
        }

        private string ExtractVideoTypeSuffix(string filePath)
        {
            try
            {
                var match = Regex.Match(
                    Path.GetFileNameWithoutExtension(filePath),
                    @"_(Avi|AVI|Dicom|DICOM)$", RegexOptions.IgnoreCase);
                if (!match.Success) return string.Empty;
                string v = match.Groups[1].Value;
                if (v.Equals("avi", StringComparison.OrdinalIgnoreCase)) return "_Avi";
                if (v.Equals("dicom", StringComparison.OrdinalIgnoreCase)) return "_Dicom";
                return "_" + v;
            }
            catch { return string.Empty; }
        }

        private async Task<string> ResolveStudyIdForImport(
            DicomFile dicomFile, string patientName, int patientCode,
            Dictionary<string, string> studyIdMap, HashSet<string> reservedStudyIds)
        {
            try
            {
                if (dicomFile == null) return DateTime.Now.ToString("yyyyMMdd") + "0001";

                var ds = dicomFile.Dataset;
                string originalKey = await BuildOriginalStudyKey(ds, patientName, patientCode);

                if (studyIdMap.TryGetValue(originalKey, out string cached)) return cached;

                string rawStudyId = ds.GetSingleValueOrDefault(DicomTag.StudyID, "").Trim();
                if (!string.IsNullOrWhiteSpace(rawStudyId) && Regex.IsMatch(rawStudyId, @"^\d{12}$"))
                {
                    studyIdMap[originalKey] = rawStudyId;
                    reservedStudyIds.Add(rawStudyId);
                    return rawStudyId;
                }

                string studyDate = await GetStudyDateFromDataset(ds);
                var usedStudyIds = await GetExistingStudyIds(patientName, patientCode);
                foreach (var r in reservedStudyIds) usedStudyIds.Add(r);

                string newStudyId = await GenerateNextStudyId(studyDate, usedStudyIds);
                studyIdMap[originalKey] = newStudyId;
                reservedStudyIds.Add(newStudyId);
                return newStudyId;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return DateTime.Now.ToString("yyyyMMdd") + "0001"; }
        }

        private async Task<string> BuildOriginalStudyKey(DicomDataset ds, string patientName, int patientCode)
        {
            try
            {
                string studyId = ds.GetSingleValueOrDefault(DicomTag.StudyID, "").Trim();
                if (!string.IsNullOrWhiteSpace(studyId)) return $"{patientName}|{patientCode}|SID|{studyId}";

                string studyDate = ds.GetSingleValueOrDefault(DicomTag.StudyDate, "").Trim();
                if (!string.IsNullOrWhiteSpace(studyDate) && Regex.IsMatch(studyDate, @"^\d{8}$"))
                    return $"{patientName}|{patientCode}|SDATE|{studyDate}";

                return $"{patientName}|{patientCode}|SDATE|{DateTime.Now:yyyyMMdd}";
            }
            catch (Exception ex) { await Common.WriteLog(ex); return $"{patientName}|{patientCode}|SDATE|{DateTime.Now:yyyyMMdd}"; }
        }

        private async Task<string> GetStudyDateFromDataset(DicomDataset ds)
        {
            try
            {
                string studyDate = ds.GetSingleValueOrDefault(DicomTag.StudyDate, "").Trim();
                return !string.IsNullOrWhiteSpace(studyDate) && Regex.IsMatch(studyDate, @"^\d{8}$")
                    ? studyDate : DateTime.Now.ToString("yyyyMMdd");
            }
            catch (Exception ex) { await Common.WriteLog(ex); return DateTime.Now.ToString("yyyyMMdd"); }
        }

        private async Task<HashSet<string>> GetExistingStudyIds(string patientName, int patientCode)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string patientFolder = Path.Combine(GetDicomRootPath(), $"{patientName}_{patientCode}");
                if (!Directory.Exists(patientFolder)) return result;

                foreach (var dateDir in Directory.GetDirectories(patientFolder))
                    foreach (var studyDir in Directory.GetDirectories(dateDir))
                    {
                        string id = Path.GetFileName(studyDir);
                        if (Regex.IsMatch(id, @"^\d{12}$")) result.Add(id);
                    }
            }
            catch (Exception ex) { await Common.WriteLog(ex); }
            return result;
        }

        private async Task<string> GenerateNextStudyId(string studyDate, HashSet<string> usedStudyIds)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(studyDate) || !Regex.IsMatch(studyDate, @"^\d{8}$"))
                    studyDate = DateTime.Now.ToString("yyyyMMdd");

                int seq = 1;
                while (true) { string candidate = studyDate + seq.ToString("D4"); if (!usedStudyIds.Contains(candidate)) return candidate; seq++; }
            }
            catch (Exception ex) { await Common.WriteLog(ex); return DateTime.Now.ToString("yyyyMMdd") + "0001"; }
        }

        private async Task<bool> IsMultiFrameDicom(string filePath)
        {
            try
            {
                var dicomFile = DicomFile.Open(filePath, FileReadOption.ReadAll);
                return dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.NumberOfFrames, 1) > 1;
            }
            catch (Exception ex) { await Common.WriteLog(ex); return false; }
        }

        private async Task<string> FindPatientFolder(PatientModel patient)
        {
            try
            {
                string dicomRoot = GetDicomRootPath();
                if (!Directory.Exists(dicomRoot)) return null;
                string expected = $"{patient.PatientName}_{patient.PatientCode}";
                string direct = Path.Combine(dicomRoot, expected);
                if (Directory.Exists(direct)) return direct;
                return Directory.GetDirectories(dicomRoot)
                    .FirstOrDefault(x => string.Equals(Path.GetFileName(x), expected, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex) { await Common.WriteLog(ex); return null; }
        }

        private async Task<string> FindPatientFolderByRoot(PatientModel patient, string rootPath)
        {
            try
            {
                if (!Directory.Exists(rootPath)) return null;
                string expected = $"{patient.PatientName}_{patient.PatientCode}";
                string direct = Path.Combine(rootPath, expected);
                if (Directory.Exists(direct)) return direct;
                return Directory.GetDirectories(rootPath)
                    .FirstOrDefault(x => string.Equals(Path.GetFileName(x), expected, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex) { await Common.WriteLog(ex); return null; }
        }

        private string GetDicomRootPath() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DICOM");
        private string GetVideoRootPath() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VIDEO");

        private void CopyFileWithUniqueName(string sourcePath, string targetDir, string extension)
        {
            Directory.CreateDirectory(targetDir);
            string destPath = Path.Combine(targetDir, $"__import__{Guid.NewGuid():N}{extension}");
            File.Copy(sourcePath, destPath, true);
        }

        private void CopyDirectory(string sourceDir, string targetDir, bool overwrite)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite);
            foreach (string dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)), overwrite);
        }

        private void SafeMoveFile(string sourcePath, string destPath)
        {
            Exception lastEx = null;
            for (int i = 0; i < 10; i++)
            {
                try { if (File.Exists(destPath)) File.Delete(destPath); File.Move(sourcePath, destPath); return; }
                catch (Exception ex) { lastEx = ex; Thread.Sleep(100); }
            }
            if (lastEx != null) throw lastEx;
        }

        private void SafeDeleteFile(string filePath)
        {
            Exception lastEx = null;
            for (int i = 0; i < 10; i++)
            {
                try { if (File.Exists(filePath)) File.Delete(filePath); return; }
                catch (Exception ex) { lastEx = ex; Thread.Sleep(100); }
            }
            if (lastEx != null) throw lastEx;
        }
    }
}