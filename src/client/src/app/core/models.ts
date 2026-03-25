export type Client = {
  id: string;
  displayName: string;
  createdAt: string;
};

export type Appointment = {
  id: string;
  clientId: string;
  startsAt: string;
  durationMinutes: number;
  createdAt: string;
};

export type AuditEntry = {
  id: string;
  occurredAt: string;
  eventType: string;
  message: string;
};

