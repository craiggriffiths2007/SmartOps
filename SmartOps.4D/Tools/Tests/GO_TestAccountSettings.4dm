var $report:=New object("success"; False)
GO_Seed
var $service:=cs.SmartOpsService.new()
ds.startTransaction()
Try
    var $admin:=ds.GoAccount.query("Role = :1"; "Admin").first()
    var $staff:=ds.GoAccount.query("Role = :1 AND GoTenantId = :2"; "Staff"; $admin.GoTenantId).first()
    var $bridge:=cs.SmartOpsWebBridge.new($service)
    If ($bridge.preferences($admin).CompactContracts)
        throw(500; "Cards should be the initial preference")
    End if
    $bridge.savePreferences($admin; New object("CompactContracts"; True))
    $bridge:=cs.SmartOpsWebBridge.new(cs.SmartOpsService.new())
    If (Not($bridge.preferences($admin).CompactContracts))
        throw(500; "List preference did not persist through a fresh bridge")
    End if
    If ($bridge.preferences($staff).CompactContracts)
        throw(500; "Contract layout leaked to another account")
    End if
    $bridge.savePreferences($admin; New object("CompactContracts"; False))
    If ($bridge.preferences($admin).CompactContracts)
        throw(500; "Cards preference did not persist")
    End if
    If ($bridge.preferences($admin).DiaryView#"month")
        throw(500; "Month should be the initial diary view")
    End if
    var $diaryView : Text
    For each ($diaryView; New collection("list"; "week"; "month"))
        $bridge.savePreferences($admin; New object("DiaryView"; $diaryView))
        $bridge:=cs.SmartOpsWebBridge.new(cs.SmartOpsService.new())
        If ($bridge.preferences($admin).DiaryView#$diaryView)
            throw(500; "Diary view did not persist through a fresh bridge")
        End if
    End for each
    $bridge.savePreferences($admin; New object("DiaryView"; "week"))
    If ($bridge.preferences($staff).DiaryView#"month")
        throw(500; "Diary view leaked to another account")
    End if
    $bridge.savePreferences($admin; New object("CompactContracts"; True))
    If ($bridge.preferences($admin).DiaryView#"week")
        throw(500; "Contract layout erased the diary view")
    End if
    $bridge.savePreferences($admin; New object("DiaryView"; "list"))
    If (Not($bridge.preferences($admin).CompactContracts))
        throw(500; "Diary view erased the contract layout")
    End if
    var $invalidDiaryRejected:=False
    Try
        $bridge.savePreferences($admin; New object("DiaryView"; "invalid"; "CompactContracts"; False))
    Catch
        $invalidDiaryRejected:=True
    End try
    If (Not($invalidDiaryRejected) | ($bridge.preferences($admin).DiaryView#"list") | Not($bridge.preferences($admin).CompactContracts))
        throw(500; "Invalid diary view changed saved preferences")
    End if
    var $profile:=$bridge.profile($admin)
    If (($profile.Username#$admin.Username) | ($profile.PhoneNumber#""))
        throw(500; "Initial account profile is incorrect")
    End if
    $bridge.saveProfile($admin; New object("PhoneNumber"; "01234567890"; "ProfilePicture"; "aGVsbG8="))
    $profile:=$bridge.profile($admin)
    If (($profile.PhoneNumber#"01234567890") | ($profile.ProfilePicture#"aGVsbG8="))
        throw(500; "Phone or photo did not round trip through 4D")
    End if
    $bridge.saveProfile($admin; New object("PhoneNumber"; ""))
    $profile:=$bridge.profile($admin)
    If (($profile.PhoneNumber#"") | ($profile.ProfilePicture#"aGVsbG8="))
        throw(500; "Saving phone number erased the photo")
    End if
    If ($bridge.profile($staff).ProfilePicture#"")
        throw(500; "Profile leaked to another account in the same company")
    End if
    var $snapshot:=$bridge.snapshot($admin)
    If ($snapshot.account.ProfilePicture#"aGVsbG8=")
        throw(500; "Snapshot did not retain the account photo")
    End if
    var $oldHash:=$admin.PasswordHash
    var $rejected:=False
    Try
        $service.changeAccountPassword($admin; New object("OldPassword"; "incorrect"; "NewPassword"; "LuckyWillow9944"))
    Catch
        $rejected:=True
    End try
    If (Not($rejected) | ($admin.PasswordHash#$oldHash))
        throw(500; "Incorrect current password changed the account")
    End if
    $service.login(New object("TenantCode"; "SMARTOPS"; "Username"; $admin.Username; "Password"; "Passowrd1!"))
    var $response:=$service.changeAccountPassword($admin; New object("OldPassword"; "Passowrd1!"; "NewPassword"; "LuckyWillow9944"))
    If ($response.status#200)
        throw(500; "Alphanumeric password change failed")
    End if
    If (Not(Verify password hash(Generate digest("LuckyWillow9944"; SHA256 digest); $admin.PasswordHash)))
        throw(500; "The authoritative login password was not changed")
    End if
    If (ds.GoSession.query("AccountId = :1"; $admin.Id).length#0)
        throw(500; "Password change did not revoke sessions")
    End if
    $response:=$service.login(New object("TenantCode"; "SMARTOPS"; "Username"; $admin.Username; "Password"; "LuckyWillow9944"))
    If ($response.status#200)
        throw(500; "New password cannot log in")
    End if
    // Signed-in web account changes do not require the old password.
    $response:=$service.resetAccountPassword($admin; "AnotherPassword456")
    $response:=$service.login(New object("TenantCode"; "SMARTOPS"; "Username"; $admin.Username; "Password"; "AnotherPassword456"))
    If ($response.status#200)
        throw(500; "Signed-in password change did not persist")
    End if
    var $body:=New object("Id"; ""; "UserName"; "passwordtest"; "Email"; "test@example.invalid"; "FirstName"; "Test"; "LastName"; "User"; "Password"; "LuckyWillow9944"; "Roles"; New collection("Users"; "Diary"))
    $service.debugStage:="test: branch schema"
    var $branchSchema:=$service.schema.Branches
    $service.debugStage:="test: branch create"
    var $branch:=$service.newRow("Branches"; $admin.GoTenantId)
    $service.debugStage:="test: branch code"
    $branch[$branchSchema.fieldMap.Code]:="TESTBRANCH"
    $service.debugStage:="test: branch save"
    $service.save($branch)
    var $staffBranchRejected:=False
    Try
        $bridge.commit($staff; New object("changes"; New collection(New object("table"; "Branches"; "key"; "-1"; "operation"; "add"; "references"; New object; "values"; New object("Code"; "FORBIDDEN")))))
    Catch
        $staffBranchRejected:=True
    End try
    If (Not($staffBranchRejected))
        throw(500; "Staff was allowed to manage branches through the web adapter")
    End if
    var $adminBranchResult:=$bridge.commit($admin; New object("changes"; New collection(New object("table"; "Branches"; "key"; "-2"; "operation"; "add"; "references"; New object; "values"; New object("Code"; "ADMINBRANCH")))))
    If (Num($adminBranchResult.ids["Branches:-2"])<=0)
        throw(500; "Admin could not manage branches")
    End if
    $body.BranchCode:="TESTBRANCH"
    $body.LockBranch:=True
    $service.debugStage:="test: user save"
    var $created:=$bridge.saveUser($admin; $body)
    var $target:=ds.GoAccount.query("Username = :1 AND GoTenantId = :2"; "passwordtest"; $admin.GoTenantId).first()
    $response:=$service.login(New object("TenantCode"; "SMARTOPS"; "Username"; "passwordtest"; "Password"; "LuckyWillow9944"))
    If ($response.status#200)
        throw(500; "Add User password did not persist")
    End if
    var $saved:=$bridge.userRow($target)
    If (($saved.Email#"test@example.invalid") | Not($saved.Roles.includes("Users")) | $saved.Roles.includes("Admin"))
        throw(500; "User details or roles did not persist")
    End if
    If ($saved.BranchCode#$body.BranchCode)
        throw(500; "Assigned user branch did not persist")
    End if
    If ($bridge.userRow($staff).BranchCode#"")
        throw(500; "Branch assignment leaked to another user")
    End if
    If (Not($bridge.userRow($target).LockBranch))
        throw(500; "Lock branch did not persist")
    End if
    If ($bridge.userRow($staff).LockBranch)
        throw(500; "Branch lock leaked to another user")
    End if
    $bridge.requireLockedBranch($target; "Job"; New object("BranchId"; $branch.Id); 0)
    var $branchChangeRejected:=False
    Try
        $bridge.requireLockedBranch($target; "Job"; New object("BranchId"; 0); 0)
    Catch
        $branchChangeRejected:=True
    End try
    If (Not($branchChangeRejected))
        throw(500; "Locked branch could be bypassed in a new booking")
    End if
    var $existingAgent:=$service.newRow("Agent"; $admin.GoTenantId)
    $existingAgent.BranchId:=Num($adminBranchResult.ids["Branches:-2"])
    $service.save($existingAgent)
    $bridge.requireLockedBranch($target; "Agent"; New object("BranchId"; $existingAgent.BranchId); $existingAgent.Id)
    $branchChangeRejected:=False
    Try
        $bridge.requireLockedBranch($target; "Agent"; New object("BranchId"; $branch.Id); $existingAgent.Id)
    Catch
        $branchChangeRejected:=True
    End try
    If (Not($branchChangeRejected))
        throw(500; "Locked branch could be changed on an existing record")
    End if
    $body.Id:=$created.Id
    $target.reload()
    $body.Revision:=$target.GoRevision
    $body.Password:="ReplacementPass123"
    $bridge.saveUser($admin; $body)
    If (ds.GoSession.query("AccountId = :1"; $target.Id).length#0)
        throw(500; "Admin password change did not revoke target sessions")
    End if
    $response:=$service.login(New object("TenantCode"; "SMARTOPS"; "Username"; "passwordtest"; "Password"; "LuckyWillow9944"))
    If ($response.status#401)
        throw(500; "Old Add User password still works after Edit User")
    End if
    $response:=$service.login(New object("TenantCode"; "SMARTOPS"; "Username"; "passwordtest"; "Password"; "ReplacementPass123"))
    If ($response.status#200)
        throw(500; "Edit User replacement password did not persist")
    End if
    $target.reload()
    $body.Revision:=$target.GoRevision
    $body.Password:=""
    $body.BranchCode:=""
    $body.LockBranch:=True
    $bridge.saveUser($admin; $body)
    $bridge.requireLockedBranch($target; "Job"; New object("BranchId"; Null); 0)
    $target.reload()
    $body.Revision:=$target.GoRevision
    $body.FirstName:="Updated"
    $body.BranchCode:=""
    $body.LockBranch:=False
    $bridge.saveUser($admin; $body)
    $response:=$service.login(New object("TenantCode"; "SMARTOPS"; "Username"; "passwordtest"; "Password"; "ReplacementPass123"))
    If (($response.status#200) | ($bridge.userRow($target).FirstName#"Updated"))
        throw(500; "Blank Edit User password changed the password")
    End if
    If ($bridge.userRow($target).LockBranch)
        throw(500; "Unlocking branch did not persist")
    End if
    If ($bridge.userRow($target).BranchCode#"")
        throw(500; "All branch assignment did not persist")
    End if
    $target.reload()
    $body.Revision:=$target.GoRevision
    $body.BranchCode:="__nonexistent_branch__"
    $rejected:=False
    Try
        $bridge.saveUser($admin; $body)
    Catch
        $rejected:=True
    End try
    If (Not($rejected) | ($bridge.userRow($target).BranchCode#""))
        throw(500; "Invalid branch changed the user assignment")
    End if
    $body.BranchCode:=""
    $rejected:=False
    Try
        $body.Roles:=New collection("Admin")
        $bridge.saveUser($staff; $body)
    Catch
        $rejected:=True
    End try
    If (Not($rejected))
        throw(500; "Staff was able to grant Admin")
    End if
    $bridge.deleteUser($admin; New object("Id"; $created.Id))
    $response:=$service.login(New object("TenantCode"; "SMARTOPS"; "Username"; "passwordtest"; "Password"; "ReplacementPass123"))
    If ($response.status#401)
        throw(500; "Deleted user can still log in")
    End if
    $report.success:=True
    $report.checks:="Branch assignment and lock persistence; locked All; new and existing record enforcement; admin-only branch management; Diary view persistence, validation and account isolation; independent contract and diary preferences; Profile persistence; phone clearing; photo preservation; account isolation; snapshot photo; legacy API current-password validation; signed-in change without old password; Add/Edit User login; blank password preservation; role persistence; role escalation rejection; deleted-user login rejection; alphanumeric password; authoritative hash; session revocation; new-password login"
Catch
    $report.errors:=Last errors
    $report.stage:=$bridge.service.debugStage
End try
ds.cancelTransaction()
File(Folder(fk database folder).path+"account-settings-test-result.json").setText(JSON Stringify($report))
QUIT 4D
