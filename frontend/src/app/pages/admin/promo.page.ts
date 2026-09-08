import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../core/admin.service';
import { PromoCode } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { AdminNavComponent } from './dashboard.page';

interface PromoForm {
  code: string;
  discountType: 'Percent' | 'Amount';
  discountValue: number;
  minOrderTotal: number;
  validTo: string | null;
  usageLimit: number | null;
  isActive: boolean;
}

/** Управление промокодами (FR-55 … FR-57). */
@Component({
  selector: 'app-admin-promo',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminNavComponent],
  template: `
    <app-admin-nav />
    <h1>Промокоды</h1>

    <form class="card row" style="margin-bottom:16px; align-items:flex-end" (ngSubmit)="save()">
      <label style="width:170px">
        <span>Код</span>
        <input [(ngModel)]="form.code" name="code" required maxlength="40" placeholder="SALE15" />
      </label>
      <label style="width:150px">
        <span>Тип скидки</span>
        <select [(ngModel)]="form.discountType" name="discountType">
          <option value="Percent">Процент</option>
          <option value="Amount">Сумма, ₽</option>
        </select>
      </label>
      <label style="width:130px">
        <span>Значение</span>
        <input type="number" min="1" [(ngModel)]="form.discountValue" name="discountValue" required />
      </label>
      <label style="width:150px">
        <span>Мин. сумма, ₽</span>
        <input type="number" min="0" [(ngModel)]="form.minOrderTotal" name="minOrderTotal" />
      </label>
      <label style="width:170px">
        <span>Действует до</span>
        <input type="date" [(ngModel)]="form.validTo" name="validTo" />
      </label>
      <label style="width:130px">
        <span>Лимит</span>
        <input type="number" min="1" [(ngModel)]="form.usageLimit" name="usageLimit" />
      </label>
      <button type="submit" class="btn btn--primary" [disabled]="busy()">
        {{ editingId() ? 'Сохранить' : 'Создать' }}
      </button>
      @if (editingId()) {
        <button type="button" class="btn" (click)="reset()">Отмена</button>
      }
    </form>

    <div class="card table-wrapper">
      <table>
        <thead>
          <tr>
            <th>Код</th><th>Скидка</th><th>Мин. сумма</th><th>Действует до</th>
            <th>Использован</th><th>Активен</th><th></th>
          </tr>
        </thead>
        <tbody>
          @for (promo of promos(); track promo.id) {
            <tr>
              <td><strong>{{ promo.code }}</strong></td>
              <td>{{ promo.discountType === 'Percent' ? promo.discountValue + '%' : promo.discountValue + ' ₽' }}</td>
              <td>{{ promo.minOrderTotal }} ₽</td>
              <td>{{ promo.validTo ? (promo.validTo | date: 'dd.MM.yyyy') : 'бессрочно' }}</td>
              <td>{{ promo.usedCount }}{{ promo.usageLimit ? ' / ' + promo.usageLimit : '' }}</td>
              <td>{{ promo.isActive ? 'да' : 'нет' }}</td>
              <td class="text-right nowrap">
                <button type="button" class="btn btn--sm" (click)="edit(promo)">Изменить</button>
                <button type="button" class="btn btn--sm btn--danger" (click)="remove(promo)">Удалить</button>
              </td>
            </tr>
          }
          @if (promos().length === 0) {
            <tr><td colspan="7" class="muted">Промокодов нет.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`.nowrap { white-space: nowrap; }`],
})
export class AdminPromoPage implements OnInit {
  private readonly admin = inject(AdminService);
  private readonly toast = inject(ToastService);

  readonly promos = signal<PromoCode[]>([]);
  readonly editingId = signal<number | null>(null);
  readonly busy = signal(false);

  form: PromoForm = this.empty();

  ngOnInit(): void {
    this.load();
  }

  save(): void {
    const body = {
      ...this.form,
      validFrom: null,
      validTo: this.form.validTo ? new Date(this.form.validTo).toISOString() : null,
      usageLimit: this.form.usageLimit || null,
    };

    this.busy.set(true);
    const id = this.editingId();
    const request = id ? this.admin.updatePromo(id, body) : this.admin.createPromo(body);

    request.subscribe({
      next: () => {
        this.busy.set(false);
        this.toast.success(id ? 'Промокод обновлён' : 'Промокод создан');
        this.reset();
        this.load();
      },
      error: () => this.busy.set(false),
    });
  }

  edit(promo: PromoCode): void {
    this.editingId.set(promo.id);
    this.form = {
      code: promo.code,
      discountType: promo.discountType,
      discountValue: promo.discountValue,
      minOrderTotal: promo.minOrderTotal,
      validTo: promo.validTo ? promo.validTo.slice(0, 10) : null,
      usageLimit: promo.usageLimit ?? null,
      isActive: promo.isActive,
    };
  }

  remove(promo: PromoCode): void {
    if (!confirm(`Удалить промокод ${promo.code}?`)) return;

    this.admin.deletePromo(promo.id).subscribe(() => {
      this.toast.info('Промокод удалён или отключён');
      this.load();
    });
  }

  reset(): void {
    this.editingId.set(null);
    this.form = this.empty();
  }

  private load(): void {
    this.admin.promos().subscribe((promos) => this.promos.set(promos));
  }

  private empty(): PromoForm {
    return {
      code: '',
      discountType: 'Percent',
      discountValue: 10,
      minOrderTotal: 0,
      validTo: null,
      usageLimit: null,
      isActive: true,
    };
  }
}
