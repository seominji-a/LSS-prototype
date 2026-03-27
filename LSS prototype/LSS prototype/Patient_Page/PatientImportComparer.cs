using System;
using System.Collections.Generic;
using System.Linq;

namespace LSS_prototype.Patient_Page
{
    internal static class PatientImportComparer
    {
        public static string Normalizing(string value)
            => (value ?? string.Empty).Trim();

        public static PatientCompareResult ComparePatients(PatientModel exist, PatientModel import)
        {
            if (exist == null || import == null)
                return PatientCompareResult.None;

            bool sameCode = exist.PatientCode == import.PatientCode;
            bool sameBirth = exist.BirthDate.Date == import.BirthDate.Date;
            bool sameSex = string.Equals(Normalizing(exist.Sex), Normalizing(import.Sex), StringComparison.OrdinalIgnoreCase);
            bool sameName = string.Equals(Normalizing(exist.PatientName), Normalizing(import.PatientName), StringComparison.OrdinalIgnoreCase);
            bool sameAccession =
                !string.IsNullOrWhiteSpace(Normalizing(exist.AccessionNumber)) &&
                !string.IsNullOrWhiteSpace(Normalizing(import.AccessionNumber)) &&
                string.Equals(Normalizing(exist.AccessionNumber), Normalizing(import.AccessionNumber), StringComparison.OrdinalIgnoreCase);

            if (sameAccession) return PatientCompareResult.ExactMatch;
            if (sameCode && sameBirth && sameSex && sameName) return PatientCompareResult.ExactMatch;
            if (sameCode && sameBirth && sameSex) return PatientCompareResult.MergeCandidate;
            if (sameCode) return PatientCompareResult.Conflict;

            return PatientCompareResult.None;
        }

        public static bool IsMergeCandidatePatient(PatientModel exist, PatientModel import)
            => ComparePatients(exist, import) == PatientCompareResult.MergeCandidate;

        public static PatientModel FindExistingPatientForImport(
            PatientModel group,
            List<PatientModel> localPatients,
            List<PatientModel> importedEmrPatients)
        {
            if (group == null) return null;

            if (!string.IsNullOrWhiteSpace(Normalizing(group.AccessionNumber)))
            {
                var emrByAcc = importedEmrPatients.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(Normalizing(x.AccessionNumber)) &&
                    string.Equals(Normalizing(x.AccessionNumber), Normalizing(group.AccessionNumber), StringComparison.OrdinalIgnoreCase));
                if (emrByAcc != null) return emrByAcc;
            }

            var localExact = localPatients.FirstOrDefault(x =>
                ComparePatients(x, group) == PatientCompareResult.ExactMatch);
            if (localExact != null) return localExact;

            var emrExact = importedEmrPatients.FirstOrDefault(x =>
                ComparePatients(x, group) == PatientCompareResult.ExactMatch);
            if (emrExact != null) return emrExact;

            var localMerge = localPatients.FirstOrDefault(x =>
                ComparePatients(x, group) == PatientCompareResult.MergeCandidate);
            if (localMerge != null) return localMerge;

            var emrMerge = importedEmrPatients.FirstOrDefault(x =>
                ComparePatients(x, group) == PatientCompareResult.MergeCandidate);
            if (emrMerge != null) return emrMerge;

            return null;
        }

        public static PatientModel FindConflictPatientForImport(
            PatientModel group,
            List<PatientModel> localPatients,
            List<PatientModel> importedEmrPatients)
        {
            if (group == null) return null;

            var localConflict = localPatients.FirstOrDefault(x =>
                ComparePatients(x, group) == PatientCompareResult.Conflict);
            if (localConflict != null) return localConflict;

            var emrConflict = importedEmrPatients.FirstOrDefault(x =>
                ComparePatients(x, group) == PatientCompareResult.Conflict);
            if (emrConflict != null) return emrConflict;

            return null;
        }
    }
}