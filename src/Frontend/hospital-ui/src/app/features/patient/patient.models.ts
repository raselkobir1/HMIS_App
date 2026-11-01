import { PaginationQuery } from '../../core/models/api-response.model';

export const PatientPermissions = {
  view: 'Patient.View',
  create: 'Patient.Create',
  update: 'Patient.Update',
  deactivate: 'Patient.Deactivate',
  mergeRequest: 'Patient.Merge.Request',
  mergeApprove: 'Patient.Merge.Approve',
  medicalView: 'Patient.MedicalInfo.View',
  medicalManage: 'Patient.MedicalInfo.Manage',
  documentsView: 'Patient.Documents.View',
  documentsUpload: 'Patient.Documents.Upload',
  documentsDelete: 'Patient.Documents.Delete',
} as const;

export type Gender = 'Male' | 'Female' | 'Other';
export type PatientStatus = 'Active' | 'Inactive' | 'Merged' | 'Deceased';
export type IdentificationType = 'NationalId' | 'BirthRegistration' | 'Passport' | 'Other';
export type BloodGroup = 'APositive' | 'ANegative' | 'BPositive' | 'BNegative' | 'ABPositive' | 'ABNegative' | 'OPositive' | 'ONegative';

export const GENDERS: Gender[] = ['Male', 'Female', 'Other'];
export const ID_TYPES: { value: IdentificationType; label: string }[] = [
  { value: 'NationalId', label: 'National ID (NID)' },
  { value: 'BirthRegistration', label: 'Birth registration' },
  { value: 'Passport', label: 'Passport' },
  { value: 'Other', label: 'Other' },
];
export const BLOOD_GROUPS: { value: BloodGroup; label: string }[] = [
  { value: 'APositive', label: 'A+' },
  { value: 'ANegative', label: 'A−' },
  { value: 'BPositive', label: 'B+' },
  { value: 'BNegative', label: 'B−' },
  { value: 'ABPositive', label: 'AB+' },
  { value: 'ABNegative', label: 'AB−' },
  { value: 'OPositive', label: 'O+' },
  { value: 'ONegative', label: 'O−' },
];
export const bloodGroupLabel = (value: BloodGroup | null | undefined) =>
  BLOOD_GROUPS.find((b) => b.value === value)?.label ?? '—';
export const idTypeLabel = (value: IdentificationType | null | undefined) =>
  ID_TYPES.find((t) => t.value === value)?.label ?? '—';

export interface Address {
  line: string | null;
  upazila: string | null;
  district: string | null;
  postCode: string | null;
}

export interface EmergencyContact {
  name: string;
  relation: string;
  mobileNumber: string;
}

export interface PatientDetails {
  fullName: string;
  fullNameBangla: string | null;
  gender: Gender;
  dateOfBirth: string;
  isDateOfBirthEstimated: boolean;
  bloodGroup: BloodGroup | null;
  mobileNumber: string;
  email: string | null;
  presentAddress: Address;
  permanentAddress: Address;
  identificationType: IdentificationType | null;
  identificationNumber: string | null;
  guardianName: string | null;
  guardianRelation: string | null;
  emergencyContacts: EmergencyContact[];
}

export interface Patient extends PatientDetails {
  id: string;
  mrNumber: string;
  hasPhoto: boolean;
  status: PatientStatus;
  mergedIntoPatientId: string | null;
  mergedIntoMRNumber: string | null;
  deceasedAt: string | null;
  /** Duplicate records merged into this patient; their history is shown here. */
  mergedRecords: { id: string; mrNumber: string }[];
  /** Only in the registration response, when a registration fee was billed. */
  registrationBill?: { invoiceId: string; invoiceNumber: string; amount: number } | null;
  createdAt: string;
  rowVersion: number;
}

export interface PatientListItem {
  id: string;
  mrNumber: string;
  fullName: string;
  gender: Gender;
  dateOfBirth: string;
  isDateOfBirthEstimated: boolean;
  mobileNumber: string;
  district: string | null;
  status: PatientStatus;
}

export interface PatientSearchQuery extends PaginationQuery {
  status?: PatientStatus;
}

export interface DuplicateCandidate {
  id: string;
  mrNumber: string;
  fullName: string;
  gender: Gender;
  dateOfBirth: string;
  mobileNumber: string;
  reasons: string[];
  isBlocking: boolean;
}

export type AllergyType = 'Drug' | 'Food' | 'Environmental' | 'Other';
export type AllergySeverity = 'Mild' | 'Moderate' | 'Severe';
export type AllergyStatus = 'Active' | 'Resolved' | 'EnteredInError';
export type HistoryCategory = 'PastIllness' | 'Surgical' | 'Family' | 'Social' | 'CurrentMedication';

export const ALLERGY_TYPES: AllergyType[] = ['Drug', 'Food', 'Environmental', 'Other'];
export const SEVERITIES: AllergySeverity[] = ['Mild', 'Moderate', 'Severe'];
export const HISTORY_CATEGORIES: { value: HistoryCategory; label: string }[] = [
  { value: 'PastIllness', label: 'Past illness' },
  { value: 'Surgical', label: 'Surgical' },
  { value: 'Family', label: 'Family history' },
  { value: 'Social', label: 'Social history' },
  { value: 'CurrentMedication', label: 'Current medication' },
];

export interface Allergy {
  id: string;
  allergen: string;
  type: AllergyType;
  severity: AllergySeverity;
  reaction: string | null;
  status: AllergyStatus;
  createdAt: string;
  rowVersion: number;
}

export interface MedicalHistory {
  id: string;
  category: HistoryCategory;
  description: string;
  onsetDate: string | null;
  isEnteredInError: boolean;
  createdAt: string;
  rowVersion: number;
}

export interface MedicalInfo {
  allergies: Allergy[];
  history: MedicalHistory[];
}

export type DocumentCategory = 'LabReport' | 'ImagingReport' | 'Prescription' | 'DischargeSummary' | 'Identification' | 'Consent' | 'Other';
export const DOCUMENT_CATEGORIES: { value: DocumentCategory; label: string }[] = [
  { value: 'LabReport', label: 'Lab report' },
  { value: 'ImagingReport', label: 'Imaging report' },
  { value: 'Prescription', label: 'Prescription' },
  { value: 'DischargeSummary', label: 'Discharge summary' },
  { value: 'Identification', label: 'Identification' },
  { value: 'Consent', label: 'Consent' },
  { value: 'Other', label: 'Other' },
];

export interface PatientDocument {
  id: string;
  category: DocumentCategory;
  title: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
}

export type MergeStatus = 'Pending' | 'Approved' | 'Rejected';

export interface MergeRequest {
  id: string;
  sourcePatientId: string;
  sourceMRNumber: string;
  sourceName: string;
  targetPatientId: string;
  targetMRNumber: string;
  targetName: string;
  reason: string;
  status: MergeStatus;
  requestedAt: string;
  reviewedAt: string | null;
  reviewNote: string | null;
}

/** "34 y", "8 m", "12 d" — prefixed with "~" when the date of birth was estimated from an age. */
export function ageLabel(dateOfBirth: string, estimated = false): string {
  const [y, m, d] = dateOfBirth.split('-').map(Number);
  const born = new Date(y, m - 1, d);
  const now = new Date();
  let years = now.getFullYear() - born.getFullYear();
  if (now.getMonth() < born.getMonth() || (now.getMonth() === born.getMonth() && now.getDate() < born.getDate())) {
    years--;
  }
  let label: string;
  if (years >= 1) {
    label = `${years} y`;
  } else {
    const months = (now.getFullYear() - born.getFullYear()) * 12 + now.getMonth() - born.getMonth() - (now.getDate() < born.getDate() ? 1 : 0);
    label = months >= 1 ? `${months} m` : `${Math.max(0, Math.floor((now.getTime() - born.getTime()) / 86_400_000))} d`;
  }
  return estimated ? `~${label}` : label;
}
