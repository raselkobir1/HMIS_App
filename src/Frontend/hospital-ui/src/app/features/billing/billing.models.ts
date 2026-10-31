import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResponse, PaginationQuery } from '../../core/models/api-response.model';

export const BillingPermissions = {
  view: 'Billing.View',
  servicesManage: 'Billing.Services.Manage',
  invoiceManage: 'Billing.Invoice.Manage',
  invoiceCancel: 'Billing.Invoice.Cancel',
  paymentCollect: 'Billing.Payment.Collect',
  discountApply: 'Billing.Discount.Apply',
  discountApprove: 'Billing.Discount.Approve',
  refundRequest: 'Billing.Refund.Request',
  refundApprove: 'Billing.Refund.Approve',
  reportsView: 'Billing.Reports.View',
} as const;

/** Total discount above this share of the subtotal needs approval (mirrors BillingPolicy on the server). */
export const DISCOUNT_APPROVAL_PERCENT = 10;

export type ServiceCategory =
  | 'Registration'
  | 'Consultation'
  | 'Procedure'
  | 'Laboratory'
  | 'Radiology'
  | 'Bed'
  | 'Nursing'
  | 'Pharmacy'
  | 'Other';
export type InvoiceStatus = 'Draft' | 'Finalized' | 'Cancelled';
export type PaymentStatus = 'Unpaid' | 'PartiallyPaid' | 'Paid';
export type DiscountType = 'Amount' | 'Percent';
export type ApprovalStatus = 'Pending' | 'Approved' | 'Rejected';
export type PaymentMethod = 'Cash' | 'Card' | 'Bkash' | 'Nagad' | 'Rocket' | 'BankTransfer' | 'Advance';
export type RefundType = 'AdvanceReturn' | 'InvoiceCancellation';
export type RefundStatus = 'Requested' | 'Approved' | 'Rejected' | 'Paid';

export const SERVICE_CATEGORIES: ServiceCategory[] = [
  'Registration',
  'Consultation',
  'Procedure',
  'Laboratory',
  'Radiology',
  'Bed',
  'Nursing',
  'Pharmacy',
  'Other',
];

export const METHOD_LABELS: Record<PaymentMethod, string> = {
  Cash: 'Cash',
  Card: 'Card',
  Bkash: 'bKash',
  Nagad: 'Nagad',
  Rocket: 'Rocket',
  BankTransfer: 'Bank transfer',
  Advance: 'From advance',
};

/** Methods that move money (everything except applying the patient's advance). */
export const MONEY_METHODS: PaymentMethod[] = ['Cash', 'Card', 'Bkash', 'Nagad', 'Rocket', 'BankTransfer'];

export const needsReference = (method: PaymentMethod | null | undefined): boolean => !!method && method !== 'Cash' && method !== 'Advance';

export const PAYMENT_STATUS_LABELS: Record<PaymentStatus, string> = { Unpaid: 'Unpaid', PartiallyPaid: 'Partly paid', Paid: 'Paid' };
export const REFUND_STATUS_LABELS: Record<RefundStatus, string> = {
  Requested: 'Awaiting approval',
  Approved: 'Approved, to pay out',
  Rejected: 'Rejected',
  Paid: 'Paid out',
};
export const REFUND_TYPE_LABELS: Record<RefundType, string> = { AdvanceReturn: 'Advance return', InvoiceCancellation: 'Cancel & refund' };
export const SOURCE_LABELS: Record<string, string> = { OpdAppointment: 'OPD consultation' };

/** Badge class and label for an invoice in lists. */
export function invoiceBadge(i: { status: InvoiceStatus; paymentStatus: PaymentStatus; cancellationRequested: boolean }): {
  label: string;
  css: string;
} {
  if (i.status === 'Cancelled') return { label: 'Cancelled', css: 'badge muted' };
  if (i.status === 'Draft') return { label: 'Draft', css: 'badge info' };
  if (i.cancellationRequested) return { label: 'Refund pending', css: 'badge warn' };
  return i.paymentStatus === 'Paid'
    ? { label: 'Paid', css: 'badge ok' }
    : { label: PAYMENT_STATUS_LABELS[i.paymentStatus], css: 'badge warn' };
}

export interface BillableService {
  id: string;
  code: string;
  name: string;
  category: ServiceCategory;
  price: number;
  isPriceEditable: boolean;
  isActive: boolean;
  rowVersion: number;
}

export interface ServiceSave {
  code?: string;
  name: string;
  category: ServiceCategory;
  price: number;
  isPriceEditable: boolean;
  rowVersion?: number;
}

export interface ServiceQuery extends PaginationQuery {
  category?: ServiceCategory;
  isActive?: boolean;
}

export interface PatientBrief {
  id: string;
  mrNumber: string;
  fullName: string;
  gender: string;
  dateOfBirth: string | null;
  mobileNumber: string;
  status: string;
}

export interface InvoiceLine {
  id: string;
  serviceId: string | null;
  serviceCode: string | null;
  description: string;
  category: ServiceCategory;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface InvoiceLineInput {
  id?: string | null;
  serviceId: string;
  quantity: number;
  unitPrice?: number | null;
}

export interface Discount {
  id: string;
  type: DiscountType;
  value: number;
  amount: number;
  reason: string;
  status: ApprovalStatus;
  requestedByName: string | null;
  requestedAt: string;
  decidedByName: string | null;
  decidedAt: string | null;
  decisionNote: string | null;
}

export interface InvoiceSummary {
  id: string;
  invoiceNumber: string;
  invoiceDate: string;
  patientId: string;
  patientMRNumber: string | null;
  patientName: string | null;
  status: InvoiceStatus;
  paymentStatus: PaymentStatus;
  sourceType: string | null;
  sourceReference: string | null;
  subtotal: number;
  discountAmount: number;
  netAmount: number;
  paidAmount: number;
  dueAmount: number;
  cancellationRequested: boolean;
}

export interface Payment {
  id: string;
  receiptNumber: string;
  patientId: string;
  invoiceId: string | null;
  invoiceNumber: string | null;
  amount: number;
  method: PaymentMethod;
  reference: string | null;
  notes: string | null;
  receivedAt: string;
  receivedByName: string | null;
}

export interface Refund {
  id: string;
  refundNumber: string;
  patientId: string;
  patientMRNumber: string | null;
  patientName: string | null;
  invoiceId: string | null;
  invoiceNumber: string | null;
  type: RefundType;
  amount: number;
  reason: string;
  status: RefundStatus;
  requestedByName: string | null;
  requestedAt: string;
  decidedByName: string | null;
  decidedAt: string | null;
  decisionNote: string | null;
  paidByName: string | null;
  paidAt: string | null;
  method: PaymentMethod | null;
  reference: string | null;
}

export interface Invoice {
  id: string;
  invoiceNumber: string;
  invoiceDate: string;
  patient: PatientBrief | null;
  status: InvoiceStatus;
  paymentStatus: PaymentStatus;
  sourceType: string | null;
  sourceReference: string | null;
  notes: string | null;
  subtotal: number;
  discountAmount: number;
  netAmount: number;
  paidAmount: number;
  advanceAppliedAmount: number;
  refundedAmount: number;
  dueAmount: number;
  cancellationRequested: boolean;
  finalizedAt: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  createdAt: string;
  createdByName: string | null;
  lines: InvoiceLine[];
  discounts: Discount[];
  payments: Payment[];
  refunds: Refund[];
  patientAdvanceBalance: number;
  rowVersion: number;
}

export interface InvoiceQuery extends PaginationQuery {
  patientId?: string;
  status?: InvoiceStatus;
  dueOnly?: boolean;
  dateFrom?: string;
  dateTo?: string;
}

export interface PaymentCreate {
  clientRequestId: string;
  patientId: string;
  invoiceId: string | null;
  amount: number;
  method: PaymentMethod;
  reference: string | null;
  notes: string | null;
}

export interface Receipt {
  payment: Payment;
  patient: PatientBrief | null;
  invoice: InvoiceSummary | null;
  lines: InvoiceLine[];
  patientAdvanceBalance: number;
}

export interface RefundCreate {
  patientId: string;
  type: RefundType;
  invoiceId: string | null;
  amount: number | null;
  reason: string;
}

export interface PatientAccount {
  patient: PatientBrief;
  outstandingAmount: number;
  advanceBalance: number;
  invoices: InvoiceSummary[];
  payments: Payment[];
  refunds: Refund[];
}

export interface PendingDiscount {
  discountId: string;
  invoiceId: string;
  invoiceNumber: string;
  patientMRNumber: string | null;
  patientName: string | null;
  subtotal: number;
  discountAlreadyGiven: number;
  type: DiscountType;
  value: number;
  amount: number;
  totalPercentOfSubtotal: number;
  reason: string;
  requestedByName: string | null;
  requestedAt: string;
}

export interface Approvals {
  discounts: PendingDiscount[];
  refundsToApprove: Refund[];
  refundsToPay: Refund[];
}

export interface CollectionReport {
  from: string;
  to: string;
  cashierId: string | null;
  cashierName: string | null;
  allCashiers: boolean;
  totalReceived: number;
  totalRefunded: number;
  net: number;
  advanceApplied: number;
  byMethod: { method: PaymentMethod; received: number; refunded: number; net: number }[];
  byCashier: { userId: string | null; name: string | null; received: number; refunded: number; net: number }[];
  transactions: {
    kind: 'Payment' | 'Deposit' | 'Refund';
    id: string;
    number: string;
    at: string;
    patientMRNumber: string | null;
    patientName: string | null;
    invoiceNumber: string | null;
    method: PaymentMethod;
    reference: string | null;
    amount: number;
    userName: string | null;
  }[];
}

@Injectable({ providedIn: 'root' })
export class BillingApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/billing`;

  services(query: ServiceQuery): Observable<PagedResponse<BillableService>> {
    return this.get('/services', query);
  }

  createService(request: ServiceSave): Observable<BillableService> {
    return this.send('post', '/services', request);
  }

  updateService(id: string, request: ServiceSave): Observable<BillableService> {
    return this.send('put', `/services/${id}`, request);
  }

  setServiceStatus(id: string, isActive: boolean): Observable<BillableService> {
    return this.send('patch', `/services/${id}/status`, { isActive });
  }

  invoices(query: InvoiceQuery): Observable<PagedResponse<InvoiceSummary>> {
    return this.get('/invoices', query);
  }

  invoice(id: string): Observable<Invoice> {
    return this.get(`/invoices/${id}`, {});
  }

  createInvoice(patientId: string, notes: string | null, lines: InvoiceLineInput[]): Observable<Invoice> {
    return this.send('post', '/invoices', { patientId, notes, lines });
  }

  updateInvoice(id: string, notes: string | null, lines: InvoiceLineInput[], rowVersion: number): Observable<Invoice> {
    return this.send('put', `/invoices/${id}`, { notes, lines, rowVersion });
  }

  finalizeInvoice(id: string, rowVersion: number): Observable<Invoice> {
    return this.send('post', `/invoices/${id}/finalize`, { rowVersion });
  }

  cancelInvoice(id: string, reason: string): Observable<Invoice> {
    return this.send('post', `/invoices/${id}/cancel`, { reason });
  }

  requestDiscount(id: string, type: DiscountType, value: number, reason: string): Observable<Invoice> {
    return this.send('post', `/invoices/${id}/discounts`, { type, value, reason });
  }

  decideDiscount(discountId: string, approve: boolean, note: string | null): Observable<Invoice> {
    return this.send('post', `/discounts/${discountId}/${approve ? 'approve' : 'reject'}`, { note });
  }

  collect(request: PaymentCreate): Observable<Receipt> {
    return this.send('post', '/payments', request);
  }

  receipt(paymentId: string): Observable<Receipt> {
    return this.get(`/payments/${paymentId}/receipt`, {});
  }

  requestRefund(request: RefundCreate): Observable<Refund> {
    return this.send('post', '/refunds', request);
  }

  decideRefund(id: string, approve: boolean, note: string | null): Observable<Refund> {
    return this.send('post', `/refunds/${id}/${approve ? 'approve' : 'reject'}`, { note });
  }

  payRefund(id: string, method: PaymentMethod, reference: string | null): Observable<Refund> {
    return this.send('post', `/refunds/${id}/pay`, { method, reference });
  }

  account(patientId: string): Observable<PatientAccount> {
    return this.get(`/patients/${patientId}/account`, {});
  }

  approvals(): Observable<Approvals> {
    return this.get('/approvals', {});
  }

  collections(from: string, to: string, cashierId?: string | null): Observable<CollectionReport> {
    return this.get('/reports/collections', { from, to, cashierId });
  }

  private get<T>(path: string, query: object): Observable<T> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<ApiResponse<T>>(`${this.base}${path}`, { params }).pipe(map((r) => r.data as T));
  }

  private send<T>(method: 'post' | 'put' | 'patch', path: string, body: unknown): Observable<T> {
    return this.http.request<ApiResponse<T>>(method, `${this.base}${path}`, { body }).pipe(map((r) => r.data as T));
  }
}
