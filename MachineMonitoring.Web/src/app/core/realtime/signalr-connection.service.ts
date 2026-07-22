import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

@Injectable({
  providedIn: 'root',
})
export class SignalrConnectionService {
  private readonly platformId = inject(PLATFORM_ID);

  private connection: HubConnection | null = null;
  private connectPromise: Promise<void> | null = null;
  private joinedMachineId: string | null = null;

  async connect(machineId: string): Promise<void> {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const normalizedMachineId = machineId.trim();

    if (!normalizedMachineId) {
      throw new Error('Machine id is required to connect to SignalR.');
    }

    if (this.connectPromise) {
      await this.connectPromise;
    }

    if (
      this.connection?.state === HubConnectionState.Connected &&
      this.joinedMachineId === normalizedMachineId
    ) {
      return;
    }

    this.connectPromise = this.connectCore(normalizedMachineId);

    try {
      await this.connectPromise;
    } finally {
      this.connectPromise = null;
    }
  }

  async disconnect(): Promise<void> {
    if (this.connectPromise) {
      try {
        await this.connectPromise;
      } catch {
        // La connessione non è riuscita: procediamo comunque con il cleanup.
      }
    }

    if (!this.connection) {
      return;
    }

    if (this.joinedMachineId && this.connection.state === HubConnectionState.Connected) {
      await this.connection.invoke('LeaveMachineAsync', this.joinedMachineId);
    }

    this.joinedMachineId = null;

    await this.connection.stop();
    this.connection = null;
  }

  private registerConnectionLifecycleHandlers(connection: HubConnection): void {
    connection.onreconnecting((error) => {
      console.warn('SignalR reconnecting.', error);
    });

    connection.onreconnected(async (connectionId) => {
      console.info('SignalR reconnected.', connectionId);

      if (this.joinedMachineId) {
        await connection.invoke('JoinMachineAsync', this.joinedMachineId);
      }
    });

    connection.onclose((error) => {
      console.warn('SignalR connection closed.', error);
    });
  }

  private async connectCore(machineId: string): Promise<void> {
    const connection = this.getOrCreateConnection();

    if (connection.state === HubConnectionState.Disconnected) {
      await connection.start();

      console.info('SignalR connected.', connection.connectionId);
    }

    if (connection.state !== HubConnectionState.Connected) {
      throw new Error(`SignalR connection is in state '${connection.state}'.`);
    }

    if (this.joinedMachineId === machineId) {
      return;
    }

    if (this.joinedMachineId) {
      await connection.invoke('LeaveMachineAsync', this.joinedMachineId);
    }

    await connection.invoke('JoinMachineAsync', machineId);

    this.joinedMachineId = machineId;

    console.info(`SignalR joined machine group: machine:${machineId}`);
  }

  on<TPayload>(methodName: string, handler: (payload: TPayload) => void): () => void {
    const connection = this.getOrCreateConnection();

    connection.on(methodName, handler);

    return () => {
      connection.off(methodName, handler);
    };
  }

  private getOrCreateConnection(): HubConnection {
    if (this.connection) {
      return this.connection;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl('http://localhost:5221/hubs/machine-monitoring')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.registerConnectionLifecycleHandlers(this.connection);

    return this.connection;
  }
}
