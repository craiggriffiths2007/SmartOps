// Start the native 4D HTTP API and browser workspace automatically.
// GO_StartLAN seeds a completely empty development database.
GO_StartLAN

// Open the login screen only when the HTTP web server started successfully.
If ((WEB Server).isRunning)
    GO_Open
End if
