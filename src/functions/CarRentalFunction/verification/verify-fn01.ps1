param(
    [string]$BaseUrl = "http://localhost:5113",
    [int]$ProcessingDelaySeconds = 2
)

$ErrorActionPreference = "Stop"

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

Write-Host "FN-01 verification started against $BaseUrl"

$userId = "fn01-" + [guid]::NewGuid().ToString("N").Substring(0, 8)

$viewBody = @{
    userId = $userId
    action = "ViewCar"
    carId = "car-1"
} | ConvertTo-Json

$viewResponse = Invoke-RestMethod -Uri "$BaseUrl/api/events" -Method Post -ContentType "application/json" -Body $viewBody
Assert-True ($viewResponse.publishedCount -eq 1) "Expected ViewCar publishedCount=1."
Assert-True ($viewResponse.eventIds.Count -eq 1) "Expected one event id for ViewCar."

Start-Sleep -Seconds $ProcessingDelaySeconds

$reserveBody = @{
    userId = $userId
    action = "ReserveCar"
    carId = "car-2"
} | ConvertTo-Json

$reserveResponse = Invoke-RestMethod -Uri "$BaseUrl/api/events" -Method Post -ContentType "application/json" -Body $reserveBody
Assert-True ($reserveResponse.publishedCount -eq 2) "Expected ReserveCar publishedCount=2."
Assert-True ($reserveResponse.eventIds.Count -eq 2) "Expected two event ids for ReserveCar."

Start-Sleep -Seconds $ProcessingDelaySeconds

$query = "$BaseUrl/api/events?userId=$userId&sort=createdAt_desc&page=1&pageSize=50"
$eventsResponse = Invoke-RestMethod -Uri $query -Method Get

Assert-True ($eventsResponse.totalCount -ge 3) "Expected at least 3 persisted events for the test user."

$types = @($eventsResponse.items | ForEach-Object { $_.type })
Assert-True (($types -contains "PageView")) "Expected a PageView event."
Assert-True (($types -contains "Click")) "Expected a Click event."
Assert-True (($types -contains "Purchase")) "Expected a Purchase event."

Write-Host "FN-01 verification succeeded."
Write-Host "UserId: $userId"
Write-Host "Total persisted events found: $($eventsResponse.totalCount)"
