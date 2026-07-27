param(
    [string]$ReferencePath = "Assets/Art/Textures/Road.png",
    [string]$OutputDirectory = "tmp/imagegen/road_texture"
)

$ErrorActionPreference = "Stop"
$baseUrl = "https://canvas.dxx.cn"
$accessPack = "C:\Users\Administrator\Downloads\gorilla-agent-access-2026-07-27T09-01-49-183Z.md"
$accessText = Get-Content -Raw -Encoding UTF8 $accessPack
$apiKey = [regex]::Match($accessText, '(?m)^- API key:\s*(.+)$').Groups[1].Value.Trim()
if (-not $apiKey) {
    throw "Could not read Gorilla API key from the access pack."
}

$headers = @{ Authorization = "Bearer $apiKey" }
$resolvedReference = (Resolve-Path $ReferencePath).Path
$resolvedOutput = Join-Path (Get-Location) $OutputDirectory
New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null

$bytes = [System.IO.File]::ReadAllBytes($resolvedReference)
$uploadBody = @{
    base64 = [Convert]::ToBase64String($bytes)
    mimeType = "image/png"
    filename = "Road_Current_Reference.png"
    purpose = "reference_image"
} | ConvertTo-Json -Compress
$uploadBytes = [System.Text.Encoding]::UTF8.GetBytes($uploadBody)
$upload = Invoke-RestMethod `
    -Uri "$baseUrl/api/media/upload-json" `
    -Headers $headers `
    -Method Post `
    -ContentType "application/json; charset=utf-8" `
    -Body $uploadBytes

$assetUrl = $upload.file.assetUrl
if (-not $assetUrl) {
    throw "Gorilla media upload did not return file.assetUrl."
}

$prompt = @"
Transform the reference into a high-quality seamless asphalt road albedo texture for Unity.
Strict orthographic top-down surface, no perspective, no directional lighting, perfectly tileable on all four edges.
Use a deeper neutral charcoal gray than the reference without becoming black. Keep crisp fine aggregate and subtle micro-surface depth.
The asphalt is dry, rough, and low-gloss. Fine dense aggregate is evenly distributed with very few bright white stones.
Remove the obvious tire marks, long scratches, and large stains from the reference so repeated tiling has no focal pattern.
No road markings, arrows, curbs, cracks, potholes, water, oil, moss, leaves, text, watermark, vignette, or blur.
Output only a square road material texture suitable for 8x8 tiling in a game.
"@

$modules = (Invoke-RestMethod -Uri "$baseUrl/api/mcp/modules" -Headers $headers -Method Get).modules
$module = $modules | Where-Object { $_.id -eq "module_1774928100822_ltprtx" } | Select-Object -First 1
if (-not $module) {
    throw "Gorilla image-edit module was not found."
}

$textAlias = ($module.exposedInputs | Where-Object { $_.dataType -eq "text" -and $_.required } | Select-Object -First 1).alias
$imageAlias = ($module.exposedInputs | Where-Object { $_.dataType -eq "image" -and $_.required } | Select-Object -First 1).alias
$request = @{
    moduleId = $module.id
    moduleName = $module.name
    inputs = @{
        $textAlias = $prompt
        $imageAlias = $assetUrl
    }
    config = @{
        aspectRatio = "1:1"
        resolution = "2K"
    }
} | ConvertTo-Json -Depth 8 -Compress
$requestBytes = [System.Text.Encoding]::UTF8.GetBytes($request)
$task = Invoke-RestMethod `
    -Uri "$baseUrl/api/execute/playground-tasks" `
    -Headers $headers `
    -Method Post `
    -ContentType "application/json; charset=utf-8" `
    -Body $requestBytes

$taskId = if ($task.taskId) {
    $task.taskId
} elseif ($task.id) {
    $task.id
} elseif ($task.task.id) {
    $task.task.id
} elseif ($task.data.taskId) {
    $task.data.taskId
} elseif ($task.data.id) {
    $task.data.id
} else {
    $null
}
if (-not $taskId) {
    Write-Output ($task | ConvertTo-Json -Depth 8)
    throw "Gorilla did not return an async task id."
}

Write-Output "Submitted Gorilla task $taskId"
$deadline = (Get-Date).AddMinutes(12)
do {
    Start-Sleep -Seconds 4
    $pollResponse = Invoke-RestMethod -Uri "$baseUrl/api/execute/playground-tasks/$taskId" -Headers $headers -Method Get
    $status = if ($pollResponse.task) { $pollResponse.task } else { $pollResponse }
    Write-Output "Status: $($status.status)"
    if ($status.status -eq "error" -or $status.status -eq "failed") {
        throw "Gorilla task failed: $($status.errorMsg)"
    }
    if ((Get-Date) -gt $deadline) {
        throw "Gorilla task timed out after 12 minutes."
    }
} until ($status.status -eq "completed")

$outputUrl = $null
$candidates = @($status.result.outputs, $status.outputs, $status.result.output) | Where-Object { $_ }
$json = $candidates | ConvertTo-Json -Depth 12 -Compress
$urlMatch = [regex]::Match($json, '(https?://[^"\\]+|/assets/[^"\\]+)')
if ($urlMatch.Success) {
    $outputUrl = $urlMatch.Value
}
if (-not $outputUrl) {
    throw "Could not locate an image URL in the Gorilla task output."
}
if ($outputUrl.StartsWith('/')) {
    $outputUrl = "$baseUrl$outputUrl"
}

$sourceOutput = Join-Path $resolvedOutput "Road_Gorilla_Source.png"
Invoke-WebRequest -Uri $outputUrl -Headers $headers -OutFile $sourceOutput
Write-Output "Downloaded: $sourceOutput"
