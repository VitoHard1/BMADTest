import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { EventService } from '../../services/event.service';
import { EventFormComponent } from './event-form.component';

describe('EventFormComponent', () => {
  let fixture: ComponentFixture<EventFormComponent>;
  let component: EventFormComponent;
  let mockEventService: { createEvent: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    mockEventService = {
      createEvent: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [EventFormComponent],
      providers: [{ provide: EventService, useValue: mockEventService }]
    }).compileComponents();

    fixture = TestBed.createComponent(EventFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should not submit when userId is empty', () => {
    mockEventService.createEvent.mockReturnValue(
      of({ publishedCount: 1, eventIds: ['id-1'] })
    );

    (component as any).submit('ViewCar');

    expect(mockEventService.createEvent).not.toHaveBeenCalled();
    expect((component as any).form.controls.userId.touched).toBe(true);
  });

  it('should auto-fill description when car changes and submit ViewCar payload', () => {
    mockEventService.createEvent.mockReturnValue(
      of({ publishedCount: 1, eventIds: ['id-1'] })
    );

    (component as any).form.controls.userId.setValue(' user-1 ');
    (component as any).form.controls.carId.setValue('car-2');
    expect((component as any).form.controls.description.value).toBe('Viewing car-2 VW Golf');
    (component as any).submit('ViewCar');

    expect(mockEventService.createEvent).toHaveBeenCalledWith({
      userId: 'user-1',
      action: 'ViewCar',
      carId: 'car-2',
      description: 'Viewing car-2 VW Golf'
    });
  });

  it('should submit ReserveCar payload with reserve description', () => {
    mockEventService.createEvent.mockReturnValue(
      of({ publishedCount: 2, eventIds: ['id-1', 'id-2'] })
    );

    (component as any).form.controls.userId.setValue('user-1');
    (component as any).form.controls.carId.setValue('car-2');
    (component as any).submit('ReserveCar');

    expect(mockEventService.createEvent).toHaveBeenCalledWith({
      userId: 'user-1',
      action: 'ReserveCar',
      carId: 'car-2',
      description: 'Reserving car-2 VW Golf'
    });
  });

  it('should render success message after successful submit', () => {
    mockEventService.createEvent.mockReturnValue(
      of({ publishedCount: 2, eventIds: ['id-1', 'id-2'] })
    );

    (component as any).form.controls.userId.setValue('user-1');
    (component as any).submit('ViewCar');

    expect((component as any).successMessage()).toBe('Success: published 2 event(s). IDs: id-1, id-2');
  });

  it('should render API error message when request fails', () => {
    const error = new HttpErrorResponse({
      status: 503,
      error: { detail: 'Failed to publish events to the message queue.' }
    });
    mockEventService.createEvent.mockReturnValue(throwError(() => error));

    (component as any).form.controls.userId.setValue('user-1');
    (component as any).submit('ViewCar');

    expect((component as any).errorMessage()).toBe('Failed to publish events to the message queue.');
  });

  it('should emit eventCreated after successful submit', () => {
    mockEventService.createEvent.mockReturnValue(
      of({ publishedCount: 1, eventIds: ['id-1'] })
    );
    const emitSpy = vi.spyOn((component as any).eventCreated, 'emit');

    (component as any).form.controls.userId.setValue('user-1');
    (component as any).submit('ViewCar');

    expect(emitSpy).toHaveBeenCalledTimes(1);
  });

  it('should re-enable action buttons after request completes', () => {
    const result$ = new Subject<{ publishedCount: number; eventIds: string[] }>();
    mockEventService.createEvent.mockReturnValue(result$.asObservable());

    (component as any).form.controls.userId.setValue('user-1');
    (component as any).submit('ReserveCar');
    expect((component as any).isSubmitting()).toBe(true);

    result$.next({ publishedCount: 2, eventIds: ['id-1', 'id-2'] });
    result$.complete();

    expect((component as any).isSubmitting()).toBe(false);
  });
});
