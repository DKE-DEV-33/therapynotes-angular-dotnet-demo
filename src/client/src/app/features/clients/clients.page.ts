import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SchedulingApi } from '../../core/scheduling-api.service';
import { Client } from '../../core/models';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="page">
      <header class="head">
        <h1>Clients</h1>
        <div class="sub">Create a client, then schedule an appointment. Events are written to audit + outbox.</div>
      </header>

      <div class="grid">
        <div class="card">
          <h2>New Client</h2>
          <form (ngSubmit)="create()" class="form">
            <label>
              <div class="label">Display name</div>
              <input [(ngModel)]="displayName" name="displayName" placeholder="e.g. Alex Morgan" />
            </label>
            <button type="submit" [disabled]="!canCreate()">Create</button>
            <div class="error" *ngIf="error()">{{ error() }}</div>
          </form>
        </div>

        <div class="card">
          <div class="row">
            <h2>Client List</h2>
            <button class="ghost" (click)="refresh()" [disabled]="busy()">Refresh</button>
          </div>
          <div class="empty" *ngIf="!busy() && clients().length === 0">No clients yet.</div>
          <ul class="list" *ngIf="clients().length > 0">
            <li *ngFor="let c of clients()">
              <div class="main">{{ c.displayName }}</div>
              <div class="meta">
                <span class="pill">id {{ shortId(c.id) }}</span>
                <span class="pill">{{ c.createdAt | date: 'medium' }}</span>
              </div>
            </li>
          </ul>
        </div>
      </div>
    </section>
  `,
  styles: [
    `
      .page {
        display: grid;
        gap: 16px;
      }
      .head h1 {
        margin: 0;
        font-size: 28px;
        letter-spacing: 0.2px;
      }
      .sub {
        margin-top: 6px;
        color: rgba(255, 255, 255, 0.68);
      }
      .grid {
        display: grid;
        grid-template-columns: 0.95fr 1.2fr;
        gap: 16px;
      }
      .card {
        background: rgba(255, 255, 255, 0.06);
        border: 1px solid rgba(255, 255, 255, 0.12);
        border-radius: 14px;
        box-shadow: 0 12px 28px rgba(0, 0, 0, 0.25);
        padding: 16px;
      }
      .card h2 {
        margin: 0 0 12px;
        font-size: 14px;
        letter-spacing: 0.14em;
        text-transform: uppercase;
        color: rgba(255, 255, 255, 0.72);
      }
      .row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 10px;
      }
      .form {
        display: grid;
        gap: 10px;
      }
      .label {
        font-size: 12px;
        color: rgba(255, 255, 255, 0.68);
        margin-bottom: 6px;
      }
      input {
        width: 100%;
        padding: 10px 12px;
        border-radius: 12px;
        border: 1px solid rgba(255, 255, 255, 0.14);
        background: rgba(0, 0, 0, 0.28);
        color: rgba(255, 255, 255, 0.92);
        outline: none;
      }
      input:focus {
        border-color: rgba(77, 227, 193, 0.35);
        box-shadow: 0 0 0 4px rgba(77, 227, 193, 0.09);
      }
      button {
        padding: 10px 12px;
        border-radius: 12px;
        border: 1px solid rgba(77, 227, 193, 0.25);
        background: linear-gradient(180deg, rgba(77, 227, 193, 0.22), rgba(113, 168, 255, 0.14));
        color: rgba(255, 255, 255, 0.92);
        font-weight: 600;
        cursor: pointer;
      }
      button:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }
      .ghost {
        background: rgba(255, 255, 255, 0.06);
        border-color: rgba(255, 255, 255, 0.14);
      }
      .error {
        color: #ff5b7a;
        font-size: 13px;
      }
      .empty {
        color: rgba(255, 255, 255, 0.68);
        padding: 8px 0;
      }
      .list {
        list-style: none;
        padding: 0;
        margin: 0;
        display: grid;
        gap: 10px;
      }
      li {
        padding: 12px;
        border-radius: 12px;
        border: 1px solid rgba(255, 255, 255, 0.12);
        background: rgba(0, 0, 0, 0.18);
      }
      .main {
        font-weight: 650;
        margin-bottom: 6px;
      }
      .meta {
        display: flex;
        gap: 8px;
        flex-wrap: wrap;
      }
      .pill {
        font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono',
          'Courier New', monospace;
        font-size: 12px;
        color: rgba(255, 255, 255, 0.72);
        padding: 3px 8px;
        border-radius: 999px;
        border: 1px solid rgba(255, 255, 255, 0.12);
      }
      @media (max-width: 920px) {
        .grid {
          grid-template-columns: 1fr;
        }
      }
    `
  ]
})
export class ClientsPage {
  private readonly api = inject(SchedulingApi);

  protected clients = signal<Client[]>([]);
  protected busy = signal(false);
  protected error = signal<string | null>(null);

  protected displayName = '';

  constructor() {
    void this.refresh();
  }

  async refresh(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      this.clients.set(await this.api.getClients());
    } catch (e: any) {
      this.error.set(e?.message ?? 'Failed to load clients.');
    } finally {
      this.busy.set(false);
    }
  }

  async create(): Promise<void> {
    if (!this.canCreate()) return;

    this.error.set(null);
    this.busy.set(true);
    try {
      await this.api.createClient(this.displayName.trim());
      this.displayName = '';
      this.clients.set(await this.api.getClients());
    } catch (e: any) {
      this.error.set(e?.error ?? e?.message ?? 'Failed to create client.');
    } finally {
      this.busy.set(false);
    }
  }

  protected shortId(id: string): string {
    return id.length > 8 ? id.slice(0, 8) : id;
  }

  protected canCreate(): boolean {
    return this.displayName.trim().length > 0 && !this.busy();
  }
}
