import { inject, Injectable } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { catchError, EMPTY, filter, Observable, switchMap, tap } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { apiErrorMessage } from '../../../shared/utilities/server-errors';
import { BillingApiService, Invoice, Refund } from '../billing.models';
import { ReasonDialog, ReasonDialogData } from './reason-dialog';
import { RefundPayDialog } from './refund-dialogs';

/**
 * Approval and payout actions shared by the invoice, patient account and approvals pages. Each opens its dialog,
 * calls the API and reports the outcome; the observable completes without a value when the user cancels or it fails.
 */
@Injectable({ providedIn: 'root' })
export class BillingActions {
  private readonly api = inject(BillingApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  decideDiscount(discountId: string, approve: boolean): Observable<Invoice> {
    return this.ask({
      title: approve ? 'Approve discount' : 'Reject discount',
      label: approve ? 'Note (optional)' : 'Reason for rejecting',
      confirmText: approve ? 'Approve' : 'Reject',
      required: !approve,
      destructive: !approve,
    }).pipe(
      switchMap((note) => this.api.decideDiscount(discountId, approve, note || null)),
      tap(() => this.notifications.success(approve ? 'Discount approved.' : 'Discount rejected.')),
      catchError((err) => this.fail(err)),
    );
  }

  decideRefund(refund: Refund, approve: boolean): Observable<Refund> {
    return this.ask({
      title: `${approve ? 'Approve' : 'Reject'} refund ${refund.refundNumber}`,
      message: `${refund.patientName ?? ''} — ${refund.amount.toFixed(2)}: ${refund.reason}`,
      label: approve ? 'Note (optional)' : 'Reason for rejecting',
      confirmText: approve ? 'Approve' : 'Reject',
      required: !approve,
      destructive: !approve,
    }).pipe(
      switchMap((note) => this.api.decideRefund(refund.id, approve, note || null)),
      tap(() => this.notifications.success(approve ? 'Refund approved; the cashier can pay it out.' : 'Refund rejected.')),
      catchError((err) => this.fail(err)),
    );
  }

  payRefund(refund: Refund): Observable<Refund> {
    return this.dialog.open(RefundPayDialog, { data: refund, width: '520px' }).afterClosed().pipe(filter((r): r is Refund => !!r));
  }

  cancelInvoice(invoice: { id: string; invoiceNumber: string; status: string }): Observable<Invoice> {
    const draft = invoice.status === 'Draft';
    return this.ask({
      title: draft ? `Discard draft ${invoice.invoiceNumber}?` : `Cancel invoice ${invoice.invoiceNumber}?`,
      message: draft
        ? 'The draft is kept for the record but can no longer be used.'
        : 'No cash was received for this invoice. Advance applied to it goes back to the patient’s advance.',
      label: 'Reason',
      confirmText: draft ? 'Discard draft' : 'Cancel invoice',
      required: true,
      destructive: true,
    }).pipe(
      switchMap((reason) => this.api.cancelInvoice(invoice.id, reason)),
      tap(() => this.notifications.success(draft ? 'Draft discarded.' : 'Invoice cancelled.')),
      catchError((err) => this.fail(err)),
    );
  }

  private ask(data: ReasonDialogData): Observable<string> {
    return this.dialog
      .open(ReasonDialog, { data })
      .afterClosed()
      .pipe(filter((text): text is string => text !== undefined));
  }

  private fail(err: unknown): Observable<never> {
    this.notifications.error(apiErrorMessage(err));
    return EMPTY;
  }
}
