using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSS_prototype.Patient_Page
{
    internal enum PatientCompareResult
    {
        None = 0,
        ExactMatch = 1,
        MergeCandidate = 2,
        Conflict = 3
    }

    internal enum ImportActionType
    {
        NewLocalPatient,
        ExistingLocalPatientAddStudy,
        NewEmrPatient,
        ExistingEmrPatientAddStudy,
        SkipDuplicateStudy,
        SkipConflictPatient
    }

    internal class ImportPlan
    {
        public PatientModel Group { get; set; }
        public ImportActionType ActionType { get; set; }
        public string Reason { get; set; }
        public PatientModel ExistingPatient { get; set; }
    }
}
