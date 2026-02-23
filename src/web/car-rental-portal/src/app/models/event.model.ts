export type EventType = 'PageView' | 'Click' | 'Purchase';

export interface EventModel {
  id: string;
  userId: string;
  type: EventType;
  description: string;
  createdAt: string;
}
