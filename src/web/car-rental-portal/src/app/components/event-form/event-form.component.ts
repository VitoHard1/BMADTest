import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  CarId,
  CreateEventRequestModel,
  EventAction
} from '../../models/create-event-request.model';
import { EventService } from '../../services/event.service';

type ProblemDetailsError = {
  detail?: string;
  errors?: Record<string, string[]>;
};

type CarOption = {
  id: CarId;
  name: string;
};

@Component({
  selector: 'app-event-form',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './event-form.component.html',
  styleUrl: './event-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EventFormComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly eventService = inject(EventService);
  private readonly fb = inject(FormBuilder);

  private readonly cars: CarOption[] = [
    { id: 'car-1', name: 'Toyota Corolla' },
    { id: 'car-2', name: 'VW Golf' }
  ];

  readonly eventCreated = output<void>();

  protected readonly isSubmitting = signal(false);
  protected readonly successMessage = signal('');
  protected readonly errorMessage = signal('');

  private descriptionEdited = false;
  private currentAction: EventAction = 'ViewCar';

  protected readonly form = this.fb.nonNullable.group({
    userId: ['', [Validators.required, Validators.maxLength(100)]],
    carId: ['car-1' as CarId, [Validators.required]],
    description: ['']
  });

  constructor() {
    this.applyDescriptionTemplate('ViewCar');

    this.form.controls.carId.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (!this.descriptionEdited) {
          this.applyDescriptionTemplate(this.currentAction);
        }
      });
  }

  protected submit(action: EventAction): void {
    this.currentAction = action;

    this.successMessage.set('');
    this.errorMessage.set('');

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    if (!this.descriptionEdited) {
      this.applyDescriptionTemplate(action);
    }

    const request = this.buildRequest(action);

    this.isSubmitting.set(true);
    this.eventService
      .createEvent(request)
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (response) => {
          const ids = response.eventIds.join(', ');
          this.successMessage.set(
            `Success: published ${response.publishedCount} event(s). IDs: ${ids}`
          );
          this.errorMessage.set('');
          this.eventCreated.emit();
        },
        error: (error: unknown) => {
          this.successMessage.set('');
          this.errorMessage.set(this.getErrorMessage(error));
        }
      });
  }

  protected onDescriptionInput(): void {
    this.descriptionEdited = true;
  }

  protected hasError(controlName: 'userId' | 'carId', errorName: 'required'): boolean {
    const control = this.form.controls[controlName];
    return control.hasError(errorName) && (control.touched || control.dirty);
  }

  private buildRequest(action: EventAction): CreateEventRequestModel {
    const raw = this.form.getRawValue();
    const description = raw.description.trim();

    return {
      userId: raw.userId.trim(),
      action,
      carId: raw.carId,
      description: description.length > 0 ? description : undefined
    };
  }

  private getErrorMessage(error: unknown): string {
    if (!(error instanceof HttpErrorResponse)) {
      return 'Unexpected error while submitting the event.';
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

    return `Request failed with status ${error.status}.`;
  }

  private applyDescriptionTemplate(action: EventAction): void {
    const carId = this.form.controls.carId.value;
    const carName = this.getCarName(carId);
    const description =
      action === 'ViewCar'
        ? `Viewing ${carId} ${carName}`
        : `Reserving ${carId} ${carName}`;

    this.form.controls.description.setValue(description);
  }

  private getCarName(carId: CarId): string {
    const car = this.cars.find((c) => c.id === carId);
    return car?.name ?? '';
  }
}
