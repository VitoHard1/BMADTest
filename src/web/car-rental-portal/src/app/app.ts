import { Component } from '@angular/core';
import { EventFormComponent } from './components/event-form/event-form.component';
import { EventsTableComponent } from './components/events-table/events-table.component';

@Component({
  selector: 'app-root',
  imports: [EventFormComponent, EventsTableComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected readonly title = 'Reenbit Event Hub';
}
