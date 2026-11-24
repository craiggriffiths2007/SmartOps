// Legacy /questions URL now opens the integrated, authenticated workspace editor.
// No separate login or question-session cookie is required.
Function handle($request : 4D.IncomingMessage) : 4D.OutgoingMessage
	var $response:=4D.OutgoingMessage.new()
	$response.setStatus(302)
	$response.setHeader("Location"; "/#questions")
	$response.setHeader("Cache-Control"; "no-store")
	return $response
