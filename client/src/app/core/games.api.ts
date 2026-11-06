import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { GameDto, OnlinePlayerDto } from './models';

/** Thin typed wrapper over /api/v1/games (api-spec §2) — no business logic here. */
@Injectable({ providedIn: 'root' })
export class GamesApi {
  private readonly http = inject(HttpClient);

  list(): Promise<GameDto[]> {
    return firstValueFrom(this.http.get<GameDto[]>('/api/v1/games'));
  }

  byId(id: string): Promise<GameDto> {
    return firstValueFrom(this.http.get<GameDto>(`/api/v1/games/${id}`));
  }

  create(): Promise<GameDto> {
    return firstValueFrom(this.http.post<GameDto>('/api/v1/games', {}));
  }

  join(id: string): Promise<GameDto> {
    return firstValueFrom(this.http.post<GameDto>(`/api/v1/games/${id}/join`, {}));
  }

  move(id: string, position: number): Promise<GameDto> {
    return firstValueFrom(this.http.post<GameDto>(`/api/v1/games/${id}/moves`, { position }));
  }

  cancel(id: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/v1/games/${id}/cancel`, {}));
  }

  leave(id: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/v1/games/${id}/leave`, {}));
  }

  active(): Promise<GameDto | null> {
    return firstValueFrom(this.http.get<GameDto | null>('/api/v1/games/active'));
  }

  online(): Promise<OnlinePlayerDto[]> {
    return firstValueFrom(this.http.get<OnlinePlayerDto[]>('/api/v1/players/online'));
  }
}
