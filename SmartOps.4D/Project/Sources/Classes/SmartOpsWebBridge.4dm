
// Data adapter for the retained ASP.NET/Razor interface.
// 4D remains authoritative.
// Auditing temporarily disabled during integration testing.

property service : cs.SmartOpsService

Class constructor($service : cs.SmartOpsService)
	This.service:=$service
	
	
Function snapshot($account : 4D.Entity) : Object
	
	This.service.requireRole($account; "Admin,Staff")
	
	var $result:=New object("tenantId"; $account.GoTenantId; "tables"; New object; "revisions"; New object)
	var $name : Text
	
	For each ($name; This.service.schema)
		
		If ((Position("Go"; $name)#1) & (Position("AspNet"; $name)#1))
			
			var $schema:=This.service.schema[$name]
			var $rows:=New collection
			var $revisions:=New object
			var $entity : 4D.Entity
			
			For each ($entity; ds[$schema.nativeTable].query("GoTenantId = :1"; $account.GoTenantId))
				
				$rows.push(This.service.publicRow($name; $entity))
				$revisions[String($entity[$schema.key])]:=Num($entity.GoRevision)
				
			End for each 
			
			$result.tables[$name]:=$rows
			$result.revisions[$name]:=$revisions
			
		End if 
		
	End for each 
	
    $result.users:=New collection
    var $webUser : 4D.Entity
    For each ($webUser; ds.GoAccount.query("GoTenantId = :1 AND (Role = :2 OR Role = :3) AND Active = true"; $account.GoTenantId; "Admin"; "Staff"))
        $result.users.push(This.userRow($webUser))
    End for each
	$result.account:=This.userRow($account)

	$result.media:=New collection
	var $media : 4D.Entity
	
	For each ($media; ds.GoMediaFile.query("GoTenantId = :1 AND Completed = true"; $account.GoTenantId))
		
		$result.media.push(New object("Id"; $media.Id; "Filename"; $media.Filename; "ReceivedAt"; $media.ReceivedAt))
		
	End for each 
	
	return $result
	
	
// Profile metadata is stored in 4D under an account-specific key. The bearer
// token determines both the account and company; no target ID is accepted.
Function profile($account : 4D.Entity) : Object
    var $accountId:=$account.Id
    var $key:="4d-account-"+String($accountId)
    var $profile : 4D.Entity
    $profile:=ds.AspNetUsers.query("Id = :1 AND GoTenantId = :2"; $key; $account.GoTenantId).first()
    var $result:=New object("Username"; $account.Username; "PhoneNumber"; ""; "ProfilePicture"; "")
    If ($profile#Null)
        $result.PhoneNumber:=String($profile.PhoneNumber)
        // The retained catalog stores byte-array fields as base64 text.
        $result.ProfilePicture:=String($profile.ProfilePicture)
    End if
    return $result

Function saveProfile($account : 4D.Entity; $body : Object) : Object
    This.service.debugStage:="profile: validate save"
    If (Value type($body.PhoneNumber)#Is text)
        throw(400; "Phone number must be text")
    End if
    var $phone:=String($body.PhoneNumber)
    If (Length($phone)>100)
        throw(400; "Phone number must be at most 100 characters")
    End if
    var $bytes : Blob
    If (OB Is defined($body; "ProfilePicture"))
        If (Value type($body.ProfilePicture)#Is text)
            throw(400; "Profile picture must be base64 text")
        End if
        var $encoded:=String($body.ProfilePicture)
        If (Length($encoded)>11000000)
            throw(400; "Profile picture is too large")
        End if
        This.service.debugStage:="profile: decode picture"
        BASE64 DECODE($encoded; $bytes)
        If ((BLOB size($bytes)=0) | (BLOB size($bytes)>8000000))
            throw(400; "Invalid profile picture size")
        End if
    End if
    var $accountId:=$account.Id
    var $key:="4d-account-"+String($accountId)
    var $profile : 4D.Entity
    $profile:=ds.AspNetUsers.query("Id = :1 AND GoTenantId = :2"; $key; $account.GoTenantId).first()
    If ($profile=Null)
        $profile:=ds.AspNetUsers.new()
        $profile.GoTenantId:=$account.GoTenantId
        $profile.TenantId:=$account.GoTenantId
        $profile.Id:=$key
        $profile.UserName:=$account.Username
    End if
    $profile.PhoneNumber:=$body.PhoneNumber
    If (OB Is defined($body; "ProfilePicture"))
        $profile.ProfilePicture:=$encoded
        $profile.ProfilePictureUpdated:=Timestamp
    End if
    This.service.debugStage:="profile: save"
    This.service.save($profile)
    return New object("ok"; True)

Function preferences($account : 4D.Entity) : Object
    This.service.requireRole($account; "Admin,Staff")
    var $key:="4d-account-"+String($account.Id)
    var $claim : 4D.Entity
    $claim:=ds.AspNetUserClaims.query("UserId = :1 AND GoTenantId = :2 AND ClaimType = :3"; $key; $account.GoTenantId; "Contracts.CompactList").first()
    var $compact:=False
    If ($claim#Null)
        $compact:=($claim.ClaimValue="true")
    End if
    $claim:=ds.AspNetUserClaims.query("UserId = :1 AND GoTenantId = :2 AND ClaimType = :3"; $key; $account.GoTenantId; "Diary.View").first()
    var $diaryView:="month"
    If ($claim#Null)
        If (New collection("list"; "week"; "month").includes($claim.ClaimValue))
            $diaryView:=$claim.ClaimValue
        End if
    End if
    return New object("CompactContracts"; $compact; "DiaryView"; $diaryView)

Function savePreferences($account : 4D.Entity; $body : Object) : Object
    This.service.debugStage:="preferences: validate"
    This.service.requireRole($account; "Admin,Staff")
    If (OB Is defined($body; "CompactContracts"))
        If (Value type($body.CompactContracts)#Is Boolean)
            throw(400; "CompactContracts must be a boolean")
        End if
    End if
    If (OB Is defined($body; "DiaryView"))
        If (Value type($body.DiaryView)#Is text)
            throw(400; "DiaryView must be list, week or month")
        End if
        If (Not(New collection("list"; "week"; "month").includes($body.DiaryView)))
            throw(400; "DiaryView must be list, week or month")
        End if
    End if
    // The bearer account supplies the user and tenant; client-supplied IDs are ignored.
    var $key:="4d-account-"+String($account.Id)
    This.service.debugStage:="preferences: find profile"
    var $profile : 4D.Entity
    $profile:=ds.AspNetUsers.query("Id = :1 AND GoTenantId = :2"; $key; $account.GoTenantId).first()
    If ($profile=Null)
        This.service.debugStage:="preferences: create profile"
        $profile:=ds.AspNetUsers.new()
        $profile.Id:=$key
        $profile.GoTenantId:=$account.GoTenantId
        $profile.TenantId:=$account.GoTenantId
        $profile.UserName:=$account.Username
        $profile.PhoneNumber:=""
        $profile.ProfilePicture:=""
        This.service.save($profile)
    End if
    var $claim : 4D.Entity
    If (OB Is defined($body; "CompactContracts"))
        This.service.debugStage:="preferences: find claim"
        $claim:=ds.AspNetUserClaims.query("UserId = :1 AND GoTenantId = :2 AND ClaimType = :3"; $key; $account.GoTenantId; "Contracts.CompactList").first()
        If ($claim=Null)
            This.service.debugStage:="preferences: create claim"
            $claim:=ds.AspNetUserClaims.new()
            $claim.UserId:=$key
            $claim.GoTenantId:=$account.GoTenantId
            $claim.ClaimType:="Contracts.CompactList"
        End if
        $claim.ClaimValue:="false"
        If ($body.CompactContracts)
            $claim.ClaimValue:="true"
        End if
        This.service.debugStage:="preferences: save claim"
        This.service.save($claim)
    End if
    If (OB Is defined($body; "DiaryView"))
        $claim:=ds.AspNetUserClaims.query("UserId = :1 AND GoTenantId = :2 AND ClaimType = :3"; $key; $account.GoTenantId; "Diary.View").first()
        If ($claim=Null)
            $claim:=ds.AspNetUserClaims.new()
            $claim.UserId:=$key
            $claim.GoTenantId:=$account.GoTenantId
            $claim.ClaimType:="Diary.View"
        End if
        $claim.ClaimValue:=$body.DiaryView
        This.service.debugStage:="preferences: save diary view"
        This.service.save($claim)
    End if
    return New object("ok"; True)

Function userRoles($account : 4D.Entity) : Collection
    var $roles:=New collection
    var $id:=$account.Id
    var $key:="4d-account-"+String($id)
    var $profile:=ds.AspNetUsers.query("Id = :1 AND GoTenantId = :2"; $key; $account.GoTenantId).first()
    If ($profile#Null)
        If ($profile.IsApproved)
            var $membership : 4D.Entity
            For each ($membership; ds.AspNetUserRoles.query("UserId = :1 AND GoTenantId = :2"; $key; $account.GoTenantId))
                var $role:=ds.AspNetRoles.get($membership.RoleId)
                If ($role#Null)
                    $roles.push($role.Name)
                End if
            End for each
            return $roles
        End if
    End if
    If ($account.Role="Admin")
        $roles.push("Admin")
    Else
        $roles:=New collection("Users"; "Diary"; "Branch")
    End if
    return $roles

Function userRow($account : 4D.Entity) : Object
    var $result:=This.profile($account)
    var $id:=$account.Id
    $result.Id:="4d-account-"+String($id)
    $result.UserName:=$account.Username
    $result.Email:=""
    $result.FirstName:=$account.Username
    $result.LastName:=""
    $result.TenantId:=$account.GoTenantId
    $result.Revision:=$account.GoRevision
    $result.Roles:=This.userRoles($account)
    $result.LockBranch:=False
    var $lockClaim : 4D.Entity
    $lockClaim:=ds.AspNetUserClaims.query("UserId = :1 AND GoTenantId = :2 AND ClaimType = :3"; $result.Id; $account.GoTenantId; "Branch.Locked").first()
    If ($lockClaim#Null)
        $result.LockBranch:=($lockClaim.ClaimValue="true")
    End if
    $result.BranchCode:=""
    var $branchClaim : 4D.Entity
    $branchClaim:=ds.AspNetUserClaims.query("UserId = :1 AND GoTenantId = :2 AND ClaimType = :3"; $result.Id; $account.GoTenantId; "Branch.Default").first()
    If ($branchClaim#Null)
        $result.BranchCode:=String($branchClaim.ClaimValue)
    End if
    var $profile:=ds.AspNetUsers.query("Id = :1 AND GoTenantId = :2"; $result.Id; $account.GoTenantId).first()
    If ($profile#Null)
        $result.Email:=String($profile.Email)
        $result.FirstName:=String($profile.FirstName)
        $result.LastName:=String($profile.LastName)
    End if
    return $result

Function requireUserManager($actor : 4D.Entity)
    This.service.requireRole($actor; "Admin,Staff")
    If (($actor.Role#"Admin") & Not(This.userRoles($actor).includes("Users")))
        throw(403; "You do not have permission to manage users")
    End if

Function userTarget($actor : 4D.Entity; $key : Text) : 4D.Entity
    var $id:=Num(Substring($key; 12))
    If (($key#("4d-account-"+String($id))) | ($id<=0))
        throw(400; "Reload the user list before saving")
    End if
    var $target:=This.service.row("GoAccount"; $id; $actor.GoTenantId)
    If (($target.Role#"Admin") & ($target.Role#"Staff"))
        throw(403; "This account cannot be managed here")
    End if
    If (($target.Role="Admin") & ($actor.Role#"Admin"))
        throw(403; "Only an Admin can edit an Admin")
    End if
    return $target

Function saveUser($actor : 4D.Entity; $body : Object) : Object
    This.requireUserManager($actor)
    var $name:=Lowercase(This.service.requireText($body.UserName; "Username"; 200))
    var $email:=This.service.requireText($body.Email; "Email"; 256)
    If (Value type($body.Roles)#Is collection)
        throw(400; "Select valid roles")
    End if
    var $roleName : Text
    For each ($roleName; $body.Roles)
        If (Not(New collection("Admin"; "Users"; "Diary"; "Branch").includes($roleName)))
            throw(400; "Select valid roles")
        End if
        If (($roleName="Admin") & ($actor.Role#"Admin"))
            throw(403; "Only an Admin can assign Admin")
        End if
    End for each
    // Store a branch code in account claims so no native table schema change is needed.
    // Validate before writing any part of the user; blank means All.
    This.service.debugStage:="users: validate branch"
    var $branchCode:=""
    If (OB Is defined($body; "BranchCode"))
        If (Value type($body.BranchCode)#Is text)
            throw(400; "Choose a branch from your company or All")
        End if
        $branchCode:=Trim($body.BranchCode)
        If ($branchCode#"")
            var $branchSchema:=This.service.schema.Branches
            var $branch:=ds[$branchSchema.nativeTable].query("GoTenantId = :1 AND "+$branchSchema.fieldMap.Code+" = :2"; $actor.GoTenantId; $branchCode).first()
            If ($branch=Null)
                throw(400; "Choose a branch from your company or All")
            End if
        End if
    End if
    If (OB Is defined($body; "LockBranch"))
        If (Value type($body.LockBranch)#Is Boolean)
            throw(400; "Lock branch must be a boolean")
        End if
    End if
    var $key:=String($body.Id)
    var $target : 4D.Entity
    var $creating:=($key="")
    If ($creating)
        $target:=ds.GoAccount.new()
        $target.GoTenantId:=$actor.GoTenantId
        $target.Active:=True
    Else
        $target:=This.userTarget($actor; $key)
        If (Num($body.Revision)#Num($target.GoRevision))
            throw(409; "This user changed in 4D. Reload before saving.")
        End if
    End if
    var $duplicate:=ds.GoAccount.query("GoTenantId = :1 AND Username = :2"; $actor.GoTenantId; $name).first()
    If ($duplicate#Null)
        If ($duplicate.Id#$target.Id)
            throw(400; "That username is already in use")
        End if
    End if
    var $password:=String($body.Password)
    var $passwordHash:=""
    If (($password#"") | $creating)
        $password:=This.service.requirePassword($password)
        If (Length($password)<12)
            throw(400; "Use at least 12 password characters")
        End if
        $passwordHash:=Generate password hash(Generate digest($password; SHA256 digest); New object("cost"; 12))
    End if
    var $invalidate:=($passwordHash#"")
    If (Not($creating))
        var $oldRoles:=This.userRoles($target)
        If (($target.Username#$name) | ($oldRoles.length#$body.Roles.length))
            $invalidate:=True
        End if
        For each ($roleName; $oldRoles)
            If (Not($body.Roles.includes($roleName)))
                $invalidate:=True
            End if
        End for each
    End if
    $target.Username:=$name
    $target.Role:="Staff"
    If ($body.Roles.includes("Admin"))
        $target.Role:="Admin"
    End if
    If ($passwordHash#"")
        $target.PasswordHash:=$passwordHash
    End if
    This.service.save($target)
    var $id:=$target.Id
    $key:="4d-account-"+String($id)
    var $profile : 4D.Entity
    $profile:=ds.AspNetUsers.query("Id = :1 AND GoTenantId = :2"; $key; $actor.GoTenantId).first()
    If ($profile=Null)
        $profile:=ds.AspNetUsers.new()
        $profile.Id:=$key
        $profile.GoTenantId:=$actor.GoTenantId
        $profile.TenantId:=$actor.GoTenantId
    End if
    $profile.UserName:=$name
    $profile.Email:=$email
    $profile.FirstName:=String($body.FirstName)
    $profile.LastName:=String($body.LastName)
    $profile.IsApproved:=True
    If (Value type($body.ProfilePicture)=Is text)
        $profile.ProfilePicture:=$body.ProfilePicture
        $profile.ProfilePictureUpdated:=Timestamp
    End if
    This.service.save($profile)
    If (OB Is defined($body; "BranchCode"))
        This.service.debugStage:="users: save branch claim"
        var $branchClaim : 4D.Entity
        $branchClaim:=ds.AspNetUserClaims.query("UserId = :1 AND GoTenantId = :2 AND ClaimType = :3"; $key; $actor.GoTenantId; "Branch.Default").first()
        If ($branchClaim=Null)
            This.service.debugStage:="users: create branch claim"
            $branchClaim:=ds.AspNetUserClaims.new()
            $branchClaim.UserId:=$key
            $branchClaim.GoTenantId:=$actor.GoTenantId
            $branchClaim.ClaimType:="Branch.Default"
        End if
        This.service.debugStage:="users: branch claim value"
        $branchClaim.ClaimValue:=$branchCode
        This.service.debugStage:="users: persist branch claim"
        This.service.save($branchClaim)
    End if
    If (OB Is defined($body; "LockBranch"))
        var $lockClaim : 4D.Entity
        $lockClaim:=ds.AspNetUserClaims.query("UserId = :1 AND GoTenantId = :2 AND ClaimType = :3"; $key; $actor.GoTenantId; "Branch.Locked").first()
        If ($lockClaim=Null)
            $lockClaim:=ds.AspNetUserClaims.new()
            $lockClaim.UserId:=$key
            $lockClaim.GoTenantId:=$actor.GoTenantId
            $lockClaim.ClaimType:="Branch.Locked"
        End if
        $lockClaim.ClaimValue:="false"
        If ($body.LockBranch)
            $lockClaim.ClaimValue:="true"
        End if
        This.service.save($lockClaim)
    End if
    var $membership : 4D.Entity
    For each ($membership; ds.AspNetUserRoles.query("UserId = :1 AND GoTenantId = :2"; $key; $actor.GoTenantId))
        var $drop:=$membership.drop()
        If (Not($drop.success))
            throw(409; "Could not update user roles")
        End if
    End for each
    For each ($roleName; $body.Roles)
        var $tenant:=$actor.GoTenantId
        var $roleId:="4d-role-"+String($tenant)+"-"+$roleName
        var $role : 4D.Entity
        $role:=ds.AspNetRoles.get($roleId)
        If ($role=Null)
            $role:=ds.AspNetRoles.new()
            $role.Id:=$roleId
            $role.GoTenantId:=$tenant
            $role.Name:=$roleName
            $role.NormalizedName:=Uppercase($roleName)
            This.service.save($role)
        End if
        $membership:=ds.AspNetUserRoles.new()
        $membership.GoKey:=Generate UUID
        $membership.GoTenantId:=$tenant
        $membership.UserId:=$key
        $membership.RoleId:=$roleId
        This.service.save($membership)
    End for each
    If ($invalidate)
        var $session : 4D.Entity
        For each ($session; ds.GoSession.query("AccountId = :1"; $target.Id))
            var $d:=$session.drop()
            If (Not($d.success))
                throw(409; "Could not revoke existing sessions")
            End if
        End for each
    End if
    return New object("Id"; $key)

Function deleteUser($actor : 4D.Entity; $body : Object) : Object
    This.requireUserManager($actor)
    var $target:=This.userTarget($actor; String($body.Id))
    If ($target.Id=$actor.Id)
        throw(400; "You cannot delete the account you are signed in with")
    End if
    // Disable the login while retaining references and account history.
    $target.Active:=False
    This.service.save($target)
    return New object("ok"; True)

// Existing records retain their branch; new records use the user's assignment.
Function requireLockedBranch($account : 4D.Entity; $table : Text; $values : Object; $recordId : Real)
    If (Not(New collection("Agent"; "Vehicles"; "Job").includes($table)))
        return
    End if
    var $settings:=This.userRow($account)
    If (Not($settings.LockBranch))
        return
    End if
    If (($recordId>0) & Not(OB Is defined($values; "BranchId")))
        return
    End if
    var $expected : Real
    $expected:=0
    If ($recordId>0)
        var $existing:=This.service.row($table; $recordId; $account.GoTenantId)
        var $existingBranch : Variant
        $existingBranch:=$existing[This.service.schema[$table].fieldMap.BranchId]
        If ($existingBranch#Null)
            $expected:=Num($existingBranch)
        End if
    Else
        If ($settings.BranchCode#"")
            var $schema:=This.service.schema.Branches
            var $branch : 4D.Entity
            $branch:=ds[$schema.nativeTable].query("GoTenantId = :1 AND "+$schema.fieldMap.Code+" = :2"; $account.GoTenantId; $settings.BranchCode).first()
            If ($branch#Null)
                $expected:=Num($branch[$schema.key])
            End if
        End if
    End if
    var $selected : Real
    $selected:=0
    If (OB Is defined($values; "BranchId"))
        If ($values.BranchId#Null)
            $selected:=Num($values.BranchId)
        End if
    End if
    If ($selected#$expected)
        throw(403; "Your branch selection is locked")
    End if

Function commit($account : 4D.Entity; $body : Object) : Object
	
	This.service.debugStage:="commit: starting"
	This.service.requireRole($account; "Admin,Staff")
	
	This.service.debugStage:="commit: validating changes"
	
	If (Value type($body.changes)#Is collection)
		throw(400; "Expected changes array")
	End if 
	
	If ($body.changes.length>2000)
		throw(400; "Too many changes in one request")
	End if 
	
	var $ids:=New object
	var $change : Object
	var $row : 4D.Entity
	var $table : Text
	
	// ---------------------------------------------------------
	// FIRST PASS
	// Allocate new keys before resolving relationships.
	// ---------------------------------------------------------
	
	This.service.debugStage:="commit: first pass"
	
	For each ($change; $body.changes)
		
		$table:=String($change.table)
		
		// Ignore original GlassOps audit records.
		If ($table="AuditLogs")
			continue
		End if 
		
		This.service.debugStage:="commit: preparing "+$table+" "+String($change.operation)
		
		If ((Position("Go"; $table)=1) | (Position("AspNet"; $table)=1) | Not(OB Is defined(This.service.schema; $table)))
			throw(403; "Table is not available through the web adapter")
		End if 
		
		If ($table="Branches")
			This.service.requireRole($account; "Admin")
		End if

		If ($table="Tenant")
			
			If (($change.operation#"update") | (Num($change.recordId)#$account.GoTenantId))
				throw(403; "Only the current company's settings can be updated")
			End if 
			
		End if 
		
        If ($change.operation="add")
            This.requireLockedBranch($account; $table; $change.values; 0)
        Else
            If ($change.operation="update")
                This.requireLockedBranch($account; $table; $change.values; Num($change.recordId))
            End if
        End if
		If ($change.operation="add")
			
			This.service.debugStage:="commit: creating "+$table
			
			$row:=This.service.newRow($table; $account.GoTenantId)
			// GUID/text keys come from the web client; unlike integer keys,
			// 4D cannot allocate them by saving an empty row.
			var $keyField : Object
			For each ($keyField; This.service.schema[$table].fields)
				If (($keyField.name=This.service.schema[$table].key) & ($keyField.kind="text"))
					$row[This.service.schema[$table].key]:=This.service.requireText($change.key; "Record key"; 450)
				End if
			End for each
			
			This.service.debugStage:="commit: saving new "+$table
			
			This.service.save($row)
			
			This.service.debugStage:="commit: new record saved "+$table
			
			$ids[$table+":"+String($change.key)]:=$row[This.service.schema[$table].key]
			
		Else 
			
			This.service.debugStage:="commit: loading existing "+$table
			
			$row:=This.service.row($table; $change.recordId; $account.GoTenantId)
			
			This.service.debugStage:="commit: checking revision "+$table
			
			If (Num($change.revision)#Num($row.GoRevision))
				throw(409; "The record changed in 4D. Reload the page and try again.")
			End if 
			
		End if 
		
	End for each 
	
	
	// ---------------------------------------------------------
	// SECOND PASS
	// Populate fields and resolve foreign key relationships.
	// ---------------------------------------------------------
	
	This.service.debugStage:="commit: second pass"
	
	For each ($change; $body.changes)
		
		$table:=String($change.table)
		
		// Ignore original GlassOps audit records.
		If ($table="AuditLogs")
			continue
		End if 
		
		This.service.debugStage:="commit: applying fields "+$table
		
		var $recordId : Variant
		$recordId:=$change.recordId
		
		If ($change.operation="add")
			$recordId:=$ids[$table+":"+String($change.key)]
		End if 
		
		This.service.debugStage:="commit: retrieving record "+$table
		
		$row:=This.service.row($table; $recordId; $account.GoTenantId)
		
		If ($change.operation="delete")
			
			This.service.debugStage:="commit: deleting "+$table
			
			var $dropped:=$row.drop()
			
			If (Not($dropped.success))
				throw(409; "Could not delete record")
			End if 
			
			This.service.debugStage:="commit: deleted "+$table
			
		Else 
			
			var $field : Object
			
			For each ($field; This.service.schema[$table].fields)
				
				If (($field.name#"Id") & ($field.name#"TenantId") & (Position("Go"; $field.name)#1) & ($field.kind#"blob"))
					
					If (OB Is defined($change.values; $field.name))
						
						This.service.debugStage:="commit: field "+$table+"."+$field.name
						
						var $value : Variant
						$value:=$change.values[$field.name]
						
						If (OB Is defined($change.references; $field.name))
							
							This.service.debugStage:="commit: resolving reference "+$table+"."+$field.name
							
							var $referenceKey:=String($change.references[$field.name])+":"+String($value)
							
							If (OB Is defined($ids; $referenceKey))
								$value:=$ids[$referenceKey]
							End if 
							
						End if 
						
						This.service.debugStage:="commit: assigning "+$table+"."+$field.name
						
						$row[$field.nativeName]:=$value
						
					End if 
					
				End if 
				
			End for each 
			
			This.service.debugStage:="commit: saving fields "+$table
			If ($table="SystemSettings")
				If ($row.Key="SurveyAppLatestBuild")
					If (Not(Match regex("^[0-9]{1,10}$"; String($row.Value))))
						throw(400; "Enter a whole app build number")
					End if
					If (Num($row.Value)>2147483647)
						throw(400; "App build number is too large")
					End if
				End if
			End if
			If ($table="Job")
				This.service.normalizeJob($row)
			End if
			
			This.service.save($row)
			
			This.service.debugStage:="commit: fields saved "+$table
			
		End if 
		
	End for each 
	
	
	// ---------------------------------------------------------
	// AUDITING TEMPORARILY DISABLED
	// ---------------------------------------------------------
	
	// Original GlassOps AuditLogs are ignored in both loops.
	// The 4D web-adapter audit call is disabled during testing.
	//
	// This.service.audit($account; "web-adapter-save"; ""; 0)
	
	This.service.debugStage:="commit: completed"
	
	return New object("ids"; $ids)
