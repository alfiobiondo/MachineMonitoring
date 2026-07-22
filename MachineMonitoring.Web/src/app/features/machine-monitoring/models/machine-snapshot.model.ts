export interface MachineSnapshot {
  machine: MachineSnapshotMachine;
  runtimeVersion: number | null;
  productionLot: MachineSnapshotProductionLot | null;
  currentWorkpiece: MachineSnapshotWorkpiece | null;
  currentOperation: MachineSnapshotOperation | null;
  activeAlarms: readonly MachineSnapshotAlarm[];
  warnings: readonly MachineSnapshotWarning[];
  snapshotAt: string;
}

export interface MachineSnapshotMachine {
  id: string;
  name: string;
  status: string | null;
  lastChangedAt: string | null;
}

export interface MachineSnapshotProductionLot {
  id: string;
  code: string;
  status: string;
  progressPercentage: number;
  completedOperations: number;
  totalOperations: number;
}

export interface MachineSnapshotWorkpiece {
  id: string;
  code: string;
  status: string;
  sequenceNumber: number;
  position: number;
  totalWorkpieces: number;
  progressPercentage: number;
  completedOperations: number;
  totalOperations: number;
}

export interface MachineSnapshotOperation {
  id: string;
  type: string;
  status: string;
  sequenceNumber: number;
  position: number;
  totalOperations: number;
  progressPercentage: number;
  currentPhase: string | null;
  startedAt: string | null;
}

export interface MachineSnapshotAlarm {
  id: string;
  code: string;
  severity: string;
  status: string;
  message: string;
  isBlocking: boolean;
  raisedAt: string;
  acknowledgedAt?: string | null;
}

export interface MachineSnapshotWarning {
  id: string;
  machineId: string;
  code: string;
  severity: string;
  title: string;
  message: string;
  detectedAt: string;
  resolvedAt: string | null;
  isActive: boolean;
  sourceId: string | null;
}

export type MachineNotificationKind = 'alarm' | 'warning';
export type MachineNotificationLifecycleStatus = 'Active' | 'Acknowledged';

export interface MachineNotificationItem {
  id: string;
  machineId: string;
  kind: MachineNotificationKind;
  category: MachineNotificationKind;
  lifecycleStatus: MachineNotificationLifecycleStatus;
  severity: string;
  title: string;
  message: string;
  timestamp: string;
  raisedAt: string;
  acknowledgedAt?: string | null;
  isBlocking: boolean;
  isActive: boolean;
  sourceId: string;
}
