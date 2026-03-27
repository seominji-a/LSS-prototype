using System;
using System.Collections.Generic;
using System.Linq;

namespace LSS_prototype.Patient_Page
{
    internal static class PatientImportComparer
    {
        public static string N(string value)
            => (value ?? string.Empty).Trim();

        public static PatientCompareResult ComparePatients(PatientModel a, PatientModel b)
        {
            if (a == null || b == null)
                return PatientCompareResult.None;

            bool sameCode = a.PatientCode == b.PatientCode;
            bool sameBirth = a.BirthDate.Date == b.BirthDate.Date;
            bool sameSex = string.Equals(N(a.Sex), N(b.Sex), StringComparison.OrdinalIgnoreCase);
            bool sameName = string.Equals(N(a.PatientName), N(b.PatientName), StringComparison.OrdinalIgnoreCase);
            bool sameAccession =
                !string.IsNullOrWhiteSpace(N(a.AccessionNumber)) &&
                !string.IsNullOrWhiteSpace(N(b.AccessionNumber)) &&
                string.Equals(N(a.AccessionNumber), N(b.AccessionNumber), StringComparison.OrdinalIgnoreCase);

            if (sameAccession) return PatientCompareResult.ExactMatch;
            if (sameCode && sameBirth && sameSex && sameName) return PatientCompareResult.ExactMatch;
            if (sameCode && sameBirth && sameSex) return PatientCompareResult.MergeCandidate;
            if (sameCode) return PatientCompareResult.Conflict;

            return PatientCompareResult.None;
        }

        public static bool IsMergeCandidatePatient(PatientModel existing, PatientModel incoming)
            => ComparePatients(existing, incoming) == PatientCompareResult.MergeCandidate;

        public static PatientModel FindExistingPatientForImport(
            PatientModel group,
            List<PatientModel> localPatients,
            List<PatientModel> importedEmrPatients)
        {
            if (group == null) return null;

            if (!string.IsNullOrWhiteSpace(N(group.AccessionNumber)))
            {
                var emrByAcc = importedEmrPatients.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(N(x.AccessionNumber)) &&
                    string.Equals(N(x.AccessionNumber), N(group.AccessionNumber), StringComparison.OrdinalIgnoreCase));
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