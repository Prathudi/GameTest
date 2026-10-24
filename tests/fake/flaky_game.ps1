# A fake game that behaves differently on alternate launches: every second launch exits at once
# (a "crash"); the other launches report a port and then exit after 3 seconds without answering.
$counter = Join-Path $PSScriptRoot "flaky_counter.txt"
$n = 0
if (Test-Path $counter) { $n = [int](Get-Content $counter -Raw) }
$n++
Set-Content $counter $n
if ($n % 2 -eq 0) {
    [Console]::Error.WriteLine("flaky_game: pretending to crash on launch number $n")
    exit 4
}
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
[Console]::Out.WriteLine("GameTest: agent listening on 127.0.0.1:$($listener.LocalEndpoint.Port)")
[Console]::Out.Flush()
Start-Sleep -Seconds 3
exit 0
