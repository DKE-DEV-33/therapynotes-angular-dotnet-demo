import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'clients' },
  {
    path: 'clients',
    loadComponent: () => import('./features/clients/clients.page').then(m => m.ClientsPage)
  },
  {
    path: 'appointments',
    loadComponent: () => import('./features/appointments/appointments.page').then(m => m.AppointmentsPage)
  },
  {
    path: 'audit',
    loadComponent: () => import('./features/audit/audit.page').then(m => m.AuditPage)
  },
  { path: '**', redirectTo: 'clients' }
];
