import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { EventType, EventModel } from '../../models/event.model';
import { EventService, GetEventsQueryModel } from '../../services/event.service';

type ProblemDetailsError = {
  detail?: string;
  errors?: Record<string, string[]>;
};

type TypeFilterOption = 'All' | EventType;

@Component({
  selector: 'app-events-table',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './events-table.component.html',
  styleUrl: './events-table.component.scss'
})
export class EventsTableComponent implements OnInit {
  protected readonly typeOptions: TypeFilterOption[] = ['All', 'PageView', 'Click', 'Purchase'];

  protected readonly filterForm;

  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly events = signal<EventModel[]>([]);

  constructor(
    private readonly fb: FormBuilder,
    private readonly eventService: EventService
  ) {
    this.filterForm = this.fb.nonNullable.group({
      userId: [''],
      type: ['All' as TypeFilterOption]
    });
  }

  ngOnInit(): void {
    this.loadEvents();
  }

  protected applyFilters(): void {
    this.loadEvents();
  }

  refresh(): void {
    this.loadEvents();
  }

  private loadEvents(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    this.eventService.getEvents(this.buildQuery()).subscribe({
      next: (response) => {
        this.events.set(response.items);
        this.isLoading.set(false);
      },
      error: (error: unknown) => {
        this.events.set([]);
        this.errorMessage.set(this.getErrorMessage(error));
        this.isLoading.set(false);
      }
    });
  }

  private buildQuery(): GetEventsQueryModel {
    const raw = this.filterForm.getRawValue();
    const query: GetEventsQueryModel = { sort: 'createdAt_desc' };
    const userId = raw.userId.trim();

    if (userId.length > 0) {
      query.userId = userId;
    }

    if (raw.type !== 'All') {
      query.type = raw.type;
    }

    return query;
  }

  private getErrorMessage(error: unknown): string {
    if (!(error instanceof HttpErrorResponse)) {
      return 'Unexpected error while loading events.';
    }

    const problem = error.error as ProblemDetailsError;
    if (problem?.detail) {
      return problem.detail;
    }

    if (problem?.errors) {
      for (const fieldErrors of Object.values(problem.errors)) {
        if (fieldErrors.length > 0) {
          return fieldErrors[0];
        }
      }
    }

    return `Failed to load events (status ${error.status}).`;
  }
}
