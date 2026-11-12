// One shared function serializes application requests for this single-user trial.
// ORDA transactions still protect multi-record writes and optimistic stamps.
shared singleton Class constructor()
	
shared Function handle($request : 4D.IncomingMessage) : 4D.OutgoingMessage
	var $service:=cs.SmartOpsService.new()
	var $result : 4D.OutgoingMessage
	var $body:=New object
	var $writing:=False
	var $isSurvey:=($request.urlPath[0]#"api")
	Try
		If ($request.verb#"GET")
			If (Position("application/json"; Lowercase($request.getHeader("Content-Type")))#1)
				return $service.reply(415; New object("error"; "Use application/json"))
			End if 
			Try
				$body:=$request.getJSON()
			Catch
				return $service.reply(400; New object("error"; "Invalid JSON"))
			End try
			If (Value type($body)#Is object)
				return $service.reply(400; New object("error"; "Expected a JSON object"))
			End if 
		End if 
		// Login failures must persist their lockout counter, so login owns its saves.
		$writing:=($request.verb#"GET") & ($request.urlPath.join("/")#"api/auth/login") & ($request.urlPath.join("/")#"AgentLogin")
		If ($writing)
			ds.startTransaction()
		End if 
		If ($isSurvey)
			var $mobile:=cs.SmartOpsSurveyService.new()
			// Share the service instance so the error log includes mobile stages.
			$mobile.service:=$service
			$result:=$mobile.dispatch($request; $body)
		Else 
			$result:=$service.dispatch($request; $body)
		End if 
		If ($writing)
			If ($result.status<400)
				ds.validateTransaction()
			Else 
				ds.cancelTransaction()
			End if 
		End if 
	Catch
		var $errors:=Last errors
		
		If ($writing)
			ds.cancelTransaction()
		End if 
		$result:=$service.failure($errors; $request.urlPath.join("/"); $request.verb)
	End try
	return $result