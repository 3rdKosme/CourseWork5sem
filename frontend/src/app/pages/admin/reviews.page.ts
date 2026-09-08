import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../core/admin.service';
import { Paged, Review } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { PaginationComponent, RatingComponent } from '../../shared/ui';
import { AdminNavComponent } from './dashboard.page';

/** Модерация отзывов (FR-51, FR-52). */
@Component({
  selector: 'app-admin-reviews',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminNavComponent, RatingComponent, PaginationComponent],
  template: `
    <app-admin-nav />
    <h1>Модерация отзывов</h1>

    <div class="card row" style="margin-bottom:16px">
      <label class="inline">
        <input type="radio" name="filter" [value]="'pending'" [(ngModel)]="filter" (ngModelChange)="load(1)" />
        На модерации
      </label>
      <label class="inline">
        <input type="radio" name="filter" [value]="'approved'" [(ngModel)]="filter" (ngModelChange)="load(1)" />
        Опубликованные
      </label>
      <label class="inline">
        <input type="radio" name="filter" [value]="'all'" [(ngModel)]="filter" (ngModelChange)="load(1)" />
        Все
      </label>
    </div>

    @if (page(); as data) {
      @if (data.items.length === 0) {
        <div class="card empty">Отзывов нет.</div>
      }

      @for (review of data.items; track review.id) {
        <article class="card stack" style="margin-bottom:12px">
          <div class="spread">
            <div>
              <strong>{{ review.productName }}</strong>
              <div class="muted">{{ review.authorName }} · {{ review.createdAt | date: 'dd.MM.yyyy HH:mm' }}</div>
            </div>
            <app-rating [value]="review.rating" />
          </div>

          @if (review.title) { <h3>{{ review.title }}</h3> }
          <p>{{ review.body }}</p>

          <div class="row">
            @if (!review.isApproved) {
              <button type="button" class="btn btn--primary btn--sm" (click)="approve(review)">Опубликовать</button>
            } @else {
              <span class="status status--Completed">Опубликован</span>
            }
            <button type="button" class="btn btn--sm btn--danger" (click)="remove(review)">Удалить</button>
          </div>
        </article>
      }

      <app-pagination [page]="data.page" [totalPages]="data.totalPages" (pageChange)="load($event)" />
    }
  `,
  styles: [
    `
      .inline {
        display: flex;
        align-items: center;
        gap: 8px;
        margin: 0;
      }

      .inline input {
        width: auto;
      }
    `,
  ],
})
export class AdminReviewsPage implements OnInit {
  private readonly admin = inject(AdminService);
  private readonly toast = inject(ToastService);

  readonly page = signal<Paged<Review> | null>(null);
  filter: 'pending' | 'approved' | 'all' = 'pending';

  ngOnInit(): void {
    this.load(1);
  }

  load(page: number): void {
    const approved = this.filter === 'all' ? undefined : this.filter === 'approved';
    this.admin.reviews(approved, page).subscribe((result) => this.page.set(result));
  }

  approve(review: Review): void {
    this.admin.approveReview(review.id).subscribe(() => {
      this.toast.success('Отзыв опубликован, рейтинг товара пересчитан');
      this.load(this.page()?.page ?? 1);
    });
  }

  remove(review: Review): void {
    if (!confirm('Удалить отзыв?')) return;

    this.admin.deleteReview(review.id).subscribe(() => {
      this.toast.info('Отзыв удалён');
      this.load(this.page()?.page ?? 1);
    });
  }
}
