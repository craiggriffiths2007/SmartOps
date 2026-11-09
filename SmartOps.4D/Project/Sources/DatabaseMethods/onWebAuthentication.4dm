#DECLARE($url : Text; $content : Text; $clientIP : Text; $serverIP : Text; $user : Text; $password : Text) : Boolean
// Custom /api handlers bypass this method and perform their own authentication.
// Keep built-in dynamic entry points unavailable even if REST is enabled later.
var $lower:=Lowercase($url)
If ((Position("/rest";$lower)=1) | (Position("/4daction";$lower)=1) | (Position("/$";$lower)=1))
	return False
End if
return True
