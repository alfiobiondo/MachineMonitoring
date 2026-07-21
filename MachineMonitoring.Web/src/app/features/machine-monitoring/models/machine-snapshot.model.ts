export interface MachineSnapshot {
  machine: MachineSnapshotMachine;
  runtimeVersion: number | null;
  productionLot: MachineSnapshotProductionLot | null;
  currentWorkpiece: MachineSnapshotWorkpiece | null;
  currentOperation: MachineSnapshotOperation | null;
  activeAlarms: readonly MachineSnapshotAlarm[];
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
}
