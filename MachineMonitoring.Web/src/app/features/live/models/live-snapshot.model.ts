export interface LiveSnapshot {
  machine: LiveMachine;
  runtimeVersion: number | null;
  productionLot: LiveProductionLot | null;
  currentWorkpiece: LiveWorkpiece | null;
  currentOperation: LiveOperation | null;
  activeAlarms: readonly LiveAlarm[];
  snapshotAt: string;
}

export interface LiveMachine {
  id: string;
  name: string;
  status: string | null;
  lastChangedAt: string | null;
}

export interface LiveProductionLot {
  id: string;
  code: string;
  status: string;
  progressPercentage: number;
  completedOperations: number;
  totalOperations: number;
}

export interface LiveWorkpiece {
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

export interface LiveOperation {
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

export interface LiveAlarm {
  id: string;
  code: string;
  severity: string;
  status: string;
  message: string;
  isBlocking: boolean;
  raisedAt: string;
}