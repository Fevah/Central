# Central Platform — Feature Test Checklist

Last updated: 2026-03-31

Comprehensive test checklist for the Central desktop + API platform.
Every checkbox is a manually testable item. Items with matching unit/integration tests
have the test class and method appended after a dash.

**Test suite: 1,900 tests across 148 test classes. 0 failures.**

---

## A. Application Lifecycle

### A1. Startup

- [ ] App launches without crash (check crash.log)
- [ ] Splash screen appears with progress bar
- [ ] Splash shows "Central vX.X.X -- Initializing..." with assembly version
- [ ] Startup workers execute in order (DB connect, load data, init modules)
- [ ] startup.log includes version, machine name, .NET version
- [ ] XAML layout errors recovered gracefully (toast shown, not crash/hang)
- [ ] Missing resource in module panel shows warning, doesn't deadlock
- [ ] crash.log + startup.log written on errors

### A2. Startup Health Check

- [ ] StartupHealthCheck.CheckAsync verifies critical DB tables exist at startup
- [ ] 25 required tables checked (app_users, roles, switches, sd_requests, sync_configs, audit_log, etc.)
- [ ] Missing tables reported in startup.log
- [ ] Warnings for empty app_users table
- [ ] DB latency measured and logged
- [ ] Results logged before auth flow begins

### A3. Auto-Migration on Startup

- [ ] MigrationRunner checks db/migrations/ for pending .sql files on startup
- [ ] Pending migrations applied automatically before health check
- [ ] Count of applied migrations logged to startup.log
- [ ] Splash shows "Applied N database migrations" when migrations run
- [ ] No error if migrations directory doesn't exist

### A4. Login Flow

- [ ] Windows auto-login succeeds (matches Windows username to app_users)
- [ ] Windows auto-login populates UserSession.CurrentUser
- [ ] LoginWindow appears when auto-login fails (wrong/missing username)
- [ ] LoginWindow accepts valid username/password
- [ ] LoginWindow rejects invalid credentials with error message

### A5. Offline Mode

- [ ] Offline mode activates when DB is unreachable (5s timeout)
- [ ] Status bar shows "Offline" when DB is down
- [ ] Auto-reconnect fires after DB comes back (10s retry)
- [ ] Data loads automatically after reconnect
- [ ] ConnectivityManager fires ConnectionChanged event

### A6. Command-Line Args

- [ ] `--dsn "Host=..."` overrides database connection string
- [ ] `--auth-method offline` starts in offline mode without login dialog
- [ ] `--auth-method password --user admin --password secret` auto-logs in
- [ ] Password cleared from memory after use (ClearPassword)
- [ ] Empty args returns all nulls -- `StartupArgsTests.EmptyArgs_ReturnsAllNulls`
- [ ] --dsn flag parsed correctly -- `StartupArgsTests.DsnFlag_Parsed`
- [ ] Short flags (-s, -u, -p, -a) all parsed -- `StartupArgsTests.ShortFlags_Parsed`
- [ ] Long flags (--server, --auth-method) all parsed -- `StartupArgsTests.LongFlags_Parsed`
- [ ] Mixed short + long flags in same invocation -- `StartupArgsTests.MixedFlags_Parsed`

### A7. Backstage

- [ ] User Profile tab shows current user info
- [ ] Settings tab shows DB-backed per-user settings
- [ ] Theme gallery shows 9 installed themes
- [ ] Theme changes apply immediately (DX ThemeManager)
- [ ] Theme persists across restarts (saved to user_settings)
- [ ] Connection tab shows DB connection status
- [ ] About tab shows version and build info
- [ ] Switch User button opens LoginWindow
- [ ] Exit button closes application
- [ ] API URL setting in backstage saves correctly
- [ ] Auto-connect toggle persists and works on next startup
- [ ] Email settings group: smtp_host, port, username, password, from, SSL
- [ ] Security settings group: password min length, lockout threshold/duration, expiry days, require MFA
- [ ] Settings persisted via SettingsProvider (per-user DB-backed) — `PreferenceKeysTests` (7 tests)

### A8. Exception Handling

- [ ] TaskScheduler.UnobservedTaskException: logged to AppLogger (DB) + crash.log + AuditService + toast
- [ ] DispatcherUnhandledException: logged to AppLogger + crash.log + AuditService + toast + MessageBox for fatal
- [ ] XAML parse errors: recovered gracefully with args.Handled = true + toast
- [ ] Layout overflow: recovered with args.Handled = true
- [ ] All unhandled exceptions appear in: startup.log, crash.log, app_log table, audit_log table, toast notification
- [ ] Admin can see all errors in App Log panel + Audit Log panel

### A9. DX Offline Package Cache

- [ ] 78 DevExpress 25.2.5 NuGet packages downloaded to packages-offline/
- [ ] NuGet.config references DevExpress-Offline as local source
- [ ] Enables fully offline development/build

---

## B. Authentication & Security

### B1. RBAC Roles & Permissions

- [ ] Admin role sees all ribbon tabs, all panels, all grid editing
- [ ] Operator role sees permitted tabs, can edit but not delete (where configured)
- [ ] Viewer role sees permitted tabs, grid editing disabled
- [ ] Custom roles respect per-module permissions (View/Edit/Delete)
- [ ] Ribbon buttons hidden for denied permissions (IsVisible binding)
- [ ] Panels closed for denied permissions (DockController.Close)
- [ ] Grid inline editing disabled for read-only roles
- [ ] Permissions tree shows 25 module:action codes — `PermissionNodeTests.Defaults`, `PermissionNodeTests.PropertyChanged_AllFields`
- [ ] Permission guard requires permission or throws — `PermissionGuardTests.Require_WithPermission_DoesNotThrow`, `PermissionGuardTests.Require_WithoutPermission_Throws`
- [ ] Permission guard throw message contains code — `PermissionGuardTests.Require_ThrowsMessageContainsCode`
- [ ] Permission guard requires site access — `PermissionGuardTests.RequireSite_WithAccess_DoesNotThrow`, `PermissionGuardTests.RequireSite_WithoutAccess_Throws`
- [ ] Permission guard no site restrictions all allowed — `PermissionGuardTests.RequireSite_NoRestrictions_AllAllowed`
- [ ] Permission guard case insensitive — `PermissionGuardTests.Require_CaseInsensitive_Works`
- [ ] All 25 permission codes follow module:action format — `PermissionCodeTests.AllCodes_FollowModuleActionFormat`, `PermissionCodeExtendedTests.AllCodes_ContainColon`, `PermissionCodeExtendedTests.AllCodes_AreLowercase`
- [ ] Permission codes: devices:read/write/delete, switches:read/ping/ssh, links:read, bgp:read/sync, admin:users/roles/settings/migrations/purge/backup, tasks:read/write, projects:read, sprints:write, scheduler:read, vlans:read, admin:ad/locations/references/containers — `PermissionCodeTests` (25 facts), `PermissionCodeExtendedTests` (43 tests)
- [ ] Checkbox toggles grant/revoke permission

### B2. Site-Level Access

- [ ] role_sites controls which buildings each role sees — `RoleSiteAccessTests.Defaults_AreCorrect`, `RoleSiteAccessTests.AllProperties_FirePropertyChanged`
- [ ] RoleSiteAccess Building/Allowed PropertyChanged — `RoleSiteAccessTests.PropertyChanged_Building_Fires`, `RoleSiteAccessTests.PropertyChanged_Allowed_Fires`
- [ ] RoleSiteAccess Allowed default true, toggle — `RoleSiteAccessTests.SetAllowed_True_AfterFalse`, `RoleSiteAccessTests.Building_CanBeSetToEmptyString`
- [ ] IPAM grid only shows devices from allowed sites
- [ ] Switches grid only shows switches from allowed sites
- [ ] SQL-level filtering (WHERE building = ANY(@sites)) confirmed

### B3. AuthContext

- [ ] Initial state: not authenticated, null user, NotAuthenticated state -- `AuthContextTests.InitialState_NotAuthenticated`
- [ ] SetSession: sets user, permissions, sites, auth state -- `AuthContextTests.SetSession_SetsAll`
- [ ] HasPermission: granted permissions work, ungranted denied -- `AuthContextTests.HasPermission_Granted`, `AuthContextExtendedTests.HasPermission_True_WhenGranted`, `AuthContextExtendedTests.HasPermission_False_WhenNotGranted`, `AuthContextExtendedTests.HasPermission_CaseInsensitive`
- [ ] SuperAdmin (priority 1000): always has all permissions -- `AuthContextTests.SuperAdmin_AllPermissions`, `AuthContextExtendedTests.IsSuperAdmin_True_WhenPriorityGTE1000`, `AuthContextExtendedTests.HasPermission_True_ForSuperAdmin_EvenWithoutGrant`
- [ ] Site access: no restrictions = all sites; restricted = only listed -- `AuthContextTests.SiteAccess_Restricted`, `AuthContextExtendedTests.HasSiteAccess_True_WhenNoRestrictions`, `AuthContextExtendedTests.HasSiteAccess_True_WhenSiteInList`, `AuthContextExtendedTests.HasSiteAccess_False_WhenSiteNotInList`, `AuthContextExtendedTests.HasSiteAccess_True_ForSuperAdmin`
- [ ] SetOfflineAdmin: full permissions, offline state -- `AuthContextTests.SetOfflineAdmin_FullPermissions`, `AuthContextExtendedTests.SetOfflineAdmin_SetsOfflineState`
- [ ] Logout: clears everything -- `AuthContextTests.Logout_ClearsAll`, `AuthContextExtendedTests.Logout_ResetsState`
- [ ] UpdateAllowedSites: changes site access live -- `AuthContextTests.UpdateAllowedSites`, `AuthContextExtendedTests.UpdateAllowedSites_ChangesSiteAccess`
- [ ] HasAnyPermission: true if any one matches -- `AuthContextTests.HasAnyPermission`, `AuthContextExtendedTests.HasAnyPermission_True_WhenOneMatches`, `AuthContextExtendedTests.HasAnyPermission_False_WhenNoneMatch`
- [ ] PermissionCount: reflects granted count -- `AuthContextTests.PermissionCount`, `AuthContextExtendedTests.PermissionCount_ReflectsGranted`, `AuthContextExtendedTests.PermissionCount_Zero_AfterLogout`
- [ ] IsAuthenticated state tracking — `AuthContextExtendedTests.IsAuthenticated_False_WhenNotAuthenticated`, `AuthContextExtendedTests.IsAuthenticated_True_AfterSetSession`
- [ ] IsSuperAdmin boundary (below 1000 = false) — `AuthContextExtendedTests.IsSuperAdmin_False_WhenPriorityBelow1000`
- [ ] CanView/CanEdit/CanDelete legacy mapping — `AuthContextExtendedTests.CanView_MapsToReadPermission`, `AuthContextExtendedTests.CanEdit_MapsToWritePermission`, `AuthContextExtendedTests.CanDelete_MapsToDeletePermission`
- [ ] CanViewReserved permission — `AuthContextExtendedTests.CanViewReserved_True_WhenGranted`, `AuthContextExtendedTests.CanViewReserved_False_WhenNotGranted`
- [ ] IsAdmin (super admin or Admin role) — `AuthContextExtendedTests.IsAdmin_True_WhenSuperAdmin`, `AuthContextExtendedTests.IsAdmin_True_WhenAdminRole`, `AuthContextExtendedTests.IsAdmin_False_WhenNeitherSuperAdminNorAdminRole`
- [ ] PermissionsChanged event fires on SetSession/Logout — `AuthContextExtendedTests.PermissionsChanged_FiresOnSetSession`, `AuthContextExtendedTests.PermissionsChanged_FiresOnLogout`
- [ ] PropertyChanged fires on AuthState change — `AuthContextExtendedTests.PropertyChanged_FiresOnAuthStateChange`
- [ ] All 9 AuthStates enum values present -- `AuthContextTests.AuthStates_AllValues`, `AuthStatesTests.AllStates_AreDefined`
- [ ] AuthStates ordinal values: NotAuthenticated=0, Windows=1, Offline=2, Password=3, EntraId=4, Okta=5, Saml=6, Local=7, ApiToken=8 — `AuthStatesTests` (9 facts), `AuthStatesExtendedTests` (18 tests — individual ordinal checks + AllStates_Defined + AuthenticatedStates_AreNotZero)

### B4. Auth Framework Models

- [ ] AuthResult, UserTypes, AuthStates, SecureString, IdentityProviderConfig, ClaimMapping, AppUser -- `AuthFrameworkTests` (24 tests), `AppUserTests` (21 tests), `AuthUserTests` (4 tests — defaults, set properties, IsActive default/deactivate)
- [ ] AppUser IsAdUser (ActiveDirectory=true, Manual=false) — `AppUserTests.IsAdUser_ActiveDirectory_True`, `AppUserTests.IsAdUser_Manual_False`, `AppUserExtendedTests.IsAdUser_VariousTypes`
- [ ] AppUser IsSystemUser (System=true, Standard=false) — `AppUserTests.IsSystemUser_System_True`, `AppUserTests.IsSystemUser_Standard_False`, `AppUserExtendedTests.IsSystemUser_VariousTypes`
- [ ] AppUser Initials from display name (first+last) — `AppUserTests.Initials_FromDisplayName_FirstAndLast`, `AppUserExtendedTests.Initials_TwoWordDisplayName`, `AppUserExtendedTests.Initials_ThreeWordDisplayName_UsesFirstAndLast`
- [ ] AppUser Initials single word first two chars — `AppUserTests.Initials_SingleWord_FirstTwoChars`
- [ ] AppUser Initials single char — `AppUserTests.Initials_SingleChar`, `AppUserExtendedTests.Initials_SingleCharUsername`
- [ ] AppUser Initials fallback to username — `AppUserExtendedTests.Initials_FallsBackToUsername_WhenDisplayNameEmpty`
- [ ] AppUser PropertyChanged fires on name/role/isActive — `AppUserTests.PropertyChanged_Fires_OnNameChange`, `AppUserTests.PropertyChanged_Fires_OnRoleChange`, `AppUserTests.PropertyChanged_Fires_OnIsActiveChange`
- [ ] AppUser PropertyChanged fires on extended fields (email, department, title, phone, mobile, company, adGuid, lastAdSync, lastLoginAt, loginCount, createdAt, autoLogin, userType, adSid, passwordHash, salt) — `AppUserExtendedTests` (16 PropertyChanged tests)
- [ ] AppUser defaults (Viewer, active, ActiveDirectory) — `AppUserTests.Defaults_AreCorrect`
- [ ] AppUser nullable datetime fields default null — `AppUserExtendedTests.NullableDateTimes_DefaultNull`, `AppUserExtendedTests.NullableDateTimes_CanBeCleared`
- [ ] AppUser DetailPermissions default empty — `AppUserTests.DetailPermissions_DefaultEmpty`

### B5. Password Hashing

- [ ] Generate salt produces unique salts -- `PasswordHasherTests.GenerateSalt_Unique`
- [ ] Hash consistency: same input + salt = same hash -- `PasswordHasherTests.Hash_Consistency`
- [ ] Different salts produce different hashes -- `PasswordHasherTests.DifferentSalts_DifferentHashes`
- [ ] Verify correct password -- `PasswordHasherTests.Verify_CorrectPassword`
- [ ] Verify wrong password -- `PasswordHasherTests.Verify_WrongPassword`
- [ ] Empty password handled -- `PasswordHasherTests.EmptyPassword_Handled`
- [ ] Set Password dialog (SHA256 + salt) works from backstage

### B5a. Credential Encryption (AES-256)

- [ ] Encrypt/Decrypt roundtrip — `CredentialEncryptorTests.Encrypt_Decrypt_Roundtrip`
- [ ] Encrypt returns Base64 — `CredentialEncryptorTests.Encrypt_ReturnsBase64`
- [ ] Encrypt empty string returns empty — `CredentialEncryptorTests.Encrypt_EmptyString_ReturnsEmpty`
- [ ] Encrypt null returns empty — `CredentialEncryptorTests.Encrypt_Null_ReturnsEmpty`
- [ ] Decrypt empty string returns empty — `CredentialEncryptorTests.Decrypt_EmptyString_ReturnsEmpty`
- [ ] Decrypt null returns empty — `CredentialEncryptorTests.Decrypt_Null_ReturnsEmpty`
- [ ] Decrypt non-Base64 returns as-is (legacy) — `CredentialEncryptorTests.Decrypt_NonBase64_ReturnsAsIs`
- [ ] Decrypt too-short returns as-is — `CredentialEncryptorTests.Decrypt_TooShort_ReturnsAsIs`
- [ ] Encrypt produces different ciphertext each time (random IV) — `CredentialEncryptorTests.Encrypt_ProducesDifferentCiphertext_EachTime`
- [ ] Encrypt/Decrypt special characters — `CredentialEncryptorTests.Encrypt_Decrypt_SpecialCharacters`
- [ ] Encrypt/Decrypt unicode — `CredentialEncryptorTests.Encrypt_Decrypt_Unicode`
- [ ] IsEncrypted empty string returns false — `CredentialEncryptorTests.IsEncrypted_EmptyString_ReturnsFalse`
- [ ] IsEncrypted null returns false — `CredentialEncryptorTests.IsEncrypted_Null_ReturnsFalse`
- [ ] IsEncrypted valid encrypted returns true — `CredentialEncryptorTests.IsEncrypted_ValidEncrypted_ReturnsTrue`
- [ ] IsEncrypted plain text returns false — `CredentialEncryptorTests.IsEncrypted_PlainText_ReturnsFalse`
- [ ] IsEncrypted short Base64 returns false — `CredentialEncryptorTests.IsEncrypted_ShortBase64_ReturnsFalse`
- [ ] Initialize with different key cannot decrypt old data — `CredentialEncryptorTests.Initialize_WithDifferentKey_CannotDecryptOldData`
- [ ] Encrypt/Decrypt long value — `CredentialEncryptorTests.Encrypt_Decrypt_LongValue`

### B6. Password Policy

- [ ] Default policy: min 8, uppercase, lowercase, digit, special, 90-day expiry -- `PasswordPolicyTests.DefaultPolicy_Reasonable`
- [ ] Relaxed policy: min 4, no complexity, no expiry -- `PasswordPolicyTests.RelaxedPolicy_Permissive`
- [ ] Validate rejects: too short, no uppercase, no lowercase, no digit, no special -- `PasswordPolicyTests.Validate_Rejects_*`
- [ ] Validate accepts strong passwords -- `PasswordPolicyTests.Validate_AcceptsStrong`
- [ ] Validate exactly min length passes — `PasswordPolicyExtendedTests.Validate_ExactlyMinLength_Passes`
- [ ] Validate one less than min length fails — `PasswordPolicyExtendedTests.Validate_OneLessThanMinLength_Fails`
- [ ] Validate exactly max length passes — `PasswordPolicyExtendedTests.Validate_ExactlyMaxLength_Passes`
- [ ] Validate over max length fails — `PasswordPolicyExtendedTests.Validate_OverMaxLength_Fails`
- [ ] Validate various special characters — `PasswordPolicyExtendedTests.Validate_VariousSpecialChars`
- [ ] Validate unicode password — `PasswordPolicyExtendedTests.Validate_UnicodePassword_WithAllRequirements_Passes`
- [ ] Password history: blocks reuse of last N passwords -- `PasswordPolicyTests.PasswordHistory_BlocksReuse`
- [ ] IsExpired: returns true when password_changed_at > ExpiryDays ago -- `PasswordPolicyTests.IsExpired`
- [ ] IsExpired exactly on boundary not expired — `PasswordPolicyExtendedTests.IsExpired_ExactlyOnBoundary_NotExpired`
- [ ] IsExpired just past boundary expired — `PasswordPolicyExtendedTests.IsExpired_JustPastBoundary_Expired`
- [ ] IsExpired null date not expired — `PasswordPolicyExtendedTests.IsExpired_NullDate_NotExpired`
- [ ] IsTooRecent: blocks change if password changed < MinAgeDays ago -- `PasswordPolicyTests.IsTooRecent`
- [ ] IsTooRecent exactly min age too recent — `PasswordPolicyExtendedTests.IsTooRecent_ExactlyMinAge_TooRecent`
- [ ] IsTooRecent past min age allowed — `PasswordPolicyExtendedTests.IsTooRecent_PastMinAge_Allowed`
- [ ] IsTooRecent zero min age never blocked — `PasswordPolicyExtendedTests.IsTooRecent_ZeroMinAge_NeverBlocked`
- [ ] IsTooRecent null date allowed — `PasswordPolicyExtendedTests.IsTooRecent_NullDate_Allowed`
- [ ] Password history no salt skips check — `PasswordPolicyExtendedTests.Validate_PasswordHistory_NoSalt_SkipsCheck`
- [ ] Password history zero count skips check — `PasswordPolicyExtendedTests.Validate_PasswordHistory_ZeroCount_SkipsCheck`
- [ ] Multiple validation errors all reported in one result -- `PasswordPolicyTests.MultipleErrors`
- [ ] ErrorSummary multiple errors joined by semicolon — `PasswordPolicyExtendedTests.ErrorSummary_MultipleErrors_JoinedBySemicolon`
- [ ] ErrorSummary no errors empty — `PasswordPolicyExtendedTests.ErrorSummary_NoErrors_Empty`
- [ ] Description property shows human-readable policy summary -- `PasswordPolicyTests.Description`
- [ ] Description no expiry omits expiry text — `PasswordPolicyExtendedTests.Description_NoExpiry_OmitsExpiryText`
- [ ] Description relaxed policy minimal — `PasswordPolicyExtendedTests.Description_RelaxedPolicy_Minimal`
- [ ] Policy description shown below user label in SetPasswordWindow
- [ ] Password validated against PasswordPolicy.Default before save
- [ ] Validation errors shown in red (all errors at once)
- [ ] Password history checked against last 5 hashes in password_history table
- [ ] password_changed_at updated on app_users after password change
- [ ] New hash saved to password_history for future reuse prevention
- [ ] Audit log entry created on password change

### B7. Account Lockout

- [ ] Account lockout activates after 5 failed attempts
- [ ] Account lockout expires after 15 minutes
- [ ] Brute-force lockout: 5 failed password attempts locks for 30 minutes

### B8. TOTP MFA

- [ ] GenerateSecret returns valid Base32 secret -- `TotpServiceTests.GenerateSecret_Valid`
- [ ] GenerateQrUri produces otpauth:// URI -- `TotpServiceTests.GenerateQrUri_Valid`
- [ ] GenerateCurrentCode returns 6-digit code -- `TotpServiceTests.GenerateCurrentCode`
- [ ] VerifyCode validates current TOTP within +/-1 time step -- `TotpServiceTests.VerifyCode_Valid`
- [ ] VerifyCode rejects wrong codes -- `TotpServiceTests.VerifyCode_RejectsWrong`
- [ ] GenerateRecoveryCodes returns 8 unique hyphenated hex codes -- `TotpServiceTests.GenerateRecoveryCodes`
- [ ] Recovery codes stored hashed in mfa_recovery_codes table
- [ ] VerifyRecoveryCodeAsync marks code as used (single-use)
- [ ] EnableMfaAsync sets mfa_enabled=true and stores encrypted secret
- [ ] DisableMfaAsync clears secret and recovery codes

### B9. MFA Enrollment Dialog

- [ ] MfaEnrollmentDialog shows TOTP secret key formatted with spaces
- [ ] QR URI displayed + copy button for authenticator app enrollment
- [ ] 6-digit verification code input with Enter key support
- [ ] Correct code shows "Verified! MFA is now enabled" in green
- [ ] Wrong code shows error in red
- [ ] Recovery codes (8) generated and displayed after successful verification
- [ ] Copy Recovery Codes button copies to clipboard
- [ ] OnMfaEnabled delegate called with secret + recovery codes on success
- [ ] "Setup MFA" button in Admin > User Management ribbon group
- [ ] On successful verification: encrypts secret, saves to app_users.mfa_secret_enc
- [ ] Recovery codes saved hashed to mfa_recovery_codes table
- [ ] Audit log entry created on MFA enable

### B10. Identity Provider Config

- [ ] IdentityProviderConfig model -- `IdentityConfigTests.IdentityProviderConfig_*`
- [ ] ClaimMapping model -- `IdentityConfigTests.ClaimMapping_*`
- [ ] DomainMapping model -- `IdentityConfigTests.DomainMapping_*`
- [ ] ExternalIdentity model -- `IdentityConfigTests.ExternalIdentity_*`
- [ ] AuthEvent model -- `IdentityConfigTests.AuthEvent_*`
- [ ] AuthResult claims -- `IdentityConfigTests.AuthResult_Claims`
- [ ] AuthRequest model -- `IdentityConfigTests.AuthRequest_*`

### B11. User Types

- [ ] UserTypes.All has 5 entries -- `UserTypesTests.All_Has5`
- [ ] IsProtected: System and Service only -- `UserTypesTests.IsProtected_SystemService`
- [ ] IsProtected: Standard, ActiveDirectory, Admin, null, empty = false -- `UserTypesTests.IsProtected_NotProtected`
- [ ] AppUser.Initials: two-word name = first letters -- `UserTypesTests.Initials_TwoWord`
- [ ] AppUser.Initials: single word = first 2 chars -- `UserTypesTests.Initials_SingleWord`
- [ ] AppUser.Initials: from username when DisplayName empty -- `UserTypesTests.Initials_FromUsername`
- [ ] AppUser.StatusText: Active/Inactive -- `UserTypesTests.StatusText`
- [ ] AppUser.StatusColor: green/grey -- `UserTypesTests.StatusColor`

### B12. Authentication Framework (SSO/OIDC/SAML)

- [ ] LoginWindow shows SSO buttons when identity_providers configured in DB
- [ ] Email-based IdP discovery: enter email, system routes to correct provider
- [ ] "Sign in with Microsoft" button opens system browser, OIDC+PKCE flow
- [ ] "Sign in with Okta" button opens system browser, OIDC+PKCE flow
- [ ] "Sign in with SSO" button triggers SAML2 SP-initiated flow
- [ ] Duo MFA prompt appears after SAML if duo_enabled=true in config
- [ ] Claims mapping: external group claim maps to Central role via claim_mappings table
- [ ] JIT provisioning: first-time external user created in app_users automatically
- [ ] Session refresh timer: silently re-validates every 20 minutes
- [ ] Auth state indicator in status bar: Online (green), Entra/Okta/SSO (blue), Offline (amber)
- [ ] Auth Events panel shows all login/logout/failed events with timestamps
- [ ] Identity Providers panel: CRUD for providers + domain mappings
- [ ] Existing Windows auto-login and manual password login still work unchanged
- [ ] SecureString password handling in LoginWindow — `SecureStringExtensionTests.ToSecureString_NonEmpty_CorrectLength`, `SecureStringExtensionTests.ToPlainText_Roundtrips`
- [ ] SecureString empty string roundtrip — `SecureStringExtensionTests.ToSecureString_EmptyString_ReturnsSecureString`, `SecureStringExtensionTests.ToPlainText_Empty_ReturnsEmpty`
- [ ] SecureString is read-only after creation — `SecureStringExtensionTests.ToSecureString_IsReadOnly`
- [ ] SecureString password hash consistency — `SecureStringExtensionTests.ToPasswordHash_ProducesConsistentHash`
- [ ] SecureString different salts produce different hashes — `SecureStringExtensionTests.ToPasswordHash_DifferentSalts_DifferentHashes`
- [ ] SecureString verify hash correct/wrong — `SecureStringExtensionTests.VerifyHash_CorrectPassword_ReturnsTrue`, `SecureStringExtensionTests.VerifyHash_WrongPassword_ReturnsFalse`
- [ ] SecureString unicode chars in hash — `SecureStringExtensionTests.ToPasswordHash_UnicodeChars`

### B13. API Key Authentication

- [ ] X-API-Key header checked by middleware before JWT
- [ ] Key validated against api_keys table (SHA256 hash -- raw key never stored)
- [ ] Valid key sets ClaimsPrincipal with name, role, auth_method=api_key
- [ ] last_used_at and use_count updated on each use
- [ ] Falls through to JWT auth if no X-API-Key header

### B14. API Key Management Panel

- [ ] API Keys panel opens from Admin > Identity > API Keys
- [ ] Generate Key button prompts for name, creates key, shows raw key once
- [ ] Raw key copied to clipboard automatically
- [ ] Grid shows name, role, active, uses, last used, created, expires
- [ ] Revoke button sets is_active=false without deleting
- [ ] Delete button removes key permanently with confirmation
- [ ] Audit log entry on key create and revoke

### B15. Active Sessions

- [ ] Sessions panel opens from Admin > Identity > Sessions
- [ ] Grid shows user, name, auth method, machine, IP, started, last activity, duration
- [ ] Force Logout button terminates selected session
- [ ] Force Logout All terminates all sessions for the selected user
- [ ] CreateSessionAsync called on login, EndSessionAsync on logout
- [ ] ForceEndSessionAsync/ForceEndAllSessionsAsync for admin use
- [ ] active_sessions table with pg_notify trigger (migration 054)
- [ ] Audit log entries for force logout actions
- [ ] Session token (GUID) generated and stored in active_sessions table
- [ ] Machine name recorded from Environment.MachineName

### B16. Structured Audit Trail

- [ ] AuditService.Instance logs all CRUD operations
- [ ] No persist func: does not throw -- `AuditServiceTests.NoPersistFunc_NoThrow`
- [ ] Persist func called with correct entry -- `AuditServiceTests.PersistFunc_CalledCorrectly`
- [ ] Broadcast func called with action -- `AuditServiceTests.BroadcastFunc_Called`
- [ ] LogCreateAsync sets Create action -- `AuditServiceTests.LogCreate_SetsAction`
- [ ] LogDeleteAsync sets Delete action -- `AuditServiceTests.LogDelete_SetsAction`
- [ ] Before/After JSON serialized correctly -- `AuditServiceTests.BeforeAfterJson_Serialized`
- [ ] Persist throws: does not crash -- `AuditServiceTests.PersistThrows_NoCrash`
- [ ] LogUpdateAsync records before/after snapshots as JSONB — `EntityBaseTests.TakeSnapshot_CapturesAllProperties`
- [ ] LogViewAsync records data access
- [ ] LogExportAsync records data exports
- [ ] LogLoginAsync records login attempts
- [ ] LogSettingChangeAsync records old/new setting values
- [ ] audit_log table with before_json/after_json columns (migration 052)
- [ ] GetAuditLogAsync supports filtering by entity_type and username
- [ ] SetPersistFunc wires to DbRepository at startup
- [ ] Audit logging never blocks the primary operation (try/catch)

### B17. Audit Trail Wiring

- [ ] AuditService initialized at startup with DbRepository persistence
- [ ] Device save logs Create/Update with device name
- [ ] Device delete logs Delete with device ID and name
- [ ] User delete logs Delete with user ID and username
- [ ] CSV export logs Export with panel name and file path
- [ ] Password change logs PasswordChange with user ID

### B18. Audit Broadcasting via SignalR

- [ ] AuditService.SetBroadcastFunc wires SignalR broadcasting
- [ ] Every audit log entry triggers real-time broadcast to all connected clients
- [ ] Broadcast includes: action, entityType, entityName, username
- [ ] Broadcasting never blocks the primary operation (try/catch)

### B19. Audit Log Viewer Panel

- [ ] Audit Log panel opens from Admin > Identity > Audit Log
- [ ] Grid shows timestamp, action, entity type, entity ID/name, user, details, before/after JSON
- [ ] Entity type dropdown filter (Device, Switch, User, Setting)
- [ ] Username text filter
- [ ] Refresh button reloads with current filters
- [ ] Filter change triggers auto-refresh
- [ ] Sorted by timestamp descending (newest first)

### B20. Security Headers Middleware

- [ ] X-Frame-Options: DENY (prevents clickjacking)
- [ ] X-Content-Type-Options: nosniff (prevents MIME sniffing)
- [ ] X-XSS-Protection: 1; mode=block
- [ ] Referrer-Policy: strict-origin-when-cross-origin
- [ ] Content-Security-Policy: default-src 'none'; frame-ancestors 'none'
- [ ] Permissions-Policy: camera=(), microphone=(), geolocation=()
- [ ] Cache-Control: no-store, no-cache, must-revalidate (default for API)

---

## C. Ribbon & UI Framework

### C1. Ribbon General

- [ ] All ribbon tabs render without errors — `RibbonBuilderTests.AddPage_CreatesPage`
- [ ] ItemClick events fire on button click — `RibbonBuilderTests.AddButton_CreatesButton`
- [ ] Ribbon page headers display correctly — `RibbonBuilderTests.MultiplePages_SortOrder`
- [ ] Settings cog (BarSubItem) appears in page headers
- [ ] Save Layout / Restore Default work from settings cog
- [ ] RibbonBuilder AddPage with permission — `RibbonBuilderTests.AddPage_WithPermission`
- [ ] RibbonBuilder AddGroup creates groups — `RibbonBuilderTests.AddGroup_CreatesGroup`
- [ ] RibbonBuilder AddLargeButton — `RibbonBuilderTests.AddLargeButton_CreatesLargeButton`
- [ ] RibbonBuilder AddCheckButton — `RibbonBuilderTests.AddCheckButton_CreatesCheckButton`
- [ ] RibbonBuilder AddToggleButton — `RibbonBuilderTests.AddToggleButton_CreatesToggle`
- [ ] RibbonBuilder AddSeparator — `RibbonBuilderTests.AddSeparator_CreatesSeparator`
- [ ] RibbonBuilder AddSplitButton with sub-items — `RibbonBuilderTests.AddSplitButton_CreatesWithSubItems`
- [ ] RibbonGroupRegistration typed accessors — `RibbonBuilderTests.RibbonGroupRegistration_Accessors`
- [ ] WidgetCommandAttribute stores Name/GroupName/Description — `WidgetCommandTests.WidgetCommandAttribute_StoresNameGroupDescription`, `WidgetCommandTests.WidgetCommandAttribute_EmptyDescription`
- [ ] WidgetCommandAttribute CommandParameter default null — `WidgetCommandTests.WidgetCommandAttribute_CommandParameter_Null_ByDefault`, `WidgetCommandTests.WidgetCommandAttribute_CommandParameter_CanBeSet`
- [ ] WidgetCommandAttribute is subclass of Attribute, targets Property — `WidgetCommandTests.WidgetCommandAttribute_IsAttribute`, `WidgetCommandTests.WidgetCommandAttribute_TargetsProperty`
- [ ] WidgetCommandData.Apply text replacements — `WidgetCommandTests.Apply_NoReplacements_ReturnsOriginal`, `WidgetCommandTests.Apply_SingleReplacement`, `WidgetCommandTests.Apply_MultipleReplacements`, `WidgetCommandTests.Apply_NoMatchingPlaceholder_ReturnsUnchanged`
- [ ] WidgetCommandData.Apply edge cases (empty, multiple same, partial, special chars) — `WidgetCommandTests.Apply_EmptyTemplate_ReturnsEmpty`, `WidgetCommandTests.Apply_MultipleSamePlaceholder`, `WidgetCommandTests.Apply_PartialPlaceholder_NotReplaced`, `WidgetCommandTests.Apply_EmptyValue_RemovesPlaceholder`, `WidgetCommandTests.Apply_SpecialCharacters`
- [ ] WidgetCommandData TextReplacements default empty — `WidgetCommandTests.TextReplacements_DefaultEmpty`

### C2. Context Tabs

- [ ] Links tab (blue) appears when Links panel is active
- [ ] Switch tab (green) appears when Switches panel is active
- [ ] Admin tab (amber) appears when Admin panel is active
- [ ] Context tabs hide when switching to unrelated panel

### C3. Quick Access Toolbar

- [ ] Save/Refresh/Undo buttons appear in QAT
- [ ] QAT buttons are functional
- [ ] QAT is user-customizable (right-click add/remove)

### C4. Ribbon Customization (3-layer override)

- [ ] User RibbonTreePanel opens from Admin ribbon
- [ ] Tree shows hierarchy: pages > groups > items — `RibbonConfigTests.RibbonTreeItem_NodeIcon_ByType`, `RibbonTreeItemTests.NodeIcon_ReturnsCorrectIcon`
- [ ] Icon picker button opens ImagePickerWindow
- [ ] Selected icon appears in tree preview — `RibbonConfigTests.RibbonTreeItem_PropertyChanged_IconName_AlsoNotifiesIconPreview`
- [ ] Custom text overrides default label — `RibbonConfigTests.RibbonTreeItem_DisplayText_PrefersCustomText`
- [ ] Hide/Show toggle works (IsHidden property) — `RibbonConfigTests.RibbonTreeItem_PropertyChanged_IsHidden_AlsoNotifiesHiddenIcon`
- [ ] Move Up/Down reorders items
- [ ] Apply button saves all overrides
- [ ] Reset All clears user overrides
- [ ] Auto-save fires on icon pick (SaveSingleOverride)
- [ ] Auto-save fires on hide toggle
- [ ] Admin RibbonAdminTreePanel opens from Admin ribbon
- [ ] Admin can set default icon for all users
- [ ] Admin can set display style (large/small/smallNoText) — `RibbonConfigTests.RibbonTreeItem_DisplayStyle_DefaultsToSmall`
- [ ] Admin can set link target (panel/url/action/page) — `RibbonConfigTests.RibbonTreeItem_LinkTarget_DefaultsToNull`
- [ ] Admin can add new pages/groups/items
- [ ] Admin can add separators
- [ ] Push All Defaults propagates to all users
- [ ] UserRibbonOverride defaults — `RibbonConfigTests.UserRibbonOverride_Defaults`, `RibbonConfigExtendedTests.UserRibbonOverride_Defaults`
- [ ] UserRibbonOverride set properties — `RibbonConfigTests.UserRibbonOverride_SetProperties`, `RibbonConfigExtendedTests.UserRibbonOverride_SetProperties`
- [ ] UserRibbonOverride IsHidden can be true — `RibbonConfigExtendedTests.UserRibbonOverride_IsHidden_True`
- [ ] RibbonPageConfig PropertyChanged Header — `RibbonConfigTests.RibbonPageConfig_PropertyChanged_Header`
- [ ] RibbonPageConfig PropertyChanged IsVisible — `RibbonConfigTests.RibbonPageConfig_PropertyChanged_IsVisible`
- [ ] RibbonGroupConfig PropertyChanged Header — `RibbonConfigTests.RibbonGroupConfig_PropertyChanged_Header`
- [ ] RibbonItemConfig PropertyChanged Content — `RibbonConfigTests.RibbonItemConfig_PropertyChanged_Content`
- [ ] RibbonTreeItem DisplayText falls back to Text when CustomText empty — `RibbonConfigTests.RibbonTreeItem_DisplayText_EmptyCustomText_FallsBackToText`
- [ ] RibbonTreeItem HiddenIcon when hidden/visible — `RibbonConfigTests.RibbonTreeItem_HiddenIcon_WhenHidden`, `RibbonConfigTests.RibbonTreeItem_HiddenIcon_WhenVisible`
- [ ] RibbonTreeItem NodeType change notifies NodeIcon — `RibbonConfigTests.RibbonTreeItem_PropertyChanged_NodeType_AlsoNotifiesNodeIcon`
- [ ] RibbonTreeItem CustomText change notifies DisplayText — `RibbonConfigTests.RibbonTreeItem_PropertyChanged_CustomText_AlsoNotifiesDisplayText`, `RibbonTreeItemTests.PropertyChanged_CustomText_AlsoNotifiesDisplayText`
- [ ] RibbonTreeItem IconName change notifies IconPreview — `RibbonTreeItemTests.PropertyChanged_IconName_AlsoNotifiesIconPreview`
- [ ] RibbonTreeItem defaults (Id=0, ParentId=0, DisplayStyle=small, etc.) — `RibbonTreeItemTests.Defaults_AreCorrect`
- [ ] RibbonTreeItem DisplayStyle and LinkTarget can be set — `RibbonTreeItemTests.DisplayStyle_CanBeSet`, `RibbonTreeItemTests.LinkTarget_CanBeSet`
- [ ] PreloadIconOverridesAsync prevents icon flash on startup — `IconOverrideServiceTests.IsLoaded_TrueAfterLoad`
- [ ] 3-layer resolution: user_ribbon_overrides > ribbon_items > admin_ribbon_defaults — `IconOverrideServiceTests.Resolve_UserOverride_TakesPriority`

### C5. Icon System -- Icon Library

- [ ] IconService loads metadata on startup (11,676 icons)
- [ ] IconService.AllIcons contains OfficePro + Universal packs
- [ ] IconService.GetCategories() returns distinct category list
- [ ] IconService.GetIconSets() returns ["OfficePro", "Universal"]
- [ ] IconService.Search() filters by name, category, size
- [ ] IconOverrideService loads admin defaults — `IconOverrideServiceTests.Resolve_AdminDefault_Returned`
- [ ] IconOverrideService user override takes priority — `IconOverrideServiceTests.Resolve_UserOverride_TakesPriority`
- [ ] IconOverrideService resolves color — `IconOverrideServiceTests.ResolveColor_ReturnsJustColor`
- [ ] IconOverrideService resolves icon name — `IconOverrideServiceTests.ResolveIconName_ReturnsIconName`
- [ ] IconOverrideService case-insensitive lookup — `IconOverrideServiceTests.Load_CaseInsensitive`
- [ ] IconOverrideService reload overwrites previous — `IconOverrideServiceTests.Load_OverwritesPreviousData`

### C6. Icon System -- ImagePickerWindow

- [ ] Window opens as DXDialogWindow (themed)
- [ ] Pack checkboxes show OfficePro + Universal
- [ ] All packs selected by default
- [ ] Category checkboxes populate from selected packs
- [ ] Changing pack selection updates category list
- [ ] No icons load until categories are selected
- [ ] Select All Categories button selects all
- [ ] Clear All Categories button clears all
- [ ] Search box filters icons by name
- [ ] Icons load asynchronously (Loading... indicator)
- [ ] Generation counter cancels stale loads on rapid filter change
- [ ] Icons render from pre-rendered PNG 32px (not live SVG)
- [ ] Count label shows "N icons" or "N of M icons (narrow with search)"
- [ ] Select button returns icon ID + name
- [ ] Double-click selects icon
- [ ] Clear button returns -1 (explicitly cleared)
- [ ] Delete button removes icon from DB after confirmation

### C7. SVG Rendering

- [ ] SvgHelper.RenderSvgToImageSource renders SVG to BitmapImage
- [ ] currentColor replaced with #FFFFFF for dark theme
- [ ] In-memory cache (hash-keyed) prevents re-rendering
- [ ] Disk cache writes to %LocalAppData%/Central/icon_cache/
- [ ] LoadFromDiskCache reads cached SVG files

### C8. Themes

- [ ] Theme gallery shows 9 installed themes
- [ ] Theme changes apply immediately
- [ ] Theme persists across restarts

### C9. Keyboard Shortcuts

- [ ] Ctrl+R / F5 -- Refresh all data
- [ ] Ctrl+N -- New record (routes by active panel)
- [ ] Delete -- Delete selected record
- [ ] Ctrl+E -- Export devices
- [ ] Ctrl+P -- Print preview
- [ ] Ctrl+F -- Toggle global search
- [ ] Ctrl+S -- Save/commit current row
- [ ] Ctrl+D -- Toggle details panel
- [ ] Ctrl+G -- Go to dialog
- [ ] Ctrl+I -- Import wizard
- [ ] Ctrl+Z -- Undo
- [ ] Ctrl+Y -- Redo
- [ ] Ctrl+Tab -- Cycle to next panel
- [ ] F1 -- Keyboard help

---

## D. Grid Framework

### D1. Inline Editing

- [ ] Inline editing works (NavigationStyle=Cell)
- [ ] Dropdown columns wired via BindComboSources()
- [ ] ValidateRow auto-saves on row commit — `GridValidationHelperTests` (14 tests)
- [ ] ShownEditor event wired in code-behind constructor
- [ ] Natural numeric sort on interface columns

### D2. Context Menu (Global)

- [ ] Right-click shows context menu on all 22 grids
- [ ] Customize Grid... opens GridCustomizerDialog
- [ ] Manage Saved Filters... opens SavedFilterDialog
- [ ] Configure Links... opens LinkCustomizerDialog
- [ ] Export to CSV... opens SaveFileDialog + TableView.ExportToCsv
- [ ] Copy to Clipboard (SelectAll + CopyToClipboard)
- [ ] Clear All Filters resets grid.FilterString
- [ ] Print Preview... opens TableView.ShowPrintPreview
- [ ] Column Chooser... opens TableView.ShowColumnChooser
- [ ] Best Fit All Columns fires TableView.BestFitColumns
- [ ] Select All Rows fires grid.SelectAll
- [ ] Quick filter presets (up to 10 saved filters inline)
- [ ] Separator between groups for visual clarity

### D3. Saved Filters

- [ ] Save current filter with name
- [ ] Load saved filter applies expression
- [ ] Delete saved filter removes it
- [ ] Filters are per-user per-panel
- [ ] Default saved filter auto-applies on panel load
- [ ] Quick filter presets appear in column right-click menu

### D4. Export & Print

- [ ] Export to Clipboard works from context menu
- [ ] Export to Clipboard works from ribbon button
- [ ] Print Preview opens for active grid
- [ ] Column Chooser opens for active grid
- [ ] CSV export via DX TableView.ExportToCsv()
- [ ] Default filename includes panel name + date (e.g. Devices_20260328.csv)
- [ ] Toast notification on successful export or error

### D5. View Toggles (Home Ribbon)

- [ ] Search Panel toggle shows/hides search
- [ ] Filter Row toggle shows/hides auto-filter row
- [ ] Group Panel toggle shows/hides group area
- [ ] Grid Lines toggle shows/hides cell borders
- [ ] Best Fit auto-sizes columns

### D6. Undo/Redo

- [ ] Undo button reverts last operation — `UndoServiceTests.Undo_RevertsSinglePropertyChange`
- [ ] Redo button re-applies undone operation — `UndoServiceTests.Redo_ReappliesPropertyChange`
- [ ] Split button dropdown shows last 10 operations — `UndoServiceTests.UndoHistory_ReturnsDescriptionsInOrder`
- [ ] Delete records can be undone (RecordRemove) — `UndoServiceTests.RecordRemove_UndoReinsertsItem`
- [ ] Multi-row delete uses BeginBatch/CommitBatch — `UndoServiceTests.Batch_CommitsMultipleChanges`
- [ ] Undo initial state empty — `UndoServiceTests.Initial_State_Empty`
- [ ] New change clears redo stack — `UndoServiceTests.NewChange_ClearsRedoStack`
- [ ] Batch merges consecutive same-property changes — `UndoServiceTests.Batch_MergesConsecutiveSamePropertyChanges`
- [ ] Discard batch does not commit — `UndoServiceTests.DiscardBatch_DoesNotCommit`
- [ ] Commit empty batch does not push — `UndoServiceTests.CommitBatch_EmptyBatch_DoesNotPush`
- [ ] Clear removes all undo/redo history — `UndoServiceTests.Clear_RemovesAllHistory`
- [ ] RecordAdd undo removes item — `UndoServiceTests.RecordAdd_UndoRemovesItem`
- [ ] RecordAdd redo re-adds item — `UndoServiceTests.RecordAdd_RedoReAddsItem`
- [ ] RecordRemove redo removes again — `UndoServiceTests.RecordRemove_RedoRemovesAgain`
- [ ] StateChanged fires on undo/redo/push/clear — `UndoServiceTests.StateChanged_FiresOnUndoPush`, `UndoServiceTests.StateChanged_FiresOnUndo`, `UndoServiceTests.StateChanged_FiresOnRedo`, `UndoServiceTests.StateChanged_FiresOnClear`
- [ ] Multiple undo/redo works correctly — `UndoServiceTests.MultipleUndoRedo_WorksCorrectly`
- [ ] Undo when empty does nothing — `UndoServiceTests.Undo_WhenEmpty_DoesNothing`
- [ ] Redo when empty does nothing — `UndoServiceTests.Redo_WhenEmpty_DoesNothing`

### D7. Bulk Edit

- [ ] BulkEditWindow opens from context menu
- [ ] Field picker shows all editable fields (reflection-based)
- [ ] Preview shows changes before applying
- [ ] Apply updates all selected rows
- [ ] Works with any model type

### D8. Toast Notifications

- [ ] Info toast (blue) auto-hides after 4s — `NotificationTypeTests.Notification_Color_ByType(Info, "#3B82F6")`
- [ ] Success toast (green) auto-hides after 4s — `NotificationTypeTests.Notification_Color_ByType(Success, "#22C55E")`
- [ ] Warning toast (amber) auto-hides after 4s — `NotificationTypeTests.Notification_Color_ByType(Warning, "#F59E0B")`
- [ ] Error toast (red) auto-hides after 4s — `NotificationTypeTests.Notification_Color_ByType(Error, "#EF4444")`
- [ ] Toast appears bottom-right
- [ ] SignalR DataChanged triggers toast for external changes

### D9. Layout Persistence

- [ ] Grid column widths save/restore
- [ ] Grid column order saves/restores
- [ ] Grid column visibility saves/restores
- [ ] Dock panel positions save/restore
- [ ] Window bounds (size, position) save/restore
- [ ] Panel open/close states save/restore
- [ ] Restore Default resets to XAML defaults
- [ ] Layout saved to user_settings table per user
- [ ] Grid customizations per panel per user (JSONB)
- [ ] Link rules per panel per user

### D10. Panel Floating / Multi-Monitor

- [ ] Any panel tab can be dragged out to a separate window
- [ ] Floating panels are real OS windows (FloatingMode.Desktop)
- [ ] Floating panels can be moved to second monitor and maximized
- [ ] Drag floating panel back to dock it into the main window
- [ ] Right-click tab > "Float" works as alternative to dragging
- [ ] EnableGlobalFloating covers all panels including closed ones
- [ ] Layout save/restore preserves floating panel positions

### D11. Grid Customizer

- [ ] GridCustomizerDialog with row height, alternating rows, summary footer, group panel, auto-filter
- [ ] panel_customizations table stores per-user per-panel settings as JSONB — `PanelCustomizationExtendedTests.PanelCustomizationRecord_SetValues`
- [ ] Settings: grid, filter, form, link types — `PanelCustomizationExtendedTests.GridSettings_SetLists_WorkCorrectly`, `PanelCustomizationExtendedTests.FormLayout_WithGroups`, `PanelCustomizationExtendedTests.FieldGroup_Collapsed`, `PanelCustomizationExtendedTests.LinkRule_WithValues`
- [ ] Customizer wired to all 22 grids across all modules

### D12. Panel Customization Models

- [ ] GridSettings defaults -- `PanelCustomizationTests.GridSettings_Defaults`, `PanelCustomizationModelsTests` (5 tests)
- [ ] FormLayout defaults -- `PanelCustomizationTests.FormLayout_Defaults`
- [ ] LinkRule defaults -- `PanelCustomizationTests.LinkRule_Defaults`
- [ ] FieldGroup defaults -- `PanelCustomizationTests.FieldGroup_Defaults`
- [ ] GridSettings serialization round-trip -- `PanelCustomizationTests.GridSettings_RoundTrip`
- [ ] LinkRule list serialization round-trip -- `PanelCustomizationTests.LinkRule_ListRoundTrip`
- [ ] Panel customization extended edge cases — `PanelCustomizationTests2` (6 tests)

### D13. Detail Panel (Asset Details)

- [ ] Right-docked detail panel visible
- [ ] Updates on grid row selection (SelectionChanged)
- [ ] Shows correct detail fields for selected entity

### D14. Config Compare Panel

- [ ] Side-by-side diff view with line numbers — `ConfigDiffServiceTests` (9 tests), `ConfigDiffServiceExtendedTests` (9 tests)
- [ ] Diff identical lines no changes — `ConfigDiffServiceExtendedTests.BuildAlignedDiff_IdenticalLines_NoChanges`
- [ ] Diff fully different all changed — `ConfigDiffServiceExtendedTests.BuildAlignedDiff_FullyDifferent_AllChanged`
- [ ] Diff added/removed lines — `ConfigDiffServiceExtendedTests.BuildAlignedDiff_AddedLines`, `ConfigDiffServiceExtendedTests.BuildAlignedDiff_RemovedLines`
- [ ] Diff empty old/new/both — `ConfigDiffServiceExtendedTests.BuildAlignedDiff_EmptyOld`, `ConfigDiffServiceExtendedTests.BuildAlignedDiff_EmptyNew`, `ConfigDiffServiceExtendedTests.BuildAlignedDiff_BothEmpty`
- [ ] Diff large config handles correctly — `ConfigDiffServiceExtendedTests.BuildAlignedDiff_LargeConfig_HandlesCorrectly`
- [ ] Diff duplicate lines handled — `ConfigDiffServiceExtendedTests.BuildAlignedDiff_DuplicateLines_HandledCorrectly`
- [ ] Pink highlighting on changed lines — `ConfigDiffServiceTests.OneLine_Replaced`
- [ ] Synced scroll between left and right panels
- [ ] Compare button toggles panel from Details > Config tab
- [ ] ConfigVersionEntry defaults (empty strings, zero values) — `ConfigVersionEntryTests.Defaults_AreCorrect`
- [ ] ConfigVersionEntry DisplayDate format — `ConfigVersionEntryTests.DisplayDate_FormatsCorrectly`, `ConfigVersionEntryTests.DisplayDate_MinValue`
- [ ] ConfigVersionEntry DisplayVersion format — `ConfigVersionEntryTests.DisplayVersion_FormatsCorrectly`, `ConfigVersionEntryTests.DisplayVersion_VariousNumbers`
- [ ] ConfigVersionEntry DisplaySummary format — `ConfigVersionEntryTests.DisplaySummary_FormatsCorrectly`, `ConfigVersionEntryTests.DisplaySummary_EmptyDiffStatus`
- [ ] ConfigVersionEntry IsSelected PropertyChanged — `ConfigVersionEntryTests.IsSelected_PropertyChanged_Fires`, `ConfigVersionEntryTests.IsSelected_PropertyChanged_FiresOnToggle`
- [ ] ConfigVersionEntry Id assignment — `ConfigVersionEntryTests.Id_CanBeAssigned`

### D15. Import Wizard

- [ ] Ctrl+I opens Import Wizard dialog
- [ ] Browse button opens file picker (CSV/TSV)
- [ ] Target table dropdown lists all DB tables
- [ ] Upsert key field (default: id)
- [ ] Field mapping grid: CSV Column, Sample Value, Target Column, Converter, Skip checkbox
- [ ] Column names auto-suggested from CSV headers (snake_case)
- [ ] Sample values shown from first row
- [ ] 6 converter types available: direct, constant, expression, date_format, combine, split
- [ ] Import button processes all rows with field mapping + converters
- [ ] Progress shown: imported count + failed count
- [ ] Audit log entry on successful import
- [ ] Notification toast on completion
- [ ] "Import Data" button in Home > Export ribbon group

### D16. Home Dashboard Panel

- [ ] Dashboard panel is the first tab (before Devices)
- [ ] Platform KPIs: Total Devices, Active Switches, Active Users, Total Links, VLANs, Tasks Open — `DashboardDataTests.Defaults`, `DashboardDataExtendedTests.DashboardData_AllPlatformDefaults`, `DashboardDataExtendedTests.DashboardData_SetPlatformCounts`
- [ ] Service Desk KPIs: Open Tickets, Closed Today, Avg Resolution (hours), SLA Compliant (%) — `DashboardDataExtendedTests.DashboardData_AllServiceDeskDefaults`, `DashboardDataExtendedTests.DashboardData_SetServiceDeskCounts`
- [ ] System Health KPIs: Sync Configs, Sync Failures, Auth Events (24h), Failed Logins (24h) — `DashboardDataExtendedTests.DashboardData_SystemHealthDefaults`
- [ ] All KPI cards show trend arrows (green/red) vs previous period
- [ ] Recent Activity feed shows last 20 auth events + sync log entries — `DashboardDataExtendedTests.DashboardData_RecentActivity_DefaultEmpty`, `DashboardDataExtendedTests.DashboardData_RecentActivity_CanBePopulated`
- [ ] ActivityItem defaults and set properties — `DashboardDataExtendedTests.ActivityItem_Defaults`, `DashboardDataExtendedTests.ActivityItem_SetProperties`
- [ ] Refresh button reloads all dashboard data
- [ ] Last refreshed timestamp shown
- [ ] Dashboard loads on panel activation (lazy)

### D17. Cross-Panel Features

- [ ] Save device triggers links panel refresh (DataModifiedMessage)
- [ ] Save switch triggers related panels refresh
- [ ] Delete entity triggers dependent panels refresh
- [ ] Go to Switch A/B from Links context menu
- [ ] Go to Device from Switches context menu
- [ ] Navigation activates target panel + selects row

---

## E. Modules

### E1. IPAM (Devices)

#### Grid
- [ ] DeviceRecord grid loads all devices from DB
- [ ] Inline editing works (NavigationStyle=Cell)
- [ ] Dropdown columns wired via BindComboSources()
- [ ] RESERVED rows highlighted with distinct background — `DeviceRecordTests.StatusColor_Reserved_Amber`
- [ ] ValidateRow auto-saves on row commit
- [ ] TotalSummary shows device count
- [ ] GroupSummary shows count per group
- [ ] ShowFilterPanelMode="ShowAlways" visible
- [ ] ShowAutoFilterRow visible
- [ ] Search panel works (full-text)
- [ ] Column filtering works
- [ ] Alternating row colors (UseEvenRowBackground)
- [ ] Primary column frozen (Fixed=Left)
- [ ] Natural numeric sort on interface columns

#### CRUD
- [ ] New device creates empty row — `DeviceRecordTests.Defaults_EmptyStrings`
- [ ] Edit device modifies existing row — `DeviceRecordTests.PropertyChanged_AllFieldsFire`
- [ ] Delete device removes row (with confirmation)
- [ ] Duplicate device creates copy
- [ ] Undo after delete restores row — `UndoServiceTests.RecordRemove_UndoReinsertsItem`

#### Context Menu
- [ ] Right-click shows context menu
- [ ] New/Edit/Duplicate/Delete actions work
- [ ] Bulk Edit Selected opens BulkEditWindow
- [ ] Export to Clipboard copies grid data
- [ ] Cross-panel navigation (Go to Switch) works
- [ ] Refresh reloads data

#### Device Status Colors
- [ ] Device status Active = green — `DeviceRecordTests.StatusColor_Active_Green`
- [ ] Device status Reserved = amber — `DeviceRecordTests.StatusColor_Reserved_Amber`
- [ ] Device status Decommissioned = red — `DeviceRecordTests.StatusColor_Decommissioned_Red`
- [ ] Device status Maintenance = purple — `DeviceRecordTests.StatusColor_Maintenance_Purple`
- [ ] Device status unknown = grey — `DeviceRecordTests.StatusColor_Unknown_Grey`
- [ ] IsLinked true when LinkedHostname set — `DeviceRecordTests.IsLinked_True_WhenLinkedHostnameSet`, `DeviceRecordExtendedTests.IsLinked_WithHostname_True`, `DeviceRecordExtendedTests.IsLinked_Empty_False`
- [ ] IsActive true when status Active — `DeviceRecordTests.IsActive_True_WhenStatusActive`, `DeviceRecordExtendedTests.IsActive_Active_True`, `DeviceRecordExtendedTests.IsActive_Reserved_False`, `DeviceRecordExtendedTests.IsActive_Decommissioned_False`
- [ ] DeviceRecord PropertyChanged fires on all key fields — `DeviceRecordExtendedTests.PropertyChanged_Fires_OnAllKeys`
- [ ] DeviceRecord DetailLinks default empty — `DeviceRecordExtendedTests.DetailLinks_DefaultEmpty`
- [ ] DeviceRecord defaults all empty strings — `DeviceRecordExtendedTests.Defaults_AllEmptyStrings`
- [ ] DeviceLinkSummary defaults — `DeviceRecordExtendedTests.DeviceLinkSummary_Defaults`

#### Server Model
- [ ] Server PopulateNicDetails all four NICs — `ServerModelTests.PopulateNicDetails_AllFourNics`
- [ ] Server PopulateNicDetails only populated NICs — `ServerModelTests.PopulateNicDetails_OnlyPopulatedNics`
- [ ] Server PopulateNicDetails no NICs returns empty — `ServerModelTests.PopulateNicDetails_NoNics_EmptyList`
- [ ] Server PopulateNicDetails clears previous — `ServerModelTests.PopulateNicDetails_ClearsPrevious`
- [ ] Server default status is RESERVED — `ServerModelTests.Server_DefaultStatus_Reserved`
- [ ] Server PropertyChanged fires — `ServerModelTests.Server_PropertyChanged_Fires`

#### Server AS / ASN
- [ ] ServerAS defaults (Active status, empty building/ASN) — `ServerASTests.Defaults_AreCorrect`
- [ ] ServerAS PropertyChanged fires on all fields (Id, Building, ServerAsn, Status) — `ServerASTests.PropertyChanged_Id_Fires`, `ServerASTests.PropertyChanged_Building_Fires`, `ServerASTests.PropertyChanged_ServerAsn_Fires`, `ServerASTests.PropertyChanged_Status_Fires`, `ServerASTests.AllProperties_FirePropertyChanged`
- [ ] ServerAS status can be changed — `ServerASTests.Status_CanBeChanged`

#### Master-Detail
- [ ] Row expansion shows detail data — `MasterDeviceTests.Defaults`, `MasterDeviceTests.PropertyChanged_SelectedFields`
- [ ] Detail grid has TotalSummary

### E2. Switches

#### Grid
- [ ] SwitchRecord grid loads all switches
- [ ] Ping status icons (green/red/grey circles) display correctly — `SwitchRecordTests.PingColor_Ok_Green`, `SwitchRecordTests.PingColor_Failed_Red`, `SwitchRecordTests.PingColor_Unknown_Grey`
- [ ] SSH status icons display correctly — `SwitchRecordTests.SshColor_Ok_Green`, `SwitchRecordTests.SshColor_Failed_Red`, `SwitchRecordTests.SshColor_Unknown_Grey`
- [ ] Latency column shows ping time — `SwitchRecordTests.PingStatus_Ok_ShowsLatency`
- [ ] INotifyPropertyChanged updates icons in real-time — `SwitchRecordTests.LastPingOk_Change_NotifiesPingStatus_PingIcon_PingColor`
- [ ] Uptime parsing and display — `SwitchRecordTests.UptimeMinutes_ParsesCorrectly`, `SwitchRecordTests.UptimeDisplay_FormatsCorrectly`
- [ ] Loopback display with prefix — `SwitchRecordTests.LoopbackDisplay_WithPrefix`
- [ ] EffectiveSshIp uses override when set — `SwitchRecordTests.EffectiveSshIp_UsesOverrideWhenSet`
- [ ] EffectiveSshIp falls back to management IP — `SwitchRecordTests.EffectiveSshIp_FallsBackToManagementIp`
- [ ] SSH port defaults to 22 — `SwitchRecordTests.SshPort_Defaults_To22`
- [ ] IsPinging shows hourglass icon — `SwitchRecordTests.PingIcon_IsPinging_ShowsHourglass`
- [ ] UptimeMinutes edge cases (365d, 0d 0h 1m, 1d only, non-numeric, unrecognized unit) — `SwitchRecordExtendedTests.UptimeMinutes_EdgeCases`
- [ ] UptimeDisplay edge cases (1m, 1h 0m, 1d 0h 0m) — `SwitchRecordExtendedTests.UptimeDisplay_EdgeCases`
- [ ] PingIcon IsPinging overrides OK status — `SwitchRecordExtendedTests.PingIcon_IsPinging_OverridesOkStatus`
- [ ] PingStatus high latency / sub-millisecond — `SwitchRecordExtendedTests.PingStatus_HighLatency_ShowsRoundedMs`, `SwitchRecordExtendedTests.PingStatus_SubMillisecond`
- [ ] SshIcon all states (true/false/null) — `SwitchRecordExtendedTests.SshIcon_AllStates`
- [ ] SshStatus unknown shows dash — `SwitchRecordExtendedTests.SshStatus_Unknown_ShowsDash`
- [ ] LoopbackDisplay null IP empty — `SwitchRecordExtendedTests.LoopbackDisplay_NullIp_Empty`
- [ ] PropertyChanged Hostname/ManagementIp/LastPingMs/LoopbackPrefix cascades — `SwitchRecordExtendedTests.PropertyChanged_Hostname`, `SwitchRecordExtendedTests.PropertyChanged_ManagementIp`, `SwitchRecordExtendedTests.PropertyChanged_LastPingMs_NotifiesPingLatency`, `SwitchRecordExtendedTests.PropertyChanged_LoopbackPrefix_NotifiesLoopbackDisplay`
- [ ] DetailInterfaces default empty — `SwitchRecordExtendedTests.DetailInterfaces_DefaultEmpty`
- [ ] EffectiveSshIp both empty returns empty — `SwitchRecordExtendedTests.EffectiveSshIp_BothEmpty_ReturnsEmpty`
- [ ] All status fields defaults — `SwitchRecordExtendedTests.AllStatusFields_Defaults`

#### Connectivity
- [ ] Ping All button pings all switches in parallel
- [ ] Ping Selected button pings selected switches
- [ ] Sync BGP button syncs BGP config from live switch
- [ ] Sync All BGP syncs all switches
- [ ] ResolveCredentials from switch — `DeployServiceTests.ResolveCredentials_FromSwitch`
- [ ] ResolveCredentials falls back to device — `DeployServiceTests.ResolveCredentials_FallsBackToDevice`
- [ ] ResolveCredentials no match uses defaults — `DeployServiceTests.ResolveCredentials_NoMatch_DefaultsUsed`
- [ ] ResolveCredentials SshOverrideIp used first — `DeployServiceTests.ResolveCredentials_SwitchOverrideIp_UsedFirst`
- [ ] SshCredentials IsValid with/without IP/password — `DeployServiceTests.SshCredentials_IsValid_WithIpAndPassword`, `DeployServiceTests.SshCredentials_IsValid_NoIp_False`, `DeployServiceTests.SshCredentials_IsValid_NoPassword_False`
- [ ] SshCredentials defaults (port=22, username=admin) — `DeployServiceTests.SshCredentials_Defaults`

#### MLAG & MSTP Models
- [ ] MlagConfig defaults and PropertyChanged — `MlagMstpTests.MlagConfig_Defaults`, `MlagMstpTests.MlagConfig_PropertyChanged_AllFields`
- [ ] MstpConfig defaults and PropertyChanged — `MlagMstpTests.MstpConfig_Defaults`, `MlagMstpTests.MstpConfig_PropertyChanged_AllFields`

#### Interfaces & Optics
- [ ] SwitchInterface parse extracts interfaces from PicOS output — `SwitchInterfaceTests.Parse_PicOsFormat_ExtractsInterfaces`
- [ ] SwitchInterface parse empty output returns empty — `SwitchInterfaceTests.Parse_EmptyOutput_ReturnsEmpty`
- [ ] SwitchInterface parse skips non-interface lines — `SwitchInterfaceTests.Parse_SkipsNonInterfaceLines`
- [ ] SwitchInterface status color by link status — `SwitchInterfaceTests.StatusColor_ByLinkStatus`
- [ ] SwitchInterface LinkStatus change notifies StatusColor — `SwitchInterfaceTests.LinkStatus_Change_NotifiesStatusColor`
- [ ] SwitchInterface MergeOptics updates matching interfaces — `SwitchInterfaceTests.MergeOptics_UpdatesMatchingInterfaces`
- [ ] SwitchInterface MergeOptics RxColor red for no light — `SwitchInterfaceTests.MergeOptics_RxColor_Red_NoLight`
- [ ] SwitchInterface MergeOptics RxColor yellow for marginal — `SwitchInterfaceTests.MergeOptics_RxColor_Yellow_Marginal`
- [ ] SwitchInterface MergeLldp merges neighbor info — `SwitchInterfaceTests.MergeLldp_MergesNeighborInfo`
- [ ] InterfaceOptics display helpers — `InterfaceOpticsTests.DisplayTx_WithValue`, `InterfaceOpticsTests.DisplayRx_WithValue`, `InterfaceOpticsTests.DisplayTemp_WithValue`
- [ ] InterfaceOptics RxColor by power level — `InterfaceOpticsTests.RxColor_ByPowerLevel`
- [ ] InterfaceOptics parse basic single interface — `InterfaceOpticsTests.Parse_BasicSingleInterface`

#### Switch Version Parsing
- [ ] SwitchVersion parse extracts hardware model, serial, uptime, MAC, versions — `SwitchVersionTests.Parse_BasicOutput_ExtractsFields`
- [ ] SwitchVersion parse empty output returns defaults — `SwitchVersionTests.Parse_EmptyOutput_DefaultFields`
- [ ] SwitchVersion parse MAC Ethernet variant — `SwitchVersionTests.Parse_MACEthernetVariant_ExtractsMac`
- [ ] SwitchVersion parse L2L3 version — `SwitchVersionTests.Parse_L2L3Version_FromOsKey`
- [ ] SwitchVersion CapturedAt is set to now — `SwitchVersionTests.Parse_CapturedAt_IsSetToNow`
- [ ] SwitchVersion handles Windows line endings — `SwitchVersionTests.Parse_WindowsLineEndings_Works`

#### Context Menu
- [ ] All standard context menu items present
- [ ] Go to Device navigates to IPAM panel

### E3. Links (P2P, B2B, FW)

#### P2P Links
- [ ] Grid loads point-to-point links
- [ ] Inline editing works
- [ ] Multi-column port dropdowns show Interface, Admin, Link, Speed, Description, LLDP
- [ ] Master-detail shows PicOS config preview (DetailConfigLines) — `NetworkLinkTests.GenerateDetailConfig_PopulatesBothSides`
- [ ] Config preview uses green monospace font
- [ ] DataModifiedMessage fires on save
- [ ] P2PLink BuildConfig side A generates correct commands — `NetworkLinkTests.P2PLink_BuildConfig_SideA_GeneratesCommands`, `DeployServiceTests.BuildP2PCommands_SideA_GeneratesCorrectCommands`
- [ ] P2PLink BuildConfig side B uses correct IP and port — `NetworkLinkTests.P2PLink_BuildConfig_SideB_UsesCorrectIpAndPort`, `DeployServiceTests.BuildP2PCommands_SideB_GeneratesCorrectCommands`
- [ ] P2PLink BuildConfig empty VLAN returns empty — `NetworkLinkTests.P2PLink_BuildConfig_EmptyVlan_ReturnsEmpty`, `DeployServiceTests.BuildP2PCommands_EmptyVlan_ReturnsEmpty`
- [ ] P2PLink mismatch indicators work — `NetworkLinkTests.P2PLink_MismatchA_AffectsColor`
- [ ] Link validation all fields set returns empty — `NetworkLinkTests.Validate_AllFieldsSet_ReturnsEmpty`
- [ ] Link validation missing field returns warning — `NetworkLinkTests.Validate_MissingDeviceA_ReturnsWarning`
- [ ] Link validation icon shows complete/incomplete — `NetworkLinkTests.ValidationIcon_Complete`
- [ ] Link prefix extraction — `NetworkLinkTests.LinkHelper_ExtractPrefix`, `NetworkLinkExtendedTests.LinkHelper_ExtractPrefix`
- [ ] P2P Validate complete no warnings — `NetworkLinkExtendedTests.P2PLink_Validate_Complete_NoWarnings`
- [ ] P2P Validate missing DeviceA/Vlan/Subnet warnings — `NetworkLinkExtendedTests.P2PLink_Validate_MissingDeviceA_Warning`, `NetworkLinkExtendedTests.P2PLink_Validate_MissingVlan_Warning`, `NetworkLinkExtendedTests.P2PLink_Validate_MissingSubnet_Warning`
- [ ] P2P ValidationIcon/Color complete vs incomplete — `NetworkLinkExtendedTests.P2PLink_ValidationIcon_Complete_Check`, `NetworkLinkExtendedTests.P2PLink_ValidationIcon_Incomplete_Warning`
- [ ] P2P ValidationTooltip complete/incomplete — `NetworkLinkExtendedTests.P2PLink_ValidationTooltip_Incomplete_ContainsWarnings`, `NetworkLinkExtendedTests.P2PLink_ValidationTooltip_Complete_Ready`
- [ ] P2P MismatchA/B color indicators — `NetworkLinkExtendedTests.P2PLink_MismatchA_Color`, `NetworkLinkExtendedTests.P2PLink_NoMismatch_DefaultColor`, `NetworkLinkExtendedTests.P2PLink_MismatchB_Color`
- [ ] P2P GenerateDetailConfig populates both sides — `NetworkLinkExtendedTests.P2PLink_GenerateDetailConfig_PopulatesLines`
- [ ] B2B GenerateDetailConfig includes BGP — `NetworkLinkExtendedTests.B2BLink_GenerateDetailConfig_IncludesBgp`
- [ ] FW BuildConfig side A/B has FW prefix — `NetworkLinkExtendedTests.FWLink_BuildConfig_SideA_HasFWPrefix`, `NetworkLinkExtendedTests.FWLink_BuildConfig_SideB_HasFWPrefix`
- [ ] ConfigA and ConfigB return joined commands — `NetworkLinkTests.ConfigA_And_ConfigB_ReturnJoinedCommands`, `NetworkLinkExtendedTests.P2PLink_ConfigA_ReturnsJoinedString`, `NetworkLinkExtendedTests.P2PLink_ConfigB_ReturnsJoinedString`
- [ ] LinkConfigLine defaults — `NetworkLinkExtendedTests.LinkConfigLine_Defaults`

#### B2B Links
- [ ] Grid loads back-to-back links
- [ ] BuildConfig generates `set protocols bgp neighbor ... remote-as ... bfd` — `NetworkLinkTests.B2BLink_BuildConfig_SideA_IncludesBgpNeighbor`, `DeployServiceTests.BuildB2BCommands_WithBgp_IncludesBgpNeighbor`
- [ ] B2B BuildConfig empty VLAN returns empty — `DeployServiceTests.BuildB2BCommands_EmptyVlan_ReturnsEmpty`
- [ ] Config does NOT emit `port-mode "trunk"`
- [ ] B2BLink BuildConfig no peer ASN skips BGP — `NetworkLinkTests.B2BLink_BuildConfig_NoPeerAsn_SkipsBgp`

#### FW Links
- [ ] Grid loads firewall links
- [ ] Config does NOT emit `port-mode "trunk"`
- [ ] FWLink BuildConfig switch side — `NetworkLinkTests.FWLink_BuildConfig_SwitchSide`, `DeployServiceTests.BuildFWCommands_SideA_GeneratesCorrectCommands`
- [ ] FWLink BuildConfig firewall side — `NetworkLinkTests.FWLink_BuildConfig_FirewallSide`
- [ ] FW BuildConfig empty VLAN returns empty — `DeployServiceTests.BuildFWCommands_EmptyVlan_ReturnsEmpty`

#### Config Builder Models
- [ ] ConfigLine record properties — `BuilderSectionTests.ConfigLine_Properties`
- [ ] ConfigLine equality — `BuilderSectionTests.ConfigLine_Equality`
- [ ] BuilderSection defaults — `BuilderSectionTests.BuilderSection_Defaults`
- [ ] BuilderSection PropertyChanged IsEnabled — `BuilderSectionTests.BuilderSection_PropertyChanged_IsEnabled`
- [ ] BuilderSection PropertyChanged LineCount — `BuilderSectionTests.BuilderSection_PropertyChanged_LineCount`
- [ ] BuilderSection Items is observable — `BuilderSectionTests.BuilderSection_Items_IsObservable`
- [ ] BuilderItem defaults — `BuilderSectionTests.BuilderItem_Defaults`
- [ ] BuilderItem PropertyChanged IsEnabled — `BuilderSectionTests.BuilderItem_PropertyChanged_IsEnabled`
- [ ] BuilderItem PropertyChanged DisplayText — `BuilderSectionTests.BuilderItem_PropertyChanged_DisplayText`

### E4. Routing (BGP)

- [ ] Top grid shows BGP config per switch (AS, router-id, settings) — `BgpRecordTests.BgpRecord_Defaults`
- [ ] Master-detail: bottom tabs show Neighbors + Advertised Networks — `BgpRecordTests.BgpNeighborRecord_Defaults`, `BgpRecordTests.BgpNetworkRecord_Defaults`
- [ ] SSH sync downloads live BGP config
- [ ] fast_external_failover, bestpath_multipath_relax columns editable — `BgpRecordTests.BgpRecord_PropertyChanged_AllFields`
- [ ] last_synced timestamp updates after sync

### E5. VLANs

- [ ] VLAN grid loads all VLANs
- [ ] Standard grid features (summary, filter, search, export)
- [ ] IsBlockRoot identifies /21 VLAN block roots — `VlanEntryTests.IsBlockRoot_ByVlanId`, `VlanEntryExtendedTests.IsBlockRoot_Various`
- [ ] RowColor blocked/default/root-downgraded styling — `VlanEntryTests.RowColor_Blocked_ReturnsBlockedColor`, `VlanEntryTests.RowColor_Default_ReturnsDefaultVlanColor`, `VlanEntryExtendedTests.RowColor_Blocked_ReturnsBlockedColor`, `VlanEntryExtendedTests.RowColor_RootDowngraded_ReturnsDowngradedColor`, `VlanEntryExtendedTests.RowColor_Default_ReturnsDefaultColor`, `VlanEntryExtendedTests.RowColor_Normal_Transparent`, `VlanEntryExtendedTests.RowColor_BlockedPriority_OverDefault`
- [ ] BlockLockedText locked/blocked display — `VlanEntryTests.BlockLockedText_Locked`, `VlanEntryTests.BlockLockedText_Blocked`, `VlanEntryExtendedTests.BlockLockedText_Locked`, `VlanEntryExtendedTests.BlockLockedText_Blocked`, `VlanEntryExtendedTests.BlockLockedText_Neither`
- [ ] SitePrefix maps building to IP prefix — `VlanEntryTests.SitePrefix_MapsCorrectly`, `VlanEntryExtendedTests.SitePrefix_KnownSites`
- [ ] SiteNetwork replaces placeholder with site prefix — `VlanEntryTests.SiteNetwork_ReplacesPlaceholder`, `VlanEntryExtendedTests.SiteNetwork_ReplacesOctet`, `VlanEntryExtendedTests.SiteNetwork_UnknownSite_Empty`, `VlanEntryExtendedTests.SiteNetwork_EmptySite_Empty`
- [ ] SiteGateway replaces placeholder with site prefix — `VlanEntryTests.SiteGateway_ReplacesPlaceholder`, `VlanEntryExtendedTests.SiteGateway_ReplacesOctet`, `VlanEntryExtendedTests.SiteGateway_UnknownSite_Empty`
- [ ] BuildingNumberMap case-insensitive — `VlanEntryTests.BuildingNumberMap_CaseInsensitive`, `VlanEntryExtendedTests.BuildingNumberMap_CaseInsensitive`
- [ ] BuildingNumberMap contains all expected sites — `VlanEntryExtendedTests.BuildingNumberMap_ContainsAllExpectedSites`
- [ ] VLAN PropertyChanged cascades — `VlanEntryTests.Site_NotifiesSiteNetwork_SiteGateway_SitePrefix`, `VlanEntryExtendedTests.PropertyChanged_BlockLocked_NotifiesBlockLockedText_RowColor`, `VlanEntryExtendedTests.PropertyChanged_IsBlocked_NotifiesRowColor`, `VlanEntryExtendedTests.PropertyChanged_IsDefault_NotifiesRowColor`, `VlanEntryExtendedTests.PropertyChanged_Site_NotifiesSiteNetwork_SiteGateway_SitePrefix`, `VlanEntryExtendedTests.PropertyChanged_Subnet_NotifiesRowColor`
- [ ] DetailSites default empty — `VlanEntryExtendedTests.DetailSites_DefaultEmpty`

### E6. Tasks

#### E6.1. Task Tree Panel

- [ ] Task grid loads with tree hierarchy
- [ ] Tree nodes expand/collapse
- [ ] CRUD operations work
- [ ] Project selector dropdown shows all projects + "(All Projects)"
- [ ] Sprint selector dropdown updates when project changes
- [ ] Type filter dropdown (Epic/Story/Task/Bug/SubTask/Milestone)
- [ ] Selecting a project reloads tasks filtered by project
- [ ] WBS column (read-only)
- [ ] Points column with DX SpinEdit (0-999, float)
- [ ] WorkRemaining column with DX SpinEdit (0-9999, float, n1 mask)
- [ ] Sprint name column (read-only)
- [ ] Category dropdown (Feature/Enhancement/TechDebt/Bug/Ops)
- [ ] Risk dropdown (None/Low/Medium/High/Critical)
- [ ] Start Date column with DX DateEdit
- [ ] Finish Date column with DX DateEdit
- [ ] Due Date column with DX DateEdit
- [ ] Color stripe column (4px colored bar from task.color)
- [ ] Milestone added to TaskType dropdown
- [ ] TotalSummary: task count, sum of points, sum of remaining hours
- [ ] ShowFilterPanelMode="ShowAlways" enabled
- [ ] ShowAutoFilterRow enabled
- [ ] AllowColumnFiltering enabled
- [ ] UseEvenRowBackground enabled
- [ ] Title column fixed left
- [ ] New tasks inherit selected project ID
- [ ] New sub-tasks inherit parent's project ID
- [ ] Tasks tab appears as permanent top-level ribbon tab (not context tab)

#### E6.2. Task Hierarchy & Schema (Phase 1)

- [ ] Portfolio model with INotifyPropertyChanged — `ProjectModelsTests.Portfolio_Defaults`, `ProjectModelsTests.Portfolio_PropertyChanged_Fires`
- [ ] Programme model with INotifyPropertyChanged — `ProjectModelsTests.Programme_Defaults`, `ProjectModelsTests.Programme_PropertyChanged_Fires`
- [ ] TaskProject model with INotifyPropertyChanged + DisplayName computed — `ProjectModelsTests.TaskProject_Defaults`, `ProjectModelsTests.TaskProject_DisplayName_Normal`, `ProjectModelsTests.TaskProject_DisplayName_Archived`, `ProjectModelsTests.TaskProject_PropertyChanged_Fires`
- [ ] ProjectMember model with INotifyPropertyChanged — `ProjectModelsTests.ProjectMember_DefaultRole`, `ProjectModelsTests.ProjectMember_PropertyChanged_Fires`
- [ ] Sprint model with INotifyPropertyChanged + DateRange + DisplayName computed — `ProjectModelsTests.Sprint_Defaults`, `ProjectModelsTests.Sprint_DateRange_BothDates`, `ProjectModelsTests.Sprint_DateRange_MissingDates_Empty`, `ProjectModelsTests.Sprint_DisplayName_IncludesStatus`
- [ ] Release model with INotifyPropertyChanged — `ProjectModelsTests.Release_Defaults`, `ProjectModelsTests.Release_PropertyChanged_Fires`
- [ ] TaskLink model with INotifyPropertyChanged + LinkDisplay computed — `ProjectModelsTests.TaskLink_Defaults`, `ProjectModelsTests.TaskLink_LinkDisplay`
- [ ] TaskDependency model with INotifyPropertyChanged + DepDisplay computed — `ProjectModelsTests.TaskDependency_Defaults`, `ProjectModelsTests.TaskDependency_DepDisplay_NoLag`, `ProjectModelsTests.TaskDependency_DepDisplay_WithLag`
- [ ] TaskItem expanded: 23 new fields (ProjectId, SprintId, Wbs, IsEpic, etc.)
- [ ] TaskItem computed: RiskColor, SeverityColor, StartDateDisplay, FinishDateDisplay, PointsDisplay — `TaskItemEdgeCaseTests` (29 tests)

#### E6.3. Task Repository

- [ ] GetPortfoliosAsync, UpsertPortfolioAsync, DeletePortfolioAsync
- [ ] GetProgrammesAsync, UpsertProgrammeAsync, DeleteProgrammeAsync
- [ ] GetTaskProjectsAsync, UpsertTaskProjectAsync, DeleteTaskProjectAsync
- [ ] GetProjectMembersAsync, UpsertProjectMemberAsync, RemoveProjectMemberAsync
- [ ] GetSprintsAsync, UpsertSprintAsync, DeleteSprintAsync
- [ ] GetReleasesAsync, UpsertReleaseAsync, DeleteReleaseAsync
- [ ] GetTaskLinksAsync, UpsertTaskLinkAsync, DeleteTaskLinkAsync
- [ ] GetTaskDependenciesAsync, UpsertTaskDependencyAsync, DeleteTaskDependencyAsync
- [ ] GetTasksAsync(projectId) filters by project when provided
- [ ] UpsertTaskAsync persists all 23 new fields on insert and update
- [ ] DeleteTaskAsync(id) executes DELETE FROM tasks

#### E6.4. Product Backlog (Phase 2)

- [ ] TaskBacklogPanel opens from ribbon Backlog toggle
- [ ] Project selector dropdown filters backlog items
- [ ] Sprint selector dropdown for commit target
- [ ] "Commit Selected" sets committed_to on multi-selected items
- [ ] "Uncommit" clears committed_to on multi-selected items
- [ ] Committed items show sprint name in Sprint column
- [ ] Category filter dropdown
- [ ] TreeListControl with drag-and-drop enabled (AllowDragDrop)
- [ ] BacklogPriority column sorted ascending
- [ ] Inline editing: Title, Category, Points, Tags
- [ ] TotalSummary: item count + sum of points
- [ ] Multi-select rows (MultiSelectMode="Row")
- [ ] ValidateNode auto-saves on row commit

#### E6.5. Sprint Planning (Phase 2)

- [ ] SprintPlanPanel opens from ribbon Sprint Plan toggle
- [ ] Project selector loads sprints for project
- [ ] Sprint selector filters grid to sprint items
- [ ] Sprint header shows name, date range, goal
- [ ] Capacity progress bar (blue < 80%, amber 80-100%, red > 100%)
- [ ] Capacity text shows "X / Y pts"
- [ ] "+ New Sprint" creates sprint with 2-week default
- [ ] "Close Sprint" records velocity, snapshots burndown, sets status=Closed
- [ ] SprintAllocation model defaults and PropertyChanged — `SprintPlanningModelsTests.SprintAllocation_Defaults`, `SprintPlanningModelsTests.SprintAllocation_PropertyChanged_Fires`, `SprintPlanningModelsTests.SprintAllocation_SetAllProperties`
- [ ] SprintBurndownPoint defaults and properties — `SprintPlanningModelsTests.SprintBurndownPoint_Defaults`, `SprintPlanningModelsTests.SprintBurndownPoint_SetAllProperties`
- [ ] GridControl with inline editing (Status, Points, WorkRemaining)
- [ ] SprintPriority column sorted ascending
- [ ] TotalSummary: item count + sum points + sum remaining hours

#### E6.6. Sprint Burndown (Phase 2)

- [ ] SprintBurndownPanel opens from ribbon Burndown toggle
- [ ] Sprint selector loads available sprints
- [ ] Actual burndown line (blue, with markers)
- [ ] Ideal burndown line (grey, thin)
- [ ] X-axis: dates, Y-axis: remaining (points or hours)
- [ ] Metric toggle: Points / Hours
- [ ] "Snapshot Now" captures current burndown data point
- [ ] Crosshair shows argument + value labels on hover
- [ ] Legend (top-right): Actual / Ideal
- [ ] Velocity summary bar

#### E6.7. Kanban Board (Phase 3)

- [ ] KanbanBoardPanel opens from ribbon Kanban toggle
- [ ] Project selector dropdown loads board for selected project
- [ ] Horizontal scrolling column layout (260px per column)
- [ ] Column headers show name + WIP count (red text when over limit) — `KanbanModelsTests.BoardColumn_Defaults`, `KanbanModelsTests.BoardColumn_HeaderDisplay_WithLimit`, `KanbanModelsTests.BoardColumn_HeaderDisplay_NoLimit`, `KanbanModelsTests.BoardColumn_IsOverWip_OverLimit_True`, `KanbanModelsTests.BoardColumn_IsOverWip_NoLimit_False`, `KanbanModelsTests.BoardColumn_IsOverWip_UnderLimit_False`, `KanbanModelsTests.BoardColumn_IsOverWip_AtLimit_False`
- [ ] WIP display format with/without limit — `KanbanModelsTests.BoardColumn_WipDisplay_WithLimit`, `KanbanModelsTests.BoardColumn_WipDisplay_NoLimit`
- [ ] Cards show: color stripe, title, type badge, priority icon, points, assigned, due date
- [ ] Drag-and-drop cards between columns
- [ ] Drop updates board_column + status
- [ ] WIP limit indicator turns red when exceeded — `KanbanModelsTests.BoardColumn_PropertyChanged_WipLimit_NotifiesRelated`, `KanbanModelsTests.BoardColumn_PropertyChanged_CurrentCount_NotifiesRelated`
- [ ] Swim lane selector (None, Assigned To, Priority, Type) — `KanbanModelsTests.BoardLane_Defaults`, `KanbanModelsTests.BoardLane_PropertyChanged_Fires`
- [ ] Double-click card opens TaskDetailPanel
- [ ] Dark theme styling

#### E6.8. Gantt Chart (Phase 4)

- [ ] GanttPanel opens from ribbon "Gantt" toggle
- [ ] Project selector loads tasks with start/finish dates
- [ ] DX GanttControl with KeyFieldName=Id, ParentFieldName=ParentId
- [ ] StartDateMapping, FinishDateMapping, NameMapping, ProgressMapping bound
- [ ] BaselineStartDateMapping, BaselineFinishDateMapping for overlay
- [ ] Columns: ID, Title (fixed left), Start, Finish, Status, Assigned, Points
- [ ] Milestones: IsMilestone=true get diamond rendering
- [ ] Zoom In / Zoom Out / Fit All buttons
- [ ] "Today" button fits range around current date
- [ ] "Save Baseline" captures project baseline — `TaskBaselineTests.TaskBaseline_Defaults`, `TaskBaselineTests.TaskBaseline_SetProperties`
- [ ] "Show Baseline" checkbox toggles baseline overlay
- [ ] "Critical Path" checkbox (placeholder)
- [ ] Auto-expand all nodes
- [ ] Inline editing (start/finish dates, title, points)

#### E6.9. QA & Bug Tracking (Phase 6)

- [ ] QAPanel opens from ribbon "QA / Bugs" toggle
- [ ] Project selector filters bugs by project
- [ ] "+ New Bug" creates task with type=Bug, severity=Major, status=New
- [ ] "Batch Triage" multi-selects bugs, sets severity + priority + status=Triaged
- [ ] Severity filter dropdown (Blocker/Critical/Major/Minor/Cosmetic)
- [ ] Status filter dropdown (New/Triaged/InProgress/Resolved/Verified/Closed)
- [ ] Bug Priority filter dropdown (Critical/High/Medium/Low)
- [ ] Severity column with colored dot indicator — `TaskModelsTests.TaskItem_SeverityColor_MapsCorrectly`
- [ ] Bug Priority column (editable dropdown, separate from severity)
- [ ] ID column (read-only, fixed left)
- [ ] TotalSummary: bug count + sum of points
- [ ] Multi-select rows for batch operations

#### E6.10. QA Dashboard (Phase 6)

- [ ] QADashboardPanel opens from ribbon "QA Dashboard" toggle
- [ ] Bugs by Severity chart (bar chart, red)
- [ ] Bug Aging chart (bar chart, amber, 6 time buckets)
- [ ] Opened vs Closed chart (line chart, red=opened, green=closed, last 30 days)
- [ ] Open Bugs by Assignee chart (bar chart, blue, top 10)
- [ ] 2x2 grid layout

#### E6.11. Custom Columns (Phase 7)

- [ ] Custom columns defined per project (Text/RichText/Number/Hours/DropList/Date/DateTime/People/Computed) — `TaskModelsTests.CustomColumn_GetDropListOptions_ParsesJson`, `TaskModelsTests.CustomColumn_GetAggregationType_ParsesJson`, `CustomColumnModelsTests.CustomColumn_Defaults`, `CustomColumnModelsTests.CustomColumn_PropertyChanged_Fires`
- [ ] CustomColumn GetDropListOptions: empty/valid JSON/no key/invalid JSON — `CustomColumnModelsTests.GetDropListOptions_EmptyConfig_ReturnsEmpty`, `CustomColumnModelsTests.GetDropListOptions_ValidJson_ReturnsOptions`, `CustomColumnModelsTests.GetDropListOptions_NoOptionsKey_ReturnsEmpty`, `CustomColumnModelsTests.GetDropListOptions_InvalidJson_ReturnsEmpty`
- [ ] CustomColumn GetAggregationType: empty/valid/no key/invalid — `CustomColumnModelsTests.GetAggregationType_EmptyConfig_ReturnsNull`, `CustomColumnModelsTests.GetAggregationType_ValidJson_ReturnsType`, `CustomColumnModelsTests.GetAggregationType_NoKey_ReturnsNull`, `CustomColumnModelsTests.GetAggregationType_InvalidJson_ReturnsNull`
- [ ] TaskCustomValue DisplayValue by type (Text/Number/Hours/Date/DateTime) — `CustomColumnModelsTests.TaskCustomValue_DisplayValue_ByType`
- [ ] TaskCustomValue null values return empty — `CustomColumnModelsTests.TaskCustomValue_DisplayValue_NullValues_Empty`
- [ ] TaskCustomValue PropertyChanged cascades to DisplayValue — `CustomColumnModelsTests.TaskCustomValue_PropertyChanged_ValueText_NotifiesDisplayValue`, `CustomColumnModelsTests.TaskCustomValue_PropertyChanged_ValueNumber_NotifiesDisplayValue`, `CustomColumnModelsTests.TaskCustomValue_PropertyChanged_ValueDate_NotifiesDisplayValue`
- [ ] CustomColumnPermission defaults (CanView/CanEdit true) — `CustomColumnModelsTests.CustomColumnPermission_Defaults`
- [ ] Dynamic column rendering in TaskTreePanel
- [ ] Type-aware editors: SpinEdit, DateEdit, ComboBoxEdit
- [ ] Custom columns load when project changes
- [ ] Custom values populated per task via CustomValues dictionary

#### E6.12. Report Builder (Phase 8)

- [ ] ReportBuilderPanel opens from ribbon "Reports" toggle
- [ ] Report selector dropdown loads saved reports — `ReportModelsTests.SavedReport_Defaults`, `ReportModelsTests.SavedReport_DisplayPath_NoFolder`, `ReportModelsTests.SavedReport_DisplayPath_WithFolder`, `ReportModelsTests.SavedReport_PropertyChanged_Fires`
- [ ] ReportFilter defaults — `ReportModelsTests.ReportFilter_Defaults`
- [ ] Entity type selector (task, device, switch) — `SprintAndPlanningTests.ReportQuery_Defaults`, `ReportModelsTests.ReportQuery_Defaults`
- [ ] Filter builder: add/clear filter conditions via DX GridControl
- [ ] 11 operators: =, !=, >, <, >=, <=, contains, between, in, isNull, isNotNull
- [ ] Logic column (AND/OR)
- [ ] "Run Query" executes filters, populates results grid
- [ ] Results grid with auto-generated columns from DataTable
- [ ] "Save Report" persists query as JSON
- [ ] "Export CSV" exports result DataTable

#### E6.13. Task Dashboard (Phase 8)

- [ ] TaskDashboardPanel opens from ribbon "Dashboard" toggle
- [ ] Tasks by Status -- pie chart
- [ ] Points by Type -- bar chart
- [ ] Tasks Created (30 days) -- line chart
- [ ] Sprint Velocity -- bar chart (last 10 closed sprints)
- [ ] 2x2 grid layout — `SprintAndPlanningTests.DashboardTile_Defaults`, `SprintAndPlanningTests.Dashboard_PropertyChanged`, `ReportModelsTests.Dashboard_Defaults`, `ReportModelsTests.Dashboard_PropertyChanged_Fires`, `ReportModelsTests.DashboardTile_Defaults`, `ReportModelsTests.DashboardTile_SetProperties`

#### E6.14. Timesheet (Phase 9)

- [ ] TimesheetPanel opens from ribbon toggle
- [ ] Week picker loads entries for Mon-Sun
- [ ] Hours column (SpinEdit 0-24), Activity dropdown (5 types), Notes — `SprintAndPlanningTests.TimeEntry_PropertyChanged_Fires`, `TimeActivityModelsTests.TimeEntry_Defaults`, `TimeActivityModelsTests.TimeEntry_EntryDateDisplay`, `TimeActivityModelsTests.TimeEntry_PropertyChanged_Fires`
- [ ] TotalSummary: sum hours + entry count
- [ ] Total hours green display in toolbar
- [ ] ValidateRow auto-saves

#### E6.15. Activity Feed (Phase 9)

- [ ] ActivityFeedPanel opens from ribbon toggle
- [ ] Project selector + refresh
- [ ] Card template: action icon, summary, user, time ago — `SprintAndPlanningTests.ActivityFeedItem_TimeAgo_JustNow`, `SprintAndPlanningTests.ActivityFeedItem_TimeAgo_Minutes`, `TimeActivityModelsTests.ActivityFeedItem_ActionIcon_Correct`, `TimeActivityModelsTests.ActivityFeedItem_TimeAgo_JustNow`, `TimeActivityModelsTests.ActivityFeedItem_TimeAgo_Minutes`, `TimeActivityModelsTests.ActivityFeedItem_TimeAgo_Hours`, `TimeActivityModelsTests.ActivityFeedItem_TimeAgo_Days`
- [ ] TaskViewConfig defaults and PropertyChanged — `TimeActivityModelsTests.TaskViewConfig_Defaults`, `TimeActivityModelsTests.TaskViewConfig_PropertyChanged_Fires`
- [ ] Auto-populated by PG trigger

#### E6.16. My Tasks (Phase 10)

- [ ] Shows tasks assigned to current user across all projects
- [ ] Group By: None/Project/Due/Priority/Status
- [ ] Inline editing Status + WorkRemaining
- [ ] Summary: count, points, remaining

#### E6.17. Portfolio (Phase 10)

- [ ] Portfolio > Programme > Project hierarchy with roll-ups
- [ ] Columns: Name, Level, Tasks, Points, Complete%, OpenBugs, ActiveSprints
- [ ] BuildPortfolioTreeAsync aggregates from all data

#### E6.18. Task Import (Phase 11)

- [ ] TaskImportPanel opens from ribbon "Import" toggle
- [ ] Step 1: Browse file, format auto-detect (.xlsx/.csv/.xml), project selector
- [ ] Step 2: Column mapping grid with auto-detect, sample values
- [ ] Step 3: Preview + Import with progress bar
- [ ] "Update existing" checkbox matches by Title within project

#### E6.19. Task File Parser

- [ ] ParseFile routes by extension (.csv, .xml, .xlsx) -- `TaskFileParserTests` (10 tests)
- [ ] CSV parser: header row + data rows
- [ ] MS Project XML parser: XDocument, handles namespace
- [ ] MS Project fields: Name, WBS, Start, Finish, Duration, PercentComplete, Priority, Milestone, PredecessorLink
- [ ] Excel placeholder returns info message

#### E6.20. Task Context Menus (all panels)

- [ ] Task Tree: New Task, New Sub-Task, Delete Task, Export, Refresh
- [ ] Backlog: Commit to Sprint, Uncommit, Export, Refresh
- [ ] Sprint Plan: New Task in Sprint, Export, Refresh
- [ ] QA: New Bug, Batch Triage, Export, Refresh
- [ ] My Tasks: Go to Task in Tree, Export, Refresh
- [ ] Timesheet: Log Time, Delete Entry, Export, Refresh
- [ ] Report Results: Export to Clipboard, Export to CSV
- [ ] Portfolio: Refresh
- [ ] ExportTreeToClipboard helper: SelectAll + CopyToClipboard

#### E6.21. Task Module Engine Compliance

- [ ] SignalR DataChanged handlers for 13 new table types
- [ ] Task delete records UndoService.RecordRemove
- [ ] Undo restores deleted task to collection at original index
- [ ] Task delete publishes DataModifiedMessage("tasks", "Task", "Delete")
- [ ] Time entry delete publishes DataModifiedMessage
- [ ] SprintPlan, QA, MyTasks, Timesheet added to ActivePanel enum
- [ ] GetActiveGrid returns correct grid for each new panel
- [ ] Home tab actions work on SprintPlan/QA/MyTasks/Timesheet
- [ ] Tasks ribbon: Actions, Sprint, Scheduling, View, Panels (15 check buttons)
- [ ] DeleteTaskAsync: repo delete + DataModifiedMessage + undo
- [ ] All 15 task panels included in backstage close-all handler
- [ ] Delete confirmation dialog with "will delete all sub-tasks" warning

#### E6.22. Task Detail Panel

- [ ] TaskDetailDocPanel wired in DockLayoutManager
- [ ] Task tree CurrentItemChanged shows task in detail panel
- [ ] Detail shows: status icon, title, type, priority, assigned, dates, hours, tags, description, comments

#### E6.23. Task Models

- [ ] TaskItem model tests -- `TaskModelsTests` (42 tests) + `TaskItemEdgeCaseTests` (29 tests) + `TaskItemTheoryTests` (22 tests)
- [ ] TaskItem StatusIcon/StatusColor exhaustive (Open/InProgress/Review/Done/Blocked/Cancelled/empty/Pending) — `TaskItemTheoryTests.StatusIcon_And_StatusColor`
- [ ] TaskItem PriorityIcon/PriorityColor exhaustive (Critical/High/Medium/Low/None/empty/Urgent) — `TaskItemTheoryTests.PriorityIcon_And_PriorityColor`
- [ ] TaskItem TypeIcon all types (Epic/Story/Task/Bug/SubTask/Milestone/Feature/empty) — `TaskItemTheoryTests.TypeIcon_AllTypes`
- [ ] TaskItem RiskColor all levels (Critical/High/Medium/Low/None/empty) — `TaskItemTheoryTests.RiskColor_AllLevels`
- [ ] TaskItem SeverityColor all levels (Blocker/Critical/Major/Minor/Cosmetic/None/empty) — `TaskItemTheoryTests.SeverityColor_AllLevels`
- [ ] TaskItem IsComplete by status — `TaskItemTheoryTests.IsComplete_ByStatus`
- [ ] TaskItem ProgressPercent: Done=100, halfway, over-estimate capped, no estimate, no hours — `TaskItemTheoryTests.ProgressPercent_Done_Returns100`, `TaskItemTheoryTests.ProgressPercent_HalfwayThrough`, `TaskItemTheoryTests.ProgressPercent_OverEstimate_CapsAt99`, `TaskItemTheoryTests.ProgressPercent_NoEstimate_Returns0`, `TaskItemTheoryTests.ProgressPercent_NoHours_Returns0`
- [ ] TaskItem PointsDisplay with/without points — `TaskItemTheoryTests.PointsDisplay_WithPoints`, `TaskItemTheoryTests.PointsDisplay_NoPoints_Empty`
- [ ] TaskItem date displays — `TaskItemTheoryTests.StartDateDisplay_WithDate`, `TaskItemTheoryTests.StartDateDisplay_Null_Empty`, `TaskItemTheoryTests.FinishDateDisplay_WithDate`, `TaskItemTheoryTests.FinishDateDisplay_Null_Empty`
- [ ] TaskItem PropertyChanged cascades (StartDate->StartDateDisplay, FinishDate->FinishDateDisplay) — `TaskItemTheoryTests.PropertyChanged_StartDate_NotifiesStartDateDisplay`, `TaskItemTheoryTests.PropertyChanged_FinishDate_NotifiesFinishDateDisplay`
- [ ] TaskItem CustomValues default empty, can add — `TaskItemTheoryTests.CustomValues_DefaultEmpty`, `TaskItemTheoryTests.CustomValues_CanAdd`
- [ ] TaskItem baseline dates default null — `TaskItemTheoryTests.BaselineDates_DefaultNull`
- [ ] Sprint and planning model tests -- `SprintAndPlanningTests` (21 tests)
- [ ] Task repository integration tests -- `TaskRepositoryIntegrationTests` (13 tests)

### E7. Service Desk

#### E7.1. ManageEngine Sync

- [ ] Read (Sync) button pulls tickets from ManageEngine
- [ ] Incremental sync only pulls changed records
- [ ] Priority, urgency, impact fields populated
- [ ] resolved_at populated from completed_time (not synced_at)
- [ ] Refresh token auto-rotates if Zoho returns new token
- [ ] Auth failure shows error toast
- [ ] Sync status updates in status bar during pull

#### E7.2. SD Request Grid

- [ ] Grid loads with data, sorted by created date descending
- [ ] Status column shows colour-coded text (Open=blue, Closed=green, etc.) — `SdRequestExtendedTests.StatusColor_Cancelled_BritishSpelling`, `SdRequestExtendedTests.StatusColor_Resolved_Green`, `SdRequestExtendedTests.StatusColor_EmptyString_Default`, `SdRequestExtendedTests2.StatusColor_AllStatuses`
- [ ] Priority column shows colour-coded text — `SdRequestExtendedTests.PriorityColor_Unknown_Default`, `SdRequestExtendedTests.PriorityColor_Empty_Default`, `SdRequestExtendedTests2.PriorityColor_AllPriorities`
- [ ] Overdue icon (!) shows for open tickets past due date — `SdRequestExtendedTests.IsOverdue_CancelledBritish_False`, `SdRequestExtendedTests.IsOverdue_Resolved_False`, `SdRequestExtendedTests.IsOverdue_Archive_False`, `SdRequestExtendedTests.IsOverdue_InProgress_PastDue_True`, `SdRequestExtendedTests.IsOverdue_OnHold_PastDue_True`, `SdRequestExtendedTests.OverdueIcon_WhenOverdue_Warning`, `SdRequestExtendedTests.OverdueIcon_NotOverdue_Empty`, `SdRequestExtendedTests2.IsOverdue_True_WhenPastDueAndOpen`, `SdRequestExtendedTests2.IsOverdue_False_WhenNoDueBy`, `SdRequestExtendedTests2.IsOverdue_False_WhenResolved`, `SdRequestExtendedTests2.IsOverdue_False_WhenCanceled`, `SdRequestExtendedTests2.IsOverdue_False_WhenArchive`, `SdRequestExtendedTests2.IsOverdue_False_WhenFutureDue`
- [ ] IsClosed by status (Resolved/Closed=true, others=false) — `SdRequestExtendedTests.IsClosed_AllStatuses`, `SdRequestExtendedTests2.IsClosed_VariousStatuses`
- [ ] "Open" hyperlink column opens ticket in browser
- [ ] Inline editing: Status, Priority, Group, Technician, Category dropdowns
- [ ] Editing a field turns row amber (dirty tracking) -- `SdRequestDirtyTrackingTests` (7 tests), `SdRequestExtendedTests.DirtyTracking_GroupNameChange_MarksDirty`, `SdRequestExtendedTests.DirtyTracking_TechnicianChange_MarksDirty`, `SdRequestExtendedTests.DirtyTracking_CategoryChange_MarksDirty`, `SdRequestExtendedTests.DirtyTracking_PriorityChange_MarksDirty`, `SdRequestExtendedTests.DirtyTracking_RevertAll_ClearsDirty`, `SdRequestExtendedTests2.DirtyTracking_NotDirty_BeforeAcceptChanges`, `SdRequestExtendedTests2.DirtyTracking_Dirty_AfterStatusChange`, `SdRequestExtendedTests2.DirtyTracking_Clean_AfterRevert`, `SdRequestExtendedTests2.DirtyTracking_Dirty_AfterPriorityChange`, `SdRequestExtendedTests2.DirtyTracking_Dirty_AfterGroupChange`, `SdRequestExtendedTests2.DirtyTracking_Dirty_AfterTechnicianChange`, `SdRequestExtendedTests2.DirtyTracking_Dirty_AfterCategoryChange`
- [ ] RowColor amber when dirty, transparent when clean — `SdRequestExtendedTests2.RowColor_Amber_WhenDirty`, `SdRequestExtendedTests2.RowColor_Transparent_WhenClean`
- [ ] PropertyChanged cascades: Priority->PriorityColor, DueBy->IsOverdue, IsDirty->RowColor — `SdRequestExtendedTests.PropertyChanged_Priority_AlsoNotifiesPriorityColor`, `SdRequestExtendedTests.PropertyChanged_DueBy_AlsoNotifiesIsOverdue`, `SdRequestExtendedTests.PropertyChanged_IsDirty_AlsoNotifiesRowColor`, `SdRequestExtendedTests2.PropertyChanged_Subject_Fires`, `SdRequestExtendedTests2.PropertyChanged_Status_FiresStatusColor`, `SdRequestExtendedTests2.PropertyChanged_Priority_FiresPriorityColor`, `SdRequestExtendedTests2.PropertyChanged_DueBy_FiresIsOverdue`
- [ ] SdRequest defaults (all empty strings, nulls, false) — `SdRequestExtendedTests2.Defaults_AreCorrect`
- [ ] "3 unsaved changes" count shows in toolbar
- [ ] Save Changes button writes to ManageEngine API
- [ ] Discard button reverts to original values
- [ ] Context menu: Read, Update Status/Priority, Assign Tech, Add Note
- [ ] Context menu: Open in Browser, Clear Filter, Export, Refresh
- [ ] Total summary count at bottom of grid

#### E7.3. SD Overview Dashboard

- [ ] KPI cards: Incoming, Closed, Escalations, SLA Compliant, Resolution Time, Open, Tech:Ticket
- [ ] KPI trend arrows show comparison vs previous period
- [ ] Double-click KPI card opens drill-down grid
- [ ] Bar chart: created (dark red) vs closed (olive green) per bucket
- [ ] Avg resolution days line (flat period-wide mean)
- [ ] Open issues line (point-in-time count)
- [ ] Double-click bar opens drill-down grid for that day
- [ ] Summary text shows totals + closure rate %

#### E7.4. SD Tech Closures

- [ ] Bar chart shows per-tech daily closures
- [ ] Expected target dashed line visible
- [ ] Double-click a bar opens drill-down grid

#### E7.5. SD Aging

- [ ] 5 side-by-side bars per tech (0-1d green to 7+ red)
- [ ] Double-click a bar opens drill-down grid

#### E7.6. SD Settings Panel (Global Filters)

- [ ] Time Range dropdown changes all chart date ranges
- [ ] Time Scale dropdown changes bucket size (day/week/month) — `SdFilterStateExtendedTests.FormatLabel_Month_Bucket`, `SdFilterStateExtendedTests.FormatLabel_Week_Bucket`, `SdFilterStateExtendedTests.FormatLabel_Day_ShortRange_DayName`, `SdFilterStateExtendedTests.FormatLabel_Day_LongRange_MonthDay`, `SdFilterStateFormatTests.FormatLabel_DayBucket_ShortRange_ShowsDayOfWeek`, `SdFilterStateFormatTests.FormatLabel_DayBucket_LongRange_ShowsMonthDay`, `SdFilterStateFormatTests.FormatLabel_WeekBucket`, `SdFilterStateFormatTests.FormatLabel_MonthBucket`, `SdFilterStateFormatTests.FormatLabel_MonthBucket_December`
- [ ] FormatLabel day bucket boundary (14 days shows DOW, 15+ shows month) — `SdFilterStateFormatTests.FormatLabel_DayBucket_Exactly14Days_ShowsDayOfWeek`, `SdFilterStateFormatTests.FormatLabel_DayBucket_15Days_ShowsMonthDay`
- [ ] SdFilterState.Default() sets correct week range — `SdFilterStateExtendedTests.Default_SetsCorrectWeek`, `SdFilterStateFormatTests.Default_ReturnsThisWeek`, `SdFilterStateFormatTests.Default_RangeStartIsMonday`, `SdFilterStateFormatTests.Default_RangeEndIsNextMonday`
- [ ] SdFilterState display options have correct defaults — `SdFilterStateExtendedTests.Default_AllDisplayOptions_HaveDefaults`, `SdFilterStateFormatTests.Defaults_AreCorrect`
- [ ] SdFilterState grid options have correct defaults — `SdFilterStateExtendedTests.Default_GridOptions_HaveDefaults`, `SdFilterStateFormatTests.GridDefaults_AreCorrect`
- [ ] Technician checkboxes filter closures + aging charts
- [ ] All/None buttons for tech checkboxes work
- [ ] Team buttons select only that team's members
- [ ] Group category checkboxes filter overview + request grid
- [ ] Uncategorized groups show as individual checkboxes
- [ ] Open Issues Line toggle
- [ ] Avg Resolution Line toggle
- [ ] Grid Display options apply to all SD grids
- [ ] Apply button triggers refresh
- [ ] Reset button restores defaults

#### E7.7. SD Groups

- [ ] Grid shows all ME groups with Active checkbox
- [ ] Inline editing group name + sort order, auto-saves on row commit
- [ ] Active: X / Total: Y summary at bottom
- [ ] Disabling a group removes it from request grid dropdown

#### E7.8. SD Technicians

- [ ] Grid shows all ME technicians with Active checkbox
- [ ] Toggle Active auto-saves to DB
- [ ] Active: X / Total: Y summary at bottom
- [ ] Disabling a tech removes from all dropdowns, charts, and filters
- [ ] Cascade: disabling refreshes request grid combos + chart filter panels

#### E7.9. SD Requesters

- [ ] Grid shows all ME requesters (read-only, synced)
- [ ] VIP: X / Total: Y summary at bottom

#### E7.10. SD Teams

- [ ] Teams grid: add/edit/delete teams
- [ ] Right panel: checked listbox of all technicians
- [ ] Check/uncheck assigns techs to team, auto-saves
- [ ] Team buttons appear in chart filter panels

#### E7.11. Group Categories

- [ ] Tree view shows parent categories with nested ME groups
- [ ] Drag groups under categories to nest
- [ ] Add Category button creates new category node
- [ ] Delete button removes category (children move to root)
- [ ] Save button persists to DB + refreshes SD Settings filter
- [ ] Category shows in SD Settings as gold checkbox with count
- [ ] Checking category selects all child groups in filter
- [ ] Tooltip on category checkbox shows member list

#### E7.12. Cross-Panel Linking

- [ ] Click technician in SD Technicians > Request grid auto-filters to that tech
- [ ] Click group in SD Groups > Request grid auto-filters to that group
- [ ] Click requester in SD Requesters > Request grid auto-filters to that requester
- [ ] Right-click "Clear Filter" on Request grid resets the filter
- [ ] Service Desk panel activates when linked filter applies

#### E7.13. Write-Back to ManageEngine

- [ ] Update Status writes correct value to ME API
- [ ] Update Priority writes correct value to ME API
- [ ] Assign Technician writes correct value to ME API
- [ ] Add Note posts to ME API
- [ ] Bulk dirty row save writes all changed rows
- [ ] Write-back errors show in status bar + app log
- [ ] Integration log records sync/write actions with duration

#### E7.14. SD Models

- [ ] SD request model tests -- `SdRequestTests` (15 tests), `SdRequestExtendedTests` (21 tests), `SdRequestExtendedTests2` (43 tests)
- [ ] SD filter state tests -- `SdFilterStateTests` (9 tests), `SdFilterStateExtendedTests` (7 tests), `SdFilterStateFormatTests` (12 tests)
- [ ] SD models tests -- `SdModelsTests` (10 tests), `SdServiceModelsTests` (14 tests)
- [ ] SD dirty tracking tests -- `SdRequestDirtyTrackingTests` (7 tests)
- [ ] SdGroupCategory defaults, MemberCount, PropertyChanged — `SdServiceModelsTests.SdGroupCategory_Defaults`, `SdServiceModelsTests.SdGroupCategory_MemberCount`, `SdServiceModelsTests.SdGroupCategory_PropertyChanged_Fires`
- [ ] SdGroup defaults and PropertyChanged — `SdServiceModelsTests.SdGroup_Defaults`, `SdServiceModelsTests.SdGroup_PropertyChanged_Fires`
- [ ] SdRequester defaults — `SdServiceModelsTests.SdRequester_Defaults`
- [ ] SdTechnician defaults, PropertyChanged — `SdServiceModelsTests.SdTechnician_Defaults`, `SdServiceModelsTests.SdTechnician_PropertyChanged_IsActive`
- [ ] SdKpiSummary defaults — `SdServiceModelsTests.SdKpiSummary_Defaults`
- [ ] SdTeam defaults — `SdServiceModelsTests.SdTeam_Defaults`
- [ ] SdWeeklyTotal DayLabel — `SdServiceModelsTests.SdWeeklyTotal_DayLabel`
- [ ] SdAgingBucket Total sum — `SdServiceModelsTests.SdAgingBucket_Total`, `SdServiceModelsTests.SdAgingBucket_Total_Empty`
- [ ] SdTechDaily DayLabel — `SdServiceModelsTests.SdTechDaily_DayLabel`

#### E7.15. SD Infrastructure

- [ ] ManageEngine integration record exists in integrations table
- [ ] OAuth credentials stored encrypted in integration_credentials — `IntegrationServiceTests` (8 tests), `IntegrationModelTests` (9 tests), `IntegrationModelExtendedTests` (12 tests)
- [ ] Integration defaults, StatusIcon, StatusText, PropertyChanged — `IntegrationModelExtendedTests.Integration_Defaults`, `IntegrationModelExtendedTests.Integration_StatusIcon_Enabled`, `IntegrationModelExtendedTests.Integration_StatusIcon_Disabled`, `IntegrationModelExtendedTests.Integration_StatusText_Enabled`, `IntegrationModelExtendedTests.Integration_StatusText_Disabled`, `IntegrationModelExtendedTests.Integration_PropertyChanged_AllProperties`, `IntegrationModelExtendedTests.Integration_IsEnabled_FiresStatusIcon`
- [ ] IntegrationCredential defaults + ExpiresAt nullable — `IntegrationModelExtendedTests.IntegrationCredential_Defaults`, `IntegrationModelExtendedTests.IntegrationCredential_ExpiresAt_CanBeSet`, `IntegrationModelExtendedTests.IntegrationCredential_ExpiresAt_CanBeNull`
- [ ] IntegrationLogEntry defaults + set properties — `IntegrationModelExtendedTests.IntegrationLogEntry_Defaults`, `IntegrationModelExtendedTests.IntegrationLogEntry_SetProperties`
- [ ] config_json has oauth_url + portal_url
- [ ] sd_groups seeded from sd_requests distinct group_name
- [ ] sd_group_categories + members tables exist
- [ ] sd_teams + members tables exist
- [ ] resolved_at + me_completed_time columns on sd_requests
- [ ] All SD permissions granted to Admin role

### E8. Admin

#### E8.1. Users Panel

- [ ] User grid loads from app_users
- [ ] Role dropdown bound to DB roles
- [ ] New user creates record
- [ ] Edit user modifies record
- [ ] Delete user removes record
- [ ] Extended user fields (department, title, phone, mobile, company) visible — `AppUserTests.Defaults_AreCorrect`, `AppUserTests.PropertyChanged_Fires_OnNameChange`, `AppUserExtendedTests.PropertyChanged_Department_Fires`, `AppUserExtendedTests.PropertyChanged_Title_Fires`, `AppUserExtendedTests.PropertyChanged_Phone_Fires`, `AppUserExtendedTests.PropertyChanged_Mobile_Fires`, `AppUserExtendedTests.PropertyChanged_Company_Fires`
- [ ] UserType dropdown shows all 5 types — `UserTypesTests.All_Has5Types`
- [ ] Protected users (System, Service) cannot be deleted — `AppUserTests.IsProtected_System_True`, `AppUserTests.IsProtected_Service_True`, `AppUserTests.IsProtected_Standard_False`
- [ ] Inactive users show at 0.5 opacity, protected users show bold — `AppUserTests.StatusColor_Active_Green`, `AppUserTests.StatusColor_Inactive_Grey`

#### E8.2. Roles & Permissions Panel

- [ ] Split view: roles grid left, permissions tree + site checkboxes right
- [ ] RoleRecord PermissionSummary counts — `RoleRecordTests.PermissionSummary_NoPermissions`, `RoleRecordTests.PermissionSummary_AllPermissions`, `RoleRecordTests.PermissionSummary_SomePermissions`, `RoleRecordExtendedTests.PermissionSummary_VariousCounts`
- [ ] RoleRecord defaults (empty name, priority 0) — `RoleRecordTests.Defaults_AreCorrect`
- [ ] RoleRecord PropertyChanged fires — `RoleRecordTests.PropertyChanged_Fires_OnNameChange`, `RoleRecordExtendedTests.PropertyChanged_Id_Fires`, `RoleRecordExtendedTests.PropertyChanged_Description_Fires`, `RoleRecordExtendedTests.PropertyChanged_Priority_Fires`, `RoleRecordExtendedTests.PropertyChanged_IsSystem_Fires`
- [ ] RoleRecord permission boolean PropertyChanged (DevicesView/Edit/Delete, SwitchesView, LinksView, BgpView/Sync, VlansView, TasksView, ServiceDeskView/Sync) — `RoleRecordExtendedTests` (11 permission PropertyChanged tests)
- [ ] RoleRecord DevicesViewReserved default true — `RoleRecordExtendedTests.DevicesViewReserved_DefaultTrue`, `RoleRecordExtendedTests.DevicesViewReserved_PropertyChanged`
- [ ] RoleRecord DetailUsers default empty — `RoleRecordTests.DetailUsers_DefaultEmpty`
- [ ] RolePermission defaults — `RoleRecordTests.RolePermission_Defaults`
- [ ] RoleUserDetail defaults + set properties — `RoleRecordTests.RoleUserDetail_Defaults`, `RoleRecordExtendedTests.RoleUserDetail_CanSetProperties`
- [ ] UserPermissionDetail defaults — `RoleRecordTests.UserPermissionDetail_Defaults`
- [ ] Permissions tree shows 25 module:action codes
- [ ] Checkbox toggles grant/revoke permission
- [ ] Site access checkboxes control building access

#### E8.3. Lookup Values Panel

- [ ] Category/Value grid loads — `LookupItemTests.Defaults_AreCorrect`
- [ ] CRUD works — `LookupItemTests.AllProperties_FirePropertyChanged`
- [ ] SortOrder column respected
- [ ] LookupItem PropertyChanged fires on all fields (Id, Category, Value, SortOrder, GridName, Module) — `LookupItemTests.PropertyChanged_Fires_OnId`, `LookupItemTests.PropertyChanged_Fires_OnCategory`, `LookupItemTests.PropertyChanged_Fires_OnValue`, `LookupItemTests.PropertyChanged_Fires_OnSortOrder`, `LookupItemTests.PropertyChanged_Fires_OnGridName`, `LookupItemTests.PropertyChanged_Fires_OnModule`
- [ ] LookupItem ParentId always null (flat list) — `LookupItemTests.ParentId_AlwaysNull`, `LookupItemTests.ParentId_NullEvenWithAllFieldsSet`

#### E8.4. SSH Logs Panel

- [ ] SSH session logs display — `SshLogEntryTests.Defaults_AreCorrect`
- [ ] Filterable by switch/date
- [ ] Duration computed property — `SshLogEntryTests.Duration_WithFinishedAt_CalculatesCorrectly`, `SshLogEntryTests.Duration_WithFractionalSeconds`, `SshLogEntryTests.Duration_WithoutFinishedAt_ReturnsDash`, `SshLogEntryTests.Duration_LongOperation`, `SshLogEntryTests.Duration_ZeroSeconds`
- [ ] StatusIcon success/failure — `SshLogEntryTests.StatusIcon_Success_CheckMark`, `SshLogEntryTests.StatusIcon_Failure_CrossMark`
- [ ] SshLogEntry PropertyChanged fires on all fields — `SshLogEntryTests.PropertyChanged_Hostname_Fires`, `SshLogEntryTests.PropertyChanged_HostIp_Fires`, `SshLogEntryTests.PropertyChanged_Success_Fires`, `SshLogEntryTests.PropertyChanged_Port_Fires`, `SshLogEntryTests.PropertyChanged_Error_Fires`, `SshLogEntryTests.PropertyChanged_RawOutput_Fires`, `SshLogEntryTests.PropertyChanged_ConfigLines_Fires`, `SshLogEntryTests.PropertyChanged_SwitchId_Fires`, `SshLogEntryTests.AllProperties_FirePropertyChanged`
- [ ] SshLogEntry SwitchId nullable — `SshLogEntryTests.SwitchId_CanBeNull`

#### E8.5. App Logs Panel

- [ ] Application log entries display — `AppLogEntryTests.Defaults`, `AppLogEntryTests.DisplayTime_FormatsCorrectly`, `AppLogEntryTests.PropertyChanged_AllFields`

#### E8.6. Jobs Panel

- [ ] Job schedules grid shows 3 job types
- [ ] Enable/Disable toggle works
- [ ] Interval column editable
- [ ] Run Now button triggers immediate execution
- [ ] Job history grid shows past runs

#### E8.7. Ribbon Config Panel

- [ ] Ribbon pages/groups/items display in flat grid — `RibbonConfigTests.RibbonPageConfig_Defaults`, `RibbonConfigTests.RibbonGroupConfig_Defaults`, `RibbonConfigTests.RibbonItemConfig_Defaults`, `RibbonConfigExtendedTests.RibbonPageConfig_Defaults`, `RibbonConfigExtendedTests.RibbonGroupConfig_Defaults`, `RibbonConfigExtendedTests.RibbonItemConfig_Defaults`
- [ ] CRUD on ribbon items works — `RibbonConfigTests.RibbonItemConfig_PropertyChanged_AllSetters`, `RibbonConfigExtendedTests.RibbonItemConfig_PropertyChanged_AllProperties`
- [ ] RibbonPageConfig PropertyChanged all properties — `RibbonConfigExtendedTests.RibbonPageConfig_PropertyChanged_AllProperties`
- [ ] RibbonGroupConfig PropertyChanged all properties — `RibbonConfigExtendedTests.RibbonGroupConfig_PropertyChanged_AllProperties`
- [ ] UserRibbonOverride defaults + set + IsHidden — `RibbonConfigExtendedTests.UserRibbonOverride_Defaults`, `RibbonConfigExtendedTests.UserRibbonOverride_SetProperties`, `RibbonConfigExtendedTests.UserRibbonOverride_IsHidden_True`
- [ ] System items cannot be deleted (is_system=TRUE)

#### E8.8. Active Directory Integration

- [ ] AD Browser panel opens from Admin > Panels > AD Browser
- [ ] AdConfig IsConfigured with domain — `AdModelsTests.AdConfig_IsConfigured_WithDomain_True`, `AdModelsTests.AdConfig_IsConfigured_EmptyDomain_False`, `AdModelsTests.AdConfig_IsConfigured_WhitespaceDomain_False`, `AdModelsExtendedTests.AdConfig_IsConfigured_True_WhenDomainSet`, `AdModelsExtendedTests.AdConfig_IsConfigured_False_WhenEmptyDomain`, `AdModelsExtendedTests.AdConfig_IsConfigured_False_WhenWhitespaceDomain`
- [ ] AdConfig defaults (empty domain/ou/account/password, no SSL) — `AdModelsTests.AdConfig_Defaults`, `AdModelsExtendedTests.AdConfig_Defaults`, `AdModelsExtendedTests.AdConfig_AllProperties`
- [ ] AdUser defaults (empty strings, disabled, not imported) — `AdModelsTests.AdUser_Defaults`, `AdModelsExtendedTests.AdUser_Defaults`, `AdModelsExtendedTests.AdUser_IsImported_DefaultFalse`, `AdModelsExtendedTests.AdUser_Enabled_DefaultFalse`
- [ ] AdUser set all properties — `AdModelsTests.AdUser_SetAllProperties`, `AdModelsExtendedTests.AdUser_SetAllProperties`
- [ ] Browse AD button queries configured domain via System.DirectoryServices.AccountManagement
- [ ] AD users shown in read-only grid with ObjectGuid, DisplayName, Email, Department, Enabled
- [ ] IsImported column shows which AD users are already linked
- [ ] Import Selected creates app_users with user_type=ActiveDirectory, ad_guid linked
- [ ] Sync All updates display name, email, phone, active status from AD
- [ ] AD config stored in integrations table

#### E8.9. Schema Migration Management

- [ ] Migrations panel shows all applied (green) and pending (amber) migrations
- [ ] Applied migrations show timestamp and duration
- [ ] Apply Pending button runs all pending .sql files in transaction
- [ ] Migration history recorded in migration_history table
- [ ] Refresh reloads from DB + filesystem

#### E8.10. Database Backup & Restore

- [ ] Backup panel with Full Backup button and output path browser
- [ ] pg_dump runs with connection params from DSN
- [ ] Backup history grid shows type, file path, size, status, timestamp
- [ ] Scheduled backup via db_backup job type in job_schedules
- [ ] Failed backups logged with error message

#### E8.11. Soft-Delete Purge

- [ ] Purge panel shows tables with soft-deleted record counts
- [ ] Purge Selected deletes from one table, Purge All clears all
- [ ] Confirmation dialog before purge
- [ ] Count refreshes after each purge operation

#### E8.12. Location Management

- [ ] Locations panel with Countries grid (Code, Name, SortOrder) — `LocationModelExtendedTests.Country_Defaults`, `LocationModelExtendedTests.Country_AllProperties_FirePropertyChanged`
- [ ] Regions grid filtered by selected country — `LocationModelExtendedTests.Region_Defaults`, `LocationModelExtendedTests.Region_AllProperties_FirePropertyChanged`
- [ ] Postcode defaults and PropertyChanged — `LocationModelExtendedTests.Postcode_Defaults`, `LocationModelExtendedTests.Postcode_AllProperties_FirePropertyChanged`
- [ ] Postcode latitude/longitude precision — `LocationModelExtendedTests.Postcode_Latitude_NullToValue`, `LocationModelExtendedTests.Postcode_Longitude_NegativeValue`, `LocationModelExtendedTests.Postcode_LatLon_Precision`
- [ ] Add/Delete/Save for both countries and regions
- [ ] Seed data: GBR, USA, AUS, NZL

#### E8.13. Reference Number System

- [ ] Reference Config panel shows entity types with prefix/suffix/pad/next value — `ReferenceConfigTests` (10 tests)
- [ ] SampleOutput column shows live preview (e.g. DEV-000001)
- [ ] Auto-save on cell edit
- [ ] next_reference() PG function for atomic sequence generation
- [ ] Seeded: device, ticket, asset, task

#### E8.14. Podman Container Management

- [ ] Podman panel shows containers with Name, Image, State, Status
- [ ] Start/Stop/Restart buttons for selected container
- [ ] View Logs button shows last 100 lines in text area
- [ ] Refresh reloads container list
- [ ] Graceful handling if podman not installed
- [ ] ContainerInfo StateColor all states (running=green, exited=red, paused=amber, others=grey) — `ContainerInfoExtendedTests.StateColor_AllCases`
- [ ] ContainerInfo IsRunning all states — `ContainerInfoExtendedTests.IsRunning_AllCases`
- [ ] ContainerInfo defaults all empty — `ContainerInfoExtendedTests.Defaults_AllEmpty`
- [ ] ContainerInfo PropertyChanged fires on Status/Created/Ports/CpuPercent/MemUsage — `ContainerInfoExtendedTests.PropertyChanged_Status_Fires`, `ContainerInfoExtendedTests.PropertyChanged_Created_Fires`, `ContainerInfoExtendedTests.PropertyChanged_Ports_Fires`, `ContainerInfoExtendedTests.PropertyChanged_CpuPercent_Fires`, `ContainerInfoExtendedTests.PropertyChanged_MemUsage_Fires`
- [ ] ContainerInfo full scenario (postgres container) — `ContainerInfoExtendedTests.FullScenario_PostgresContainer`

#### E8.15. Scheduler / Calendar

- [ ] Scheduler panel with Day/Week/Month view navigation
- [ ] Period label updates on view change
- [ ] Resource dropdown filters by technician
- [ ] New Appointment creates with current time, saves to DB
- [ ] Delete with confirmation
- [ ] appointments + appointment_resources tables — `AppointmentExtendedTests.AppointmentResource_PropertyChanged_AllProperties`
- [ ] Links to tasks (task_id) and SD tickets (ticket_id) — `AppointmentExtendedTests.PropertyChanged_TaskId_Fires`, `AppointmentExtendedTests.PropertyChanged_TicketId_Fires`
- [ ] Appointment PropertyChanged fires on all fields — `AppointmentExtendedTests` (14 PropertyChanged tests)
- [ ] Appointment nullable fields (ResourceId, TaskId, TicketId, CreatedBy) — `AppointmentExtendedTests.ResourceId_CanBeNull`, `AppointmentExtendedTests.TaskId_CanBeNull`, `AppointmentExtendedTests.TicketId_CanBeNull`, `AppointmentExtendedTests.CreatedBy_CanBeNull`

#### E8.16. Notification Preferences Panel

- [ ] My Notifications panel opens from Admin > Identity > My Notifications
- [ ] Grid shows all 8 event types with channel dropdown (toast/email/both/none) and enabled toggle
- [ ] Missing preferences auto-filled with defaults (toast, enabled)
- [ ] Save All saves all preferences at once
- [ ] Auto-save on cell change

---

## F. API Server

### F1. REST Endpoints -- Core

- [ ] GET /api/devices returns device list
- [ ] POST /api/devices creates device
- [ ] PUT /api/devices/{id} updates device
- [ ] DELETE /api/devices/{id} deletes device
- [ ] GET /api/switches returns switch list
- [ ] GET /api/links returns link list
- [ ] GET /api/vlans returns VLAN list
- [ ] GET /api/bgp returns BGP config list
- [ ] GET /api/admin/users returns user list
- [ ] GET /api/jobs returns job schedules

### F2. SSH Endpoints

- [ ] POST /api/ssh/{id}/ping pings switch
- [ ] POST /api/ssh/{id}/download-config downloads config
- [ ] POST /api/ssh/{id}/sync-bgp syncs BGP
- [ ] POST /api/ssh/ping-all batch pings

### F3. JWT Authentication

- [ ] POST /api/auth/login returns JWT token
- [ ] Bearer token required on all endpoints
- [ ] 25 permission claims in token
- [ ] 401 on expired/invalid token
- [ ] Auto token refresh on 401

### F4. SignalR

- [ ] NotificationHub connects on startup
- [ ] DataChanged event fires on DB changes (pg_notify)
- [ ] PingResult event fires on ping completion
- [ ] SyncProgress event streams during SSH operations
- [ ] WPF grids auto-refresh on SignalR events
- [ ] SendNotification -- eventType, title, message, severity
- [ ] SendWebhookReceived -- source, webhookId
- [ ] SendAuditEvent -- action, entityType, entityName, username
- [ ] SendSyncComplete -- configName, status, recordsRead, recordsFailed
- [ ] SendSessionEvent -- eventType, username, authMethod
- [ ] All events broadcast to all connected clients
- [ ] NotificationHub: OnConnectedAsync joins tenant group
- [ ] NotificationHub: OnDisconnectedAsync leaves tenant group
- [ ] All Send* methods broadcast to tenant group (not Clients.All)

### F5. Swagger

- [ ] /swagger loads OpenAPI UI
- [ ] All endpoints documented
- [ ] All endpoint groups have WithTags for organized Swagger UI
- [ ] Swagger description updated for full platform scope
- [ ] Swagger UI with security definition for Bearer JWT

### F6. Background Jobs

- [ ] JobSchedulerService checks every 30s
- [ ] ping_scan runs every 10 minutes when enabled
- [ ] config_backup runs every 24 hours when enabled
- [ ] bgp_sync runs every 6 hours when enabled
- [ ] Admin can enable/disable jobs via API
- [ ] Admin can change job intervals
- [ ] Admin can trigger immediate run
- [ ] Job history records execution results

### F7. Identity Endpoints

- [ ] /api/identity/providers -- CRUD for identity providers
- [ ] /api/identity/domain-mappings -- email domain to provider routing
- [ ] /api/identity/claim-mappings -- claims to role mapping rules
- [ ] /api/identity/auth-events -- read-only auth audit trail

### F8. Location & Reference Endpoints

- [ ] /api/locations/countries -- CRUD
- [ ] /api/locations/regions -- CRUD with country filter
- [ ] /api/locations/references -- reference config list
- [ ] /api/locations/references/next/{type} -- atomic next reference number

### F9. Backup & Migration Endpoints

- [ ] /api/backup/run -- trigger pg_dump backup
- [ ] /api/backup/history -- backup history list
- [ ] /api/backup/tables -- list all DB tables
- [ ] /api/backup/migrations -- migration history
- [ ] /api/backup/purge-counts -- soft-deleted record counts
- [ ] /api/backup/purge/{table} -- purge soft-deleted records

### F10. Appointment Endpoints

- [ ] /api/appointments -- CRUD with date range filter + resources

### F11. Task Endpoints

- [ ] GET /api/tasks returns all 45 fields with project/sprint/committed joins
- [ ] GET /api/tasks?project_id=N filters by project
- [ ] POST /api/tasks accepts all Phase 1 fields
- [ ] PUT /api/tasks/{id} updates all Phase 1 fields
- [ ] POST /api/tasks/{id}/commit -- commit task to sprint
- [ ] DELETE /api/tasks/{id}/commit -- uncommit from sprint
- [ ] GET /api/tasks/{id}/links -- get task links
- [ ] POST /api/tasks/{id}/links -- create task link
- [ ] GET /api/tasks/{id}/dependencies -- get Gantt dependencies
- [ ] GET /api/tasks/{id}/time -- time entries for a task
- [ ] GET /api/tasks/{id}/comments
- [ ] POST /api/tasks/{id}/comments
- [ ] DELETE /api/tasks/{id}

### F12. Project Endpoints

- [ ] GET /api/projects returns all task projects
- [ ] POST /api/projects creates new project
- [ ] DELETE /api/projects/{id} deletes project
- [ ] GET /api/projects/portfolios returns all portfolios
- [ ] POST/PUT/DELETE /api/projects/portfolios
- [ ] GET /api/projects/programmes returns all programmes
- [ ] POST/DELETE /api/projects/programmes
- [ ] GET /api/projects/{id}/sprints returns sprints for project
- [ ] POST /api/projects/{id}/sprints creates sprint
- [ ] DELETE /api/projects/{id}/sprints/{sprintId}
- [ ] GET /api/projects/{id}/releases
- [ ] POST /api/projects/{id}/releases

### F13. Sync Engine Endpoints

- [ ] GET /api/sync/configs -- list all sync configurations
- [ ] PUT /api/sync/configs -- create/update sync config
- [ ] GET /api/sync/configs/{id}/entity-maps
- [ ] GET /api/sync/configs/{id}/log
- [ ] POST /api/sync/configs/{id}/run -- trigger sync execution
- [ ] GET /api/sync/agent-types -- list registered agent types
- [ ] GET /api/sync/converter-types -- list registered converter types

### F14. Webhook Receiver

- [ ] POST /api/webhooks/{source} receives JSON payload without auth
- [ ] Payload stored in webhook_log table
- [ ] Invalid JSON wrapped in {"raw": "..."} object
- [ ] SignalR "WebhookReceived" event broadcast
- [ ] Auto-marks matching sync_config as 'pending'
- [ ] GET /api/webhooks -- list recent webhooks
- [ ] GET /api/webhooks/{id}/payload -- retrieve full payload

### F15. Import API

- [ ] POST /api/import -- accepts { target_table, upsert_key, records[] }
- [ ] Returns { imported, failed, target_table }
- [ ] GET /api/import/tables -- list available target tables
- [ ] Table name validated against pg_tables whitelist

### F16. Dashboard API

- [ ] GET /api/dashboard -- returns full DashboardData
- [ ] All counts query from live DB tables
- [ ] Activity feed combines auth_events + sync_log (last 20)

### F17. Health Check Endpoints

- [ ] GET /api/health -- returns status, timestamp, uptime (no auth)
- [ ] GET /api/health/detailed -- DB latency, table counts, system info, sync engine, mediator diagnostics
- [ ] GET /api/health/ready -- checks DB connectivity, returns 503 if unavailable
- [ ] GET /api/health/live -- always returns 200 (process alive check)

### F18. API Rate Limiting

- [ ] Rate limiter middleware: 200 requests per 60 seconds per IP
- [ ] Returns 429 Too Many Requests when exceeded with Retry-After header
- [ ] X-RateLimit-Limit and X-RateLimit-Remaining headers on every response
- [ ] Health checks, webhooks, and SignalR hubs excluded from rate limiting
- [ ] Stale window cleanup when map exceeds 10,000 entries

### F19. API Key Management API

- [ ] GET /api/keys -- list all API keys
- [ ] POST /api/keys/generate -- create key, returns raw key once
- [ ] POST /api/keys/{id}/revoke -- soft-disable key
- [ ] DELETE /api/keys/{id} -- hard-delete key

### F20. Audit & Activity API

- [ ] GET /api/audit returns audit entries (filterable by entityType, username)
- [ ] GET /api/activity/global -- combined audit log + auth events (admin)
- [ ] GET /api/activity/me -- current user's personal activity timeline

### F21. Notification API

- [ ] GET /api/notifications/preferences -- current user's prefs
- [ ] PUT /api/notifications/preferences -- update event type channel/enabled
- [ ] GET /api/notifications/sessions -- all active sessions (admin)
- [ ] DELETE /api/notifications/sessions/{id} -- force end session

### F22. Search API

- [ ] GET /api/search?q=term -- searches across devices, switches, users, SD tickets, tasks
- [ ] Returns unified results with EntityType, EntityId, Title, Subtitle, Badge
- [ ] Case-insensitive ILIKE search
- [ ] Minimum 2 character query, configurable limit

### F23. Platform Status & Version API

- [ ] GET /api/status -- complete platform overview (auth required)
- [ ] GET /api/version -- product name, version, build date, runtime, OS, architecture, endpoint list (no auth)

### F24. Settings Export API

- [ ] GET /api/settings/export -- exports current user's settings as JSON

### F25. Validation API

- [ ] POST /api/validation/validate/{entityType} -- validates JSON against registered rules
- [ ] Returns { isValid, errors[], errorSummary }

### F26. File Management API

- [ ] POST /api/files/upload (multipart, MD5 verification, inline vs filesystem routing)
- [ ] GET /api/files/{id}/download (latest version, correct Content-Type)
- [ ] GET /api/files/{id}/versions (version history)
- [ ] GET /api/files?entity_type=X&entity_id=Y (list files for entity)
- [ ] DELETE /api/files/{id} (soft delete)

### F27. Registration & Licensing API

- [ ] POST /api/register/register -- self-service registration
- [ ] POST /api/register/verify-email -- email verification
- [ ] GET /api/register/check-slug/{slug} -- slug availability
- [ ] GET /api/register/subscription/plans -- list plans
- [ ] GET /api/register/modules -- module license status
- [ ] POST /api/register/modules/{code}/activate -- activate module
- [ ] POST /api/register/license/issue -- issue signed license key

### F28. Update API

- [ ] GET /api/updates/check -- returns update info if newer version exists
- [ ] POST /api/updates/publish -- publish new version
- [ ] GET /api/updates/versions -- list all published versions
- [ ] POST /api/updates/report -- client reports update result

### F29. Request Logging Middleware

- [ ] All requests logged: method, path, status code, duration, user
- [ ] Slow requests (>1000ms) logged at Warning level with [SLOW] tag
- [ ] Error responses (4xx/5xx) logged at Warning level
- [ ] Anonymous requests show "anonymous" as user

### F30. API Middleware Stack

- [ ] SecurityHeaders > RequestLogging > RateLimit > ApiKeyAuth > Authentication > Authorization > TenantResolution > ModuleLicense

### F31. API Client

- [ ] SearchAsync, GetDashboardAsync, GetStatusAsync
- [ ] GetActivityAsync / GetMyActivityAsync
- [ ] GetSyncConfigsAsync / RunSyncAsync
- [ ] GetAuditLogAsync, GetIdentityProvidersAsync
- [ ] ImportAsync, HealthCheckAsync

---

## G. Engine Services

### G1. Mediator

- [ ] Mediator singleton handles all in-process panel messaging with pipeline behaviors -- `MediatorTests` (11 tests)
- [ ] MediatorLoggingBehavior logs all messages to debug output
- [ ] MediatorPerformanceBehavior tracks per-message-type counts and avg latency
- [ ] Mediator.GetDiagnostics() returns subscription and message count stats
- [ ] Filtered subscriptions: handlers only called when filter function returns true
- [ ] PanelMessageBus.Publish bridges to Mediator automatically
- [ ] Subscriber ID tracking -- `MediatorAdvancedTests.SubscriberId`
- [ ] Message count tracking -- `MediatorAdvancedTests.MessageCountTracking`
- [ ] Multiple subscribers -- `MediatorAdvancedTests.MultipleSubscribers`
- [ ] Multiple filters -- `MediatorAdvancedTests.MultipleFilters`
- [ ] Logging behavior -- `MediatorAdvancedTests.LoggingBehavior`

### G2. PanelMessageBus

- [ ] Static pub/sub with 4 message types bridged to Mediator -- `PanelMessageBusTests` (7 tests)
- [ ] SelectionChanged, NavigateToPanel, DataModified, RefreshPanel messages

### G3. Link Engine

- [ ] LinkEngine manages DB-stored LinkRules -- `LinkEngineTests` (8 tests)
- [ ] Default link rules: SD Technicians>Requests, Requesters>Requests, Groups>Requests, Devices>Switches, Users>AuthEvents
- [ ] Right-click grid > "Configure Links..." opens LinkCustomizerDialog
- [ ] LinkCustomizerDialog shows source/target panel dropdowns + field names + active toggle
- [ ] Link rules persisted in panel_customizations table
- [ ] Cross-panel filtering works: click tech in SD Technicians > Request grid auto-filters

### G4. Notification Service

- [ ] NotificationService.Instance singleton with Info/Success/Warning/Error -- `NotificationServiceTests` (8 tests)
- [ ] Default toast channel
- [ ] Suppress none channel
- [ ] Toast channel
- [ ] Email channel triggers EmailRequested event
- [ ] Both channels: toast + email
- [ ] Disabled suppression
- [ ] Preference reload
- [ ] Recent cap 50
- [ ] NotificationService.NotificationReceived event for shell rendering
- [ ] Notification icon per type (Info/Success/Warning/Error) — `NotificationTypeTests.Notification_Icon_Info`, `NotificationTypeTests.Notification_Icon_Success`, `NotificationTypeTests.Notification_Icon_Warning`, `NotificationTypeTests.Notification_Icon_Error`
- [ ] Notification timestamp is recent — `NotificationTypeTests.Notification_Timestamp_IsRecent`
- [ ] Notification properties (type, title, message, source) — `NotificationTypeTests.Notification_Properties`
- [ ] Notification null source handled — `NotificationTypeTests.Notification_NullSource`

### G4a. Alert Service

- [ ] AlertService.PingFailed adds warning alert — `AlertServiceTests.PingFailed_AddsToRecent`
- [ ] AlertService.PingRecovered adds info alert — `AlertServiceTests.PingRecovered_AddsInfoAlert`
- [ ] AlertService.SshFailed adds error alert — `AlertServiceTests.SshFailed_AddsErrorAlert`
- [ ] AlertService.ConfigDrift adds warning alert — `AlertServiceTests.ConfigDrift_AddsWarningAlert`
- [ ] AlertService.BgpPeerDown adds error alert — `AlertServiceTests.BgpPeerDown_AddsErrorAlert`
- [ ] AlertRaised event fires — `AlertServiceTests.AlertRaised_EventFires`
- [ ] Alert defaults — `AlertServiceTests.Alert_Defaults`

### G5. Notification Preferences

- [ ] notification_preferences table stores per-user event/channel pairs
- [ ] 8 event types: sync_failure, sync_complete, auth_lockout, backup_complete/failure, data_changed, password_expiry, webhook_received — `NotificationPreferenceExtendedTests.NotificationEventTypes_ContainsAllKnownTypes`, `NotificationPreferenceExtendedTests.NotificationEventTypes_ExactCount`
- [ ] Channels: toast, email, both, none — `NotificationPreferenceExtendedTests.PropertyChanged_Channel_Fires`
- [ ] UpsertNotificationPreferenceAsync for save
- [ ] GetNotificationPreferencesAsync loads user's preferences
- [ ] JIT-provisioned users get default notification prefs for all 8 event types
- [ ] Auth lockout triggers NotifyEvent("auth_lockout")
- [ ] Sync failure triggers NotifyEvent("sync_failure")
- [ ] Sync completion triggers NotifyEvent("sync_complete")
- [ ] Backup completion triggers NotifyEvent("backup_complete")

### G6. Notification Models

- [ ] NotificationPreference EventDescription for all 8 known types -- `NotificationModelTests` (14 tests), `NotificationPreferenceExtendedTests.EventDescription_AllKnownTypes`
- [ ] Unknown event type returns raw string — `NotificationPreferenceExtendedTests.EventDescription_UnknownType_ReturnsRaw`
- [ ] NotificationEventTypes.All has 8 entries — `NotificationPreferenceExtendedTests.NotificationEventTypes_ExactCount`, `NotificationPreferenceExtendedTests.NotificationEventTypes_ContainsAllKnownTypes`
- [ ] NotificationPreference defaults and PropertyChanged (Id, UserId, EventType, Channel, IsEnabled) — `NotificationPreferenceExtendedTests.Defaults_AreCorrect`, `NotificationPreferenceExtendedTests.PropertyChanged_Id_Fires`, `NotificationPreferenceExtendedTests.PropertyChanged_UserId_Fires`, `NotificationPreferenceExtendedTests.PropertyChanged_EventType_Fires`, `NotificationPreferenceExtendedTests.PropertyChanged_Channel_Fires`, `NotificationPreferenceExtendedTests.PropertyChanged_IsEnabled_Fires`
- [ ] ActiveSession defaults — `NotificationPreferenceExtendedTests.ActiveSession_Defaults`
- [ ] ActiveSession Duration formats correctly — `NotificationPreferenceExtendedTests.ActiveSession_Duration_FormatDaysHoursMinutes`, `NotificationPreferenceExtendedTests.ActiveSession_Duration_LessThanOneDay`
- [ ] ActiveSession StatusColor: active=green, inactive=grey — `NotificationPreferenceExtendedTests.ActiveSession_StatusColor_Active_Green`, `NotificationPreferenceExtendedTests.ActiveSession_StatusColor_Inactive_Grey`
- [ ] ActiveSession ExpiresAt nullable — `NotificationPreferenceExtendedTests.ActiveSession_ExpiresAt_Nullable`
- [ ] ApiKeyRecord PropertyChanged fires — `ApiKeyRecordTests.PropertyChanged_AllFields`
- [ ] DashboardData defaults
- [ ] ActivityItem defaults
- [ ] SavedFilter IsShared, PropertyChanged — `SavedFilterTests` (3 tests)
- [ ] AdUser defaults
- [ ] AdConfig IsConfigured
- [ ] IconOverride defaults

### G7. Email Service

- [ ] EmailService.Instance configurable with SMTP settings -- `EmailServiceTests` (6 tests)
- [ ] Configure() accepts host, port, username, password, from address, SSL
- [ ] SendAsync sends text or HTML emails
- [ ] Predefined templates: sync failure alert, auth lockout alert, backup complete
- [ ] SendTestEmailAsync for testing SMTP configuration
- [ ] Non-blocking -- email failures don't crash the app
- [ ] EmailService configured from app settings at startup
- [ ] NotificationService.EmailRequested wired to EmailService

### G8. Cron Expression Parser

- [ ] CronExpression.Parse various expressions -- `CronExpressionTests` (14 tests) + `CronExpressionNextOccurrenceTests` (18 tests) + `CronExpressionExtendedTests` (16 tests)
- [ ] Cron first and fifteenth of month — `CronExpressionExtendedTests.Parse_FirstAndFifteenth_OfMonth`
- [ ] Cron business hours weekdays only — `CronExpressionExtendedTests.Parse_BusinessHours_WeekdaysOnly`
- [ ] Cron multiple months (quarterly) — `CronExpressionExtendedTests.Parse_MultipleMonths`
- [ ] Cron weekend only — `CronExpressionExtendedTests.Parse_WeekendOnly`
- [ ] Cron every 5 minutes work hours — `CronExpressionExtendedTests.Parse_Every5Minutes_WorkHours`
- [ ] Cron last minute of day — `CronExpressionExtendedTests.Parse_LastMinuteOfDay`
- [ ] Cron every minute on Sunday — `CronExpressionExtendedTests.Parse_EveryMinuteOnSunday`
- [ ] GetNextOccurrence cross year — `CronExpressionExtendedTests.GetNextOccurrence_CrossYear`
- [ ] GetNextOccurrence weekday skips weekend — `CronExpressionExtendedTests.GetNextOccurrence_WeekdaySkipsWeekend`
- [ ] GetNextOccurrence every hour same day — `CronExpressionExtendedTests.GetNextOccurrence_EveryHour_SameDay`
- [ ] GetNextOccurrence midnight next day — `CronExpressionExtendedTests.GetNextOccurrence_MidnightNextDay`
- [ ] ToString contains cron values — `CronExpressionExtendedTests.ToString_SpecificExpression_ContainsValues`
- [ ] TryParse too many fields/empty/whitespace — `CronExpressionExtendedTests.TryParse_TooManyFields_ReturnsFalse`, `CronExpressionExtendedTests.TryParse_EmptyString_ReturnsFalse`, `CronExpressionExtendedTests.TryParse_WhitespaceOnly_ReturnsFalse`
- [ ] TryParse valid expression returns result — `CronExpressionExtendedTests.TryParse_ValidExpression_ReturnsResult`
- [ ] Matches(DateTime) returns true when time matches — `CronExpressionNextOccurrenceTests.Matches_SpecificDateTime`, `Matches_Wildcard`
- [ ] GetNextOccurrence(after) returns next matching time — `CronExpressionNextOccurrenceTests.GetNextOccurrence_EveryMinute`, `GetNextOccurrence_SpecificTime`
- [ ] TryParse returns false for invalid expressions — `CronExpressionNextOccurrenceTests.TryParse_Valid_ReturnsTrue`, `TryParse_Invalid_ReturnsFalse`
- [ ] Supports: *, ranges (1-5), steps (*/15), lists (1,3,5) — `CronExpressionNextOccurrenceTests.Parse_CommaList`, `Parse_Range`, `Parse_Step_FromWildcard`
- [ ] Sunday (day 0) matching -- `CronEdgeCaseTests.Sunday`
- [ ] Multiple comma values -- `CronEdgeCaseTests.MultipleCommaValues`
- [ ] First day of month -- `CronEdgeCaseTests.FirstDayOfMonth`
- [ ] Specific month -- `CronEdgeCaseTests.SpecificMonth`
- [ ] GetNextOccurrence skips non-matching months -- `CronEdgeCaseTests.SkipMonths`
- [ ] Step from non-zero start -- `CronEdgeCaseTests.StepFromNonZero`
- [ ] Invalid field count throws -- `CronEdgeCaseTests.InvalidFieldCount`

### G9. Cron Integration in Job Scheduler

- [ ] job_schedules table has schedule_cron column (migration 051)
- [ ] Jobs with cron expression run when current minute matches cron
- [ ] Jobs without cron use interval_minutes (backward compatible)
- [ ] next_run_at calculated from CronExpression.GetNextOccurrence
- [ ] Cron-based jobs filtered at check time
- [ ] Both cron and interval jobs visible in Jobs admin panel

### G10. Data Validation Service

- [ ] DataValidationService.Instance singleton -- `DataValidationServiceTests` (12 tests) + `DataValidationEdgeCaseTests` (22 tests)
- [ ] Required rule: rejects null/empty/whitespace — `DataValidationEdgeCaseTests.Validate_RequiredNull_Fails`, `Validate_RequiredWhitespace_Fails`
- [ ] MinLength rule — `DataValidationEdgeCaseTests.Validate_MinLength_TooShort_Fails`, `Validate_MinLength_ExactLength_Passes`
- [ ] MaxLength rule — `DataValidationEdgeCaseTests.Validate_MaxLength_TooLong_Fails`, `Validate_MaxLength_ExactLength_Passes`
- [ ] Regex rule — `DataValidationEdgeCaseTests.Validate_Regex_Valid_Passes`, `Validate_Regex_Invalid_Fails`
- [ ] Range rule — `DataValidationEdgeCaseTests.Validate_Range_IntInRange_Passes`, `Validate_Range_IntBelowMin_Fails`
- [ ] Custom rule with Func<object?, bool> — `DataValidationEdgeCaseTests.Validate_Custom_PassesWhenTrue`, `Validate_Custom_FailsWhenFalse`
- [ ] Multiple rules per entity, all errors reported — `DataValidationEdgeCaseTests.Validate_MultipleRules_AllChecked`
- [ ] RegisterDefaults() seeds rules for Device, User, SdRequest, Appointment, Country, ReferenceConfig — `DataValidationEdgeCaseTests.RegisterDefaults_RegistersKnownTypes`
- [ ] Registered at startup in App.OnStartup

### G11. Settings Export/Import Service

- [ ] ExportAsync exports user settings as JSON -- `SettingsExportTests` (3 tests), `SettingsExportExtendedTests.ExportAsync_WithMultipleSettings_ContainsAll`, `SettingsExportExtendedTests.ExportAsync_WithEmptyData_ValidJson`, `SettingsExportExtendedTests.ExportAsync_IncludesAppVersion`
- [ ] ExportToFileAsync writes JSON to file — `SettingsExportExtendedTests.ExportImport_RoundTrip_PreservesAllData`
- [ ] ImportFromFile parses exported settings JSON — `SettingsExportExtendedTests.ExportImport_RoundTrip_PreservesAllData`
- [ ] ExportedSettings defaults (empty collections, version, ISO timestamp) — `SettingsExportExtendedTests.ExportedSettings_Defaults`, `SettingsExportExtendedTests.ExportedSettings_ExportedAt_IsIsoFormat`
- [ ] ImportFromFile malformed JSON throws — `SettingsExportExtendedTests.ImportFromFile_MalformedJson_Throws`

### G12. CommandGuard

- [ ] TryEnter/Exit prevents concurrent execution -- `CommandGuardTests` (7 tests) + `CommandGuardEdgeCaseTests` (6 tests)
- [ ] RunAsync wraps async actions with automatic TryEnter/Exit — `CommandGuardEdgeCaseTests.RunAsync_ExitsCleanly_AfterException`
- [ ] Run wraps sync actions with automatic TryEnter/Exit — `CommandGuardEdgeCaseTests.Run_Sync_ExitsClearly_AfterException`
- [ ] IsRunning tracks current state — `CommandGuardEdgeCaseTests.MultipleEnterExit_Cycles`
- [ ] Different command names are independent — `CommandGuardEdgeCaseTests.ConcurrentAttempts_OnlyOneSucceeds`
- [ ] Applied to: GlobalAdd, GlobalDelete, AddTask, AddSubTask, Refresh, SaveLayout

### G13. SafeAsync

- [ ] SafeAsync.Run wraps async void handlers with try/catch -- `SafeAsyncTests` (4 tests)
- [ ] Exceptions routed to NotificationService.Error
- [ ] Context string included in error messages
- [ ] SafeAsync.RunGuarded combines CommandGuard + safe exception handling
- [ ] RunGuarded releases guard even on exception

### G14. Sync Engine

- [ ] Sync Config panel opens from Admin > System > Sync Engine
- [ ] Sync configs grid shows Name, AgentType, Enabled, Direction, Interval, Status
- [ ] Run Sync executes SyncEngine.ExecuteSyncAsync
- [ ] Test Connection checks agent availability
- [ ] ManageEngine agent registered -- `SyncEngineTests` (6 tests)
- [ ] 7 field converters registered -- `FieldConverterTests` (15 tests)
- [ ] Entity maps / field maps define mapping per config
- [ ] Concurrent sync throttled by SemaphoreSlim
- [ ] Cancel sync via SyncEngine.CancelSync(configId)

### G15. Sync Engine Agents

- [ ] ManageEngine agent (agent_type='manage_engine') -- OAuth refresh, paged read, write-back
- [ ] CSV Import agent -- `CsvImportAgentTests` (9 tests)
- [ ] REST API agent -- `RestApiAgentTests` (6 tests)
- [ ] All 3 agents registered at startup -- `AgentRegistrationTests` (3 tests)

### G16. Sync Models

- [ ] SyncConfig StatusColor: success/failed/running/partial/never -- `SyncModelsTests` (9 tests)
- [ ] SyncLogEntry StatusColor
- [ ] SyncEntityMap PropertyChanged
- [ ] SyncFieldMap PropertyChanged
- [ ] SyncConfig PropertyChanged

### G17. Sync Pipeline Integration

- [ ] Full pipeline with direct mapping -- `SyncPipelineTests.DirectMapping`
- [ ] Full pipeline with converters -- `SyncPipelineTests.WithConverters`
- [ ] Upsert failure counted as failed record -- `SyncPipelineTests.UpsertFailure`
- [ ] Empty source: success with 0 records -- `SyncPipelineTests.EmptySource`
- [ ] Multiple entity maps synced concurrently -- `SyncPipelineTests.MultipleEntityMaps`

### G18. Converter Edge Cases

- [ ] Direct: null, int, bool pass through -- `ConverterEdgeCaseTests` (11 tests)
- [ ] Constant: always returns expression
- [ ] Combine: empty expression handled
- [ ] Split: null value, invalid expression handled
- [ ] DateFormat: null and invalid string handled
- [ ] Expression: empty value with $value ref
- [ ] Lookup: null value handled

### G19. Sync Engine Resilience

- [ ] SyncRetry.WithRetryAsync: exponential backoff (1s, 2s, 4s), configurable max retries, 30s cap
- [ ] SyncHashDetector: SHA-256 content hash, skip unchanged records
- [ ] SyncFieldValidator: pre-write validation of required/key fields
- [ ] Dead letter queue: failed records stored with error message, retry count
- [ ] Per-record retry in SyncEngine: 2 retries with 500ms backoff
- [ ] Hash lookup/update callbacks
- [ ] Validation errors routed to dead letter queue (not retried)
- [ ] RecordsSkipped counter for unchanged records

### G20. File Management Service

- [ ] FileManagementService singleton: Configure() sets filesystem storage path — `FileManagementServiceTests` (17 tests)
- [ ] ComputeMd5: byte array and Stream overloads — `FileManagementServiceTests.ComputeMd5_ByteArray_ReturnsDeterministicHash`, `ComputeMd5_Stream_ReturnsSameAsBytes`
- [ ] ShouldStoreInline: files <= 10MB stored in DB, larger on filesystem — `FileManagementServiceTests.ShouldStoreInline_SmallFile_True`, `ShouldStoreInline_LargeFile_False`
- [ ] GetStoragePath: sharded directory structure, auto-create
- [ ] SaveToFilesystemAsync / ReadFromFilesystemAsync / DeleteFromFilesystem
- [ ] FileRecord model with FileSizeDisplay (B, KB, MB, GB) — `FileManagementServiceTests.FileRecord_FileSizeDisplay`
- [ ] FileVersionRecord model

### G21. Elsa Workflows Engine

- [ ] Central.Workflows project with Elsa 3.5.3
- [ ] PostgreSQL persistence for both Management and Runtime stores
- [ ] 6 custom activities: UpdateTaskStatus, ValidateTransition, SendNotification, Approval, LogAudit, SetField -- `WorkflowActivityTests` (9 tests)
- [ ] TaskStatusTransitionWorkflow built-in
- [ ] Activities use workflow variables (SetVariable)
- [ ] ApprovalActivity uses Elsa bookmarks for suspend/resume
- [ ] Elsa management API at /elsa/api/*

---

## H. Data Layer

### H1. Database Connection

- [ ] DSN from CENTRAL_DSN env var works
- [ ] Fallback DSN (localhost defaults) works
- [ ] 5s connection timeout
- [ ] 10s background retry on failure
- [ ] pg_notify triggers fire on all 19+ tables
- [ ] pg_notify triggers on all new tables (migration 048)

### H2. Migrations

- [ ] All migrations apply cleanly on fresh DB
- [ ] Migrations are idempotent (re-run safe)
- [ ] PowerShell setup script: db\setup.ps1 applies all migrations via psql
- [ ] Migration 050: webhook_log with pg_notify trigger
- [ ] Migration 051: schedule_cron column on job_schedules
- [ ] Migration 052: audit_log + password_history
- [ ] Migration 053: api_keys with SHA256 hash
- [ ] Migration 054: notification_preferences + active_sessions
- [ ] Migration 055: saved_filters table
- [ ] Migration 056: central_platform schema with cross-tenant tables
- [ ] Migration 058: sync_failed_records + sync_record_hashes
- [ ] Migration 059: file_store + file_versions
- [ ] Migration 060: tasks_v2 (portfolios, programmes, projects, sprints, releases, links, dependencies)
- [ ] Migration 061: sprint_allocations + sprint_burndown
- [ ] Migration 062: board_columns + board_lanes
- [ ] Migration 063: workflow_assignments + workflow_approvals + workflow_execution_log
- [ ] Migration 064: task_baselines + save_project_baseline() function
- [ ] Migration 065: custom_columns + custom_column_permissions + task_custom_values
- [ ] Migration 066: saved_reports + dashboards + dashboard_snapshots
- [ ] Migration 067: time_entries + activity_feed + task_views + log_task_activity() trigger

### H3. Multi-Tenant RLS

- [ ] tenant_id UUID column on ALL public-schema tables (40+ tables)
- [ ] Default tenant UUID for all existing rows
- [ ] RLS policies (tenant_isolation) on every table
- [ ] set_tenant_context(uuid) function for DbRepository
- [ ] set_default_tenant() convenience function
- [ ] _add_tenant_rls() helper for future migrations
- [ ] Indexes on tenant_id columns

### H4. Real-Time Notifications (pg_notify)

- [ ] SignalR DataChanged handler covers: identity_providers, appointments, countries, regions, reference_config, backup_history, icon_defaults, sd_technicians, sd_groups, sd_teams
- [ ] Panel loaded flags reset on DataChanged
- [ ] Toast notifications shown for multi-user changes
- [ ] SignalR DataChanged handlers for 13 task-related tables

### H5. Admin Models

- [ ] ReferenceConfig, ContainerInfo, MigrationRecord, BackupRecord, GridSettings, Location, Appointment -- `AdminModelsTests` (20 tests) + `ReferenceConfigTests` (10 tests) + `MigrationRecordTests` (5 tests)
- [ ] ContainerInfo StateColor, IsRunning, PropertyChanged -- `ContainerInfoTests` (3 tests)
- [ ] BackupRecord FileSizeDisplay, StatusColor -- `BackupRecordTests` (2 tests)
- [ ] Location models PropertyChanged -- `LocationModelTests` (4 tests)
- [ ] Appointment models defaults + PropertyChanged -- `AppointmentModelTests` (4 tests)

### H6. First-Time Setup / Seed Data

- [ ] Start pod: podman play kube infra/pod.yaml
- [ ] Apply migrations: ./db/setup.sh or auto-apply on app startup
- [ ] Default admin: admin/admin (System user, cannot be deleted)
- [ ] Default roles: Admin (100), Operator (50), Viewer (10)
- [ ] Default lookups: status, device_type, building
- [ ] Default notification preferences seeded for admin user

---

## I. Infrastructure

### I1. Enterprise V2 -- Multi-Tenancy Foundation

- [ ] Central.Tenancy project (ITenantContext, TenantConnectionFactory, TenantSchemaManager)
- [ ] TenantContext.Default for backward-compatible single-tenant mode
- [ ] Schema name validated (alphanumeric + underscore only)
- [ ] ProvisionTenantAsync creates schema + applies all migrations
- [ ] DropTenantSchemaAsync (refuses to drop public/central_platform)
- [ ] central_platform schema tables: tenants, subscription_plans, tenant_subscriptions, module_catalog, etc.
- [ ] Seed: 3 subscription plans, 8 module catalog entries, 3 release channels
- [ ] Default tenant seeded for backward compatibility
- [ ] Tenancy models -- `TenancyTests` (10 tests)

### I2. Enterprise V2 -- Registration + Licensing

- [ ] Central.Licensing project
- [ ] RegistrationService: RegisterAsync creates global user + tenant + subscription — `RegistrationTests` (2 tests)
- [ ] Email verification, slug generation, slug uniqueness
- [ ] SubscriptionService: limits, expiry, plan upgrade
- [ ] ModuleLicenseService: IsModuleLicensedAsync, Grant, Revoke
- [ ] LicenseKeyService: RSA-4096 signed license keys — `LicenseKeyTests` (13 tests)
- [ ] Offline validation: public key embedded, verify signature + hardware + expiry — `LicenseKeyTests.ValidateLicense_TamperedPayload_InvalidSignature`

### I3. Enterprise V2 -- Multi-Tenancy Enforcement

- [ ] TenantResolutionMiddleware extracts tenant_slug from JWT
- [ ] Falls back to X-Tenant header for API key auth
- [ ] Defaults to "default" tenant for backward compatibility
- [ ] ModuleLicenseMiddleware maps API paths to module codes
- [ ] Enterprise tier bypasses module license checks

### I4. Enterprise V2 -- Client Binary Protection

- [ ] Central.Protection project
- [ ] HardwareFingerprint: CPU ID + disk serial + machine name + MAC > SHA256
- [ ] ClientLicenseValidator: RSA public key, offline validation
- [ ] DPAPI-encrypted local cache for 7-day offline grace period
- [ ] CertificatePinningHandler: SHA-256 public key pinning
- [ ] IntegrityChecker: SHA-256 of Central*.dll files at runtime — `IntegrityResultTests` (7 tests)

### I5. Enterprise V2 -- Auto-Update Manager

- [ ] Central.UpdateClient project
- [ ] UpdateManager: CheckForUpdateAsync, ApplyUpdateAsync, Rollback, RestartApplication
- [ ] SHA-256 checksum verification, backup before overwrite

### I6. Enterprise V2 -- Environment Routing

- [ ] EnvironmentService singleton manages Live/Test/Dev connection profiles — `EnvironmentServiceTests` (10 tests)
- [ ] Profiles stored in %LocalAppData%/Central/environments.json
- [ ] SwitchTo(name) changes active environment + fires EnvironmentChanged event

### I7. Enterprise V2 -- Concurrent Editing

- [ ] Central.Collaboration project -- `CollaborationTests` (10 tests)
- [ ] PresenceService: JoinEditing, LeaveEditing, DisconnectAll, GetEditors
- [ ] ConflictDetector: row_version comparison, three-way merge
- [ ] Non-overlapping changes auto-merged, overlapping flagged

### I8. Enterprise V2 -- Item-Level Security (ABAC)

- [ ] Central.Security project -- `SecurityTests` (6 tests)
- [ ] SecurityPolicyEngine: CanAccessRow, GetHiddenFields, FilterFields
- [ ] Policies: EntityType, PolicyType (row/field), Effect (allow/deny), Conditions, Priority

### I9. Enterprise V2 -- Observability

- [ ] Central.Observability project -- `ObservabilityTests` (5 tests)
- [ ] CorrelationContext: AsyncLocal<string> for request correlation ID
- [ ] StructuredLogEntry with ToCef() for SIEM integration
- [ ] Level-to-severity mapping

### I10. Enterprise V2 -- Solution Structure (21 projects)

- [ ] Central.Core, Central.Data, Central.Api, Central.Api.Client, Central.Desktop
- [ ] 8 modules: Devices, Switches, Links, Routing, VLANs, Admin, Tasks, ServiceDesk
- [ ] Central.Tests, Central.Workflows
- [ ] Central.Tenancy, Central.Licensing, Central.Protection, Central.UpdateClient
- [ ] Central.Collaboration, Central.Security, Central.Observability

### I11. Terraform Modules (IaC)

- [ ] VPC module -- subnets, NAT gateway, IGW, route tables
- [ ] EKS module -- cluster + managed node groups, OIDC provider for IRSA
- [ ] RDS module -- Aurora PostgreSQL cluster, encryption, parameter group
- [ ] ElastiCache module -- Redis replication group, failover
- [ ] ECR module -- container registries for all 8 services, lifecycle policies
- [ ] S3 module -- media/backup/config buckets, versioning, encryption
- [ ] KMS module -- customer-managed keys with rotation
- [ ] Secrets module -- DB creds, JWT key, encryption key in Secrets Manager
- [ ] Monitoring module -- CloudWatch log groups, alarms, dashboard
- [ ] K8s Service module -- reusable Deployment + Service + HPA + PDB

### I12. Terragrunt Configuration

- [ ] Root terragrunt.hcl -- S3 backend, DynamoDB locking, provider generation
- [ ] _envcommon/ -- DRY module configs
- [ ] dev/env.hcl -- 2 AZs, t3.medium, single NAT, no spot
- [ ] staging/env.hcl -- 2 AZs, t3.large, spot enabled
- [ ] prod/env.hcl -- 3 AZs, r6g/m6i, spot, read replicas, transit encryption
- [ ] Dependency chain: KMS > VPC > EKS > RDS/ElastiCache > ECR > Secrets

### I13. K8s Base Manifests

- [ ] Namespace, RBAC, ResourceQuota, LimitRange, ConfigMap
- [ ] PostgreSQL HA StatefulSet (primary + streaming replica, WAL replication, anti-affinity)
- [ ] Redis StatefulSet (AOF persistence, LRU eviction)
- [ ] Central API Deployment (2 replicas, HPA 2-8, PDB)
- [ ] Auth Service Deployment (2 replicas, HPA 2-6, PDB)
- [ ] LoadBalancer services via MetalLB

### I14. Local K8s Cluster

- [ ] Terraform module generates Vagrantfile
- [ ] 1 master + 6 workers on VMware Workstation
- [ ] Ansible roles: common, containerd, k8s-master, k8s-worker, metallb, registry
- [ ] MetalLB L2 advertisement
- [ ] Local container registry (NodePort 30500)
- [ ] Calico CNI, Kubeconfig exported

### I15. Data Migration

- [ ] pg_dump from Podman pod or backup file
- [ ] pg_restore into K8s PostgreSQL StatefulSet
- [ ] Creates secure_auth database, applies auth migrations, seeds

### I16. setup.sh K8s Commands

- [ ] k8s-up, k8s-deploy, k8s-status, k8s-psql, k8s-logs, k8s-migrate, k8s-push, k8s-down

### I17. CI/CD Pipeline

- [ ] GitHub Actions workflow on push to main/develop and PRs
- [ ] Steps: checkout, setup .NET 10, restore, build (x64 Release), test
- [ ] Test results published via dotnet-trx reporter
- [ ] Container build job on main branch only (after tests pass)
- [ ] Podman build + tag + health test in CI

### I18. Deployment Containers

- [ ] Dockerfile includes Api.Client project reference
- [ ] Dockerfile copies db/migrations/ for auto-apply
- [ ] HEALTHCHECK directive pings /api/health every 30s
- [ ] pod.yaml has postgres + api containers with resource limits
- [ ] pod.yaml uses PG 18 alpine with performance tuning

### I19. Platform Merge -- Auth Service Integration

- [ ] auth-service container in Central Podman pod (port 8081)
- [ ] Redis container in pod (port 6379, session store)
- [ ] secure_auth database created on same PG instance
- [ ] Auth-service V001-V017 migrations applied
- [ ] Seed: default tenant, admin user, roles with Central permission codes
- [ ] setup.sh: build-auth, auth-logs, auth-psql commands

### I20. Platform Merge -- Planned Phases (2-10)

- [ ] Phase 2: Rust API gateway routing
- [ ] Phase 3: Task Service (Rust/Axum)
- [ ] Phase 4: Storage + Sync (CAS dedup, SQLite offline)
- [ ] Phase 5: Flutter Mobile (biometric login, tasks, push)
- [ ] Phase 6: Angular Web Client (DevExtreme, OIDC, SSE)
- [ ] Phase 7: M365 Audit + GDPR (investigation, forensic, document tracker)
- [ ] Phase 8-10: K8s + IaC + Admin Console

---

## I2. System Tray Manager (Rust)

### Tray Icon & Status
- [ ] Tray icon appears in Windows system tray on launch
- [ ] Green icon when all 11 services running (HEALTH_ALL)
- [ ] Yellow icon when partial services running (HEALTH_PARTIAL)
- [ ] Red icon when no services running (HEALTH_NONE)
- [ ] Tooltip shows "Central Platform - {health} ({running}/{total})"
- [ ] Status polls K8s every 200ms via kubectl
- [ ] Right-click opens context menu without closing immediately

### Service Management (per service: central-api, auth, task, storage, sync, audit, admin, gateway)
- [ ] Service state shown in submenu: [UP], [STOPPED], [NOT DEPLOYED]
- [ ] Open in Browser → opens service URL
- [ ] [~] Restart → kubectl rollout restart
- [ ] [x] Stop → kubectl scale --replicas=0
- [ ] [>] Start → kubectl scale --replicas=1
- [ ] View Logs → opens terminal with kubectl logs -f

### Data Layer
- [ ] psql: central DB → kubectl exec postgres-0 -- psql
- [ ] psql: secure_auth DB → kubectl exec postgres-0 -- psql -d secure_auth
- [ ] Backup (pg_dump) → kubectl exec pg_dump
- [ ] Run Migrations → applies db/migrations/*.sql
- [ ] redis-cli → kubectl exec redis-0 -- redis-cli
- [ ] MinIO Console → opens MinIO web UI

### Global Admin
- [ ] View Tenants → queries central_platform.tenants
- [ ] Global Users → queries central_platform.global_users
- [ ] Subscriptions → queries tenant_subscriptions + plans
- [ ] Module Licenses → queries tenant_module_licenses
- [ ] New Tenant → redirects to desktop app
- [ ] Provision Schema → redirects to desktop app

### K8s Infrastructure
- [ ] Nodes → kubectl get nodes -o wide
- [ ] All Pods → kubectl -n central get pods -o wide
- [ ] Services → kubectl -n central get svc
- [ ] HPA (Autoscale) → kubectl -n central get hpa
- [ ] Events → kubectl -n central get events
- [ ] Deploy Manifests → kubectl apply -k infra/k8s/base/

### VMware VMs
- [ ] Start All → vagrant up (in terminal window)
- [ ] Stop All → vagrant halt
- [ ] Status → vagrant status + kubectl get nodes
- [ ] SSH to k8s-master, k8s-worker-01 through k8s-worker-04

### Cluster Actions
- [ ] Restart All Services → rollout restart all 8 deployments
- [ ] Stop All Services → scale all to 0
- [ ] Refresh Status → force status re-poll

### Quick Launch
- [ ] Open Gateway → http://192.168.56.203:8000
- [ ] Open Swagger → http://192.168.56.200:5000/swagger
- [ ] Open Angular → http://localhost:4200
- [ ] Launch Desktop App → Central.exe

### Tools
- [ ] Tray Manager Logs → internal log viewer
- [ ] Audit Log → view audit events
- [ ] Open Project Folder → opens C:\Development\Central
- [ ] Open Terminal → opens CMD
- [ ] Check for Updates → version manifest check
- [ ] About Central → version info dialog

---

## J. Unit Tests (1,900 total across 148 test classes)

All tests are in `desktop/Central.Tests/`. Grouped by area with test counts.
Per-section counts are based on [Fact]/[Theory] attribute counts and may differ slightly
from the test runner total (1,900) due to [Theory] parameterization.

### J1. Auth Tests (263 tests)

| Test Class | Count | Coverage |
|---|---|---|
| `AuthContextTests` | 12 | AuthContext state, permissions, sites, offline, logout |
| `AuthFrameworkTests` | 19 | AuthResult, UserTypes, AuthStates, SecureString, IdP config, claims, AppUser |
| `AuthStatesTests` | 10 | All 9 auth states, state transitions, display names |
| `CredentialEncryptorTests` | 18 | AES-256 encrypt/decrypt, roundtrip, Base64, special chars, unicode, IsEncrypted, key rotation |
| `IdentityConfigTests` | 8 | IdentityProviderConfig, ClaimMapping, DomainMapping, AuthEvent, AuthRequest |
| `PasswordHasherTests` | 7 | Salt generation, hash consistency, verify correct/wrong, empty |
| `PasswordPolicyExtendedTests` | 19 | Extended policy validation, edge cases |
| `PasswordPolicyTests` | 18 | Default/relaxed policy, validation, history, expiry, min age, errors |
| `PermissionCodeTests` | 26 | All 25 permission codes defined, uniqueness, module grouping |
| `PermissionCodeExtendedTests` | 43 | All individual permission code values, AllCodes_ContainColon, AllCodes_AreLowercase |
| `PermissionGuardTests` | 6 | Permission guard checks, role-based access |
| `SecureStringExtensionTests` | 10 | SecureString ToPlainText, ToPasswordHash, VerifyHash, roundtrips |
| `TotpServiceTests` | 7 | Secret generation, QR URI, code generation, verify, recovery codes |
| `UserTypesTests` | 8 | All types, IsProtected, Initials, StatusText, StatusColor |
| `AuthContextExtendedTests` | 30 | IsAuthenticated, IsSuperAdmin, HasPermission case-insensitive, HasAnyPermission, HasSiteAccess, CanView/Edit/Delete, CanViewReserved, IsAdmin, SetOfflineAdmin, Logout, UpdateAllowedSites, PermissionCount, PermissionsChanged, PropertyChanged |
| `AuthStatesExtendedTests` | 18 | Extended auth state validation, transitions, display names |
| `AuthUserTests` | 4 | AuthUser model defaults, PropertyChanged |

### J2. Enterprise Tests (54 tests)

| Test Class | Count | Coverage |
|---|---|---|
| `TenancyTests` | 10 | Default context, custom tenant, schema validation, Tenant/Plan/GlobalUser/Environment/ClientVersion/ModuleLicense models |
| `CollaborationTests` | 10 | Presence join/leave/disconnect, multi-editor, tenant isolation, conflict detection, three-way merge |
| `SecurityTests` | 6 | No policies=allow, deny blocks, field hiding, field filtering, priority |
| `ObservabilityTests` | 5 | Correlation ID, set/get, scope restore, CEF format, severity mapping |
| `LicenseKeyTests` | 13 | GenerateKeyPair, ValidateLicense (malformed/empty/invalidBase64/tampered/noPublicKey), LicensePayload defaults, LicenseValidationResult, LimitCheckResult |
| `RegistrationTests` | 3 | RegistrationRequest defaults, RegistrationResult Fail factory + success |
| `IntegrityResultTests` | 7 | IsIntact, Summary (intact/tampered/missing/both), defaults, zero verified |

### J3. Integration Tests (64 tests)

| Test Class | Count | Coverage |
|---|---|---|
| `AgentRegistrationTests` | 3 | All agents registered, all converters, duplicate overwrites |
| `ConverterEdgeCaseTests` | 11 | Direct/constant/combine/split/dateformat/expression/lookup edge cases |
| `CsvImportAgentTests` | 9 | TestConnection, read CSV/TSV, quoted fields, no header, GetFields, write error |
| `FieldConverterTests` | 15 | All 7 converter types with various inputs |
| `RestApiAgentTests` | 6 | AgentType, TestConnection, Initialize, GetEntityNames, Delete |
| `SyncEngineTests` | 6 | Agent registration, execute sync, disabled entity |
| `SyncModelsTests` | 9 | StatusColor, PropertyChanged for SyncConfig/EntityMap/FieldMap/LogEntry |
| `SyncPipelineTests` | 5 | Full pipeline: direct, converters, failure, empty, multiple entity maps |

### J4. Model Tests (954 tests)

| Test Class | Count | Coverage |
|---|---|---|
| `AdminModelsTests` | 20 | ReferenceConfig, ContainerInfo, MigrationRecord, BackupRecord, GridSettings, Location, Appointment |
| `ApiKeyRecordTests` | 3 | Defaults, PropertyChanged all fields, RawKey set on create |
| `AppLogEntryTests` | 3 | Defaults, DisplayTime format, PropertyChanged all fields |
| `AppointmentModelTests` | 4 | Defaults, PropertyChanged for Appointment + Resource |
| `AppUserTests` | 21 | IsAdUser, IsSystemUser, IsProtected, Initials (display name/single word/dot-separated/single char), StatusText, StatusColor, PropertyChanged, defaults, DetailPermissions |
| `AsnDefinitionTests` | 6 | DisplayText with/without description, zero devices, DetailDevices default, PropertyChanged all fields, AsnBoundDevice defaults |
| `BackupRecordTests` | 2 | FileSizeDisplay, StatusColor |
| `BgpRecordTests` | 6 | BgpRecord/BgpNeighborRecord/BgpNetworkRecord defaults, PropertyChanged |
| `BuilderSectionTests` | 10 | ConfigLine record, BuilderSection/BuilderItem defaults, PropertyChanged, observable items |
| `ConfigRangeTests` | 2 | Defaults, PropertyChanged all fields |
| `ContainerInfoTests` | 3 | StateColor, IsRunning, PropertyChanged |
| `DashboardDataTests` | 3 | Defaults, RecentActivity CanAddItems, ActivityItem defaults |
| `DeviceRecordTests` | 12 | IsLinked, IsActive, StatusColor (Active/Reserved/Decommissioned/Maintenance/Unknown), PropertyChanged, defaults |
| `EntityBaseTests` | 8 | SetField raises/suppresses PropertyChanged, TakeSnapshot captures all properties + null values, SoftDelete, Id/UpdatedAt PropertyChanged |
| `IconOverrideTests` | 2 | Defaults, SetProperties |
| `IntegrationModelTests` | 9 | StatusIcon/StatusText enabled/disabled, PropertyChanged IsEnabled notifies StatusIcon, defaults, IntegrationCredential defaults, IntegrationLogEntry defaults |
| `InterfaceOpticsTests` | 16 | DisplayTx/Rx/Temp, RxColor by power level, Parse empty/null/whitespace/basic |
| `IpRangeTests` | 2 | Defaults, PropertyChanged all fields |
| `KanbanModelsTests` | 13 | BoardColumn defaults/IsOverWip/WipDisplay/HeaderDisplay/PropertyChanged cascades, BoardLane defaults/PropertyChanged |
| `LocationModelTests` | 4 | Country/Region/Postcode PropertyChanged, lat/long |
| `MasterDeviceTests` | 3 | Defaults, PropertyChanged selected fields, all link counts |
| `MigrationRecordTests` | 5 | StatusColor green/amber, StatusText Applied/Pending, defaults |
| `MlagMstpTests` | 4 | MlagConfig/MstpConfig defaults, PropertyChanged all fields |
| `NetworkLinkTests` | 29 | P2P/B2B/FW BuildConfig, LinkHelper.ExtractPrefix, Validate, ValidationIcon/Color, ConfigA/ConfigB, GenerateDetailConfig, MismatchA/B, PropertyChanged |
| `NotificationModelTests` | 14 | EventDescription, NotificationEventTypes, ActiveSession, ApiKey, Dashboard, SavedFilter, AdUser, AdConfig, IconOverride |
| `NotificationTypeTests` | 11 | Notification Color/Icon by type, Timestamp, Properties, NullSource |
| `PanelCustomizationExtendedTests` | 5 | GridSettings list properties, FormLayout with groups, FieldGroup collapsed, LinkRule with values, PanelCustomizationRecord set values |
| `PermissionNodeTests` | 2 | Defaults, PropertyChanged all fields |
| `ProjectModelsTests` | 22 | Portfolio/Programme/TaskProject/Sprint/Release defaults + PropertyChanged, DisplayName, DateRange, TaskLink/TaskDependency display, ProjectMember |
| `ReferenceConfigTests` | 10 | SampleOutput (default/suffix/large/exceeds padding/empty prefix), PropertyChanged cascades (Prefix/PadLength/NextValue/Suffix notify SampleOutput), defaults |
| `RibbonConfigTests` | 28 | RibbonPageConfig/GroupConfig/ItemConfig defaults + PropertyChanged, UserRibbonOverride, RibbonTreeItem DisplayText/NodeIcon/HiddenIcon/DisplayStyle/LinkTarget |
| `RibbonTreeItemTests` | 15 | NodeIcon by type, DisplayText override/fallback, HiddenIcon, PropertyChanged cascades, defaults, DisplayStyle/LinkTarget |
| `SavedFilterTests` | 3 | IsShared (null=shared, set=private), PropertyChanged all fields, defaults |
| `SdFilterStateTests` | 9 | SD filter state model tests |
| `SdModelsTests` | 10 | SD data model tests |
| `SdRequestDirtyTrackingTests` | 7 | Dirty tracking, original value capture, revert |
| `SdRequestTests` | 15 | SD request model tests |
| `ServerModelTests` | 6 | PopulateNicDetails (all/partial/none/clear), default status, PropertyChanged |
| `SwitchInterfaceTests` | 19 | Parse PicOS format, StatusColor, PropertyChanged, MergeOptics (green/red/yellow/null/empty), MergeLldp |
| `SwitchRecordTests` | 38 | UptimeMinutes/Display, LoopbackDisplay, EffectiveSshIp, PingColor/Status/Latency/Icon, SshColor/Status, PropertyChanged cascades, SshPort default |
| `SwitchVersionTests` | 7 | Parse basic output, empty/no colons/MAC variant/L2L3 version, CapturedAt, Windows line endings |
| `TaskBaselineTests` | 8 | Defaults, SetProperties, GanttPredecessorLink (defaults/FinishToStart/StartToFinish/WithLag), SprintBurndownPoint defaults, SprintAllocation PropertyChanged |
| `TaskItemEdgeCaseTests` | 29 | StatusColor/Icon unknown, PriorityIcon/Color unknown, TypeIcon SubTask/Task/Custom, RiskColor all levels, SeverityColor, IsOverdue, ProgressDisplay, DueDateDisplay, PropertyChanged cascades, Children, TaskComment defaults |
| `VlanEntryTests` | 41 | IsBlockRoot, RowColor (blocked/default/root-downgraded), BlockLockedText, SitePrefix, SiteNetwork, SiteGateway, BuildingNumberMap, PropertyChanged cascades |
| `VlanEntryExtendedTests` | 23 | Extended VLAN entry edge cases |
| `AdModelsTests` | 6 | AD user/config model tests |
| `CustomColumnModelsTests` | 16 | Custom column type, config JSON, drop list options, aggregation |
| `DeviceRecordExtendedTests` | 9 | Extended device record edge cases |
| `NetworkLinkExtendedTests` | 19 | Extended link model tests |
| `ReportModelsTests` | 10 | SavedReport, ReportQuery, ReportFilter, DashboardTile models |
| `RoleRecordTests` | 9 | Role record model, priority, permissions |
| `SdFilterStateExtendedTests` | 7 | Extended SD filter state edge cases |
| `SdRequestExtendedTests` | 21 | Extended SD request model tests |
| `SdServiceModelsTests` | 14 | SD service-layer model tests |
| `SprintPlanningModelsTests` | 5 | Sprint planning model edge cases |
| `SwitchRecordExtendedTests` | 15 | Extended switch record edge cases |
| `TaskItemTheoryTests` | 22 | TaskItem theory-based parameterized tests |
| `TimeActivityModelsTests` | 10 | TimeEntry, ActivityFeedItem, TaskView models |
| `ConfigVersionEntryTests` | 12 | Defaults, DisplayDate/Version/Summary, IsSelected PropertyChanged, Id assignment, edge cases |
| `LookupItemTests` | 10 | Defaults, ParentId always null, PropertyChanged on all 6 fields |
| `RoleSiteAccessTests` | 6 | Defaults (Allowed=true), PropertyChanged Building/Allowed, toggle, empty building |
| `ServerASTests` | 7 | Defaults (Active status), PropertyChanged on Id/Building/ServerAsn/Status, status change |
| `SshLogEntryTests` | 18 | Defaults, Duration computed (fractional/null/long/zero), StatusIcon success/failure, PropertyChanged all 13 fields, SwitchId nullable |
| `NotificationPreferenceExtendedTests` | 25 | Defaults, PropertyChanged 5 fields, EventDescription 8 known + unknown types, NotificationEventTypes count, ActiveSession defaults/Duration/StatusColor/ExpiresAt |
| `LocationModelExtendedTests` | 9 | Country/Region/Postcode defaults + AllProperties_FirePropertyChanged, Postcode lat/lon precision/null/negative |
| `IntegrationModelExtendedTests` | 12 | Integration defaults/StatusIcon/StatusText/PropertyChanged/IsEnabled cascades, IntegrationCredential defaults/ExpiresAt, IntegrationLogEntry defaults/SetProperties |
| `AppUserExtendedTests` | 29 | PropertyChanged 16 extended fields, Initials edge cases, IsAdUser/IsSystemUser Theory, nullable datetimes |
| `SdFilterStateFormatTests` | 12 | FormatLabel day/week/month buckets, 14-day boundary, Default() factory, defaults/grid defaults |
| `RoleRecordExtendedTests` | 22 | PropertyChanged 15 fields (11 permission bools, Id, Description, Priority, IsSystem), PermissionSummary various counts, RoleUserDetail, DevicesViewReserved |
| `RibbonConfigExtendedTests` | 9 | RibbonPageConfig/GroupConfig/ItemConfig defaults + full PropertyChanged, UserRibbonOverride defaults/SetProperties/IsHidden |
| `SdRequestExtendedTests2` | 43 | StatusColor all 11 statuses, PriorityColor all priorities, IsClosed, IsOverdue 6 scenarios, dirty tracking 7 scenarios, RowColor, defaults, PropertyChanged cascades |
| `AdModelsExtendedTests` | 9 | Extended AD user/config model edge cases |
| `AppointmentExtendedTests` | 19 | Extended appointment model PropertyChanged, defaults, resource model |
| `ContainerInfoExtendedTests` | 19 | Extended container info StateColor/IsRunning edge cases, PropertyChanged |
| `DashboardDataExtendedTests` | 9 | Extended dashboard data model tests |
| `PanelCustomizationModelsTests` | 5 | Panel customization model defaults, serialization |
| `PanelCustomizationTests2` | 6 | Extended panel customization edge cases |

### J5. Service Tests (284 tests)

| Test Class | Count | Coverage |
|---|---|---|
| `AuditServiceTests` | 7 | No persist func, persist called, broadcast, LogCreate/Delete, JSON, throws |
| `CommandGuardTests` | 7 | Enter/exit, re-entrancy, IsRunning, sync/async Run, independent commands |
| `CommandGuardEdgeCaseTests` | 6 | Exit non-existent, sync/async exception cleanup, skip when running, concurrent attempts, multiple enter/exit cycles |
| `ConfigDiffServiceTests` | 9 | Identical lines, empty old/new/both, one-line replaced, inserted/deleted line alignment, output array length, unchanged lines match |
| `CronEdgeCaseTests` | 7 | Sunday, comma values, first day, specific month, skip months, step, invalid |
| `CronExpressionTests` | 14 | Parse, match, next occurrence, ranges, steps, lists, weekdays |
| `CronExpressionNextOccurrenceTests` | 18 | GetNextOccurrence (every minute, specific time, monthly, null if not found, skip month, weekday filter), Matches (specific/wildcard), Parse (comma/range/step/day-of-week), TryParse, ToString, invalid field count |
| `DataValidationServiceTests` | 12 | Required, min/max length, regex, range, custom, multi-rule, defaults |
| `DataValidationEdgeCaseTests` | 22 | Unregistered type OK, required null/whitespace/with value, min/max length exact, regex valid/invalid, custom pass/fail, range in/below/above, multiple rules, ValidationResult Ok/Fail/ErrorSummary, missing property, additive register, RegisterDefaults, static factories |
| `EmailServiceTests` | 6 | Configure, not configured, dictionary config, send without SMTP |
| `EnvironmentServiceTests` | 10 | TypeColor (live green, test amber, dev blue, unknown grey), TypeLabel mapping, TypeLabel unknown uppercased, defaults |
| `FileManagementServiceTests` | 17 | ComputeMd5 (byte array, different data, stream, stream resets, empty, lowercase hex), ShouldStoreInline (small/exact/large/zero), UseStorageService default, ConfigureStorageService, FileRecord.FileSizeDisplay |
| `GridValidationHelperTests` | 14 | Validate all valid/empty/null/whitespace/multiple/guid/zero int/non-zero/priority zero/sort order zero/missing property, FormatErrors correct/empty |
| `IconOverrideServiceTests` | 13 | Resolve admin/user priority, ResolveColor, ResolveIconName, IsLoaded, case-insensitive, reload |
| `IntegrationServiceTests` | 8 | Constructor sets name, IsConfigured (no creds, with creds, missing refresh, missing clientId), HasValidToken, default properties, GetAccessTokenAsync null |
| `NotificationServiceTests` | 8 | Default toast, suppress none, channels, disabled, preference reload, cap |
| `PlatformTests` | 11 | Singletons exist, permission codes, AuthStates, NotificationEventTypes, PasswordPolicy |
| `PreferenceKeysTests` | 7 | HideReserved/Theme/DockLayout correct values, all keys unique, all non-empty, pref keys start with "pref.", layout keys start with "layout." |
| `SafeAsyncTests` | 4 | Success, exception doesn't crash, re-entrancy, guard release |
| `SettingsExportTests` | 3 | ExportAsync, ExportToFile, ImportFromFile |
| `StartupArgsTests` | 5 | Empty args, --dsn, short flags, long flags, mixed |
| `UndoServiceTests` | 22 | Initial state, property change, undo/redo, batch, merge, discard, clear, RecordAdd/Remove, StateChanged, history |
| `AlertServiceTests` | 7 | Alert service notification routing |
| `ConfigDiffServiceExtendedTests` | 9 | Extended diff algorithm edge cases |
| `CronExpressionExtendedTests` | 16 | Extended cron expression parsing and matching |
| `DeployServiceTests` | 15 | Deployment service configuration and execution |
| `SettingsExportExtendedTests` | 7 | ExportedSettings defaults, ISO timestamp, export with multiple/empty data, round-trip, AppVersion, malformed JSON |

### J6. Shell Tests (64 tests)

| Test Class | Count | Coverage |
|---|---|---|
| `LinkEngineTests` | 8 | Initialize, register/unregister, add/remove rules, clear |
| `MediatorAdvancedTests` | 5 | Subscriber ID, message count, multiple subscribers, filters, logging |
| `MediatorTests` | 11 | Publish, subscribe, filter, unsubscribe, diagnostics, pipeline, async |
| `PanelCustomizationTests` | 6 | GridSettings/FormLayout/LinkRule/FieldGroup defaults, serialization |
| `PanelMessageBusTests` | 7 | Pub/sub with SelectionChanged, NavigateToPanel, DataModified, RefreshPanel |
| `RibbonBuilderTests` | 11 | AddPage, AddGroup, AddButton, AddLargeButton, AddCheckButton, AddToggleButton, AddSeparator, AddSplitButton, group accessors, sort order |
| `WidgetCommandTests` | 16 | WidgetCommandAttribute Name/GroupName/Description/CommandParameter, IsAttribute/TargetsProperty, WidgetCommandData.Apply replacements/edge cases/TextReplacements default |

### J7. Task Tests (95 tests)

| Test Class | Count | Coverage |
|---|---|---|
| `TaskModelsTests` | 42 | TaskItem StatusIcon/PriorityColor/TypeIcon, IsComplete, IsOverdue, ProgressPercent, SeverityColor, PointsDisplay, DateDisplays, TaskProject DisplayName, Sprint DateRange/DisplayName, TaskLink/Dependency display, BoardColumn WIP, CustomColumn JSON, TaskCustomValue display, TimeEntry, ActivityFeedItem, ReportFilter, SavedReport, GanttPredecessorLink |
| `TaskFileParserTests` | 10 | CSV parsing, XML parsing, Excel parsing, extension routing, field extraction |
| `SprintAndPlanningTests` | 21 | SprintAllocation, BurndownPoint, BoardColumn header/WIP/PropertyChanged, BoardLane, TaskBaseline, GanttPredecessorLink, TaskViewConfig, TimeEntry, ActivityFeedItem TimeAgo, ReportQuery, DashboardTile, Dashboard, Portfolio, Programme, ProjectMember, Release |
| `WorkflowActivityTests` | 9 | All 6 custom Elsa activities: UpdateTaskStatus, ValidateTransition, SendNotification, Approval, LogAudit, SetField |
| `TaskRepositoryIntegrationTests` | 13 | CRUD for portfolios, projects, sprints, tasks, board columns, sprint commit, custom columns, time entries, activity feed, saved reports, sprint burndown |

---

## Web App (standalone reference)

- [ ] Dashboard loads at /
- [ ] IPAM grid loads at /ipam (987 devices)
- [ ] Switch detail loads at /switches/{hostname}
- [ ] Config preview loads at /switches/{hostname}/preview
- [ ] Guide loads at /guide
- [ ] Guide detail at /guide/{id} -- connection toggles work (HTMX)
- [ ] Import page at /import -- .txt and .xlsx upload
- [ ] Running configs at /switches/{hostname}/running-configs
- [ ] HTTP Basic Auth (admin/admin default)
- [ ] Ping button on switch detail works
- [ ] Test SSH button works
- [ ] Download Running Config button works

---

Last updated: 2026-03-31
