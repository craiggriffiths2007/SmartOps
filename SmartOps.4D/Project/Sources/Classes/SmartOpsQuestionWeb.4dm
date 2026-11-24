// Legacy /questions URL now opens the original rich browser editor.
// No separate login or question-session cookie is required.
Function handle($request : 4D.IncomingMessage) : 4D.OutgoingMessage
	var $response:=4D.OutgoingMessage.new()
	$response.setStatus(302)
	$response.setHeader("Location"; "/question-editor.html")
	$response.setHeader("Cache-Control"; "no-store")
	return $response
