import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Appointment, AuditEntry, Client } from './models';

@Injectable({ providedIn: 'root' })
export class SchedulingApi {
  private readonly http = inject(HttpClient);

  async getClients(): Promise<Client[]> {
    return firstValueFrom(this.http.get<Client[]>('/api/clients'));
  }

  async createClient(displayName: string): Promise<Client> {
    return firstValueFrom(this.http.post<Client>('/api/clients', { displayName }));
  }

  async getAppointments(from?: string, to?: string): Promise<Appointment[]> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return firstValueFrom(this.http.get<Appointment[]>('/api/appointments', { params }));
  }

  async createAppointment(clientId: string, startsAt: string, durationMinutes: number): Promise<Appointment> {
    return firstValueFrom(
      this.http.post<Appointment>('/api/appointments', { clientId, startsAt, durationMinutes })
    );
  }

  async getAudit(): Promise<AuditEntry[]> {
    return firstValueFrom(this.http.get<AuditEntry[]>('/api/audit'));
  }
}

