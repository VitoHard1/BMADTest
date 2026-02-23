export type EventAction = 'ViewCar' | 'ReserveCar';
export type CarId = 'car-1' | 'car-2';

export interface CreateEventRequestModel {
  userId: string;
  action: EventAction;
  carId: CarId;
  description?: string;
}
