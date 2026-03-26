import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Appointment, Client } from '../../core/models';
import { SchedulingApi } from '../../core/scheduling-api.service';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="page">
      <header class="head">
        <h1>Appointments</h1>
        <div class="sub">
          Scheduling an appointment publishes a domain event (audit) and an integration event (outbox).
        </div>
      </header>

      <div class="grid">
        <div class="card">
          <h2>Schedule</h2>
          <form (ngSubmit)="create()" class="form">
            <label>
              <div class="label">Client</div>
              <select [(ngModel)]="clientId" name="clientId">
                <option value="" disabled>Select a client</option>
                <option *ngFor="let c of clients()" [value]="c.id">{{ c.displayName }}</option>
              </select>
            </label>

            <div class="two">
              <label>
                <div class="label">Starts (local)</div>
                <input type="datetime-local" [(ngModel)]="startsAtLocal" name="startsAtLocal" />
              </label>

              <label>
                <div class="label">Duration (minutes)</div>
                <input type="number" min="15" max="240" step="15" [(ngModel)]="durationMinutes" name="durationMinutes" />
              </label>
            </div>

            <button type="submit" [disabled]="!canCreate()">Schedule</button>
            <div class="error" *ngIf="error()">{{ error() }}</div>
          </form>
        </div>

        <div class="card">
          <div class="row">
            <h2>Upcoming</h2>
            <button class="ghost" (click)="refresh()" [disabled]="busy()">Refresh</button>
          </div>

          <div class="empty" *ngIf="!busy() && appts().length === 0">No appointments yet.</div>
          <ul class="list" *ngIf="appts().length > 0">
            <li *ngFor="let a of appts()">
              <div class="main">
                <span class="who">{{ displayNameFor(a.clientId) }}</span>
                <span class="when">{{ a.startsAt | date: 'fullDate' }} · {{ a.startsAt | date: 'shortTime' }}</span>
              </div>
              <div class="meta">
                <span class="pill">duration {{ a.durationMinutes }}m</span>
                <span class="pill">appt {{ shortId(a.id) }}</span>
                <span class="pill">client {{ shortId(a.clientId) }}</span>
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
      input,
      select {
        width: 100%;
        padding: 10px 12px;
        border-radius: 12px;
        border: 1px solid rgba(255, 255, 255, 0.14);
        background: rgba(0, 0, 0, 0.28);
        color: rgba(255, 255, 255, 0.92);
        outline: none;
      }
      input:focus,
      select:focus {
        border-color: rgba(77, 227, 193, 0.35);
        box-shadow: 0 0 0 4px rgba(77, 227, 193, 0.09);
      }
      .two {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 12px;
      }
      .two label {
        min-width: 0;
      }
      .two input {
        min-width: 0;
      }
      input[type='datetime-local'] {
        letter-spacing: 0.2px;
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
        display: flex;
        justify-content: space-between;
        gap: 10px;
        align-items: baseline;
        margin-bottom: 8px;
      }
      .who {
        font-weight: 650;
      }
      .when {
        color: rgba(255, 255, 255, 0.72);
        font-size: 13px;
        text-align: right;
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
        .two {
          grid-template-columns: 1fr;
        }
      }
    `
  ]
})
export class AppointmentsPage {
  private readonly api = inject(SchedulingApi);

  protected clients = signal<Client[]>([]);
  protected appts = signal<Appointment[]>([]);
  protected busy = signal(false);
  protected error = signal<string | null>(null);

  protected clientId = '';
  protected startsAtLocal = '';
  protected durationMinutes = 50;

  constructor() {
    void this.refresh();
  }

  async refresh(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      const [clients, appts] = await Promise.all([this.api.getClients(), this.api.getAppointments()]);
      this.clients.set(clients);
      this.appts.set(appts);
      if (!this.clientId && clients.length > 0) this.clientId = clients[0]!.id;
    } catch (e: any) {
      this.error.set(e?.message ?? 'Failed to load appointments.');
    } finally {
      this.busy.set(false);
    }
  }

  async create(): Promise<void> {
    if (!this.canCreate()) return;

    this.error.set(null);
    this.busy.set(true);
    try {
      const startsAt = this.localDateTimeToIso(this.startsAtLocal);
      await this.api.createAppointment(this.clientId, startsAt, this.durationMinutes);
      this.appts.set(await this.api.getAppointments());
    } catch (e: any) {
      this.error.set(e?.error ?? e?.message ?? 'Failed to schedule appointment.');
    } finally {
      this.busy.set(false);
    }
  }

  protected displayNameFor(clientId: string): string {
    return this.clients().find(c => c.id === clientId)?.displayName ?? 'Unknown client';
  }

  protected shortId(id: string): string {
    return id.length > 8 ? id.slice(0, 8) : id;
  }

  protected canCreate(): boolean {
    return !this.busy() && this.clientId.trim().length > 0 && this.startsAtLocal.trim().length > 0;
  }

  private localDateTimeToIso(local: string): string {
    // `datetime-local` returns no timezone; treat it as local time and convert to ISO with offset (UTC).
    // Example input: "2026-03-24T19:30"
    const d = new Date(local);
    return d.toISOString();
  }
}
