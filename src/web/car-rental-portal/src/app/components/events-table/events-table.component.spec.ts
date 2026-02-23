import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { EventService } from '../../services/event.service';
import { EventsTableComponent } from './events-table.component';

describe('EventsTableComponent', () => {
  let fixture: ComponentFixture<EventsTableComponent>;
  let component: EventsTableComponent;
  let mockEventService: { getEvents: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    mockEventService = {
      getEvents: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [EventsTableComponent],
      providers: [{ provide: EventService, useValue: mockEventService }]
    }).compileComponents();
  });

  it('should load events on init with default descending sort', () => {
    mockEventService.getEvents.mockReturnValue(
      of({
        items: [
          {
            id: 'evt-1',
            userId: 'user-1',
            type: 'PageView',
            description: 'Viewed car-1 Toyota Corolla',
            createdAt: '2026-02-20T10:00:00Z'
          }
        ],
        totalCount: 1,
        page: 1,
        pageSize: 50
      })
    );

    fixture = TestBed.createComponent(EventsTableComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(mockEventService.getEvents).toHaveBeenCalledWith({ sort: 'createdAt_desc' });
    expect(fixture.nativeElement.textContent).toContain('evt-1');
  });

  it('should apply userId and type filter when submitted', () => {
    mockEventService.getEvents.mockReturnValue(
      of({ items: [], totalCount: 0, page: 1, pageSize: 50 })
    );

    fixture = TestBed.createComponent(EventsTableComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    mockEventService.getEvents.mockClear();

    (component as any).filterForm.controls.userId.setValue('user-1');
    (component as any).filterForm.controls.type.setValue('Purchase');
    fixture.detectChanges();

    const filterForm = fixture.nativeElement.querySelector('form');
    filterForm.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(mockEventService.getEvents).toHaveBeenCalledWith({
      sort: 'createdAt_desc',
      userId: 'user-1',
      type: 'Purchase'
    });
  });

  it('should omit type query when type filter is All', () => {
    mockEventService.getEvents.mockReturnValue(
      of({ items: [], totalCount: 0, page: 1, pageSize: 50 })
    );

    fixture = TestBed.createComponent(EventsTableComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    mockEventService.getEvents.mockClear();

    (component as any).filterForm.controls.userId.setValue('user-1');
    (component as any).filterForm.controls.type.setValue('All');

    const filterForm = fixture.nativeElement.querySelector('form');
    filterForm.dispatchEvent(new Event('submit'));

    expect(mockEventService.getEvents).toHaveBeenCalledWith({
      sort: 'createdAt_desc',
      userId: 'user-1'
    });
  });

  it('should refresh with current filters', () => {
    mockEventService.getEvents.mockReturnValue(
      of({ items: [], totalCount: 0, page: 1, pageSize: 50 })
    );

    fixture = TestBed.createComponent(EventsTableComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    mockEventService.getEvents.mockClear();

    (component as any).filterForm.controls.userId.setValue('user-9');
    (component as any).filterForm.controls.type.setValue('Click');
    fixture.detectChanges();

    const refreshButton = fixture.nativeElement.querySelector('.heading button') as HTMLButtonElement;
    refreshButton.click();

    expect(mockEventService.getEvents).toHaveBeenCalledWith({
      sort: 'createdAt_desc',
      userId: 'user-9',
      type: 'Click'
    });
  });

  it('should show empty-state text when no events returned', () => {
    mockEventService.getEvents.mockReturnValue(
      of({ items: [], totalCount: 0, page: 1, pageSize: 50 })
    );

    fixture = TestBed.createComponent(EventsTableComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No events found for current filters.');
  });

  it('should show API error details when loading fails', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { detail: 'Invalid query parameters.' }
    });
    mockEventService.getEvents.mockReturnValue(throwError(() => error));

    fixture = TestBed.createComponent(EventsTableComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Invalid query parameters.');
  });

  it('should show loading state while request is in progress', () => {
    const result$ = new Subject<{
      items: never[];
      totalCount: number;
      page: number;
      pageSize: number;
    }>();
    mockEventService.getEvents.mockReturnValue(result$.asObservable());

    fixture = TestBed.createComponent(EventsTableComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Loading events...');

    result$.next({ items: [], totalCount: 0, page: 1, pageSize: 50 });
    result$.complete();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('Loading events...');
  });
});
