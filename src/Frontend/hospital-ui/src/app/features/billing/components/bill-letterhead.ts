import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';

import { BrandingService } from '../../../core/services/branding.service';

/** Hospital name, logo and contact line with the document title, for printed invoices and receipts. */
@Component({
  selector: 'app-bill-letterhead',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="letterhead">
      <div class="hospital">
        @if (branding.logoUrl(); as logo) {
          <img [src]="logo" alt="" class="logo" />
        }
        <div>
          <div class="name">{{ branding.name() }}</div>
          <div class="contact">{{ contactLine() }}</div>
        </div>
      </div>
      <div class="title">{{ title() }}</div>
    </header>
  `,
  styles: `
    .letterhead { display: flex; justify-content: space-between; align-items: flex-end; gap: 12px; padding-bottom: 6px; border-bottom: 2px solid #1a4b8c; }
    .hospital { display: flex; gap: 8px; align-items: center; }
    .logo { height: 44px; width: auto; }
    .name { font-size: 17px; font-weight: 600; color: #1a4b8c; }
    .contact { font-size: 10px; color: #444; max-width: 85mm; }
    .title { font-size: 15px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em; white-space: nowrap; }
  `,
})
export class BillLetterhead {
  readonly title = input.required<string>();
  protected readonly branding = inject(BrandingService);
  protected readonly contactLine = computed(() => {
    const c = this.branding.contact();
    return [c.address, c.phone ? 'Phone: ' + c.phone : null, c.email].filter(Boolean).join(' · ');
  });
}
