import { Directive, effect, inject, input, TemplateRef, ViewContainerRef } from '@angular/core';

import { AuthService } from '../../core/auth/auth.service';

/**
 * Renders its content only when the user holds at least one of the permissions.
 * Usage: <button *hasPermission="'Patient.Create'">…</button> or *hasPermission="['A', 'B']"
 */
@Directive({ selector: '[hasPermission]' })
export class HasPermissionDirective {
  private readonly auth = inject(AuthService);
  private readonly template = inject(TemplateRef<unknown>);
  private readonly container = inject(ViewContainerRef);

  readonly hasPermission = input.required<string | string[]>();

  private rendered = false;

  constructor() {
    effect(() => {
      const required = this.hasPermission();
      const allowed = this.auth.hasAnyPermission(Array.isArray(required) ? required : [required]);

      if (allowed && !this.rendered) {
        this.container.createEmbeddedView(this.template);
        this.rendered = true;
      } else if (!allowed && this.rendered) {
        this.container.clear();
        this.rendered = false;
      }
    });
  }
}
