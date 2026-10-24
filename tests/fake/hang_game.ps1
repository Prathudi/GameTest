# A fake game that is alive but stuck: it reports a port and accepts connections,
# but never answers a request. Used to test how the runner handles a hung game.
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
[Console]::Out.WriteLine("GameTest: agent listening on 127.0.0.1:$port")
[Console]::Out.Flush()
$clients = New-Object System.Collections.Generic.List[object]
while ($true) {
    if ($listener.Pending()) { $clients.Add($listener.AcceptTcpClient()) }
    Start-Sleep -Milliseconds 100
}
