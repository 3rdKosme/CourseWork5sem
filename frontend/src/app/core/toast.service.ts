import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  type: 'success' | 'error' | 'info';
  text: string;
}

/** Всплывающие уведомления о результате действий (раздел 8.2 ТЗ). */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  readonly toasts = signal<Toast[]>([]);

  success(text: string): void {
    this.push('success', text);
  }

  error(text: string): void {
    this.push('error', text);
  }

  info(text: string): void {
    this.push('info', text);
  }

  dismiss(id: number): void {
    this.toasts.update((items) => items.filter((t) => t.id !== id));
  }

  private push(type: Toast['type'], text: string): void {
    const toast: Toast = { id: this.nextId++, type, text };
    this.toasts.update((items) => [...items, toast]);
    setTimeout(() => this.dismiss(toast.id), type === 'error' ? 6000 : 3500);
  }
}
