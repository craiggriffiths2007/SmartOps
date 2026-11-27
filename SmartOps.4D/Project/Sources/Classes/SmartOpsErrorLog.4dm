// JSON lines outside the web root. API requests are serialized by SmartOpsHTTP.
Function write($tenant : Integer; $entry : Object) : Boolean
	Try
		var $folder:=Folder(fk logs folder)
		If (Not($folder.exists))
			$folder.create()
		End if
		var $name:="smartops-errors-"+Substring($entry.timestamp;1;10)+"-"+String($tenant)+".jsonl"
		var $handle:=$folder.file($name).open("append")
		$handle.writeLine(JSON Stringify($entry))
		return True
	Catch
		// A full/unwritable disk must not open another dialog or hide the API error.
		return False
	End try

Function read($tenant : Integer) : Object
	var $items:=New collection
	var $today:=Date(Substring(Timestamp;1;10)+"T00:00:00")
	var $day : Integer
	For ($day;0;6)
		var $date:=Substring(String($today-$day;ISO date);1;10)
		var $name:="smartops-errors-"+$date+"-"+String($tenant)+".jsonl"
		var $file:=Folder(fk logs folder).file($name)
		If ($file.exists)
			var $lines:=Split string($file.getText();Char(10))
			var $i : Integer
			For ($i;$lines.length-1;0;-1)
				If ($items.length>=200)
					break
				End if
				If (Length(Trim($lines[$i]))>0)
					Try
						var $entry:=JSON Parse($lines[$i])
						If ($entry.tenantId=$tenant)
							$items.push($entry)
						End if
					Catch
						// Ignore a partial final line from an interrupted write.
					End try
				End if
			End for
		End if
		If ($items.length>=200)
			break
		End if
	End for
	return New object("entries";$items)
