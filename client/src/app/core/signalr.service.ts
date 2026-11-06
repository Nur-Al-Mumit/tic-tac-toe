import { Injectable, inject, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { OnlinePlayerDto } from './models';
import { TokenStorage } from './token.storage';

export type ConnectionState = 'connected' | 'reconnecting' | 'disconnected';

/**
 * Wraps the /hubs/game hub connection: typed events to callbacks, automatic reconnect,
 * connection state as a signal for the UI badge. One instance per game page.
 */
@Injectable({ providedIn: 'root' })
export class SignalRService {
  private readonly tokenStorage = inject(TokenStorage);
  private connection: signalR.HubConnection | null = null;

  readonly state = signal<ConnectionState>('disconnected');

  async connect(handlers: {
    onGameStarted?: (game: unknown) => void;
    onMoveMade?: (game: unknown) => void;
    onGameFinished?: (game: unknown) => void;
    onGameCancelled?: (payload: { gameId: string }) => void;
    onGameCreated?: (game: unknown) => void;
    onGameUpdated?: (game: unknown) => void;
    onGameRemoved?: (payload: { gameId: string }) => void;
    onPresenceChanged?: (payload: { onlineCount: number; players: OnlinePlayerDto[] }) => void;
    onOpponentConnection?: (payload: { playerId: string; connected: boolean }) => void;
  }): Promise<void> {
    await this.disconnect();

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/game', { accessTokenFactory: () => this.tokenStorage.accessToken() ?? '' })
      .withAutomaticReconnect([0, 1000, 3000, 5000])
      .build();

    this.connection.on('GameStarted', (game) => handlers.onGameStarted?.(game));
    this.connection.on('MoveMade', (game) => handlers.onMoveMade?.(game));
    this.connection.on('GameFinished', (game) => handlers.onGameFinished?.(game));
    this.connection.on('GameCancelled', (payload) => handlers.onGameCancelled?.(payload));
    this.connection.on('GameCreated', (game) => handlers.onGameCreated?.(game));
    this.connection.on('GameUpdated', (game) => handlers.onGameUpdated?.(game));
    this.connection.on('GameRemoved', (payload) => handlers.onGameRemoved?.(payload));
    this.connection.on('PresenceChanged', (payload) => handlers.onPresenceChanged?.(payload));
    this.connection.on('OpponentConnection', (payload) => handlers.onOpponentConnection?.(payload));

    this.connection.onreconnecting(() => this.state.set('reconnecting'));
    this.connection.onreconnected(() => this.state.set('connected'));
    this.connection.onclose(() => this.state.set('disconnected'));

    await this.connection.start();
    this.state.set('connected');
  }

  joinGameRoom(gameId: string): Promise<void> {
    return this.connection?.invoke('JoinGameRoom', gameId) ?? Promise.resolve();
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }

    this.state.set('disconnected');
  }
}
