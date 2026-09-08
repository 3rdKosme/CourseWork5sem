import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { CartService } from './core/cart.service';
import { ToastsComponent } from './shared/ui';

/** Оболочка приложения: шапка с поиском и корзиной, подвал, контейнер уведомлений. */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterOutlet, RouterLink, RouterLinkActive, ToastsComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  readonly auth = inject(AuthService);
  readonly cart = inject(CartService);
  private readonly router = inject(Router);

  search = '';

  ngOnInit(): void {
    // Корзина доступна и гостю, поэтому загружается при старте всегда.
    this.cart.load().subscribe({ error: () => undefined });
  }

  submitSearch(): void {
    void this.router.navigate(['/catalog'], {
      queryParams: { search: this.search.trim() || null, page: 1 },
      queryParamsHandling: 'merge',
    });
  }

  logout(): void {
    this.auth.logout();
    this.cart.load().subscribe({ error: () => undefined });
  }
}
