export interface AppHeaderContext {
  machine: AppHeaderMachine;
  runtimeVersion: number | null;
  activeAlarms: readonly AppHeaderAlarm[];
  snapshotAt: string;
}

export interface AppHeaderMachine {
  id: string;
  name: string;
  status: string | null;
  lastChangedAt: string | null;
}

export interface AppHeaderAlarm {
  id: string;
  code: string;
  severity: string;
  status: string;
  message: string;
  isBlocking: boolean;
  raisedAt: string;
}
