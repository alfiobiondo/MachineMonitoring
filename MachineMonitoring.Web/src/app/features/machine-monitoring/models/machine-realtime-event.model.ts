export type MachineAlarmChangeKind = 'raised' | 'acknowledged' | 'resolved';

export interface MachineAlarmChangedEvent {
  eventId: string;
  changeKind: MachineAlarmChangeKind;
  alarmId: string;
  machineId: string;
  machineOperationId: string | null;
  code: string;
  severity: string;
  status: string;
  message: string;
  isBlocking: boolean;
  raisedAt: string;
  acknowledgedAt: string | null;
  resolvedAt: string | null;
  resolutionNotes: string | null;
  occurredAt: string;
}

export interface MachineRuntimeChangedEvent {
  eventId: string;
  machineId: string;
  status: string;
  currentOperationId: string | null;
  lastChangedAt: string;
  failureReason: string | null;
  activeAlarmId: string | null;
  version: number;
  occurredAt: string;
}

export type MachineOperationChangeKind = 'status' | 'progress';

export interface MachineOperationChangedEvent {
  eventId: string;
  changeKind: MachineOperationChangeKind;
  operationId: string;
  workpieceId: string;
  machineId: string;
  sequenceNumber: number;
  type: string;
  status: string;
  progressPercentage: number;
  currentPhase: string | null;
  failureReason: string | null;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  occurredAt: string;
}
