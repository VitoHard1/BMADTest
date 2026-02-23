# FN-01 Verification

This verification pack covers the FN-01 checks:

- End-to-end flow: `POST -> queue/channel -> consumer -> DB -> GET`
- Duplicate message behavior (idempotent no-op)
- Failure propagation behavior (retry/dead-letter path in Service Bus mode)

## Prerequisites

1. API is running in local fallback mode (`ServiceBus:ConnectionString` empty).
2. API URL is reachable (`http://localhost:5113` by default).
3. Function project is updated with FN-01 steps 1-5.

## Automated smoke check (end-to-end)

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\verification\verify-fn01.ps1
```

Optional parameters:

```powershell
powershell -ExecutionPolicy Bypass -File .\verification\verify-fn01.ps1 -BaseUrl "http://localhost:5113" -ProcessingDelaySeconds 3
```

Expected result:

- Script exits successfully.
- Output confirms at least 3 events persisted for one generated test user.
- Returned event types include `PageView`, `Click`, `Purchase`.

## Duplicate handling check (manual)

Goal: verify duplicate `Event.Id` is treated as success and logged as ignored.

1. Run system in Service Bus mode (real queue).
2. Send the same serialized event message twice with identical `id`.
3. Observe logs from consumer:
   - First attempt: persisted log.
   - Second attempt: `Duplicate event {EventId} ignored (already exists)`.
4. Query events and confirm only one row exists for that `id`.

## Failure propagation check (manual)

Goal: verify persistence failures are not swallowed.

1. Start processing with queue connected.
2. Make DB unavailable (for example, invalid DB connection or DB service down).
3. Send a valid message.
4. Observe consumer logs:
   - Error is logged.
   - Exception is rethrown.
5. In Service Bus mode, verify message retry attempts and dead-letter behavior after max delivery count.
