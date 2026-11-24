// Standalone editor assets served by the 4D HTTP server. No .NET process required.
// The page is public shell only: all question data and mutations use bearer auth.
Class constructor()

Function handle($request : 4D.IncomingMessage) : 4D.OutgoingMessage
	var $path:=$request.urlPath.join("/")
	var $file:=""
	var $mime:="text/html; charset=utf-8"
	Case of
		: (($path="questions") | ($path="questions/"))
			$file:="index.html"
		: ($path="questions/view.html")
			$file:="view.html"
		: ($path="questions/editor.js")
			$file:="editor.js"
			$mime:="text/javascript; charset=utf-8"
		: ($path="questions/editor.css")
			$file:="editor.css"
			$mime:="text/css; charset=utf-8"
		: ($path="questions/bootstrap.css")
			$file:="bootstrap.css"
			$mime:="text/css; charset=utf-8"
		: ($path="questions/defaults.json")
			$file:="../dynamic-questions.json"
			$mime:="application/json; charset=utf-8"
		Else
			var $notFound:=4D.OutgoingMessage.new()
			$notFound.setStatus(404)
			return $notFound
	End case
	If ($request.verb#"GET")
		var $invalid:=4D.OutgoingMessage.new()
		$invalid.setStatus(405)
		return $invalid
	End if
	var $resource:="/RESOURCES/question-editor/"+$file
	If ($path="questions/defaults.json")
		$resource:="/RESOURCES/dynamic-questions.json"
	End if
	var $response:=4D.OutgoingMessage.new()
	$response.setBody(File($resource).getText())
	$response.setStatus(200)
	$response.setHeader("Content-Type";$mime)
	$response.setHeader("X-Content-Type-Options";"nosniff")
	$response.setHeader("Cache-Control";"no-store")
	$response.setHeader("Content-Security-Policy";"default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; img-src 'self' data:; object-src 'none'; base-uri 'none'; frame-ancestors 'none'")
	return $response
