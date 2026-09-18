$ErrorActionPreference = 'SilentlyContinue'
try {
    $raw = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
    $event = $raw | ConvertFrom-Json
    $command = $event.tool_input.command
    if ([string]::IsNullOrWhiteSpace($command)) { $command = $event.tool_input.cmd }
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
        target = [string]($event.tool_input.path ?? $event.tool_input.file ?? $event.tool_input.file_path ?? $event.tool_input.filename ?? $event.tool_input.target ?? $event.tool_input.query ?? $event.tool_input.pattern ?? '')
        tool_input = if ($null -eq $event.tool_input) { '' } else { $event.tool_input | ConvertTo-Json -Depth 12 -Compress }
        exit_code = $event.exit_code
        error = [string]$event.error
    }
    $folder = Join-Path $env:USERPROFILE '.redtrace'
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $line = $record | ConvertTo-Json -Depth 12 -Compress
    [IO.File]::AppendAllText((Join-Path $folder 'codex-events.jsonl'), $line + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
} catch { }
exit 0
