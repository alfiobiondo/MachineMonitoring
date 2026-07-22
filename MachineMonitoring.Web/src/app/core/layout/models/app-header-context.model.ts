export interface AppHeaderContext {
  machine: AppHeaderMachine;
  runtimeVersion: number | null;
  activeAlarms: readonly AppHeaderAlarm[];
  activeWarnings: readonly AppHeaderWarning[];
  notifications: readonly AppHeaderNotification[];
  acknowledgingAlarmIds: readonly string[];
  alarmAcknowledgeError: string | null;
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
  acknowledgedAt?: string | null;
}

export interface AppHeaderWarning {
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

export interface AppHeaderNotification {
  id: string;
  machineId: string;
  kind: 'alarm' | 'warning';
  category: 'alarm' | 'warning';
  lifecycleStatus: 'Active' | 'Acknowledged';
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
