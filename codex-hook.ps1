$ErrorActionPreference = 'SilentlyContinue'
try {
    $raw = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
    $event = $raw | ConvertFrom-Json
    $command = $event.tool_input.command
    if ([string]::IsNullOrWhiteSpace($command)) { $command = $event.tool_input.cmd }
    if ([string]::IsNullOrWhiteSpace($command)) { exit 0 }
    $phase = if (($event.hook_event_name -eq 'PreToolUse') -or ($event.hookEventName -eq 'PreToolUse')) { 'start' } else { 'finish' }
    $originText = "$($event.execution_location) $($event.environment) $($event.cwd)".ToLowerInvariant()
    $origin = if ($originText.Contains('remote') -or $originText.Contains('cloud')) { 'remote' } else { 'local' }
    $response = if ($null -ne $event.tool_response) { $event.tool_response } elseif ($null -ne $event.tool_output) { $event.tool_output } else { $event.tool_result }
    $record = [ordered]@{
        timestamp = [DateTime]::UtcNow.ToString('o')
        phase = $phase
        origin = $origin
        session_id = $event.session_id
        turn_id = $event.turn_id
        tool_use_id = $event.tool_use_id
        cwd = $event.cwd
        tool_name = $event.tool_name
        command = [string]$command
        output = if ($null -eq $response) { '' } elseif ($response -is [string]) { $response } else { $response | ConvertTo-Json -Depth 12 -Compress }
    }
    $folder = Join-Path $env:USERPROFILE '.redtrace'
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $line = $record | ConvertTo-Json -Depth 12 -Compress
    [IO.File]::AppendAllText((Join-Path $folder 'codex-events.jsonl'), $line + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
} catch { }
exit 0
