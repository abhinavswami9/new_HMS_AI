export type Patient = {
  id: string;
  uhid: string;
  firstName: string;
  lastName: string;
  fullName: string;
  dateOfBirth: string;
  sex: string;
  phone: string;
  email: string;
  bloodGroup: string;
  address: string;
  emergencyContact: string;
  createdAt: string;
  active: boolean;
  version: number;
};
export type Doctor = {
  id: string;
  name: string;
  department: string;
  initials: string;
  color: string;
};
export type Appointment = {
  id: string;
  patientId: string;
  doctorId: string;
  startsAt: string;
  durationMinutes: number;
  mode: string;
  reason: string;
  status: string;
  token: number | null;
  createdAt: string;
  version: number;
};
export type Vitals = {
  temperatureC: number | null;
  pulse: number | null;
  systolic: number | null;
  diastolic: number | null;
  weightKg: number | null;
  spo2: number | null;
};
export type Allergy = { substance: string; reaction: string; severity: string };
export type PrescriptionItem = {
  medicineId: string;
  name: string;
  dose: string;
  frequency: string;
  durationDays: number;
  instructions: string;
};
export type ClinicalOrder = {
  kind: string;
  name: string;
  instructions: string;
};
export type Encounter = {
  id: string;
  patientId: string;
  appointmentId: string | null;
  doctorId: string;
  status: string;
  chiefComplaint: string;
  examination: string;
  diagnosis: string;
  plan: string;
  followUp: string;
  vitals: Vitals;
  allergies: Allergy[];
  prescription: PrescriptionItem[];
  orders: ClinicalOrder[];
  allergyReviewed: boolean;
  updatedAt: string;
  signedBy: string | null;
  signedAt: string | null;
  contentHash: string | null;
  version: number;
};
export type InvoiceLine = {
  serviceId: string;
  name: string;
  quantity: number;
  unitPrice: number;
};
export type Payment = {
  id: string;
  amount: number;
  method: string;
  reference: string;
  receivedAt: string;
  receivedBy: string;
};
export type Invoice = {
  id: string;
  number: string;
  patientId: string;
  encounterId: string | null;
  status: string;
  lines: InvoiceLine[];
  discount: number;
  taxRate: number;
  payments: Payment[];
  subtotal: number;
  tax: number;
  total: number;
  paid: number;
  balance: number;
  createdAt: string;
  issuedAt: string | null;
  version: number;
};
export type Service = {
  id: string;
  name: string;
  department: string;
  price: number;
};
export type Medicine = {
  id: string;
  genericName: string;
  brandName: string;
  strength: string;
  form: string;
};
export type Module = {
  key: string;
  name: string;
  enabled: boolean;
  phase: number;
  description: string;
};
export type Audit = {
  id: string;
  actorId: string;
  actorName: string;
  action: string;
  entity: string;
  entityId: string;
  reason: string;
  occurredAt: string;
  changes: string | null;
};
export type Workspace = {
  patients: Patient[];
  appointments: Appointment[];
  encounters: Encounter[];
  invoices: Invoice[];
  doctors: Doctor[];
  services: Service[];
  medicines: Medicine[];
  modules: Module[];
  audit: Audit[];
  hospitalName: string;
  branchName: string;
};
export type Principal = {
  id: string;
  name: string;
  role: string;
  hospitalId: string;
  branchId: string;
  permissions: string[];
};
export type Chart = {
  patient: Patient;
  encounters: Encounter[];
  appointments: Appointment[];
  invoices: Invoice[];
  historyTruncated: boolean;
};
