import { formatCurrency } from '@angular/common';
import { inject, LOCALE_ID, Pipe, PipeTransform } from '@angular/core';

/** Money in Bangladeshi Taka with two decimals, e.g. ৳1,250.00. */
@Pipe({ name: 'taka' })
export class TakaPipe implements PipeTransform {
  private readonly locale = inject(LOCALE_ID);

  transform(value: number | null | undefined): string {
    return value === null || value === undefined ? '' : formatCurrency(value, this.locale, '৳', 'BDT', '1.2-2');
  }
}
