import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { SchedulingApi } from '../../core/scheduling-api.service';
import { AuditEntry } from '../../core/models';

@Component({
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="page">
      <header class="head">
        <div>
          <h1>Audit Log</h1>
          <div class="sub">Written by a domain-event handler inside the Scheduling API.</div>
        </div>
        <button class="ghost" (click)="refresh()" [disabled]="busy()">Refresh</button>
      </header>

      <div class="card">
        <div class="empty" *ngIf="!busy() && entries().length === 0">No audit entries yet.</div>
        <div class="error" *ngIf="error()">{{ error() }}</div>

        <ul class="list" *ngIf="entries().length > 0">
          <li *ngFor="let e of entries()">
            <div class="top">
              <div class="type">{{ e.eventType }}</div>
              <div class="when">{{ e.occurredAt | date: 'medium' }}</div>
            </div>
            <div class="msg">{{ e.message }}</div>
            <div class="meta">
              <span class="pill">id {{ shortId(e.id) }}</span>
            </div>
          </li>
        </ul>
      </div>
    </section>
  `,
  styles: [
    `
      .page {
        display: grid;
        gap: 16px;
      }
      .head {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: 12px;
      }
      h1 {
        margin: 0;
        font-size: 28px;
        letter-spacing: 0.2px;
      }
      .sub {
        margin-top: 6px;
        color: rgba(255, 255, 255, 0.68);
      }
      .card {
        background: rgba(255, 255, 255, 0.06);
        border: 1px solid rgba(255, 255, 255, 0.12);
        border-radius: 14px;
        box-shadow: 0 12px 28px rgba(0, 0, 0, 0.25);
        padding: 16px;
      }
      button {
        padding: 10px 12px;
        border-radius: 12px;
        border: 1px solid rgba(255, 255, 255, 0.14);
        background: rgba(255, 255, 255, 0.06);
        color: rgba(255, 255, 255, 0.92);
        font-weight: 600;
        cursor: pointer;
      }
      button:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }
      .error {
        color: #ff5b7a;
        font-size: 13px;
        margin-bottom: 8px;
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
      .top {
        display: flex;
        justify-content: space-between;
        gap: 10px;
        align-items: baseline;
      }
      .type {
        font-weight: 650;
      }
      .when {
        color: rgba(255, 255, 255, 0.72);
        font-size: 13px;
        text-align: right;
      }
      .msg {
        margin-top: 6px;
        color: rgba(255, 255, 255, 0.9);
      }
      .meta {
        margin-top: 10px;
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
    `
  ]
})
export class AuditPage {
  private readonly api = inject(SchedulingApi);

  protected entries = signal<AuditEntry[]>([]);
  protected busy = signal(false);
  protected error = signal<string | null>(null);

  constructor() {
    void this.refresh();
  }

  async refresh(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      this.entries.set(await this.api.getAudit());
    } catch (e: any) {
      this.error.set(e?.message ?? 'Failed to load audit log.');
    } finally {
      this.busy.set(false);
    }
  }

  protected shortId(id: string): string {
    return id.length > 8 ? id.slice(0, 8) : id;
  }
}

