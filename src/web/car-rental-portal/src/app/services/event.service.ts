import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { CreateEventRequestModel } from '../models/create-event-request.model';
import { EventType, EventModel } from '../models/event.model';

export interface CreateEventResponseModel {
  publishedCount: number;
  eventIds: string[];
}

export interface GetEventsQueryModel {
  userId?: string;
  type?: EventType;
  from?: string;
  to?: string;
  sort?: 'createdAt_desc' | 'createdAt_asc';
  page?: number;
  pageSize?: number;
}

export interface GetEventsResponseModel {
  items: EventModel[];
  totalCount: number;
  page: number;
  pageSize: number;
}

interface CreateEventApiRequest extends Omit<CreateEventRequestModel, 'action'> {
  action: number;
}

@Injectable({ providedIn: 'root' })
export class EventService {
  private readonly http = inject(HttpClient);
  private readonly eventsUrl = `${environment.apiBaseUrl}/api/events`;
  private readonly actionMap: Record<CreateEventRequestModel['action'], number> = {
    ViewCar: 0,
    ReserveCar: 1
  };

  createEvent(request: CreateEventRequestModel): Observable<CreateEventResponseModel> {
    const payload: CreateEventApiRequest = {
      ...request,
      action: this.actionMap[request.action]
    };

    return this.http.post<CreateEventResponseModel>(this.eventsUrl, payload);
  }

  getEvents(query: GetEventsQueryModel = {}): Observable<GetEventsResponseModel> {
    let params = new HttpParams();

    if (query.userId) params = params.set('userId', query.userId);
    if (query.type) params = params.set('type', query.type);
    if (query.from) params = params.set('from', query.from);
    if (query.to) params = params.set('to', query.to);
    if (query.sort) params = params.set('sort', query.sort);
    if (query.page !== undefined) params = params.set('page', String(query.page));
    if (query.pageSize !== undefined) params = params.set('pageSize', String(query.pageSize));

    return this.http.get<GetEventsResponseModel>(this.eventsUrl, { params });
  }
}
