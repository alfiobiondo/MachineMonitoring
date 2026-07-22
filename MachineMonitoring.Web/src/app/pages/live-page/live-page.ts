import { Component, computed, inject } from '@angular/core';

import { MachineSnapshot } from '../../features/machine-monitoring/models/machine-snapshot.model';
import { MachineSnapshotStore } from '../../features/machine-monitoring/state/machine-snapshot.store';
import { LiveProgressCard } from '../../features/live/components/live-progress-card/live-progress-card';

interface LivePageProgressCardViewModel {
  title: string;
  status: string;
  metaText: string | null;
  label: string;
  detailText: string | null;
  progress: number | null;
  isEmpty: boolean;
}

interface LiveProductionContextViewModel {
  text: string;
  tone: 'neutral' | 'active' | 'paused' | 'faulted';
}

@Component({
  selector: 'app-live-page',
  imports: [LiveProgressCard],
  templateUrl: './live-page.html',
  styleUrl: './live-page.scss',
})
export class LivePage {
  readonly store = inject(MachineSnapshotStore);

  readonly productionContext = computed<LiveProductionContextViewModel>(() =>
    this.createProductionContext(this.snapshot()),
  );

  readonly lotCard = computed<LivePageProgressCardViewModel>(() => {
    const lot = this.snapshot()?.productionLot;

    if (lot === null || lot === undefined) {
      return {
        title: 'Lotto',
        status: 'In attesa',
        metaText: null,
        label: 'Nessun lotto attivo',
        detailText: null,
        progress: null,
        isEmpty: true,
      };
    }

    return {
      title: 'Lotto',
      status: lot.status,
      metaText: null,
      label: lot.code,
      detailText: `${lot.completedOperations} operazioni completate su ${lot.totalOperations}`,
      progress: lot.progressPercentage,
      isEmpty: false,
    };
  });

  readonly workpieceCard = computed<LivePageProgressCardViewModel>(() => {
    const workpiece = this.snapshot()?.currentWorkpiece;

    if (workpiece === null || workpiece === undefined) {
      return {
        title: 'Pezzo',
        status: 'In attesa',
        metaText: null,
        label: 'Nessun pezzo attivo',
        detailText: null,
        progress: null,
        isEmpty: true,
      };
    }

    return {
      title: 'Pezzo',
      status: workpiece.status,
      metaText: `Pezzo ${workpiece.position} di ${workpiece.totalWorkpieces}`,
      label: workpiece.code,
      detailText: null,
      progress: workpiece.progressPercentage,
      isEmpty: false,
    };
  });

  readonly operationCard = computed<LivePageProgressCardViewModel>(() => {
    const operation = this.snapshot()?.currentOperation;

    if (operation === null || operation === undefined) {
      return {
        title: 'Operazione',
        status: 'In attesa',
        metaText: null,
        label: 'Nessuna operazione attiva',
        detailText: null,
        progress: null,
        isEmpty: true,
      };
    }

    return {
      title: 'Operazione',
      status: operation.status,
      metaText: `Operazione ${operation.sequenceNumber} di ${operation.totalOperations}`,
      label: this.formatOperationType(operation.type),
      detailText: operation.currentPhase,
      progress: operation.progressPercentage,
      isEmpty: false,
    };
  });

  formatOperationType(type: string): string {
    return type.replace(/([a-z])([A-Z])/g, '$1 $2');
  }

  private snapshot(): MachineSnapshot | null {
    return this.store.snapshot();
  }

  private createProductionContext(
    snapshot: MachineSnapshot | null,
  ): LiveProductionContextViewModel {
    if (snapshot === null) {
      return {
        text: 'Nessuno snapshot caricato',
        tone: 'neutral',
      };
    }

    const hasProductionContext =
      snapshot.productionLot !== null ||
      snapshot.currentWorkpiece !== null ||
      snapshot.currentOperation !== null;

    if (!hasProductionContext) {
      if (snapshot.machine.status === 'Available') {
        return {
          text: 'Macchina disponibile · Nessun contesto caricato',
          tone: 'neutral',
        };
      }

      if (snapshot.machine.status === 'Faulted') {
        return {
          text: 'Macchina in fault · Nessun contesto caricato',
          tone: 'faulted',
        };
      }

      return {
        text: 'Nessun contesto produttivo attivo',
        tone: 'neutral',
      };
    }

    if (
      snapshot.machine.status === 'Faulted' ||
      snapshot.activeAlarms.some((alarm) => alarm.isBlocking)
    ) {
      return {
        text: 'Lavorazione bloccata',
        tone: 'faulted',
      };
    }

    if (snapshot.machine.status === 'Paused' || snapshot.currentOperation?.status === 'Paused') {
      return {
        text: 'Lavorazione in pausa',
        tone: 'paused',
      };
    }

    return {
      text: 'Lavorazione in corso',
      tone: 'active',
    };
  }
}
