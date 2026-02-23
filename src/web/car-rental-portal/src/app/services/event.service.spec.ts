import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { EventService } from './event.service';

describe('EventService', () => {
  let service: EventService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [EventService, provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(EventService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should map ViewCar action to numeric enum value in POST payload', () => {
    service
      .createEvent({
        userId: 'user-1',
        action: 'ViewCar',
        carId: 'car-1',
        description: 'Viewing car-1 Toyota Corolla'
      })
      .subscribe();

    const req = httpMock.expectOne('http://localhost:5113/api/events');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      userId: 'user-1',
      action: 0,
      carId: 'car-1',
      description: 'Viewing car-1 Toyota Corolla'
    });

    req.flush({ publishedCount: 1, eventIds: ['evt-1'] });
  });

  it('should map ReserveCar action to numeric enum value in POST payload', () => {
    service
      .createEvent({
        userId: 'user-2',
        action: 'ReserveCar',
        carId: 'car-2',
        description: 'Reserving car-2 VW Golf'
      })
      .subscribe();

    const req = httpMock.expectOne('http://localhost:5113/api/events');
    expect(req.request.body.action).toBe(1);
    req.flush({ publishedCount: 2, eventIds: ['evt-1', 'evt-2'] });
  });

  it('should send query params for active filters in GET request', () => {
    service
      .getEvents({
        userId: 'user-1',
        type: 'Purchase',
        sort: 'createdAt_desc',
        page: 2,
        pageSize: 25
      })
      .subscribe();

    const req = httpMock.expectOne((request) => request.url === 'http://localhost:5113/api/events');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('userId')).toBe('user-1');
    expect(req.request.params.get('type')).toBe('Purchase');
    expect(req.request.params.get('sort')).toBe('createdAt_desc');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('25');

    req.flush({ items: [], totalCount: 0, page: 2, pageSize: 25 });
  });

  it('should omit empty optional params in GET request', () => {
    service.getEvents({}).subscribe();

    const req = httpMock.expectOne('http://localhost:5113/api/events');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.keys().length).toBe(0);

    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 50 });
  });
});
