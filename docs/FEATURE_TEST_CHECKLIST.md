# Central Platform — Feature Test Checklist

Last updated: 2026-04-18
Test suite: 2,616 tests across ~180 test classes on the .NET side. 0 failures (unit + live-DB integration against the podman `central-postgres` container).
Rust networking-engine (`services/networking-engine/`): 229 unit tests, 0 failures.
Build: 0 errors.

Comprehensive test checklist for the Central desktop + API platform, organised by surface
area (not build phase). Every `- [ ]` is a manually testable item. Items with matching
unit/integration tests have the test class and method appended after a dash.

---

## 1. Platform Core

### 1.1 Application Lifecycle

- [ ] App launches without crash (check crash.log)
- [ ] Splash screen appears with progress bar
- [ ] Splash shows "Central vX.X.X -- Initializing..." with assembly version
- [ ] Startup workers execute in order (DB connect, load data, init modules)
- [ ] startup.log includes version, machine name, .NET version
- [ ] XAML layout errors recovered gracefully (toast shown, not crash/hang)
- [ ] Missing resource in module panel shows warning, doesn't deadlock
- [ ] crash.log + startup.log written on errors

#### Startup Health Check
- [ ] StartupHealthCheck.CheckAsync verifies critical DB tables exist at startup
- [ ] 25 required tables checked (app_users, roles, switches, sd_requests, sync_configs, audit_log, etc.)
- [ ] Missing tables reported in startup.log
- [ ] Warnings for empty app_users table
- [ ] DB latency measured and logged
- [ ] Results logged before auth flow begins

#### Auto-Migration on Startup
- [ ] MigrationRunner checks db/migrations/ for pending .sql files on startup
- [ ] Pending migrations applied automatically before health check
- [ ] Count of applied migrations logged to startup.log
- [ ] Splash shows "Applied N database migrations" when migrations run
- [ ] No error if migrations directory doesn't exist

### 1.2 Login Flow

- [ ] Windows auto-login succeeds (matches Windows username to app_users)
- [ ] Windows auto-login populates UserSession.CurrentUser
- [ ] LoginWindow appears when auto-login fails (wrong/missing username)
- [ ] LoginWindow accepts valid username/password
- [ ] LoginWindow rejects invalid credentials with error message
- [ ] LoginWindow shows SSO buttons when identity_providers configured in DB
- [ ] Email-based IdP discovery: enter email, system routes to correct provider
- [ ] Existing Windows auto-login and manual password login still work unchanged
- [ ] SecureString password handling in LoginWindow — `SecureStringExtensionTests.ToSecureString_NonEmpty_CorrectLength`, `SecureStringExtensionTests.ToPlainText_Roundtrips`

### 1.3 Offline Mode

- [ ] Offline mode activates when DB is unreachable (5s timeout)
- [ ] Status bar shows "Offline" when DB is down
- [ ] Auto-reconnect fires after DB comes back (10s retry)
- [ ] Data loads automatically after reconnect
- [ ] ConnectivityManager fires ConnectionChanged event

### 1.4 Command-Line Args

- [ ] `--dsn "Host=..."` overrides database connection string
- [ ] `--auth-method offline` starts in offline mode without login dialog
- [ ] `--auth-method password --user admin --password secret` auto-logs in
- [ ] Password cleared from memory after use (ClearPassword)
- [ ] Empty args returns all nulls — `StartupArgsTests.EmptyArgs_ReturnsAllNulls`
- [ ] --dsn flag parsed correctly — `StartupArgsTests.DsnFlag_Parsed`
- [ ] Short flags (-s, -u, -p, -a) all parsed — `StartupArgsTests.ShortFlags_Parsed`
- [ ] Long flags (--server, --auth-method) all parsed — `StartupArgsTests.LongFlags_Parsed`
- [ ] Mixed short + long flags in same invocation — `StartupArgsTests.MixedFlags_Parsed`

### 1.5 Backstage

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

### 1.6 Exception Handling

- [ ] TaskScheduler.UnobservedTaskException: logged to AppLogger (DB) + crash.log + AuditService + toast
- [ ] DispatcherUnhandledException: logged to AppLogger + crash.log + AuditService + toast + MessageBox for fatal
- [ ] XAML parse errors: recovered gracefully with args.Handled = true + toast
- [ ] Layout overflow: recovered with args.Handled = true
- [ ] All unhandled exceptions appear in: startup.log, crash.log, app_log table, audit_log table, toast notification
- [ ] Admin can see all errors in App Log panel + Audit Log panel

### 1.7 DX Offline Package Cache

- [ ] 78 DevExpress 25.2.5 NuGet packages downloaded to packages-offline/
- [ ] NuGet.config references DevExpress-Offline as local source
- [ ] Enables fully offline development/build

---

## 2. Authentication & Security

### 2.1 RBAC & Permissions

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
- [ ] Role inheritance via parent_role_id resolves effective permissions
- [ ] user_permission_overrides allow per-user grants/denies on top of role
- [ ] `v_user_effective_permissions` view returns correct final permission set
- [ ] 16 AI permission codes registered in PermissionCode.cs and seeded (AiProvidersRead/Admin, AiTenantConfig, AiUse, AiAssistantUse/Admin, AiScoringRead/Train, AiDedupRead/Merge, AiEnrichmentRead/Run, AiChurnRead, AiCallsRead/Admin, AiUsageRead)

#### Site-Level Access
- [ ] role_sites controls which buildings each role sees — `RoleSiteAccessTests.Defaults_AreCorrect`, `RoleSiteAccessTests.AllProperties_FirePropertyChanged`
- [ ] RoleSiteAccess Building/Allowed PropertyChanged — `RoleSiteAccessTests.PropertyChanged_Building_Fires`, `RoleSiteAccessTests.PropertyChanged_Allowed_Fires`
- [ ] RoleSiteAccess Allowed default true, toggle — `RoleSiteAccessTests.SetAllowed_True_AfterFalse`, `RoleSiteAccessTests.Building_CanBeSetToEmptyString`
- [ ] IPAM grid only shows devices from allowed sites
- [ ] Switches grid only shows switches from allowed sites
- [ ] SQL-level filtering (WHERE building = ANY(@sites)) confirmed

#### AuthContext
- [ ] Initial state: not authenticated, null user, NotAuthenticated state — `AuthContextTests.InitialState_NotAuthenticated`
- [ ] SetSession: sets user, permissions, sites, auth state — `AuthContextTests.SetSession_SetsAll`
- [ ] HasPermission: granted permissions work, ungranted denied — `AuthContextTests.HasPermission_Granted`, `AuthContextExtendedTests.HasPermission_True_WhenGranted`, `AuthContextExtendedTests.HasPermission_False_WhenNotGranted`, `AuthContextExtendedTests.HasPermission_CaseInsensitive`
- [ ] SuperAdmin (priority 1000): always has all permissions — `AuthContextTests.SuperAdmin_AllPermissions`, `AuthContextExtendedTests.IsSuperAdmin_True_WhenPriorityGTE1000`, `AuthContextExtendedTests.HasPermission_True_ForSuperAdmin_EvenWithoutGrant`
- [ ] Site access: no restrictions = all sites; restricted = only listed — `AuthContextTests.SiteAccess_Restricted`, `AuthContextExtendedTests.HasSiteAccess_True_WhenNoRestrictions`, `AuthContextExtendedTests.HasSiteAccess_True_WhenSiteInList`, `AuthContextExtendedTests.HasSiteAccess_False_WhenSiteNotInList`, `AuthContextExtendedTests.HasSiteAccess_True_ForSuperAdmin`
- [ ] SetOfflineAdmin: full permissions, offline state — `AuthContextTests.SetOfflineAdmin_FullPermissions`, `AuthContextExtendedTests.SetOfflineAdmin_SetsOfflineState`
- [ ] Logout: clears everything — `AuthContextTests.Logout_ClearsAll`, `AuthContextExtendedTests.Logout_ResetsState`
- [ ] UpdateAllowedSites: changes site access live — `AuthContextTests.UpdateAllowedSites`, `AuthContextExtendedTests.UpdateAllowedSites_ChangesSiteAccess`
- [ ] HasAnyPermission: true if any one matches — `AuthContextTests.HasAnyPermission`, `AuthContextExtendedTests.HasAnyPermission_True_WhenOneMatches`, `AuthContextExtendedTests.HasAnyPermission_False_WhenNoneMatch`
- [ ] PermissionCount: reflects granted count — `AuthContextTests.PermissionCount`, `AuthContextExtendedTests.PermissionCount_ReflectsGranted`, `AuthContextExtendedTests.PermissionCount_Zero_AfterLogout`
- [ ] IsAuthenticated state tracking — `AuthContextExtendedTests.IsAuthenticated_False_WhenNotAuthenticated`, `AuthContextExtendedTests.IsAuthenticated_True_AfterSetSession`
- [ ] IsSuperAdmin boundary (below 1000 = false) — `AuthContextExtendedTests.IsSuperAdmin_False_WhenPriorityBelow1000`
- [ ] CanView/CanEdit/CanDelete legacy mapping — `AuthContextExtendedTests.CanView_MapsToReadPermission`, `AuthContextExtendedTests.CanEdit_MapsToWritePermission`, `AuthContextExtendedTests.CanDelete_MapsToDeletePermission`
- [ ] CanViewReserved permission — `AuthContextExtendedTests.CanViewReserved_True_WhenGranted`, `AuthContextExtendedTests.CanViewReserved_False_WhenNotGranted`
- [ ] IsAdmin (super admin or Admin role) — `AuthContextExtendedTests.IsAdmin_True_WhenSuperAdmin`, `AuthContextExtendedTests.IsAdmin_True_WhenAdminRole`, `AuthContextExtendedTests.IsAdmin_False_WhenNeitherSuperAdminNorAdminRole`
- [ ] PermissionsChanged event fires on SetSession/Logout — `AuthContextExtendedTests.PermissionsChanged_FiresOnSetSession`, `AuthContextExtendedTests.PermissionsChanged_FiresOnLogout`
- [ ] PropertyChanged fires on AuthState change — `AuthContextExtendedTests.PropertyChanged_FiresOnAuthStateChange`
- [ ] All 9 AuthStates enum values present — `AuthContextTests.AuthStates_AllValues`, `AuthStatesTests.AllStates_AreDefined`
- [ ] AuthStates ordinal values: NotAuthenticated=0, Windows=1, Offline=2, Password=3, EntraId=4, Okta=5, Saml=6, Local=7, ApiToken=8 — `AuthStatesTests` (9 facts), `AuthStatesExtendedTests` (18 tests — individual ordinal checks + AllStates_Defined + AuthenticatedStates_AreNotZero)

#### Auth Framework Models
- [ ] AuthResult, UserTypes, AuthStates, SecureString, IdentityProviderConfig, ClaimMapping, AppUser — `AuthFrameworkTests` (24 tests), `AppUserTests` (21 tests), `AuthUserTests` (4 tests)
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

#### User Types
- [ ] UserTypes.All has 5 entries — `UserTypesTests.All_Has5`
- [ ] IsProtected: System and Service only — `UserTypesTests.IsProtected_SystemService`
- [ ] IsProtected: Standard, ActiveDirectory, Admin, null, empty = false — `UserTypesTests.IsProtected_NotProtected`
- [ ] AppUser.Initials: two-word name = first letters — `UserTypesTests.Initials_TwoWord`
- [ ] AppUser.Initials: single word = first 2 chars — `UserTypesTests.Initials_SingleWord`
- [ ] AppUser.Initials: from username when DisplayName empty — `UserTypesTests.Initials_FromUsername`
- [ ] AppUser.StatusText: Active/Inactive — `UserTypesTests.StatusText`
- [ ] AppUser.StatusColor: green/grey — `UserTypesTests.StatusColor`

### 2.2 Password Hashing, Policy, Lockout, Expiry

#### Argon2id Hashing
- [ ] Generate salt produces unique salts — `PasswordHasherTests.GenerateSalt_Unique`
- [ ] Hash consistency: same input + salt = same hash — `PasswordHasherTests.Hash_Consistency`
- [ ] Different salts produce different hashes — `PasswordHasherTests.DifferentSalts_DifferentHashes`
- [ ] Verify correct password — `PasswordHasherTests.Verify_CorrectPassword`
- [ ] Verify wrong password — `PasswordHasherTests.Verify_WrongPassword`
- [ ] Empty password handled — `PasswordHasherTests.EmptyPassword_Handled`
- [ ] Set Password dialog (Argon2id + salt) works from backstage
- [ ] Legacy SHA256 hashes auto-migrated to Argon2id on successful login

#### Password Policy
- [ ] Default policy: min 8, uppercase, lowercase, digit, special, 90-day expiry — `PasswordPolicyTests.DefaultPolicy_Reasonable`
- [ ] Relaxed policy: min 4, no complexity, no expiry — `PasswordPolicyTests.RelaxedPolicy_Permissive`
- [ ] Validate rejects: too short, no uppercase, no lowercase, no digit, no special — `PasswordPolicyTests.Validate_Rejects_*`
- [ ] Validate accepts strong passwords — `PasswordPolicyTests.Validate_AcceptsStrong`
- [ ] Validate exactly min length passes — `PasswordPolicyExtendedTests.Validate_ExactlyMinLength_Passes`
- [ ] Validate one less than min length fails — `PasswordPolicyExtendedTests.Validate_OneLessThanMinLength_Fails`
- [ ] Validate exactly max length passes — `PasswordPolicyExtendedTests.Validate_ExactlyMaxLength_Passes`
- [ ] Validate over max length fails — `PasswordPolicyExtendedTests.Validate_OverMaxLength_Fails`
- [ ] Validate various special characters — `PasswordPolicyExtendedTests.Validate_VariousSpecialChars`
- [ ] Validate unicode password — `PasswordPolicyExtendedTests.Validate_UnicodePassword_WithAllRequirements_Passes`
- [ ] Password history: blocks reuse of last N passwords — `PasswordPolicyTests.PasswordHistory_BlocksReuse`
- [ ] Password history no salt skips check — `PasswordPolicyExtendedTests.Validate_PasswordHistory_NoSalt_SkipsCheck`
- [ ] Password history zero count skips check — `PasswordPolicyExtendedTests.Validate_PasswordHistory_ZeroCount_SkipsCheck`
- [ ] Multiple validation errors all reported in one result — `PasswordPolicyTests.MultipleErrors`
- [ ] ErrorSummary multiple errors joined by semicolon — `PasswordPolicyExtendedTests.ErrorSummary_MultipleErrors_JoinedBySemicolon`
- [ ] ErrorSummary no errors empty — `PasswordPolicyExtendedTests.ErrorSummary_NoErrors_Empty`
- [ ] Description property shows human-readable policy summary — `PasswordPolicyTests.Description`
- [ ] Description no expiry omits expiry text — `PasswordPolicyExtendedTests.Description_NoExpiry_OmitsExpiryText`
- [ ] Description relaxed policy minimal — `PasswordPolicyExtendedTests.Description_RelaxedPolicy_Minimal`
- [ ] Policy description shown below user label in SetPasswordWindow
- [ ] Password validated against PasswordPolicy.Default before save
- [ ] Validation errors shown in red (all errors at once)
- [ ] Password history checked against last 5 hashes in password_history table
- [ ] password_changed_at updated on app_users after password change
- [ ] New hash saved to password_history for future reuse prevention
- [ ] Audit log entry created on password change

#### Expiry & Min-Age
- [ ] IsExpired: returns true when password_changed_at > ExpiryDays ago — `PasswordPolicyTests.IsExpired`
- [ ] IsExpired exactly on boundary not expired — `PasswordPolicyExtendedTests.IsExpired_ExactlyOnBoundary_NotExpired`
- [ ] IsExpired just past boundary expired — `PasswordPolicyExtendedTests.IsExpired_JustPastBoundary_Expired`
- [ ] IsExpired null date not expired — `PasswordPolicyExtendedTests.IsExpired_NullDate_NotExpired`
- [ ] IsTooRecent: blocks change if password changed < MinAgeDays ago — `PasswordPolicyTests.IsTooRecent`
- [ ] IsTooRecent exactly min age too recent — `PasswordPolicyExtendedTests.IsTooRecent_ExactlyMinAge_TooRecent`
- [ ] IsTooRecent past min age allowed — `PasswordPolicyExtendedTests.IsTooRecent_PastMinAge_Allowed`
- [ ] IsTooRecent zero min age never blocked — `PasswordPolicyExtendedTests.IsTooRecent_ZeroMinAge_NeverBlocked`
- [ ] IsTooRecent null date allowed — `PasswordPolicyExtendedTests.IsTooRecent_NullDate_Allowed`

#### Account Lockout
- [ ] Account lockout activates after 5 failed attempts
- [ ] Account lockout expires after 15 minutes
- [ ] Brute-force lockout: 5 failed password attempts locks for 30 minutes

### 2.3 Credential Encryption (AES-256)

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

### 2.4 TOTP MFA

- [ ] GenerateSecret returns valid Base32 secret — `TotpServiceTests.GenerateSecret_Valid`
- [ ] GenerateQrUri produces otpauth:// URI — `TotpServiceTests.GenerateQrUri_Valid`
- [ ] GenerateCurrentCode returns 6-digit code — `TotpServiceTests.GenerateCurrentCode`
- [ ] VerifyCode validates current TOTP within +/-1 time step — `TotpServiceTests.VerifyCode_Valid`
- [ ] VerifyCode rejects wrong codes — `TotpServiceTests.VerifyCode_RejectsWrong`
- [ ] GenerateRecoveryCodes returns 8 unique hyphenated hex codes — `TotpServiceTests.GenerateRecoveryCodes`
- [ ] Recovery codes stored hashed in mfa_recovery_codes table
- [ ] VerifyRecoveryCodeAsync marks code as used (single-use)
- [ ] EnableMfaAsync sets mfa_enabled=true and stores encrypted secret
- [ ] DisableMfaAsync clears secret and recovery codes

#### MFA Enrollment Dialog
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

### 2.5 Identity Providers (SSO/OIDC/SAML/Entra/Okta/Duo/Social)

- [ ] IdentityProviderConfig model — `IdentityConfigTests.IdentityProviderConfig_*`
- [ ] ClaimMapping model — `IdentityConfigTests.ClaimMapping_*`
- [ ] DomainMapping model — `IdentityConfigTests.DomainMapping_*`
- [ ] ExternalIdentity model — `IdentityConfigTests.ExternalIdentity_*`
- [ ] AuthEvent model — `IdentityConfigTests.AuthEvent_*`
- [ ] AuthResult claims — `IdentityConfigTests.AuthResult_Claims`
- [ ] AuthRequest model — `IdentityConfigTests.AuthRequest_*`
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
- [ ] SecureString empty string roundtrip — `SecureStringExtensionTests.ToSecureString_EmptyString_ReturnsSecureString`, `SecureStringExtensionTests.ToPlainText_Empty_ReturnsEmpty`
- [ ] SecureString is read-only after creation — `SecureStringExtensionTests.ToSecureString_IsReadOnly`
- [ ] SecureString password hash consistency — `SecureStringExtensionTests.ToPasswordHash_ProducesConsistentHash`
- [ ] SecureString different salts produce different hashes — `SecureStringExtensionTests.ToPasswordHash_DifferentSalts_DifferentHashes`
- [ ] SecureString verify hash correct/wrong — `SecureStringExtensionTests.VerifyHash_CorrectPassword_ReturnsTrue`, `SecureStringExtensionTests.VerifyHash_WrongPassword_ReturnsFalse`
- [ ] SecureString unicode chars in hash — `SecureStringExtensionTests.ToPasswordHash_UnicodeChars`
- [ ] Social login (Google/Microsoft/GitHub) via `social_providers` seeded + `/api/security/social-providers`
- [ ] OAuth state parameter validated against `oauth_states` table (CSRF prevention)
- [ ] user_social_logins binds external subject to app_users on first sign-in

### 2.6 API Key Authentication

- [ ] X-API-Key header checked by middleware before JWT
- [ ] Key validated against api_keys table (SHA256 hash — raw key never stored)
- [ ] Per-key salt used (migration 035_api_key_salt)
- [ ] Valid key sets ClaimsPrincipal with name, role, auth_method=api_key
- [ ] last_used_at and use_count updated on each use
- [ ] Falls through to JWT auth if no X-API-Key header

#### API Key Management Panel
- [ ] API Keys panel opens from Admin > Identity > API Keys
- [ ] Generate Key button prompts for name, creates key, shows raw key once
- [ ] Raw key copied to clipboard automatically
- [ ] Grid shows name, role, active, uses, last used, created, expires
- [ ] Revoke button sets is_active=false without deleting
- [ ] Delete button removes key permanently with confirmation
- [ ] Audit log entry on key create and revoke
- [ ] ApiKeyRecord defaults, PropertyChanged all fields, RawKey set on create — `ApiKeyRecordTests` (3 tests)

### 2.7 Active Sessions

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
- [ ] ActiveSession defaults, Duration formats, StatusColor, ExpiresAt nullable — `NotificationPreferenceExtendedTests.ActiveSession_*`

### 2.8 Audit Trail

#### Structured Audit Service
- [ ] AuditService.Instance logs all CRUD operations
- [ ] No persist func: does not throw — `AuditServiceTests.NoPersistFunc_NoThrow`
- [ ] Persist func called with correct entry — `AuditServiceTests.PersistFunc_CalledCorrectly`
- [ ] Broadcast func called with action — `AuditServiceTests.BroadcastFunc_Called`
- [ ] LogCreateAsync sets Create action — `AuditServiceTests.LogCreate_SetsAction`
- [ ] LogDeleteAsync sets Delete action — `AuditServiceTests.LogDelete_SetsAction`
- [ ] Before/After JSON serialized correctly — `AuditServiceTests.BeforeAfterJson_Serialized`
- [ ] Persist throws: does not crash — `AuditServiceTests.PersistThrows_NoCrash`
- [ ] LogUpdateAsync records before/after snapshots as JSONB — `EntityBaseTests.TakeSnapshot_CapturesAllProperties`
- [ ] LogViewAsync records data access
- [ ] LogExportAsync records data exports
- [ ] LogLoginAsync records login attempts
- [ ] LogSettingChangeAsync records old/new setting values
- [ ] audit_log table with before_json/after_json columns (migration 052)
- [ ] GetAuditLogAsync supports filtering by entity_type and username
- [ ] SetPersistFunc wires to DbRepository at startup
- [ ] Audit logging never blocks the primary operation (try/catch)

#### Wiring
- [ ] AuditService initialized at startup with DbRepository persistence
- [ ] Device save logs Create/Update with device name
- [ ] Device delete logs Delete with device ID and name
- [ ] User delete logs Delete with user ID and username
- [ ] CSV export logs Export with panel name and file path
- [ ] Password change logs PasswordChange with user ID

#### Broadcasting via SignalR
- [ ] AuditService.SetBroadcastFunc wires SignalR broadcasting
- [ ] Every audit log entry triggers real-time broadcast to all connected clients
- [ ] Broadcast includes: action, entityType, entityName, username
- [ ] Broadcasting never blocks the primary operation (try/catch)

#### Audit Log Viewer Panel
- [ ] Audit Log panel opens from Admin > Identity > Audit Log
- [ ] Grid shows timestamp, action, entity type, entity ID/name, user, details, before/after JSON
- [ ] Entity type dropdown filter (Device, Switch, User, Setting)
- [ ] Username text filter
- [ ] Refresh button reloads with current filters
- [ ] Filter change triggers auto-refresh
- [ ] Sorted by timestamp descending (newest first)

### 2.9 Security Headers Middleware

- [ ] X-Frame-Options: DENY (prevents clickjacking)
- [ ] X-Content-Type-Options: nosniff (prevents MIME sniffing)
- [ ] X-XSS-Protection: 1; mode=block
- [ ] Referrer-Policy: strict-origin-when-cross-origin
- [ ] Content-Security-Policy: default-src 'none'; frame-ancestors 'none'
- [ ] Permissions-Policy: camera=(), microphone=(), geolocation=()
- [ ] Cache-Control: no-store, no-cache, must-revalidate (default for API)

### 2.10 IP Allowlist, SSH Keys, Domain Verification, ToS

- [ ] ip_access_rules table + /api/security/ip-rules CRUD (allow/deny by CIDR)
- [ ] Denied IP returns 403 before authentication check
- [ ] user_ssh_keys table + /api/user-keys CRUD; fingerprint auto-computed
- [ ] domain_verifications: dns_txt / http_file / email methods
- [ ] terms_of_service table + user_tos_acceptance tracks version accepted
- [ ] deprovisioning_rules + deprovisioning_log auto-revoke access on conditions

---

## 3. UI Framework

### 3.1 Ribbon

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

#### Ribbon Tabs
- [ ] Home: Connection, Export, Web App, Layout (Save/Restore Default via cog)
- [ ] Devices: Actions (New/Edit/Delete), Group By, Filter, Panels (IPAM/Switches/Details), Connectivity (Sync BGP/Sync All BGP)
- [ ] Switches: Actions, Connectivity (Ping All/Ping Selected), Panels, Data
- [ ] Admin: Actions (New/Edit/Delete — routes by active panel), Panels (Roles/Users/Lookups/Details/Ribbon Config/Ribbon Admin), Data
- [ ] Tasks: permanent top-level ribbon tab with 15 check buttons
- [ ] CRM: tab (SortOrder 40) with Actions + Data + Panels groups (9 panel toggles)

#### Context Tabs
- [ ] Links tab (blue) appears when Links panel is active
- [ ] Switch tab (green) appears when Switches panel is active
- [ ] Admin tab (amber) appears when Admin panel is active
- [ ] Routing / VLANs / Tasks context tabs appear/disappear by active panel
- [ ] Context tabs hide when switching to unrelated panel

#### Quick Access Toolbar
- [ ] Save/Refresh/Undo buttons appear in QAT
- [ ] QAT buttons are functional
- [ ] QAT is user-customizable (right-click add/remove)

#### Keyboard Shortcuts
- [ ] Ctrl+R / F5 — Refresh all data
- [ ] Ctrl+N — New record (routes by active panel)
- [ ] Delete — Delete selected record
- [ ] Ctrl+E — Export devices
- [ ] Ctrl+P — Print preview
- [ ] Ctrl+F — Toggle global search
- [ ] Ctrl+S — Save/commit current row
- [ ] Ctrl+D — Toggle details panel
- [ ] Ctrl+G — Go to dialog
- [ ] Ctrl+I — Import wizard
- [ ] Ctrl+Z — Undo
- [ ] Ctrl+Y — Redo
- [ ] Ctrl+Tab — Cycle to next panel
- [ ] F1 — Keyboard help

### 3.2 Ribbon Customization (3-layer override)

| Layer | Table | Priority | Who |
|---|---|---|---|
| 1 | `admin_ribbon_defaults` | Lowest | Admin pushes defaults for all users |
| 2 | `ribbon_items` (DB seed) | Middle | Module registration / system defaults |
| 3 | `user_ribbon_overrides` | Highest | Per-user icon, text, visibility |

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
- [ ] RibbonPageConfig PropertyChanged Header/IsVisible — `RibbonConfigTests.RibbonPageConfig_PropertyChanged_Header`, `RibbonConfigTests.RibbonPageConfig_PropertyChanged_IsVisible`
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

### 3.3 Icon System

#### Icon Library
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

#### ImagePickerWindow
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

#### SVG Rendering
- [ ] SvgHelper.RenderSvgToImageSource renders SVG to BitmapImage
- [ ] currentColor replaced with #FFFFFF for dark theme
- [ ] In-memory cache (hash-keyed) prevents re-rendering
- [ ] Disk cache writes to %LocalAppData%/Central/icon_cache/
- [ ] LoadFromDiskCache reads cached SVG files

#### DevExpress SVG Gallery
- [ ] 34 DX categories available via `dx:DXImage` markup
- [ ] `DxSvgGallery.GetSvgImage("Actions", "Open2")` returns SvgImage
- [ ] Icons auto-adapt to active theme (Win11Dark, Office2019Colorful, etc.)

### 3.4 Themes

- [ ] Theme gallery shows 9 installed themes
- [ ] Theme changes apply immediately
- [ ] Theme persists across restarts

---

## 4. Grid Framework

### 4.1 Inline Editing, Context Menu, Saved Filters

#### Inline Editing
- [ ] Inline editing works (NavigationStyle=Cell)
- [ ] Dropdown columns wired via BindComboSources()
- [ ] ValidateRow auto-saves on row commit — `GridValidationHelperTests` (14 tests)
- [ ] ShownEditor event wired in code-behind constructor
- [ ] Natural numeric sort on interface columns

#### Context Menu (Global)
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

#### Saved Filters
- [ ] Save current filter with name
- [ ] Load saved filter applies expression
- [ ] Delete saved filter removes it
- [ ] Filters are per-user per-panel
- [ ] Default saved filter auto-applies on panel load
- [ ] Quick filter presets appear in column right-click menu
- [ ] SavedFilter IsShared, PropertyChanged — `SavedFilterTests` (3 tests)

### 4.2 Export & Print

- [ ] Export to Clipboard works from context menu
- [ ] Export to Clipboard works from ribbon button
- [ ] Print Preview opens for active grid
- [ ] Column Chooser opens for active grid
- [ ] CSV export via DX TableView.ExportToCsv()
- [ ] Excel export via DX XlsxExportOptions on all grids
- [ ] PDF export via DX PdfExportOptions on all grids
- [ ] Default filename includes panel name + date (e.g. Devices_20260417.csv)
- [ ] Toast notification on successful export or error

### 4.3 Home Ribbon View Toggles

- [ ] Search Panel toggle shows/hides search
- [ ] Filter Row toggle shows/hides auto-filter row
- [ ] Group Panel toggle shows/hides group area
- [ ] Grid Lines toggle shows/hides cell borders
- [ ] Best Fit auto-sizes columns

### 4.4 Undo/Redo + Bulk Edit

#### Undo/Redo
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

#### Bulk Edit
- [ ] BulkEditWindow opens from context menu
- [ ] Field picker shows all editable fields (reflection-based)
- [ ] Preview shows changes before applying
- [ ] Apply updates all selected rows
- [ ] Works with any model type

### 4.5 Toast Notifications

- [ ] Info toast (blue) auto-hides after 4s — `NotificationTypeTests.Notification_Color_ByType(Info, "#3B82F6")`
- [ ] Success toast (green) auto-hides after 4s — `NotificationTypeTests.Notification_Color_ByType(Success, "#22C55E")`
- [ ] Warning toast (amber) auto-hides after 4s — `NotificationTypeTests.Notification_Color_ByType(Warning, "#F59E0B")`
- [ ] Error toast (red) auto-hides after 4s — `NotificationTypeTests.Notification_Color_ByType(Error, "#EF4444")`
- [ ] Toast appears bottom-right
- [ ] SignalR DataChanged triggers toast for external changes
- [ ] Notification icon per type (Info/Success/Warning/Error) — `NotificationTypeTests.Notification_Icon_*`
- [ ] Notification timestamp is recent — `NotificationTypeTests.Notification_Timestamp_IsRecent`
- [ ] Notification properties (type, title, message, source) — `NotificationTypeTests.Notification_Properties`
- [ ] Notification null source handled — `NotificationTypeTests.Notification_NullSource`

### 4.6 Layout Persistence + Panel Floating

#### Layout Persistence
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

#### Panel Floating / Multi-Monitor
- [ ] Any panel tab can be dragged out to a separate window
- [ ] Floating panels are real OS windows (FloatingMode.Desktop)
- [ ] Floating panels can be moved to second monitor and maximized
- [ ] Drag floating panel back to dock it into the main window
- [ ] Right-click tab > "Float" works as alternative to dragging
- [ ] EnableGlobalFloating covers all panels including closed ones
- [ ] Layout save/restore preserves floating panel positions

### 4.7 Grid Customizer

- [ ] GridCustomizerDialog with row height, alternating rows, summary footer, group panel, auto-filter
- [ ] panel_customizations table stores per-user per-panel settings as JSONB — `PanelCustomizationExtendedTests.PanelCustomizationRecord_SetValues`
- [ ] Settings: grid, filter, form, link types — `PanelCustomizationExtendedTests.GridSettings_SetLists_WorkCorrectly`, `PanelCustomizationExtendedTests.FormLayout_WithGroups`, `PanelCustomizationExtendedTests.FieldGroup_Collapsed`, `PanelCustomizationExtendedTests.LinkRule_WithValues`
- [ ] Customizer wired to all 22 grids across all modules
- [ ] GridSettings defaults — `PanelCustomizationTests.GridSettings_Defaults`, `PanelCustomizationModelsTests` (5 tests)
- [ ] FormLayout defaults — `PanelCustomizationTests.FormLayout_Defaults`
- [ ] LinkRule defaults — `PanelCustomizationTests.LinkRule_Defaults`
- [ ] FieldGroup defaults — `PanelCustomizationTests.FieldGroup_Defaults`
- [ ] GridSettings serialization round-trip — `PanelCustomizationTests.GridSettings_RoundTrip`
- [ ] LinkRule list serialization round-trip — `PanelCustomizationTests.LinkRule_ListRoundTrip`
- [ ] Panel customization extended edge cases — `PanelCustomizationTests2` (6 tests)

### 4.8 Detail Panel + Config Compare

#### Detail Panel (Asset Details)
- [ ] Right-docked detail panel visible
- [ ] Updates on grid row selection (SelectionChanged)
- [ ] Shows correct detail fields for selected entity

#### Config Compare Panel
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

### 4.9 Import Wizard

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

### 4.10 Home Dashboard Panel

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

### 4.11 Cross-Panel Features

- [ ] Save device triggers links panel refresh (DataModifiedMessage)
- [ ] Save switch triggers related panels refresh
- [ ] Delete entity triggers dependent panels refresh
- [ ] Go to Switch A/B from Links context menu
- [ ] Go to Device from Switches context menu
- [ ] Navigation activates target panel + selects row

---

## 5. Data Layer

### 5.1 Database Connection + ConnectivityManager

- [ ] DSN from CENTRAL_DSN env var works
- [ ] Fallback DSN (localhost defaults) works
- [ ] 5s connection timeout
- [ ] 10s background retry on failure
- [ ] pg_notify triggers fire on all 19+ tables
- [ ] pg_notify triggers on all new tables (migration 048)
- [ ] ConnectivityManager handles DB connection with offline mode
- [ ] ConnectionChanged event fires on state transitions

### 5.2 Migrations

See the Migrations Reference table at the bottom of this document for the full list of all 80 migrations.

- [ ] All migrations apply cleanly on fresh DB
- [ ] Migrations are idempotent (re-run safe)
- [ ] PowerShell setup script: db\setup.ps1 applies all migrations via psql
- [ ] MigrationRunner applies pending migrations at startup (see 1.1)
- [ ] migration_history table records applied files + timestamp + duration

### 5.3 Multi-Tenancy (RLS, zoned vs dedicated, connection resolver)

#### Row-Level Security
- [x] tenant_id UUID column on ALL public-schema tables (40+ tables)
- [x] Default tenant UUID for all existing rows
- [x] PostgreSQL RLS policies filtering by `current_setting('app.tenant_id')` — `get_current_tenant_id()` function
- [x] Policies enforced at DB level (FORCE ROW LEVEL SECURITY)
- [x] Separate policies for SELECT/INSERT/UPDATE/DELETE operations
- [x] Admin role bypass via `is_super_admin()` session variable
- [x] set_tenant_context(uuid) function for DbRepository
- [x] set_default_tenant() convenience function
- [x] _add_tenant_rls() helper for future migrations
- [x] Indexes on tenant_id columns

#### Connection Pooling (PgBouncer)
- [x] PgBouncer transaction mode — `pool_mode = transaction`
- [x] Per-tier pool sizing: Free=2, Normal=20, Pro=50, Enterprise=100
- [x] 30s connection/query wait timeouts
- [x] Queue depth monitoring at 80% (PgBouncer exporter + PgBouncerHighQueueDepth alert)

#### Tenant Sizing Model (Normal vs Enterprise)
- [x] Tenant sizing column (`zoned` vs `dedicated`) in `central_platform.tenants`
- [x] Auto-upgrade trigger on tier='enterprise' queues `provision_dedicated` job
- [x] Rust `tenant-provisioner` service: pg_dump → CREATE DATABASE → restore → migrate → create K8s namespace
- [x] `TenantConnectionResolver` (.NET) with 3-tier lookup (connection map → DNS → fallback)
- [x] K8s tenant-template/ overlay for enterprise namespaces with NetworkPolicy + ResourceQuota

#### Citus Sharding
- [x] Auto-detect at 50TB / 1M users via `tenant_shard_config` + `evaluate_sharding_thresholds()` + alerts
- [x] Shard key `tenant_id` for deterministic routing
- [x] Citus extension (PG equivalent of Vitess) for sharding + query routing
- [x] Online shard rebalancing (Citus native)
- [x] Cross-shard queries via Citus router + reference tables

### 5.4 Real-Time Notifications (pg_notify)

- [ ] SignalR DataChanged handler covers: identity_providers, appointments, countries, regions, reference_config, backup_history, icon_defaults, sd_technicians, sd_groups, sd_teams
- [ ] Panel loaded flags reset on DataChanged
- [ ] Toast notifications shown for multi-user changes
- [ ] SignalR DataChanged handlers for 13 task-related tables
- [ ] `broadcast_record_change` trigger on 7 CRM tables publishes pg_notify events consumed by Elsa dispatcher

### 5.5 TimescaleDB Hypertables + Citus Sharding

- [x] TimescaleDB on audit_log, activity_feed, auth_events, team_activity, tenant_usage_metrics
- [x] Continuous aggregates: tenant_usage_hourly, auth_events_daily, activity_feed_hourly
- [x] Compression with tenant_id segment + 14/30/7-day policies (70% savings)
- [x] Logical replication via `wal_level=logical` + `central_replication_data` publication
- [x] Citus for horizontal scaling on high-cardinality tenants

### 5.6 Admin Models + First-Time Setup

- [ ] ReferenceConfig, ContainerInfo, MigrationRecord, BackupRecord, GridSettings, Location, Appointment — `AdminModelsTests` (20 tests) + `ReferenceConfigTests` (10 tests) + `MigrationRecordTests` (5 tests)
- [ ] ContainerInfo StateColor, IsRunning, PropertyChanged — `ContainerInfoTests` (3 tests)
- [ ] BackupRecord FileSizeDisplay, StatusColor — `BackupRecordTests` (2 tests)
- [ ] Location models PropertyChanged — `LocationModelTests` (4 tests)
- [ ] Appointment models defaults + PropertyChanged — `AppointmentModelTests` (4 tests)
- [ ] Start pod: podman play kube infra/pod.yaml
- [ ] Apply migrations: ./db/setup.sh or auto-apply on app startup
- [ ] Default admin: admin/admin (System user, cannot be deleted)
- [ ] Default roles: Admin (100), Operator (50), Viewer (10)
- [ ] Default lookups: status, device_type, building
- [ ] Default notification preferences seeded for admin user

---

## 6. API Server (Central.Api)

### 6.1 REST Endpoints

All endpoints return RFC 7807 problem+json on errors. All write endpoints require
`RequireAuthorization()` (JWT Bearer) unless explicitly marked anonymous.

#### ActivityEndpoints (`/api/activity`, auth required)
- [ ] GET /api/activity/global — combined audit log + auth events (admin only)
- [ ] GET /api/activity/me — current user's personal activity timeline
- [ ] Activity feed combines `audit_log` + `auth_events`
- [ ] Filterable by entity type + date range

#### AdminEndpoints (`/api/admin`, auth required)
- [ ] GET /api/admin/users returns user list
- [ ] PUT /api/admin/users upserts a user (Argon2id hash, user_type, active flag)
- [ ] DELETE /api/admin/users/{id} soft-deletes user (not protected system users)
- [ ] POST /api/admin/users/{id}/reset-password invalidates and re-hashes
- [ ] GET/PUT/DELETE /api/admin/roles CRUD
- [ ] GET /api/admin/lookups + PUT/DELETE for lookup_values
- [ ] Permissions CRUD (role_permission_grants)

#### ApiKeyEndpoints (`/api/keys`, auth required, `admin:users`)
- [ ] GET /api/keys — list all API keys
- [ ] POST /api/keys/generate — create key, returns raw key ONCE (never again)
- [ ] POST /api/keys/{id}/revoke — soft-disable key (is_active=false)
- [ ] DELETE /api/keys/{id} — hard-delete key
- [ ] Audit log entry on create/revoke/delete

#### AppointmentEndpoints (`/api/appointments`, auth required)
- [ ] GET /api/appointments with date range filter + resources
- [ ] POST /api/appointments creates appointment + resources in transaction
- [ ] PUT /api/appointments/{id}, DELETE /api/appointments/{id}
- [ ] Links to tasks (task_id) and SD tickets (ticket_id)

#### AuditEndpoints (`/api/audit`, auth required)
- [ ] GET /api/audit returns audit entries
- [ ] Filterable by entityType and username
- [ ] Cursor pagination
- [ ] Admin-only access (admin:users permission)

#### BackupEndpoints (`/api/backup`, auth required, `admin:backup`)
- [ ] POST /api/backup/run — trigger pg_dump backup
- [ ] GET /api/backup/history — backup history list
- [ ] GET /api/backup/tables — list all DB tables
- [ ] GET /api/backup/migrations — migration history
- [ ] GET /api/backup/purge-counts — soft-deleted record counts
- [ ] DELETE /api/backup/purge/{table} — purge soft-deleted records

#### BgpEndpoints (`/api/bgp`, auth required)
- [ ] GET /api/bgp returns BGP config list
- [ ] POST /api/bgp upsert, DELETE /api/bgp/{id}
- [ ] GET /api/bgp/{id}/neighbors, GET /api/bgp/{id}/networks
- [ ] Pagination + filter by switch_id

#### BillingEndpoints (`/api/billing`, auth required)
- [ ] /api/billing/addons CRUD (tenant add-ons)
- [ ] /api/billing/discounts apply discount codes
- [ ] /api/billing/payment-methods (card/bank/po)
- [ ] /api/billing/quotas — usage quotas and overage actions
- [ ] /api/billing/proration — proration events on plan change
- [ ] /api/billing/invoices — invoice list + detail

#### ClientLogEndpoints (`/api/log/client`, anonymous)
- [ ] POST /api/log/client accepts client-side log entries
- [ ] Sanitises/rate-limits incoming entries
- [ ] Writes to app_log table

#### CompanyEndpoints (`/api/companies`, auth required)
- [ ] GET /api/companies (list + search + filter)
- [ ] POST /api/companies, PUT /api/companies/{id}, DELETE /api/companies/{id}
- [ ] GET /api/companies/{id}/contacts — sub-route
- [ ] GET /api/companies/{id}/addresses — sub-route
- [ ] Hierarchy: parent_id for company tree

#### ContactEndpoints (`/api/contacts`, auth required)
- [ ] GET/POST/PUT/DELETE CRUD
- [ ] GET /api/contacts/{id}/communications (email/phone/SMS log)
- [ ] Auto-link contact.email to sd_requesters.contact_id

#### AddressEndpoints (`/api/addresses`, auth required)
- [ ] Polymorphic by entity_type (company/contact/tenant/user)
- [ ] Types: billing/shipping/hq/branch/site/home/work
- [ ] is_primary toggle, is_verified flag
- [ ] address_history audit trigger on update

#### ProfileEndpoints (`/api/profile`, auth required)
- [ ] GET /api/profile — current user's profile
- [ ] PUT /api/profile — update avatar/contact/preferences
- [ ] GET /api/profile/preferences, PUT /api/profile/preferences

#### TeamEndpoints (`/api/teams`, auth required)
- [ ] /api/teams/departments CRUD
- [ ] /api/teams (hierarchy: parent_id)
- [ ] /api/teams/{id}/members (team_members table)
- [ ] team_resources for team-specific content
- [ ] team_permissions for team-level RBAC

#### GroupEndpoints (`/api/groups`, auth required)
- [ ] GET/POST/PUT/DELETE CRUD
- [ ] /api/groups/{id}/members manage membership
- [ ] /api/groups/{id}/permissions grant/deny permissions
- [ ] /api/groups/{id}/rules (dynamic group rules, JSONLogic)
- [ ] /api/groups/{id}/resources — resource access

#### DeviceEndpoints (`/api/devices`, auth required)
- [ ] GET /api/devices returns device list with pagination
- [ ] POST /api/devices creates device
- [ ] PUT /api/devices/{id} updates device
- [ ] DELETE /api/devices/{id} deletes device
- [ ] POST /api/devices/batch (bulk update/delete)
- [ ] Cursor + offset pagination, filter, search, sort
- [ ] Column whitelist validation on sort/filter

#### SwitchEndpoints (`/api/switches`, auth required)
- [ ] GET /api/switches returns switch list
- [ ] POST/PUT/DELETE CRUD with credentials (encrypted)
- [ ] Filter by building/site, search by hostname
- [ ] Pagination + sort

#### LinkEndpoints (`/api/links`, auth required)
- [ ] GET /api/links (P2P/B2B/FW)
- [ ] POST/PUT/DELETE CRUD
- [ ] Sub-type filtering (p2p/b2b/fw)
- [ ] Validation: complete vs incomplete

#### VlanEndpoints (`/api/vlans`, auth required)
- [ ] GET /api/vlans returns VLAN list
- [ ] POST/PUT/DELETE CRUD
- [ ] Filter by site, block status

#### DashboardEndpoints (`/api/dashboard`, auth required)
- [ ] GET /api/dashboard returns full DashboardData
- [ ] All counts query from live DB tables
- [ ] Activity feed combines auth_events + sync_log (last 20)

#### EmailEndpoints (`/api/email`, tracking endpoints anonymous)
- [ ] /api/email/accounts CRUD (SMTP/IMAP/Exchange/Gmail w/OAuth tokens)
- [ ] /api/email/templates CRUD (merge field extraction)
- [ ] /api/email/messages — send + list
- [ ] GET /api/email/track/open/{id} — 1x1 tracking pixel (anonymous)
- [ ] GET /api/email/track/click/{id} — click redirect (anonymous)
- [ ] Auto-link inbound to CRM via linked_account_id/contact_id/deal_id/lead_id

#### EnterpriseEndpoints (invitations + role templates, auth required)
- [ ] POST /api/invitations create, GET /api/invitations list
- [ ] POST /api/invitations/{token}/accept
- [ ] DELETE /api/invitations/{id} cancel
- [ ] /api/role-templates (6 seeded templates CRUD)

#### FileEndpoints (`/api/files`, auth required)
- [ ] POST /api/files/upload (multipart, MD5 verification, inline vs filesystem routing)
- [ ] GET /api/files/{id}/download (latest version, correct Content-Type)
- [ ] GET /api/files/{id}/versions (version history)
- [ ] GET /api/files?entity_type=X&entity_id=Y (list files for entity)
- [ ] DELETE /api/files/{id} (soft delete)

#### GlobalAdminEndpoints (`/api/global-admin`, GlobalAdmin policy)
- [ ] /api/global-admin/tenants CRUD (list/create/update/deactivate)
- [ ] /api/global-admin/subscriptions — platform subscription view
- [ ] /api/global-admin/users — cross-tenant global users
- [ ] /api/global-admin/licenses — per-tenant module licensing
- [ ] Audit log entry on every write (central_platform.global_admin_audit_log)

#### HealthEndpoints (`/api/health`, anonymous)
- [ ] GET /api/health — returns status, timestamp, uptime (no auth)
- [ ] GET /api/health/detailed — DB latency, table counts, system info, sync engine, mediator diagnostics
- [ ] GET /api/health/ready — checks DB connectivity, returns 503 if unavailable
- [ ] GET /api/health/live — always returns 200 (process alive check)
- [ ] GET /api/health/metrics — Prometheus metrics endpoint

#### IdentityProviderEndpoints (`/api/identity`, auth required)
- [ ] /api/identity/providers — CRUD for identity providers
- [ ] /api/identity/domain-mappings — email domain to provider routing
- [ ] /api/identity/claim-mappings — claims to role mapping rules
- [ ] /api/identity/auth-events — read-only auth audit trail

#### ImportEndpoints (`/api/import`, auth required, `admin:users`)
- [ ] POST /api/import — accepts { target_table, upsert_key, records[] }
- [ ] Returns { imported, failed, target_table }
- [ ] GET /api/import/tables — list available target tables
- [ ] Table name validated against pg_tables whitelist

#### JobEndpoints (`/api/jobs`, auth required, `admin:settings`)
- [ ] GET /api/jobs — list job schedules
- [ ] PUT /api/jobs/{id} — enable/disable/change interval/cron
- [ ] POST /api/jobs/{id}/run — trigger immediate execution
- [ ] GET /api/jobs/history — past executions
- [ ] Job types: ping_scan, config_backup, bgp_sync, db_backup

#### LocationEndpoints (`/api/locations`, auth required)
- [ ] /api/locations/countries — CRUD
- [ ] /api/locations/regions — CRUD with country filter
- [ ] /api/locations/references — reference config list
- [ ] /api/locations/references/next/{type} — atomic next reference number
- [ ] /api/locations/postcodes — search by country

#### NotificationEndpoints (`/api/notifications`, auth required)
- [ ] GET /api/notifications/preferences — current user's prefs
- [ ] PUT /api/notifications/preferences — update event type channel/enabled
- [ ] GET /api/notifications/sessions — all active sessions (admin)
- [ ] DELETE /api/notifications/sessions/{id} — force end session

#### PresenceEndpoints (`/api/presence`, auth required)
- [ ] POST /api/presence/join — begin editing an entity
- [ ] POST /api/presence/leave — end editing
- [ ] GET /api/presence/editors/{entityType}/{entityId} — list active editors
- [ ] POST /api/presence/disconnect-all — admin disconnect
- [ ] SignalR-backed live presence

#### ProjectEndpoints (`/api/projects`, auth required)
- [ ] GET /api/projects returns all task projects
- [ ] POST /api/projects creates new project, DELETE /api/projects/{id}
- [ ] GET /api/projects/portfolios / POST / PUT / DELETE
- [ ] GET /api/projects/programmes / POST / DELETE
- [ ] GET /api/projects/{id}/sprints, POST, DELETE /api/projects/{id}/sprints/{sprintId}
- [ ] GET /api/projects/{id}/releases, POST

#### RegistrationEndpoints (`/api/register`, anonymous)
- [ ] POST /api/register/register — self-service registration
- [ ] POST /api/register/verify-email — email verification
- [ ] GET /api/register/check-slug/{slug} — slug availability
- [ ] GET /api/register/subscription/plans — list plans
- [ ] GET /api/register/modules — module license status
- [ ] POST /api/register/modules/{code}/activate — activate module
- [ ] POST /api/register/license/issue — issue signed license key

#### SearchEndpoints (`/api/search`, auth required)
- [ ] GET /api/search?q=term — searches across devices, switches, users, SD tickets, tasks
- [ ] Returns unified results with EntityType, EntityId, Title, Subtitle, Badge
- [ ] Case-insensitive ILIKE search
- [ ] Minimum 2 character query, configurable limit

#### SecurityPolicyEndpoints (`/api/security/policies`, auth required, admin)
- [ ] CRUD for ABAC row/field policies (EntityType, PolicyType, Effect, Conditions, Priority)
- [ ] GET evaluates policies for a given user + entity
- [ ] IpRulesEndpoints (`/api/security/ip-rules`) — allowlist/denylist CIDRs
- [ ] SocialProviderEndpoints (`/api/security/social-providers`) — Google/Microsoft/GitHub
- [ ] UserKeyEndpoints (`/api/user-keys`) — SSH public keys with fingerprint

#### SettingsEndpoints (`/api/settings`, auth required)
- [ ] GET /api/settings/export — exports current user's settings as JSON
- [ ] GET/PUT individual setting keys

#### SshEndpoints (`/api/ssh`, auth required, `switches:ssh`)
- [ ] POST /api/ssh/{id}/ping pings switch
- [ ] POST /api/ssh/{id}/download-config downloads config
- [ ] POST /api/ssh/{id}/sync-bgp syncs BGP
- [ ] POST /api/ssh/ping-all batch pings
- [ ] SignalR SyncProgress streaming during SSH operations
- [ ] SSH-specific rate limiting (separate bucket)

#### StatusEndpoints (`/api/status`, auth required)
- [ ] GET /api/status — complete platform overview
- [ ] Includes DB / API / SignalR / sync engine / jobs status

#### VersionEndpoints (`/api/version`, anonymous)
- [ ] GET /api/version — product name, version, build date, runtime, OS, architecture, endpoint list

#### UpdateEndpoints (`/api/updates`, tagged Updates)
- [ ] GET /api/updates/check — returns update info if newer version exists
- [ ] POST /api/updates/publish — publish new version
- [ ] GET /api/updates/versions — list all published versions
- [ ] POST /api/updates/report — client reports update result

#### SyncEndpoints (`/api/sync`, auth required, `admin:settings`)
- [ ] GET /api/sync/configs — list all sync configurations
- [ ] PUT /api/sync/configs — create/update sync config
- [ ] GET /api/sync/configs/{id}/entity-maps
- [ ] GET /api/sync/configs/{id}/log
- [ ] POST /api/sync/configs/{id}/run — trigger sync execution
- [ ] GET /api/sync/agent-types — list registered agent types
- [ ] GET /api/sync/converter-types — list registered converter types

#### TaskEndpoints (`/api/tasks`, auth required)
- [ ] GET /api/tasks returns all 45 fields with project/sprint/committed joins
- [ ] GET /api/tasks?project_id=N filters by project
- [ ] POST /api/tasks accepts all Phase 1 fields
- [ ] PUT /api/tasks/{id} updates all Phase 1 fields
- [ ] POST /api/tasks/{id}/commit — commit task to sprint
- [ ] DELETE /api/tasks/{id}/commit — uncommit from sprint
- [ ] GET /api/tasks/{id}/links, POST /api/tasks/{id}/links
- [ ] GET /api/tasks/{id}/dependencies — get Gantt dependencies
- [ ] GET /api/tasks/{id}/time — time entries for a task
- [ ] GET/POST /api/tasks/{id}/comments
- [ ] DELETE /api/tasks/{id}

#### TenantProvisioningEndpoints (`/api/global-admin`, GlobalAdmin policy)
- [ ] GET /api/global-admin/tenants/{id}/sizing (sizing + recent provisioning jobs)
- [ ] POST /api/global-admin/tenants/{id}/provision-dedicated — queue provision job
- [ ] POST /api/global-admin/tenants/{id}/decommission-dedicated — queue decommission job
- [ ] GET /api/global-admin/provisioning-jobs — platform-wide job queue

#### ValidationEndpoints (`/api/validation`, auth required)
- [ ] POST /api/validation/validate/{entityType} — validates JSON against registered rules
- [ ] Returns { isValid, errors[], errorSummary }

#### WebhookEndpoints (`/api/webhooks`)
- [ ] POST /api/webhooks/{source} receives JSON payload — anonymous, HMAC validated
- [ ] Payload stored in webhook_log table
- [ ] Invalid JSON wrapped in {"raw": "..."} object
- [ ] SignalR "WebhookReceived" event broadcast
- [ ] Auto-marks matching sync_config as 'pending'
- [ ] GET /api/webhooks — list recent webhooks (auth required)
- [ ] GET /api/webhooks/{id}/payload — retrieve full payload (auth required)
- [ ] Subscription management: /api/webhooks/event-types + /subscriptions + /deliveries

#### CrmEndpoints (`/api/crm/*`, auth required)
- [ ] /api/crm/accounts CRUD with owner/rating/stage/tags
- [ ] /api/crm/deals CRUD + pipeline stage transitions + /pipeline summary
- [ ] /api/crm/leads CRUD + scoring + /convert to account + contact + deal (transactional)
- [ ] /api/crm/activities unified timeline (call/email/meeting/note/task)
- [ ] /api/crm product + quote sub-routes (products, price books, quotes, quote_items)

#### CrmDashboardEndpoints (`/api/crm/dashboard`, auth required)
- [ ] GET /api/crm/dashboard/revenue
- [ ] GET /api/crm/dashboard/activity
- [ ] GET /api/crm/dashboard/leads
- [ ] GET /api/crm/dashboard/accounts/health
- [ ] GET /api/crm/dashboard/summary (KPI cards)
- [ ] POST /api/crm/dashboard/refresh (refresh_crm_dashboards())

#### CrmExpansionEndpoints (`/api/crm/*`, auth required)
- [ ] /api/crm/campaigns + members + costs + influence + refresh-influence
- [ ] /api/crm/marketing/segments (static + dynamic)
- [ ] /api/crm/marketing/sequences + /enroll
- [ ] GET /api/crm/marketing/landing-pages/{slug} (public)
- [ ] POST /api/crm/marketing/forms/{slug}/submit (public)
- [ ] /api/crm/salesops/territories + members + rules
- [ ] /api/crm/salesops/quotas
- [ ] /api/crm/salesops/commission-plans + /commission-payouts
- [ ] /api/crm/salesops/deals/{id}/splits (100% validation trigger)
- [ ] /api/crm/salesops/accounts/{id}/team + /plan
- [ ] /api/crm/salesops/pipeline-health + /deal-insights
- [ ] /api/crm/cpq/bundles + /pricing-rules + /discount-matrix
- [ ] /api/approvals (generic approval engine)
- [ ] /api/crm/contracts + /renewals + /clauses + /{id}/milestones + /{id}/sign
- [ ] /api/crm/subscriptions + /mrr-dashboard + /{id}/cancel + /{id}/events
- [ ] /api/crm/revenue/schedules + /entries + /schedules/{id}/generate-entries
- [ ] /api/crm/orders + /{id}/lines

#### Stage5Endpoints (portals + platform + commerce)
- [ ] /api/portal/users + /magic-link (anonymous auth, 30-min expiry)
- [ ] /api/portal/deal-registrations
- [ ] /api/portal/kb/articles (public read + authenticated write)
- [ ] /api/portal/kb/categories
- [ ] /api/portal/community/threads + /community/threads/{id}/posts (public reads)
- [ ] /api/rules/validation (JSONLogic validation rules)
- [ ] /api/rules/workflow (Elsa-integrated workflow rules)
- [ ] /api/rules/execution-log (audit debug trail)
- [ ] /api/custom-objects/entities + /fields + /records/{entity} + /permissions (field-level security)
- [ ] /api/commerce/import (import wizard with dedup strategies, dry_run)
- [ ] /api/commerce/cart + /cart/{id}/checkout (cart to order transaction)
- [ ] /api/commerce/payments (Stripe-compatible)

#### AiEndpoints (AI provider + assistant + insights)
- [ ] /api/global-admin/ai/providers — platform providers (8 seeded)
- [ ] /api/global-admin/ai/models — 9 seeded models
- [ ] /api/global-admin/ai/features — feature catalog
- [ ] /api/ai/tenant/providers — tenant BYOK (AES-256 encrypted api_key_enc)
- [ ] /api/ai/tenant/providers/{id}/test — round-trip call
- [ ] /api/ai/tenant/features — per-feature provider mapping
- [ ] /api/ai/tenant/resolve/{featureCode} — tenant to platform to none
- [ ] /api/ai/tenant/usage — aggregated usage (auto-aggregation trigger)
- [ ] /api/ai/assistant/conversations + /messages + /templates + /tools
- [ ] /api/ai/scores?entity=lead/deal/account
- [ ] /api/ai/ml-models + /{id}/train
- [ ] /api/ai/next-actions
- [ ] /api/ai/duplicates + /rules + /merge
- [ ] /api/ai/enrichment/providers + /jobs
- [ ] /api/ai/churn-risks, /api/ai/account-ltv
- [ ] /api/ai/calls (upload + transcript + sentiment)

#### Helpers
- [ ] PaginationHelpers — cursor + offset pagination
- [ ] ProblemDetails — RFC 7807 formatter
- [ ] EndpointHelpers — shared validation + auth helpers

### 6.2 JWT Authentication + Multi-Issuer

- [ ] POST /api/auth/login returns JWT token
- [ ] Bearer token required on all endpoints (except anonymous)
- [ ] 25 permission claims in token
- [ ] 401 on expired/invalid token
- [ ] Auto token refresh on 401
- [ ] Multi-issuer: Central + secure_auth auth-service tokens both accepted
- [ ] CENTRAL_JWT_SECRET required env var (no default)
- [ ] Token includes tenant_slug for tenant resolution

### 6.3 SignalR (NotificationHub)

- [ ] NotificationHub connects on startup
- [ ] DataChanged event fires on DB changes (pg_notify)
- [ ] PingResult event fires on ping completion
- [ ] SyncProgress event streams during SSH operations
- [ ] WPF grids auto-refresh on SignalR events
- [ ] SendNotification — eventType, title, message, severity
- [ ] SendWebhookReceived — source, webhookId
- [ ] SendAuditEvent — action, entityType, entityName, username
- [ ] SendSyncComplete — configName, status, recordsRead, recordsFailed
- [ ] SendSessionEvent — eventType, username, authMethod
- [ ] All events broadcast to all connected clients
- [ ] NotificationHub: OnConnectedAsync joins tenant group
- [ ] NotificationHub: OnDisconnectedAsync leaves tenant group
- [ ] All Send* methods broadcast to tenant group (not Clients.All)

### 6.4 Swagger / OpenAPI

- [ ] /swagger loads OpenAPI UI
- [ ] All endpoints documented
- [ ] All endpoint groups have WithTags for organized Swagger UI
- [ ] Swagger description updated for full platform scope
- [ ] Swagger UI with security definition for Bearer JWT

### 6.5 Rate Limiting

- [ ] Per-user rate limiter: sliding window
- [ ] RFC 6585 429 headers (Retry-After, X-RateLimit-Limit/Remaining/Reset)
- [ ] Default: 200 requests per 60 seconds per IP
- [ ] SSH-specific bucket (stricter, shared across endpoints)
- [ ] Health checks, webhooks, and SignalR hubs excluded from rate limiting
- [ ] Stale window cleanup when map exceeds 10,000 entries

### 6.6 RFC 7807 Problem Details

- [ ] All API error responses use standard problem+json ProblemDetails format
- [ ] Fields: type, title, status, detail, instance
- [ ] Correlation ID included on every response

### 6.7 Correlation IDs + Serilog JSON Logging

- [ ] CorrelationContext.AsyncLocal<string> for request correlation ID
- [ ] StructuredLogEntry with ToCef() for SIEM integration
- [ ] Level-to-severity mapping
- [ ] Serilog writes to console + rolling file + Seq sink
- [ ] All requests logged: method, path, status code, duration, user
- [ ] Slow requests (>1000ms) logged at Warning level with [SLOW] tag
- [ ] Error responses (4xx/5xx) logged at Warning level
- [ ] Anonymous requests show "anonymous" as user

### 6.8 Prometheus Metrics

- [ ] GET /api/health/metrics returns Prometheus text format
- [ ] Request counts per endpoint
- [ ] Request latency histograms
- [ ] Active SignalR connections gauge
- [ ] GC stats for memory / pause analysis
- [ ] Grafana dashboards read from this endpoint

### 6.9 Webhook HMAC Signature Validation

- [ ] Inbound webhooks validated via X-Webhook-Signature header
- [ ] HMAC-SHA256 computed over raw body using CENTRAL_WEBHOOK_SECRET
- [ ] Constant-time comparison (prevents timing attacks)
- [ ] Invalid signature returns 401

### 6.10 Column Whitelist (SQL Injection Prevention)

- [ ] Sort/filter/search parameters validated against allowed column names
- [ ] Unknown column returns 400 (not silently ignored)
- [ ] Per-entity whitelist maintained with schema changes

### 6.11 Middleware Pipeline Order

- [ ] SecurityHeaders > RequestLogging > RateLimit > ApiKeyAuth > Authentication > Authorization > TenantResolution > ModuleLicense
- [ ] TenantResolutionMiddleware extracts tenant_slug from JWT, falls back to X-Tenant header
- [ ] Defaults to "default" tenant for backward compatibility
- [ ] ModuleLicenseMiddleware maps API paths to module codes
- [ ] Enterprise tier bypasses module license checks

### API Client (Central.ApiClient)

- [ ] SearchAsync, GetDashboardAsync, GetStatusAsync
- [ ] GetActivityAsync / GetMyActivityAsync
- [ ] GetSyncConfigsAsync / RunSyncAsync
- [ ] GetAuditLogAsync, GetIdentityProvidersAsync
- [ ] ImportAsync, HealthCheckAsync

---

## 7. Modules

### 7.1 IPAM (Devices)

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
- [ ] AsnDefinition DisplayText with/without description — `AsnDefinitionTests` (6 tests)

#### Master-Detail
- [ ] Row expansion shows detail data — `MasterDeviceTests.Defaults`, `MasterDeviceTests.PropertyChanged_SelectedFields`
- [ ] Detail grid has TotalSummary

### 7.2 Switches

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

#### Config Compare / Version History
- [ ] Running config version history grid per switch
- [ ] Side-by-side diff panel (see Section 4.8)
- [ ] Compare button on Details > Config tab

#### Context Menu
- [ ] All standard context menu items present
- [ ] Go to Device navigates to IPAM panel

### 7.3 Links (P2P, B2B, FW)

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

### 7.4 Routing (BGP)

- [ ] Top grid shows BGP config per switch (AS, router-id, settings) — `BgpRecordTests.BgpRecord_Defaults`
- [ ] Master-detail: bottom tabs show Neighbors + Advertised Networks — `BgpRecordTests.BgpNeighborRecord_Defaults`, `BgpRecordTests.BgpNetworkRecord_Defaults`
- [ ] SSH sync downloads live BGP config
- [ ] fast_external_failover, bestpath_multipath_relax columns editable — `BgpRecordTests.BgpRecord_PropertyChanged_AllFields`
- [ ] last_synced timestamp updates after sync
- [ ] BGP diagram panel renders topology graphically

### 7.5 VLANs

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

### 7.6 Tasks / Projects

#### Task Tree Panel
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
- [ ] Start Date / Finish Date / Due Date columns with DX DateEdit
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

#### Task Hierarchy & Schema
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

#### Task Repository
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

#### Product Backlog
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

#### Sprint Planning
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

#### Sprint Burndown
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

#### Kanban Board
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

#### Gantt Chart
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

#### QA & Bug Tracking
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

#### QA Dashboard
- [ ] QADashboardPanel opens from ribbon "QA Dashboard" toggle
- [ ] Bugs by Severity chart (bar chart, red)
- [ ] Bug Aging chart (bar chart, amber, 6 time buckets)
- [ ] Opened vs Closed chart (line chart, red=opened, green=closed, last 30 days)
- [ ] Open Bugs by Assignee chart (bar chart, blue, top 10)
- [ ] 2x2 grid layout

#### Custom Columns
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

#### Report Builder
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

#### Task Dashboard
- [ ] TaskDashboardPanel opens from ribbon "Dashboard" toggle
- [ ] Tasks by Status — pie chart
- [ ] Points by Type — bar chart
- [ ] Tasks Created (30 days) — line chart
- [ ] Sprint Velocity — bar chart (last 10 closed sprints)
- [ ] 2x2 grid layout — `SprintAndPlanningTests.DashboardTile_Defaults`, `SprintAndPlanningTests.Dashboard_PropertyChanged`, `ReportModelsTests.Dashboard_Defaults`, `ReportModelsTests.Dashboard_PropertyChanged_Fires`, `ReportModelsTests.DashboardTile_Defaults`, `ReportModelsTests.DashboardTile_SetProperties`

#### Timesheet
- [ ] TimesheetPanel opens from ribbon toggle
- [ ] Week picker loads entries for Mon-Sun
- [ ] Hours column (SpinEdit 0-24), Activity dropdown (5 types), Notes — `SprintAndPlanningTests.TimeEntry_PropertyChanged_Fires`, `TimeActivityModelsTests.TimeEntry_Defaults`, `TimeActivityModelsTests.TimeEntry_EntryDateDisplay`, `TimeActivityModelsTests.TimeEntry_PropertyChanged_Fires`
- [ ] TotalSummary: sum hours + entry count
- [ ] Total hours green display in toolbar
- [ ] ValidateRow auto-saves

#### Activity Feed
- [ ] ActivityFeedPanel opens from ribbon toggle
- [ ] Project selector + refresh
- [ ] Card template: action icon, summary, user, time ago — `SprintAndPlanningTests.ActivityFeedItem_TimeAgo_JustNow`, `SprintAndPlanningTests.ActivityFeedItem_TimeAgo_Minutes`, `TimeActivityModelsTests.ActivityFeedItem_ActionIcon_Correct`, `TimeActivityModelsTests.ActivityFeedItem_TimeAgo_JustNow`, `TimeActivityModelsTests.ActivityFeedItem_TimeAgo_Minutes`, `TimeActivityModelsTests.ActivityFeedItem_TimeAgo_Hours`, `TimeActivityModelsTests.ActivityFeedItem_TimeAgo_Days`
- [ ] TaskViewConfig defaults and PropertyChanged — `TimeActivityModelsTests.TaskViewConfig_Defaults`, `TimeActivityModelsTests.TaskViewConfig_PropertyChanged_Fires`
- [ ] Auto-populated by PG trigger

#### My Tasks
- [ ] Shows tasks assigned to current user across all projects
- [ ] Group By: None/Project/Due/Priority/Status
- [ ] Inline editing Status + WorkRemaining
- [ ] Summary: count, points, remaining

#### Portfolio
- [ ] Portfolio > Programme > Project hierarchy with roll-ups
- [ ] Columns: Name, Level, Tasks, Points, Complete%, OpenBugs, ActiveSprints
- [ ] BuildPortfolioTreeAsync aggregates from all data

#### Task Import
- [ ] TaskImportPanel opens from ribbon "Import" toggle
- [ ] Step 1: Browse file, format auto-detect (.xlsx/.csv/.xml), project selector
- [ ] Step 2: Column mapping grid with auto-detect, sample values
- [ ] Step 3: Preview + Import with progress bar
- [ ] "Update existing" checkbox matches by Title within project

#### Task File Parser
- [ ] ParseFile routes by extension (.csv, .xml, .xlsx) — `TaskFileParserTests` (10 tests)
- [ ] CSV parser: header row + data rows
- [ ] MS Project XML parser: XDocument, handles namespace
- [ ] MS Project fields: Name, WBS, Start, Finish, Duration, PercentComplete, Priority, Milestone, PredecessorLink
- [ ] Excel placeholder returns info message

#### Task Context Menus (all panels)
- [ ] Task Tree: New Task, New Sub-Task, Delete Task, Export, Refresh
- [ ] Backlog: Commit to Sprint, Uncommit, Export, Refresh
- [ ] Sprint Plan: New Task in Sprint, Export, Refresh
- [ ] QA: New Bug, Batch Triage, Export, Refresh
- [ ] My Tasks: Go to Task in Tree, Export, Refresh
- [ ] Timesheet: Log Time, Delete Entry, Export, Refresh
- [ ] Report Results: Export to Clipboard, Export to CSV
- [ ] Portfolio: Refresh
- [ ] ExportTreeToClipboard helper: SelectAll + CopyToClipboard

#### Task Module Engine Compliance
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

#### Task Detail Panel
- [ ] TaskDetailDocPanel wired in DockLayoutManager
- [ ] Task tree CurrentItemChanged shows task in detail panel
- [ ] Detail shows: status icon, title, type, priority, assigned, dates, hours, tags, description, comments

#### Task Models
- [ ] TaskItem model tests — `TaskModelsTests` (42 tests) + `TaskItemEdgeCaseTests` (29 tests) + `TaskItemTheoryTests` (22 tests)
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
- [ ] Sprint and planning model tests — `SprintAndPlanningTests` (21 tests)
- [ ] Task repository integration tests — `TaskRepositoryIntegrationTests` (13 tests)

### 7.7 Service Desk

#### ManageEngine Sync
- [ ] Read (Sync) button pulls tickets from ManageEngine
- [ ] Incremental sync only pulls changed records
- [ ] Priority, urgency, impact fields populated
- [ ] resolved_at populated from completed_time (not synced_at)
- [ ] Refresh token auto-rotates if Zoho returns new token
- [ ] Auth failure shows error toast
- [ ] Sync status updates in status bar during pull

#### SD Request Grid
- [ ] Grid loads with data, sorted by created date descending
- [ ] Status column shows colour-coded text (Open=blue, Closed=green, etc.) — `SdRequestExtendedTests.StatusColor_Cancelled_BritishSpelling`, `SdRequestExtendedTests.StatusColor_Resolved_Green`, `SdRequestExtendedTests.StatusColor_EmptyString_Default`, `SdRequestExtendedTests2.StatusColor_AllStatuses`
- [ ] Priority column shows colour-coded text — `SdRequestExtendedTests.PriorityColor_Unknown_Default`, `SdRequestExtendedTests.PriorityColor_Empty_Default`, `SdRequestExtendedTests2.PriorityColor_AllPriorities`
- [ ] Overdue icon (!) shows for open tickets past due date — `SdRequestExtendedTests.IsOverdue_CancelledBritish_False`, `SdRequestExtendedTests.IsOverdue_Resolved_False`, `SdRequestExtendedTests.IsOverdue_Archive_False`, `SdRequestExtendedTests.IsOverdue_InProgress_PastDue_True`, `SdRequestExtendedTests.IsOverdue_OnHold_PastDue_True`, `SdRequestExtendedTests.OverdueIcon_WhenOverdue_Warning`, `SdRequestExtendedTests.OverdueIcon_NotOverdue_Empty`, `SdRequestExtendedTests2.IsOverdue_True_WhenPastDueAndOpen`, `SdRequestExtendedTests2.IsOverdue_False_WhenNoDueBy`, `SdRequestExtendedTests2.IsOverdue_False_WhenResolved`, `SdRequestExtendedTests2.IsOverdue_False_WhenCanceled`, `SdRequestExtendedTests2.IsOverdue_False_WhenArchive`, `SdRequestExtendedTests2.IsOverdue_False_WhenFutureDue`
- [ ] IsClosed by status (Resolved/Closed=true, others=false) — `SdRequestExtendedTests.IsClosed_AllStatuses`, `SdRequestExtendedTests2.IsClosed_VariousStatuses`
- [ ] "Open" hyperlink column opens ticket in browser
- [ ] Inline editing: Status, Priority, Group, Technician, Category dropdowns
- [ ] Editing a field turns row amber (dirty tracking) — `SdRequestDirtyTrackingTests` (7 tests), `SdRequestExtendedTests.DirtyTracking_*`, `SdRequestExtendedTests2.DirtyTracking_*`
- [ ] RowColor amber when dirty, transparent when clean — `SdRequestExtendedTests2.RowColor_Amber_WhenDirty`, `SdRequestExtendedTests2.RowColor_Transparent_WhenClean`
- [ ] PropertyChanged cascades: Priority->PriorityColor, DueBy->IsOverdue, IsDirty->RowColor — `SdRequestExtendedTests.PropertyChanged_*`, `SdRequestExtendedTests2.PropertyChanged_*`
- [ ] SdRequest defaults (all empty strings, nulls, false) — `SdRequestExtendedTests2.Defaults_AreCorrect`
- [ ] "3 unsaved changes" count shows in toolbar
- [ ] Save Changes button writes to ManageEngine API
- [ ] Discard button reverts to original values
- [ ] Context menu: Read, Update Status/Priority, Assign Tech, Add Note
- [ ] Context menu: Open in Browser, Clear Filter, Export, Refresh
- [ ] Total summary count at bottom of grid

#### SD Overview Dashboard
- [ ] KPI cards: Incoming, Closed, Escalations, SLA Compliant, Resolution Time, Open, Tech:Ticket
- [ ] KPI trend arrows show comparison vs previous period
- [ ] Double-click KPI card opens drill-down grid
- [ ] Bar chart: created (dark red) vs closed (olive green) per bucket
- [ ] Avg resolution days line (flat period-wide mean)
- [ ] Open issues line (point-in-time count)
- [ ] Double-click bar opens drill-down grid for that day
- [ ] Summary text shows totals + closure rate %

#### SD Tech Closures
- [ ] Bar chart shows per-tech daily closures
- [ ] Expected target dashed line visible
- [ ] Double-click a bar opens drill-down grid

#### SD Aging
- [ ] 5 side-by-side bars per tech (0-1d green to 7+ red)
- [ ] Double-click a bar opens drill-down grid

#### SD Settings Panel (Global Filters)
- [ ] Time Range dropdown changes all chart date ranges
- [ ] Time Scale dropdown changes bucket size (day/week/month) — `SdFilterStateExtendedTests.FormatLabel_*`, `SdFilterStateFormatTests.FormatLabel_*`
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

#### SD Groups
- [ ] Grid shows all ME groups with Active checkbox
- [ ] Inline editing group name + sort order, auto-saves on row commit
- [ ] Active: X / Total: Y summary at bottom
- [ ] Disabling a group removes it from request grid dropdown

#### SD Technicians
- [ ] Grid shows all ME technicians with Active checkbox
- [ ] Toggle Active auto-saves to DB
- [ ] Active: X / Total: Y summary at bottom
- [ ] Disabling a tech removes from all dropdowns, charts, and filters
- [ ] Cascade: disabling refreshes request grid combos + chart filter panels

#### SD Requesters
- [ ] Grid shows all ME requesters (read-only, synced)
- [ ] VIP: X / Total: Y summary at bottom

#### SD Teams
- [ ] Teams grid: add/edit/delete teams
- [ ] Right panel: checked listbox of all technicians
- [ ] Check/uncheck assigns techs to team, auto-saves
- [ ] Team buttons appear in chart filter panels

#### Group Categories
- [ ] Tree view shows parent categories with nested ME groups
- [ ] Drag groups under categories to nest
- [ ] Add Category button creates new category node
- [ ] Delete button removes category (children move to root)
- [ ] Save button persists to DB + refreshes SD Settings filter
- [ ] Category shows in SD Settings as gold checkbox with count
- [ ] Checking category selects all child groups in filter
- [ ] Tooltip on category checkbox shows member list

#### Cross-Panel Linking
- [ ] Click technician in SD Technicians > Request grid auto-filters to that tech
- [ ] Click group in SD Groups > Request grid auto-filters to that group
- [ ] Click requester in SD Requesters > Request grid auto-filters to that requester
- [ ] Right-click "Clear Filter" on Request grid resets the filter
- [ ] Service Desk panel activates when linked filter applies

#### Write-Back to ManageEngine
- [ ] Update Status writes correct value to ME API
- [ ] Update Priority writes correct value to ME API
- [ ] Assign Technician writes correct value to ME API
- [ ] Add Note posts to ME API
- [ ] Bulk dirty row save writes all changed rows
- [ ] Write-back errors show in status bar + app log
- [ ] Integration log records sync/write actions with duration

#### SD Models
- [ ] SD request model tests — `SdRequestTests` (15 tests), `SdRequestExtendedTests` (21 tests), `SdRequestExtendedTests2` (43 tests)
- [ ] SD filter state tests — `SdFilterStateTests` (9 tests), `SdFilterStateExtendedTests` (7 tests), `SdFilterStateFormatTests` (12 tests)
- [ ] SD models tests — `SdModelsTests` (10 tests), `SdServiceModelsTests` (14 tests)
- [ ] SD dirty tracking tests — `SdRequestDirtyTrackingTests` (7 tests)
- [ ] SdGroupCategory defaults, MemberCount, PropertyChanged — `SdServiceModelsTests.SdGroupCategory_Defaults`, `SdServiceModelsTests.SdGroupCategory_MemberCount`, `SdServiceModelsTests.SdGroupCategory_PropertyChanged_Fires`
- [ ] SdGroup defaults and PropertyChanged — `SdServiceModelsTests.SdGroup_Defaults`, `SdServiceModelsTests.SdGroup_PropertyChanged_Fires`
- [ ] SdRequester defaults — `SdServiceModelsTests.SdRequester_Defaults`
- [ ] SdTechnician defaults, PropertyChanged — `SdServiceModelsTests.SdTechnician_Defaults`, `SdServiceModelsTests.SdTechnician_PropertyChanged_IsActive`
- [ ] SdKpiSummary defaults — `SdServiceModelsTests.SdKpiSummary_Defaults`
- [ ] SdTeam defaults — `SdServiceModelsTests.SdTeam_Defaults`
- [ ] SdWeeklyTotal DayLabel — `SdServiceModelsTests.SdWeeklyTotal_DayLabel`
- [ ] SdAgingBucket Total sum — `SdServiceModelsTests.SdAgingBucket_Total`, `SdServiceModelsTests.SdAgingBucket_Total_Empty`
- [ ] SdTechDaily DayLabel — `SdServiceModelsTests.SdTechDaily_DayLabel`

#### SD Infrastructure
- [ ] ManageEngine integration record exists in integrations table
- [ ] OAuth credentials stored encrypted in integration_credentials — `IntegrationServiceTests` (8 tests), `IntegrationModelTests` (9 tests), `IntegrationModelExtendedTests` (12 tests)
- [ ] Integration defaults, StatusIcon, StatusText, PropertyChanged — `IntegrationModelExtendedTests.Integration_*`
- [ ] IntegrationCredential defaults + ExpiresAt nullable — `IntegrationModelExtendedTests.IntegrationCredential_*`
- [ ] IntegrationLogEntry defaults + set properties — `IntegrationModelExtendedTests.IntegrationLogEntry_*`
- [ ] config_json has oauth_url + portal_url
- [ ] sd_groups seeded from sd_requests distinct group_name
- [ ] sd_group_categories + members tables exist
- [ ] sd_teams + members tables exist
- [ ] resolved_at + me_completed_time columns on sd_requests
- [ ] All SD permissions granted to Admin role

### 7.8 CRM (Unified — 29-phase base + 5 expansion stages)

#### Foundation (Phases 1-5)
- [ ] Companies with hierarchy (parent_id) — `/api/companies` CRUD
- [ ] Contacts with addresses + communications — `/api/contacts`
- [ ] Teams + Departments hierarchy — `/api/teams`
- [ ] Polymorphic Addresses (billing/shipping/hq/branch/site/home/work)
- [ ] User profiles (avatar, preferences, invitations)

#### Global Admin + Admin (Phases 6-14)
- [ ] Tenant onboarding + billing accounts + invoices
- [ ] Usage metrics + FTS indexes
- [ ] Invitations + team management + role templates (6 seeded)
- [ ] Org chart (departments + user_profiles.manager_id)

#### CRM Core (Phases 15-19)
- [ ] CRM Accounts with company linking, owner, rating, stage, tags — `/api/crm/accounts`
- [ ] Contact to account M:N with role_in_account (decision_maker/influencer/user/billing/technical)
- [ ] Deal pipeline with 5 seeded stages + auto stage history trigger + /pipeline summary
- [ ] Leads with scoring rules + transactional /convert to account+contact+deal
- [ ] Unified activity timeline (call/email/meeting/note/task) with due_at + is_completed

#### Email Integration (Phase 20)
- [ ] Email accounts (SMTP/IMAP/Exchange/Gmail with OAuth token storage)
- [ ] 4 seeded templates with merge field extraction ({{contact.first_name}})
- [ ] Send queue via pg_notify
- [ ] Tracking pixel (/api/email/track/open/{id}) + click redirect
- [ ] Auto-link inbound to CRM via linked_account_id/contact_id/deal_id/lead_id

#### Pipeline Viz (Phase 21)
- [ ] /api/crm/deals/pipeline summary
- [ ] WPF Kanban Pipeline panel in CRM module

#### Quotes + Products (Phases 22-23)
- [ ] Quote versioning + auto-recalc totals trigger + line items
- [ ] Products + price books + price book entries with volume tiers

#### Dashboards + Reports (Phases 24-25)
- [ ] 4 materialized views (revenue/activity/lead_source_roi/account_health)
- [ ] KPI summary endpoint (customers, prospects, pipeline, revenue_this_month, overdue)
- [ ] Hourly refresh job (refresh_crm_dashboards())
- [ ] Saved reports (pipeline/sales_rep_perf/lead_source_roi/account_revenue/activity/forecast)
- [ ] Forecast snapshots (committed/best_case/worst_case/weighted) + live forecast endpoint

#### Integration + Documents (Phases 26-28)
- [ ] 6 integration agents seeded (Salesforce, HubSpot, Dynamics, Exchange, Gmail, Pipedrive)
- [ ] 8 sync configs for bidirectional entity sync
- [ ] crm_external_ids + crm_sync_conflicts tables
- [ ] Exchange Online + Gmail integration configs for auto-log to CRM
- [ ] crm_documents + document_templates + approval workflow
- [ ] E-signature placeholder (signature_provider + signature_envelope_id)

#### Webhooks + Polish (Phase 29)
- [ ] 28 webhook event types seeded (crm.deal.won/lost, crm.lead.converted, etc.)
- [ ] webhook_subscriptions with HMAC secret generation
- [ ] webhook_deliveries with retry + status tracking
- [ ] Cross-module: CRM Deal won to auto activity + account last_activity_at update
- [ ] Cross-module: Contact email to sd_requesters.contact_id auto-link
- [ ] Cross-module: switch_guide.crm_account_id (infra owned by company)
- [ ] Cross-module: task_projects.crm_deal_id (delivery project per won deal)

#### Expansion Stage 1 — Marketing Automation
- [ ] Campaigns with ROI tracking, hierarchy, source_code (UTM linking)
- [ ] Campaign members (leads/contacts/accounts) with response tracking
- [ ] Campaign costs auto-aggregated via trigger
- [ ] Segments — static + dynamic (JSONLogic rule_expression)
- [ ] Email sequences with triggers (manual/lead_created/deal_stage/form_submit)
- [ ] Sequence steps with wait days/hours, conditions, stop-on-reply
- [ ] Sequence enrollments with status (active/paused/completed/stopped_reply/meeting)
- [ ] Landing pages with view_count + submission_count + conversion rate
- [ ] Public forms with auto-lead-creation + UTM capture
- [ ] Multi-touch attribution (5 models: first/last/linear/position/time-decay) + campaign influence materialized view

#### Expansion Stage 2 — Sales Operations
- [ ] Territories (geographic/industry/account_size/named/role) with hierarchy
- [ ] Territory rules for auto-assignment + territory FK on accounts + leads
- [ ] Quotas per user per period (monthly/quarterly/annual) with ramping
- [ ] Commission plans (flat/tiered/gated) + tiers (accelerators) + user assignments
- [ ] Commission payouts with SPIFFs + clawbacks + breakdown
- [ ] Opportunity splits with 100% validation trigger + multi-type (revenue/overlay/quota)
- [ ] Account teams with roles + access levels
- [ ] Account plans with strategic goals + whitespace + stakeholder mapping + org chart edges
- [ ] Forecast adjustments (manager commits/overrides) + pipeline health materialized view
- [ ] Deal insights (stalled, no_activity, close_date_slipping, next_step_missing) with generator function

#### Expansion Stage 3 — CPQ + Contracts + Revenue
- [ ] Product bundles + components (optional, override prices)
- [ ] Pricing rules (volume/customer/promo/MAP-floor/bundle) with promo_code + max_uses
- [ ] Discount approval matrix (4 tiers seeded: Rep/Manager/VP/CEO)
- [ ] Generic approval engine (requests + steps sequential/parallel + actions audit + auto-resolve trigger)
- [ ] Contract clause library with versioning + legal approval flag
- [ ] Contract templates with default_clauses
- [ ] Contracts (msa/sow/nda/dpa/amendment/renewal) with full lifecycle + auto_renew + notice days
- [ ] Contract versions, clause usage (modification tracking), milestones (delivery/payment), renewals view
- [ ] Subscriptions + auto-log events on MRR changes + status transitions
- [ ] MRR dashboard materialized view (active/trial/churned + churn rate)
- [ ] Revenue schedules (ratable/point_in_time/milestone/percentage) + entries with GL journal reference (ASC 606)
- [ ] Orders + order lines with auto-recalc totals + auto-subscription-creation from recurring products

#### Expansion Stage 5 — Portals + Platform + Commerce
- [ ] Customer + partner portal users separate from app_users (portal_users, portal_sessions)
- [ ] Magic-link email login (30-min expiry, single-use tokens, SHA256-hashed storage)
- [ ] Partner deal registration with approval workflow
- [ ] Knowledge base with tsvector full-text search + category hierarchy + view/helpful counters
- [ ] Community threads + nested posts + voting (app_users OR portal_users as author)
- [ ] Validation rules (JSONLogic, pre-save, per-entity, error_field highlighting)
- [ ] Workflow rules — integrates with existing Elsa engine (no duplicate runtime)
- [ ] Generic broadcast_record_change trigger on 7 CRM tables
- [ ] rule_execution_log for audit + debug (rule_type, rule_id, result, elsa_workflow_instance_id)
- [ ] custom_entities (api_name, label, record_name_format, icon, color)
- [ ] custom_fields (text/number/date/datetime/bool/picklist/multipick/lookup/url/email/phone/currency/percent/richtext/file)
- [ ] custom_entity_records with jsonb values + GIN index
- [ ] custom_field_values on built-in entities (typed columns: text_value, number_value, date_value, etc.)
- [ ] custom_relationships (one_to_one, one_to_many, many_to_many)
- [ ] field_permissions per role per entity per field (hidden/read/write)
- [ ] get_field_permission() helper function with write-default fallback
- [ ] import_jobs with dry_run + dedup_strategy (create_new/update_existing/skip_duplicates/merge)
- [ ] import_job_rows with per-row status + errors + warnings arrays
- [ ] pg_notify('import_queue', id) for background processing
- [ ] shopping_carts + cart_items with auto-recalc trigger
- [ ] Cart to Order checkout (transactional, copies items, marks cart converted)
- [ ] payments (Stripe-compatible: stripe_payment_intent_id, stripe_charge_id, last4, brand)
- [ ] Multi-entity linking (order/invoice/cart to payment)

#### CRM WPF Module
- [ ] Central.Module.CRM registered in Bootstrapper at SortOrder 40
- [ ] CrmDataService with async PG queries for accounts/deals/leads/KPIs/pipeline
- [ ] CrmAccountsPanel — DevExpress grid with filter/group/sort on 10 columns
- [ ] CrmDealsPanel — DevExpress grid with summary text (open count, pipeline value, weighted)
- [ ] CrmPipelinePanel — Kanban-style columns per open stage with deal cards
- [ ] CrmDashboardPanel — KPI tiles (sales + customers) + per-stage progress bars
- [ ] Ribbon tab "CRM" with Actions + Data + Panels groups (9 panel toggles)

### 7.9 Admin

#### Users Panel
- [ ] User grid loads from app_users
- [ ] Role dropdown bound to DB roles
- [ ] New user creates record
- [ ] Edit user modifies record
- [ ] Delete user removes record
- [ ] Extended user fields (department, title, phone, mobile, company) visible — `AppUserTests.Defaults_AreCorrect`, `AppUserExtendedTests.PropertyChanged_*`
- [ ] UserType dropdown shows all 5 types — `UserTypesTests.All_Has5Types`
- [ ] Protected users (System, Service) cannot be deleted — `AppUserTests.IsProtected_System_True`, `AppUserTests.IsProtected_Service_True`, `AppUserTests.IsProtected_Standard_False`
- [ ] Inactive users show at 0.5 opacity, protected users show bold — `AppUserTests.StatusColor_Active_Green`, `AppUserTests.StatusColor_Inactive_Grey`

#### Roles & Permissions Panel
(See Section 2.1 for the canonical RBAC items. Per-panel specifics below.)
- [ ] Split view: roles grid left, permissions tree + site checkboxes right
- [ ] RoleRecord PermissionSummary counts — `RoleRecordTests.PermissionSummary_*`, `RoleRecordExtendedTests.PermissionSummary_VariousCounts`
- [ ] RoleRecord defaults (empty name, priority 0) — `RoleRecordTests.Defaults_AreCorrect`
- [ ] RoleRecord PropertyChanged fires — `RoleRecordTests.PropertyChanged_Fires_OnNameChange`, `RoleRecordExtendedTests.PropertyChanged_*`
- [ ] RoleRecord permission boolean PropertyChanged (DevicesView/Edit/Delete, SwitchesView, LinksView, BgpView/Sync, VlansView, TasksView, ServiceDeskView/Sync) — `RoleRecordExtendedTests` (11 permission PropertyChanged tests)
- [ ] RoleRecord DevicesViewReserved default true — `RoleRecordExtendedTests.DevicesViewReserved_DefaultTrue`, `RoleRecordExtendedTests.DevicesViewReserved_PropertyChanged`
- [ ] RoleRecord DetailUsers default empty — `RoleRecordTests.DetailUsers_DefaultEmpty`
- [ ] RolePermission defaults — `RoleRecordTests.RolePermission_Defaults`
- [ ] RoleUserDetail defaults + set properties — `RoleRecordTests.RoleUserDetail_Defaults`, `RoleRecordExtendedTests.RoleUserDetail_CanSetProperties`
- [ ] UserPermissionDetail defaults — `RoleRecordTests.UserPermissionDetail_Defaults`

#### Lookup Values Panel
- [ ] Category/Value grid loads — `LookupItemTests.Defaults_AreCorrect`
- [ ] CRUD works — `LookupItemTests.AllProperties_FirePropertyChanged`
- [ ] SortOrder column respected
- [ ] LookupItem PropertyChanged fires on all fields (Id, Category, Value, SortOrder, GridName, Module) — `LookupItemTests.PropertyChanged_Fires_*`
- [ ] LookupItem ParentId always null (flat list) — `LookupItemTests.ParentId_AlwaysNull`, `LookupItemTests.ParentId_NullEvenWithAllFieldsSet`

#### SSH Logs Panel
- [ ] SSH session logs display — `SshLogEntryTests.Defaults_AreCorrect`
- [ ] Filterable by switch/date
- [ ] Duration computed property — `SshLogEntryTests.Duration_*`
- [ ] StatusIcon success/failure — `SshLogEntryTests.StatusIcon_Success_CheckMark`, `SshLogEntryTests.StatusIcon_Failure_CrossMark`
- [ ] SshLogEntry PropertyChanged fires on all fields — `SshLogEntryTests.PropertyChanged_*`
- [ ] SshLogEntry SwitchId nullable — `SshLogEntryTests.SwitchId_CanBeNull`

#### App Logs Panel
- [ ] Application log entries display — `AppLogEntryTests.Defaults`, `AppLogEntryTests.DisplayTime_FormatsCorrectly`, `AppLogEntryTests.PropertyChanged_AllFields`

#### Jobs Panel
- [ ] Job schedules grid shows 3 job types
- [ ] Enable/Disable toggle works
- [ ] Interval column editable
- [ ] Run Now button triggers immediate execution
- [ ] Job history grid shows past runs

#### Ribbon Config Panel
(See Section 3.2 for 3-layer override canonical items.)
- [ ] Ribbon pages/groups/items display in flat grid — `RibbonConfigTests.RibbonPageConfig_Defaults`, `RibbonConfigTests.RibbonGroupConfig_Defaults`, `RibbonConfigTests.RibbonItemConfig_Defaults`, `RibbonConfigExtendedTests.*`
- [ ] CRUD on ribbon items works — `RibbonConfigTests.RibbonItemConfig_PropertyChanged_AllSetters`, `RibbonConfigExtendedTests.RibbonItemConfig_PropertyChanged_AllProperties`
- [ ] RibbonPageConfig PropertyChanged all properties — `RibbonConfigExtendedTests.RibbonPageConfig_PropertyChanged_AllProperties`
- [ ] RibbonGroupConfig PropertyChanged all properties — `RibbonConfigExtendedTests.RibbonGroupConfig_PropertyChanged_AllProperties`
- [ ] UserRibbonOverride defaults + set + IsHidden — `RibbonConfigExtendedTests.UserRibbonOverride_*`
- [ ] System items cannot be deleted (is_system=TRUE)

#### Active Directory Integration
- [ ] AD Browser panel opens from Admin > Panels > AD Browser
- [ ] AdConfig IsConfigured with domain — `AdModelsTests.AdConfig_IsConfigured_*`, `AdModelsExtendedTests.AdConfig_IsConfigured_*`
- [ ] AdConfig defaults (empty domain/ou/account/password, no SSL) — `AdModelsTests.AdConfig_Defaults`, `AdModelsExtendedTests.AdConfig_Defaults`, `AdModelsExtendedTests.AdConfig_AllProperties`
- [ ] AdUser defaults (empty strings, disabled, not imported) — `AdModelsTests.AdUser_Defaults`, `AdModelsExtendedTests.AdUser_Defaults`, `AdModelsExtendedTests.AdUser_IsImported_DefaultFalse`, `AdModelsExtendedTests.AdUser_Enabled_DefaultFalse`
- [ ] AdUser set all properties — `AdModelsTests.AdUser_SetAllProperties`, `AdModelsExtendedTests.AdUser_SetAllProperties`
- [ ] Browse AD button queries configured domain via System.DirectoryServices.AccountManagement
- [ ] AD users shown in read-only grid with ObjectGuid, DisplayName, Email, Department, Enabled
- [ ] IsImported column shows which AD users are already linked
- [ ] Import Selected creates app_users with user_type=ActiveDirectory, ad_guid linked
- [ ] Sync All updates display name, email, phone, active status from AD
- [ ] AD config stored in integrations table

#### Schema Migration Management
- [ ] Migrations panel shows all applied (green) and pending (amber) migrations
- [ ] Applied migrations show timestamp and duration
- [ ] Apply Pending button runs all pending .sql files in transaction
- [ ] Migration history recorded in migration_history table
- [ ] Refresh reloads from DB + filesystem

#### Database Backup & Restore
- [ ] Backup panel with Full Backup button and output path browser
- [ ] pg_dump runs with connection params from DSN
- [ ] Backup history grid shows type, file path, size, status, timestamp
- [ ] Scheduled backup via db_backup job type in job_schedules
- [ ] Failed backups logged with error message

#### Soft-Delete Purge
- [ ] Purge panel shows tables with soft-deleted record counts
- [ ] Purge Selected deletes from one table, Purge All clears all
- [ ] Confirmation dialog before purge
- [ ] Count refreshes after each purge operation

#### Location Management
- [ ] Locations panel with Countries grid (Code, Name, SortOrder) — `LocationModelExtendedTests.Country_Defaults`, `LocationModelExtendedTests.Country_AllProperties_FirePropertyChanged`
- [ ] Regions grid filtered by selected country — `LocationModelExtendedTests.Region_Defaults`, `LocationModelExtendedTests.Region_AllProperties_FirePropertyChanged`
- [ ] Postcode defaults and PropertyChanged — `LocationModelExtendedTests.Postcode_Defaults`, `LocationModelExtendedTests.Postcode_AllProperties_FirePropertyChanged`
- [ ] Postcode latitude/longitude precision — `LocationModelExtendedTests.Postcode_Latitude_NullToValue`, `LocationModelExtendedTests.Postcode_Longitude_NegativeValue`, `LocationModelExtendedTests.Postcode_LatLon_Precision`
- [ ] Add/Delete/Save for both countries and regions
- [ ] Seed data: GBR, USA, AUS, NZL

#### Reference Number System
- [ ] Reference Config panel shows entity types with prefix/suffix/pad/next value — `ReferenceConfigTests` (10 tests)
- [ ] SampleOutput column shows live preview (e.g. DEV-000001)
- [ ] Auto-save on cell edit
- [ ] next_reference() PG function for atomic sequence generation
- [ ] Seeded: device, ticket, asset, task

#### Podman Container Management
- [ ] Podman panel shows containers with Name, Image, State, Status
- [ ] Start/Stop/Restart buttons for selected container
- [ ] View Logs button shows last 100 lines in text area
- [ ] Refresh reloads container list
- [ ] Graceful handling if podman not installed
- [ ] ContainerInfo StateColor all states (running=green, exited=red, paused=amber, others=grey) — `ContainerInfoExtendedTests.StateColor_AllCases`
- [ ] ContainerInfo IsRunning all states — `ContainerInfoExtendedTests.IsRunning_AllCases`
- [ ] ContainerInfo defaults all empty — `ContainerInfoExtendedTests.Defaults_AllEmpty`
- [ ] ContainerInfo PropertyChanged fires on Status/Created/Ports/CpuPercent/MemUsage — `ContainerInfoExtendedTests.PropertyChanged_*`
- [ ] ContainerInfo full scenario (postgres container) — `ContainerInfoExtendedTests.FullScenario_PostgresContainer`

#### Scheduler / Calendar
- [ ] Scheduler panel with Day/Week/Month view navigation
- [ ] Period label updates on view change
- [ ] Resource dropdown filters by technician
- [ ] New Appointment creates with current time, saves to DB
- [ ] Delete with confirmation
- [ ] appointments + appointment_resources tables — `AppointmentExtendedTests.AppointmentResource_PropertyChanged_AllProperties`
- [ ] Links to tasks (task_id) and SD tickets (ticket_id) — `AppointmentExtendedTests.PropertyChanged_TaskId_Fires`, `AppointmentExtendedTests.PropertyChanged_TicketId_Fires`
- [ ] Appointment PropertyChanged fires on all fields — `AppointmentExtendedTests` (14 PropertyChanged tests)
- [ ] Appointment nullable fields (ResourceId, TaskId, TicketId, CreatedBy) — `AppointmentExtendedTests.ResourceId_CanBeNull`, `AppointmentExtendedTests.TaskId_CanBeNull`, `AppointmentExtendedTests.TicketId_CanBeNull`, `AppointmentExtendedTests.CreatedBy_CanBeNull`

#### Notification Preferences Panel
- [ ] My Notifications panel opens from Admin > Identity > My Notifications
- [ ] Grid shows all 8 event types with channel dropdown (toast/email/both/none) and enabled toggle
- [ ] Missing preferences auto-filled with defaults (toast, enabled)
- [ ] Save All saves all preferences at once
- [ ] Auto-save on cell change

### 7.10 Dashboard Module

- [ ] KPI cards (Devices, Switches, Links, VLANs, Tasks, SD, System Health)
- [ ] Notification center shows recent alerts + actionable items
- [ ] Recent activity feed (last 20 auth_events + sync_log)
- [ ] Refresh button reloads all dashboard data
- [ ] Excel export on all grids (DX XlsxExportOptions)
- [ ] PDF export on all grids (DX PdfExportOptions)
- [ ] CSV export on all grids (DX TableView.ExportToCsv)
- [ ] Dashboard panel is the first tab (before Devices) — cross-ref Section 4.10

---

## 7.X Networking Engine (Phases 1-6 COMPLETE — 2026-04-18)

Multi-tenant source-of-truth buildout living under the `net.*` schema (42 tables). Replaces the single-customer `public.switches` / `public.p2p_links` / `public.servers` shape with a proper tenanted + versioned model.

**Cross-refs:** [docs/NETWORKING_BUILDOUT_PLAN.md](NETWORKING_BUILDOUT_PLAN.md) (23-phase plan), [docs/NETWORKING_RIBBON_AUDIT.md](NETWORKING_RIBBON_AUDIT.md) (ribbon action inventory).

### 7.X.1 Universal Entity Base (Phase 1)

- [ ] `net.entity_status` enum: Planned / Reserved / Active / Deprecated / Retired — `HierarchyModelTests.EntityStatus_LifecycleTransitions`
- [ ] `net.lock_state` enum: Open / SoftLock / HardLock / Immutable — `HierarchyModelTests.LockState_Progression`
- [ ] All 42 `net.*` tables carry the 17 universal base columns (id, organization_id, status, lock_state, lock_reason, locked_by, locked_at, created_at/by, updated_at/by, deleted_at/by, notes, tags jsonb, external_refs jsonb, version) — migration 084
- [ ] Optimistic concurrency on Update via `WHERE version = @ver` — `ConcurrencyException` thrown on mismatch

### 7.X.2 Hierarchy (Phase 2)

- [ ] Region → Site → Building → Floor → Room → Rack tree (9 tables including profiles) — migration 085
- [ ] Immunocore seed: 1 UK region, 1 Milton Park site, 5 MEP-91/92/93/94/96 buildings
- [ ] `HierarchyRepository` with List/Get/Create/Update/SoftDelete for every tier — `HierarchyModelTests`
- [ ] REST `/api/net/regions` + `/sites` + `/buildings` + `/floors` + `/rooms` + `/racks` (CRUD on all 6 tiers)
- [ ] WPF `HierarchyTreePanel` (DX TreeListControl, KeyFieldName=Id + ParentFieldName=ParentId)
- [ ] `HierarchyDetailDialog` one-dialog CRUD for all 6 levels, factory methods per mode
- [ ] Right-click tree menu: New child (type-aware) / Edit / Delete (soft-delete with confirmation)
- [ ] `HierarchyValidation` static helpers — per-type rules (code required, parent FK required in New mode) — 17 tests in `HierarchyValidationTests`

### 7.X.3 Numbering Pools (Phase 3)

- [ ] 16 pool tables: asn_pool + asn_block + asn_allocation; ip_pool + subnet + ip_address; vlan_pool + vlan_block + vlan + vlan_template; mlag_domain_pool + mlag_domain; mstp_priority_rule + rule_step + priority_allocation; reservation_shelf — migration 086
- [ ] `net.subnet` carries GIST EXCLUDE on `(organization_id WITH =, network inet_ops WITH &&)` — no two active subnets may overlap (smoke-tested live)
- [ ] `AllocationService` (integer allocation — ASN / VLAN / MLAG) with `pg_advisory_xact_lock(stable_hash(container_id))` serialisation — `AllocationServiceTests` (7 pure + 6 live-DB)
- [ ] `IpAllocationService` (IPv4 next-free + subnet-carve with gap-finder) — `IpMathTests` (20) + `IpAllocationServiceTests` (9 live-DB)
- [ ] `IpMath6` + IPv6 path via `UInt128` arithmetic (RFC 4291 — no reserved addresses) — `IpMath6Tests` (14) + `IpAllocationServiceV6Tests` (6 live-DB)
- [ ] Reservation shelf with cool-down: `RetireAsync(resource, cooldown)`, `IsOnShelfAsync` — allocation skips shelved values until `available_after > now()`
- [ ] Phase-3 invariants: PoolExhaustedException / AllocationContainerNotFoundException / AllocationRangeException
- [ ] REST under `/api/net/*-pools/*-blocks/*-allocations` — 20+ endpoints, 4 permission codes (NetPoolsRead/Write/Delete/Allocate)
- [ ] Immunocore numbering imported: 5 ASNs (65112/65121/65132/65141/65162) + 5 loopback /24 subnets + 63 VLANs — migration 087
- [ ] WPF `PoolsTreePanel` with utilisation bars (DX ProgressBarEdit)
- [ ] `PoolDetailDialog` covers every pool/block/template tier; `AllocateDialog` (5 modes: ASN/VLAN/MLAG/IP/subnet-carve)
- [ ] `PoolValidation` static helpers (first <= last, VLAN/MLAG 1..4094, etc.) — 15 tests in `PoolValidationTests`

### 7.X.4 Device Catalog (Phase 4)

- [ ] 7 tables: device_role (12 Immunocore roles) + device + module + port + aggregate_ethernet + loopback + building_profile_role_count — migration 088
- [ ] `DevicesRepository` with List/Get/Create/Update/SoftDelete for every tier — `DeviceModelTests` (10)
- [ ] `Device.management_ip` + `ssh_*` + `last_ping_*` mirror `public.switches` 1:1 for dual-write sanity
- [ ] `switches` → `net.device` import with role-prefix disambiguation (hostname contains "CORE" → prefer L1Core over L1SW) — migration 089
- [ ] Bidirectional dual-write trigger with txn-scoped reentrancy guard (`set_config('net.in_dual_write', 'on', true)`) — migration 090
- [ ] `GetSwitchesFromNetDeviceAsync` reads from `net.device` projected to `SwitchRecord`, feature-flagged via `CENTRAL_USE_NET_DEVICE=1` — `DeviceReaderParityTests` (3 live-DB)
- [ ] Parity test: every imported switch yields an identical SwitchRecord through both readers
- [ ] Device naming template on `net.device_role` — `DeviceNamingService` with recognised tokens (region / site / building / rack / role / instance) — 11 tests in `DeviceNamingServiceTests`

### 7.X.5 Unified Link Model (Phase 5)

- [ ] 3 tables: link_type (7 seeded) + link + link_endpoint — migration 091
- [ ] link_type naming templates per type — `LinkNamingService` — 9 tests in `LinkNamingServiceTests`
- [ ] 2,826 legacy P2P/B2B/FW rows imported to unified model — migration 092
- [ ] 9 SQL parity tests between legacy and unified tables — `LinkImportParityTests`
- [ ] Per-type extensions stored in `link.config_json` (B2B: tx/rx/media/speed; P2P: desc_a/desc_b) — survives import

### 7.X.6 Servers + NICs (Phase 6)

- [ ] 3 tables: server_profile (Server4NIC seeded) + server + server_nic (MlagSide A/B/None) — migration 094
- [ ] `ServersRepository` with full CRUD + `RecordPingAsync` fast-path — `ServerModelTests` (4)
- [ ] `ServerNamingService` + `ServerNamingContext` record — 8 tests in `ServerNamingServiceTests`
- [ ] `ServerCreationService.CreateWithFanOutAsync` acceptance:
  - [ ] Allocates ASN from block (if given) — `ServerCreationServiceTests.CreateWithFanOut_AllocatesAsnLoopbackAndFourNicsWithAlternatingSides`
  - [ ] Allocates loopback IP from subnet (if given)
  - [ ] Creates N NICs per profile.NicCount with alternating MLAG sides {A, B, A, B}
  - [ ] Each NIC points at correct core (side A or B) looked up by role in building
  - [ ] Optional pieces skip cleanly when not provided — `CreateWithFanOut_OptionalPiecesSkipCleanlyWhenNotProvided`
  - [ ] One-core building leaves side-B NICs with null TargetDeviceId — `CreateWithFanOut_OneCoreBuildingPutsSideBAsNull`
  - [ ] Unknown profile throws `ServerProfileNotFoundException` — `CreateWithFanOut_ProfileNotFound_Throws`
- [ ] Legacy `public.servers` → `net.server` import; 160 rows dedupe to 31 distinct hostnames — migration 095
- [ ] Bidirectional dual-write trigger `public.servers` ↔ `net.server` — migration 096 (smoke-tested both directions live)
- [ ] WPF `ServerGridPanel` reads from `net.server` with joined building / profile / ASN / loopback / NIC count
- [ ] `ServerValidation` static helpers — 12 tests in `ServerValidationTests`

### 7.X.7 Cross-cutting Chunks (A/B/C)

**Chunk A — Device naming templates** (commit `3c8da8a6e`)
- [ ] `net.device_role.naming_template` column with per-role Immunocore seeds (Core → `{building_code}-CORE{instance}`, L1Core → `{building_code}-L1-CORE{instance}`, etc.)
- [ ] `DeviceNamingService.Expand` — token substitution, zero-pad instance, unknown tokens pass-through

**Chunk B — Networking ribbon audit** (commit `e1fccd2c6`)
- [ ] `NetworkingRibbonRegistrar.BuildRibbon` — registration extracted to net10.0 assembly so test project can exercise it
- [ ] Every action button publishes a `NavigateToPanelMessage` or `RefreshPanelMessage` — `NetworkingRibbonAuditTests.EveryActionButton_PublishesAMessage` (placeholder-lambda canary)
- [ ] Every action button declares a permission — `EveryActionButton_HasAPermissionCode`
- [ ] 14 theory rows pinning each (group, button) → (target panel, action payload)
- [ ] VLAN toggle serialises bool in action payload (`action:showDefault:true|false`)

**Chunk C — Dialog validation extraction** (commit `ce7c1edfa`)
- [ ] `HierarchyValidation` + `PoolValidation` + `AllocationValidation` static helpers in libs/engine/Net/Dialogs/
- [ ] `HierarchyDetailDialog` + `PoolDetailDialog` rewired to consume the validators (one source of truth)
- [ ] 44 validation tests (17 hierarchy + 15 pool + 12 allocation)

### 7.X.8 Naming template invariant (standing)

Per the plan amendment (commit `758ccaa98`), every new *-type catalog row must carry:
- [ ] `naming_template varchar(255)` column seeded with sensible default
- [ ] `XNamingService` + `XNamingContext` record with documented tokens
- [ ] Unit tests covering happy + edge paths
- [ ] CRUD REST coverage
- [ ] No placeholder `() => { }` lambdas in ribbon registrations
- [ ] Dialog validation extracted + tested

Compliance by *-type table: `link_type` ✓, `device_role` ✓, `server_profile` ✓. Pending coverage: `building_profile`, `vlan_template`, `mstp_priority_rule` (Phase 7).

### 7.X.9 Phase 10 — SERVICE-SIDE COMPLETE (2026-04-18)

**Every Phase 10 service-side deliverable shipped.** 48-commit arc from CLI flavor catalog (commit `74d0523b6`) through saved views (`1f3da348e`) plus this closure docs commit. 290 Rust unit tests + 76 live-DB integration tests across 7 suites, 0 failures. .NET ApiClient build clean.

See the slice table in `docs/NETWORKING_BUILDOUT_PLAN.md` §Phase 10 for the per-commit breakdown. This section is the acceptance-bar checklist — each bullet below is a behaviour an operator can verify manually or via integration test.

**Config generation (byte-for-byte parity with legacy ConfigBuilderService):** byte-parity achieved. Once a tenant seeds Gateway / Vrrp / DhcpRelayTarget rows, the output matches line-for-line. Lives in three Rust files: `services/networking-engine/src/cli_flavor.rs`, `config_gen.rs`, `dhcp_relay.rs`.

**CLI flavor foundation** (commit `74d0523b6`, migration 102_net_cli_flavors.sql)
- [ ] `FLAVORS` const catalog — 6 entries: PicOS (Ga, default-on), Cisco NX-OS / Cisco IOS / Arista EOS / Junos OS / FRR (all Stub, default-off)
- [ ] `net.tenant_cli_flavor` — per-tenant enable + is_default, partial unique index enforces one default per tenant
- [ ] `net.rendered_config` — render history with body_sha256 + previous_render_id SHA chain
- [ ] `cli_flavor::list_flavors` / `set_flavor_config` / `resolve_for_device`
- [ ] Dispatcher `config_gen::render_device` returns clean 400 "No renderer implemented for flavor X" for Stub flavors rather than emitting wrong-syntax CLI
- [ ] REST: GET `/api/net/cli-flavors`, PUT `/api/net/cli-flavors/:code`, POST `/api/net/devices/:id/render-config`
- [ ] `FLAVORS` invariants — codes unique, PicOS is position 0, only one Ga today, stubs default off, vendors from known set

**PicOS renderer — section pipeline (in legacy-step emit order):**
1. [ ] Header comment `# Config for {hostname} — generated {rfc3339}` + `# Flavor: PicOS 4.6 (FS N-series)`
2. [ ] `set system hostname "{resolved}"` — hostname is **parametric** via `net.device_role.naming_template` + hierarchy codes (region / site / building / rack / role) + `device_code` → `{instance}` (commit `56aa5ab40`); falls back to stored `net.device.hostname` when template is NULL / empty / whitespace-only expansion
3. [ ] `set ip routing enable true` — universal fixed emit (commit `7861abc86`)
4. [ ] **QoS / CoS preset** — 55-line forwarding-class + scheduler + classifier + scheduler-profile block held in `QOS_PRESET_LINES` const, line-count-drift asserted in tests (commit `08153f37f`)
5. [ ] **Per-port QoS bindings** — two lines per physical port (`classifier "qos-dscp-classifier"` + `scheduler-profile "qos-flex-profile"`); breakout sub-interfaces excluded via `breakout_parent_id IS NULL` (commit `69b273114`)
6. [ ] **Voice-VLAN preset** — 4-line Avaya block (OUI c8:1f:ea + local-priority 6 + DSCP 46) held in `VOICE_VLAN_PRESET_LINES` const (commit `439eb92c6`)
7. [ ] **Loopback `lo0`** — sourced from `net.ip_address` joined to `net.subnet` where `subnet_code ILIKE 'LOOPBACK%'` and `assigned_to_id = device_id` (commit `7903d5fe4`)
8. [ ] **Management SVI `vlan-152`** — sourced from `net.device.management_ip`, inet text split into address + prefix via `split_inet_text` (commit `7903d5fe4`)
9. [ ] **VLAN catalog** — `set vlans vlan-id N description "..."` per VLAN, followed by `set vlans vlan-id N l3-interface "vlan-N"` when a matching SVI exists in `ctx.l3_svis` (commit `ac80e3e9b`)
10. [ ] **L3 VLAN SVIs** — every `net.ip_address` assigned to this device whose subnet is wired to a VLAN (LOOPBACK filtered) (commit `d2567b3b6`)
11. [ ] **BGP scalar block** — local-as from `net.asn_allocation` via `net.device.asn_allocation_id`, ebgp-requires-policy false, router-id from loopback, ipv4-unicast redistribute connected, multipath ebgp maximum-paths 4 (commit `5a7852901`)
12. [ ] **BGP neighbors** — derived from `net.link_endpoint` for P2P / B2B link-types only; unmodelled cross-building B2B peers emit `remote-as "?"` to match legacy behaviour (commit `54776cc7d`)
13. [ ] **MSTP bridge-priority** — from `net.mstp_priority_allocation` keyed by device_id (commit `f6b029c0d`)
14. [ ] **MLAG peer-link** — domain id via `net.mlag_domain` scoped to device's building; peer-link interface from the `MLAG-Peer` link's local endpoint. Each half optional; partial state emits whichever half is resolvable (commit `f6b029c0d`)
15. [ ] **VRRP VIPs** — one line per (VLAN, VIP) from `net.ip_address` with `assigned_to_type = 'Vrrp'` in subnets belonging to VLANs this device L3-terminates; VRID defaults to 1, per-VIP override via `ip_address.tags.vrid` JSON path (commit `f98a058a9`)
16. [ ] **DHCP relay** — one line per (VLAN, server_ip) from `net.dhcp_relay_target` (migration 103), priority-ordered within each VLAN; scoped to SVI VLANs (commit `aadc16afd`)
17. [ ] **Merged ports section** — description from `net.link_endpoint` + L2 rules from `net.port` (port-mode + native-vlan-id) interleaved by interface name via `BTreeMap` so each port's lines cluster together (commits `992cd2f9d` + `9583b7e75` + `ee155920a`); routed / unset ports skip L2 rules
18. [ ] **Static default route** — `set protocols static route 0.0.0.0/0 next-hop X`; next-hop sourced from `net.ip_address` with `assigned_to_type = 'Gateway'` in the subnet containing `management_ip` (using the `>>=` supernet operator); tenants without seeded Gateway rows skip the line (commit `59c13011e`)
19. [ ] `set protocols lldp enable true` — universal fixed emit (commit `7861abc86`)

**Pure-function renderer invariants:**
- [ ] `PicosRenderer::render(&ctx)` takes `&DeviceContext` only — no DB access inside the render path, all fetches happen in `fetch_context`
- [ ] Per-section fixture helpers (one per code path: `fixture`, `fixture_with_addrs`, `fixture_with_bgp`, `fixture_with_bgp_and_neighbors`, `fixture_with_mstp_mlag`, `fixture_with_svis`, `fixture_with_vlans_and_svis`, `fixture_with_ports`, `fixture_with_l2_rules`, `fixture_with_port_cfg`, `fixture_with_qos_bindings`, `fixture_with_gateway`, `fixture_with_vrrp`, `fixture_with_dhcp_relay`) so each section has isolated coverage
- [ ] `split_inet_text` parses Postgres `inet` text cleanly, returns `None` for malformed input (`"10.255.91.2"`, `"10.255.91.2/abc"`, `"/32"`, `""`)
- [ ] `escape_picos` escapes embedded `"` and `\` in user-supplied strings (VLAN descriptions, link-name auto-generated peer labels)
- [ ] `resolve_device_hostname` — token expansion via `naming::expand_device`; fallback to stored when template missing / empty / whitespace-only; non-numeric `device_code` yields empty `{instance}`
- [ ] Each section has an order-assertion test pinning its position in the pipeline (e.g. VRRP between MLAG and ports; DHCP between VRRP and static route; LLDP last)
- [ ] `QOS_PRESET_LINES` + `VOICE_VLAN_PRESET_LINES` each have a count-drift assertion so silent divergence from the customer's documented policy trips CI

**Render lifecycle — write / read / diff / fan-out:**
- [ ] `render_device(pool, org, device)` — dry-run path; returns `RenderedConfig` in memory without writing to `net.rendered_config` (use from preview flows that shouldn't pollute history)
- [ ] `render_device_persisted(pool, org, device, rendered_by)` — render + write + chain to previous render's id + measure wall-clock duration (commit `87bcb3fef`)
- [ ] `RenderedConfig` extra fields (`id`, `previousRenderId`, `renderDurationMs`) skip serialisation when `None` so dry-run JSON responses stay clean
- [ ] `POST /api/net/devices/:id/render-config` → single-device render + persist via `render_device_persisted`
- [ ] `GET /api/net/devices/:id/renders?organizationId=X[&limit=N]` — list recent renders as `RenderedConfigSummary[]` (no body); limit clamps to `[1, 500]` (default 50) via `clamp_render_list_limit` (commit `2244aacbf`)
- [ ] `GET /api/net/renders/:id?organizationId=X` — full `RenderedConfigRecord` including body; scoped by tenant
- [ ] `GET /api/net/renders/:id/diff?organizationId=X` — `RenderDiff { added, removed, unchangedCount }` computed by pure `compute_line_diff` helper against the chained previous render; first-ever render returns full body as added with zero removed (commit `fd26b7368`)
- [ ] `POST /api/net/buildings/:id/render-configs` — building turn-up pack: `BuildingRenderResult` with `totalDevices` / `succeeded` / `failed` counters; per-device errors tolerated (commit `f14811e5b`)
- [ ] `POST /api/net/sites/:id/render-configs` — site turn-up pack: rolls up across every building in the site via shared `render_device_list` core; ordered `(building_code, hostname)` (commit `1f8f8508c`)

**DHCP relay target CRUD** (commit `1b7896f7a`)
- [ ] `GET /api/net/dhcp-relay-targets?organizationId=X[&vlanId=Y]` — list with optional VLAN filter
- [ ] `GET /api/net/dhcp-relay-targets/:id?organizationId=X` — fetch one
- [ ] `POST /api/net/dhcp-relay-targets` → 201 + body
- [ ] `PUT /api/net/dhcp-relay-targets/:id` — optimistic concurrency via `version` column; "not found / already updated / wrong tenant" collapsed to a single 400
- [ ] `DELETE /api/net/dhcp-relay-targets/:id` — soft-delete (deleted_at stamp); returns 204
- [ ] Priority defaults to 10 when clients omit it (Immunocore convention, pinned by test)
- [ ] Priority must be non-negative — validated at the API so a `-1` typo can't break the renderer's primary-before-backup ordering
- [ ] `server_ip` serialises as bare host (`"10.11.120.10"`) not CIDR — matches what the renderer emits
- [ ] Every mutation writes an audit entry in the same transaction as the DB write so neither can land without the other

**Still open:**
- [ ] Per-port QoS binding interleaving with description + L2 lines — deferred; QoS bindings reference forward-declared classifier + scheduler-profile names so keeping them near the QoS preset remains the simpler correctness story.
- [ ] Immunocore seed import migration shipped (commit `104053191`), but operators need to run it against their tenant to actually populate the Gateway / Vrrp / DhcpRelayTarget rows.

### 7.X.10 Bulk Export — CSV (Phase 10)

9 entities addressable via `GET /api/net/<entity>/export`:

- [ ] **Devices** — `hostname, role_code, building_code, site_code, management_ip, asn, status, version` (hostname-sorted)
- [ ] **VLANs** — `vlan_id, display_name, description, scope_level, template_code, block_code, status` (block_code + vlan_id sorted)
- [ ] **IP addresses** — `address, subnet_code, assigned_to_type, assigned_to_id, is_reserved, status` (bare host, subnet-grouped)
- [ ] **Links** — cross-tab A/B columns (`device_a, port_a, ip_a, device_b, port_b, ip_b`) matching legacy P2P/B2B/FW layout
- [ ] **Servers** — `hostname, profile_code, building_code, asn, loopback_ip, management_ip, nic_count, status` (NIC count via LATERAL COUNT(*))
- [ ] **Subnets** — `subnet_code, display_name, network, vlan_id, pool_code, scope_level, status`
- [ ] **ASN allocations** — `asn, allocated_to_type, allocated_to_hostname, block_code, allocated_at, status` (dual-joins device + server for hostname)
- [ ] **MLAG domains** — `domain_id, display_name, pool_code, scope_level, scope_entity_id, status`
- [ ] **DHCP relay targets** — `vlan_id, server_ip, priority, linked_ip_address_id, notes, status` (priority-ordered within VLAN)

Shared invariants:
- [ ] RFC 4180 escaping (embedded `,` `"` CR LF all wrap + double-quote)
- [ ] `Content-Disposition: attachment; filename="<entity>.csv"` for browser download
- [ ] Joined-through display codes rather than UUIDs (e.g. `subnet_code`, `template_code`, `pool_code`) so output round-trips cleanly through spreadsheets
- [ ] Deterministic ordering — repeated export of the same tenant state produces byte-identical output

### 7.X.11 Bulk Import — CSV (Phase 10)

6 entities addressable via `POST /api/net/<entity>/import` with `dryRun=true|false`:

- [ ] **Devices** — required: hostname (unique per tenant); optional: role_code, building_code, management_ip, status. ASN ignored on apply (use allocation CRUD)
- [ ] **VLANs** — required: vlan_id (1-4094), display_name, block_code; duplicate detection by `(block_code, vlan_id)`
- [ ] **Subnets** — required: subnet_code, display_name, network (valid CIDR), pool_code; vlan_id ignored on apply
- [ ] **Servers** — required: hostname; optional: profile_code, building_code. ASN/loopback/nic_count ignored on apply
- [ ] **DHCP relay targets** — required: vlan_id (must exist), server_ip; first-wins on duplicate numeric vlan_ids across multiple blocks
- [ ] **Links** — required: link_code, link_type, device_a, device_b (hostnames resolve). Cross-tab: 1 CSV row → 1 `net.link` + 2 `net.link_endpoint` rows in one transaction. IP columns ignored on apply

Shared invariants:
- [ ] Hand-rolled RFC 4180 parser in `bulk_import::parse_csv` — accepts LF/CRLF/CR, doubled-quote unescape, rejects unterminated quoted fields AND content-after-closing-quote
- [ ] `dryRun=true` default runs validate-only; `dryRun=false` commits
- [ ] Any per-row validation failure → `applied=false` with the whole batch rolled back
- [ ] Any DB-level failure mid-apply → tx rolls back, no partial writes
- [ ] `ImportValidationResult { totalRows, valid, invalid, dryRun, applied, outcomes[] }` envelope with per-row `{rowNumber, ok, errors[], identifier}`
- [ ] FK pre-fetch — one query per dimension regardless of row count
- [ ] Audit entry per created row in the same transaction as the INSERT
- [ ] RBAC: `write:<EntityType>` at Global (creates don't have an id for hierarchy resolution)

### 7.X.12 Bulk Edit (Phase 10)

5 entities via `POST /api/net/<entity>/bulk-edit`:

- [ ] **Devices** — whitelist: status, role_code, building_code, management_ip, notes
- [ ] **VLANs** — whitelist: display_name, description, scope_level, status, template_code, notes
- [ ] **Subnets** — whitelist: display_name, scope_level, status, notes
- [ ] **Servers** — whitelist: profile_code, building_code, management_ip, status, notes
- [ ] **DHCP relay targets** — whitelist: priority, status, notes

Shared invariants:
- [ ] `hostname` / `vlan_id` / `network` / `server_ip` explicitly gated out — editing at scale would turn a typo into a Sev-1
- [ ] Version-checked UPDATE per row; concurrent-write → whole batch rolls back
- [ ] Same-value-for-all across the selected id set (per-row different values is what bulk-import is for)
- [ ] `BulkEditResult { total, succeeded, failed, dryRun, applied, outcomes[] }` envelope
- [ ] RBAC: `write:<EntityType>` at each row's scope (hierarchy expansion kicks in for Device/Server/Building/Site)
- [ ] Audit entry per row, action="BulkEdited", source="bulk_edit"

### 7.X.13 RBAC Scoped Policy Engine (Phase 10)

- [ ] Migration 105 `net.scope_grant` — `(organization_id, user_id, action, entity_type, scope_type, scope_entity_id)` with CHECK constraints + partial-unique index for dedup
- [ ] `scope_grants::has_permission(pool, org, user_id, action, entity_type, entity_id)` resolver — single query matching Global + EntityId + hierarchy-expanded Region/Site/Building for entity types with a modelled hierarchy (Device, Server, Building, Site)
- [ ] `scope_grants::require_permission` helper — service-bypass on None user_id, Forbidden on denied, Ok on allowed
- [ ] `EngineError::Forbidden { user_id, action, entity_type }` → HTTP 403 + RFC 7807 `forbidden` code
- [ ] CRUD endpoints: `GET/POST /api/net/scope-grants`, `GET/DELETE /scope-grants/:id`, `GET /scope-grants/check` (dry-run resolver)
- [ ] **Meta-protection**: ScopeGrant CRUD itself gates on `read:ScopeGrant` / `write:ScopeGrant` / `delete:ScopeGrant`. Bootstrap: root admin uses service-bypass (no X-User-Id) or direct DB INSERT for the first `write:ScopeGrant` grant
- [ ] Enforcement wired into every state-changing surface:
  - [ ] `bulk_edit_devices` / `bulk_edit_vlans` / `bulk_edit_subnets` / `bulk_edit_servers` / `bulk_edit_dhcp_relay_targets`
  - [ ] `bulk_import::import_devices` / `import_vlans` / `import_subnets` / `import_servers` / `import_dhcp_relay_targets` / `import_links`
  - [ ] `DhcpRelayTarget` CRUD (read/write/delete per verb)
  - [ ] `ScopeGrant` CRUD (meta)
  - [ ] Config-gen renders: `render_device_config` (write:Device), `render_building_configs` (write:Building), `render_site_configs` (write:Site), `render_region_configs` (write:Region)
  - [ ] Render history: `list_device_renders` / `get_render_by_id` / `diff_render_by_id` (read:Device)
- [ ] Service-bypass: calls without `X-User-Id` skip enforcement; preserves backward compat during rollout
- [ ] Hierarchy expansion is type-dispatched — adding a new entity type to the resolver = one match arm in `fetch_entity_hierarchy`, no SQL change

### 7.X.14 XLSX Round-Trip (Phase 10)

12 endpoints, 6 entities × export.xlsx + import.xlsx:

- [ ] `xlsx_codec::csv_body_to_xlsx(csv, sheet_name)` — parses CSV, writes single-sheet workbook; first row bold; sheet name truncated to Excel's 31-char limit + banned chars (`/\*[]:?`) substituted to `_`
- [ ] `xlsx_codec::xlsx_bytes_to_csv(bytes)` — opens workbook via `calamine`, reads FIRST sheet only, serialises cell grid back to RFC 4180 CSV
- [ ] Float formatting strips trailing `.0` so VLAN id `120.0` from Excel lands as `"120"` for `i32::parse` downstream
- [ ] Content-Type on export: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- [ ] XLSX is a PURE TRANSPORT ADAPTER over the CSV pipeline — no per-entity XLSX code that could drift from CSV shape
- [ ] Same RBAC enforcement as the CSV counterpart — no new policy code needed per endpoint
- [ ] C# ApiClient `ExportXxxXlsxAsync` (byte[]) + `ImportXxxXlsxAsync` (byte[] + dryRun)

### 7.X.15 Global Search + Saved Views (Phase 10)

`GET /api/net/search?organizationId=X&q=...[&entityTypes=Device,Vlan][&limit=50]`:

- [ ] Query-time UNION across 6 entity types (Device / Vlan / Subnet / Server / Link / DhcpRelayTarget)
- [ ] `to_tsvector('english', ...) @@ plainto_tsquery('english', $q)` — tokenisation + stemming handled by Postgres
- [ ] Results ranked by `ts_rank` descending
- [ ] `entityTypes` comma filter narrows scan to the selected subset; unknown types silently dropped
- [ ] `limit` clamped to `[1, 500]` via `clamp_search_limit` (default 50)
- [ ] Empty `q` short-circuits to empty results (avoids full table scan)
- [ ] RBAC post-filter: each raw hit gets a `has_permission(read, entity_type, id)` check; unauthorised drop
- [ ] Organization_id is a hard wall — cross-tenant search returns empty

Migration 106 `net.saved_view` — per-user named queries via `GET/POST/PUT/DELETE /api/net/saved-views[/:id]`:

- [ ] `UNIQUE (organization_id, user_id, name)` — names unique in owner's namespace; two operators can both have a "Critical" view
- [ ] list returns ONLY caller's views (scoped by `X-User-Id`); service calls return empty (no admin backdoor)
- [ ] get/update/delete require `X-User-Id`; service calls reject with 400 rather than materialising orphan rows
- [ ] cross-user read returns 404 not 403 — avoids leaking existence of other users' views
- [ ] optimistic concurrency via `version` column collapsed into single UPDATE (WHERE user_id AND version → 0 rows = "not owned OR stale")
- [ ] `filters jsonb` unstructured so future UI facet additions need no schema change

### 7.X.16 WPF Operator Surface (Phase 10 UI, 2026-04-19+)

After the service side closed, this arc wires every Phase 10
engine endpoint into the WPF shell. Operators get a ribbon + panel
+ context-menu workflow for the full bulk / search / audit /
scope-grant surface without leaving the app. Cross-panel drill-down
uses a two-message pattern: `OpenPanelMessage(target)` to restore
the DockManager panel, then `NavigateToPanelMessage(target, payload)`
to drive state inside it.

**Bulk workspace** (`Central.Module.Networking.Bulk.BulkPanel`, commit `6d4f94846` + `cd7a306a8`)
- [ ] Entity combo covers all six bulk-capable entities: Devices / VLANs / Subnets / Servers / Links / DHCP relay targets
- [ ] Mode combo: create / upsert — routes to `?mode=` query param
- [ ] Dry-run checkbox default ON; Apply gated by a confirmation dialog when OFF that spells out create-vs-upsert semantics
- [ ] Format combo: CSV / XLSX — CSV keeps the editor as source of truth; XLSX buffers bytes from file picker, editor shows CSV preview
- [ ] Export fills the editor + offers Save dialog with matching extension per format
- [ ] Open file branches on format: CSV reads text into the editor; XLSX reads bytes into `_pendingXlsxBytes` + advisory note in editor
- [ ] Validate button forces dry_run=true regardless of checkbox; Apply honours it
- [ ] Per-row outcomes grid binds to a flat `OutcomeRow` projection (RowNumber / Ok / Identifier / ErrorText)
- [ ] Summary banner spells out `APPLIED / DRY-RUN / NOT APPLIED` + row counts
- [ ] Wired into MainWindow DockLayoutManager as `BulkPanel` with AllowClose + an `IsBulkPanelOpen` VM property
- [ ] Ribbon "Bulk" group — Export / Validate / Apply buttons dispatch `action:export` / `action:validate` / `action:apply`; 3 NetworkingRibbonAudit theory rows

**Search workspace** (`Central.Module.Networking.Search.SearchPanel`, commits `950eeafef` + `540b19c4d`)
- [ ] Query TextBox with Enter-to-search
- [ ] Entity-types combo with suggestions for single types + common narrowing combos (Device,Vlan etc.)
- [ ] Limit SpinEdit (1-500 server-side clamp; default 50)
- [ ] Results grid grouped by EntityType with columns: EntityType / Label / Snippet / Rank / Id
- [ ] Saved-views sidebar lists the caller's own views (X-User-Id scoped; `ListSavedViewsAsync`)
- [ ] Save current — prompts for a name, POSTs CreateSavedViewRequest, reloads sidebar
- [ ] Delete — OK/Cancel confirmation, DELETE endpoint, reloads
- [ ] Clicking a saved view populates query + entity-types and auto-runs
- [ ] Subtitle line shows `{query truncated 40 chars} · {entity-types or 'all types'}`
- [ ] Ribbon "Search" group — Run Search / Clear buttons (2 theory rows)
- [ ] Double-click / "Open in entity panel" context menu publishes OpenPanel + `selectId:{guid}:{label}` payload
- [ ] Context menu: Show audit history (publishes OpenPanel audit + `selectEntity:{Type}:{Guid}`)
- [ ] Context menu: Copy id

**Audit panel** (`Central.Module.Networking.Audit.AuditViewerPanel`, commits `8dadcdcc4` + `944ffbc52` + `55fe6df70` + `245d85dfc`)
- [ ] Filter bar split across two rows: primary filters (Entity / Action / Actor / From / To) on top; Entity ID + Correlation + Recent chips on second row
- [ ] Visible Entity ID TextBox — drill-down state observable, editable, clearable (replaces earlier invisible `_pendingEntityId` field)
- [ ] Recent: chips — `1h / 24h / 7d / 30d / All` quick-set FromDate/ToDate and auto-run
- [ ] Clear filters button zeroes every input (dates, entity id, correlation) — escape hatch from stale drill-down state
- [ ] Full timeline button — preconditions (EntityType + EntityId required); calls `/api/net/audit/entity/{type}/{id}` which returns unlimited rows (no 500-row ListAudit cap)
- [ ] OnNavigate handler accepts `selectEntity:{Type}:{Guid}` payload from cross-panel drill-down; populates filter bar + auto-runs

**Scope grants admin** (`Central.Module.Networking.ScopeGrants.ScopeGrantsAdminPanel`, commit `e198af3dc`)
- [ ] Filter bar: user id / action combo / entity type combo
- [ ] Grant grid columns: UserId / Action / EntityType / ScopeType / ScopeEntityId / Status / Notes / Id / Version
- [ ] Summary line under grid: `N total · K distinct user(s) · M distinct action(s) · G Global-scope grant(s)`
- [ ] New grant dialog with client-side validation (positive-int user id, required action/entity type, scope_entity_id required when scope_type != Global)
- [ ] Check Permission dialog — dry-runs `/api/net/scope-grants/check`, renders ALLOWED via grant X / DENIED in a MessageBox with inputs echoed
- [ ] Row context menu: Show audit history / Copy grant id
- [ ] Ribbon "Scope Grants" group — Refresh / New Grant / Check (3 theory rows)

**Cross-panel drill-down — shell plumbing**
- [ ] `OpenPanelMessage(TargetPanel)` primitive in `libs/engine/Shell/PanelMessageBus.cs` (record, IPanelMessage)
- [ ] MainWindow subscribes `OpenPanelMessage` and flips the matching `VM.Is*PanelOpen` boolean for audit / search / bulk / locks / scopeGrants / changesets / validation / devices / vlans / servers / hierarchy
- [ ] Two-message pattern: open panel (shell) → navigate payload (panel) so one message doesn't need to do both

**Entity-grid row context menus**
- [ ] DeviceGridPanel: Show audit history (hostname → uuid via `ListDevicesAsync`) / Search for this hostname / Show in hierarchy (focusBuilding) / Copy hostname
- [ ] VlanGridPanel: Show audit history (vlan_id + block_code → uuid via `ListVlansAsync`) / Search for this VLAN / Copy VLAN id
- [ ] ServerGridPanel: Show audit history (direct uuid) / Search for this hostname / Show in hierarchy / Copy id
- [ ] P2P / B2B / FW link grids: Show audit history (link_code → uuid via `ListLinksAsync` via shared `LinkAuditDrill`) / Search for this link code / Copy link code
- [ ] ScopeGrantsAdminPanel: Show audit history (direct uuid — scope_grants.rs emits AuditEvent on create+delete) / Copy grant id

**Hierarchy ↔ grids reverse drill**
- [ ] HierarchyTreePanel context menu on Building nodes: Show devices in this building / Show servers in this building
- [ ] Publishes `filterBy:{Column}:{Value}` payload — generic so each grid maps its own column name (Building vs BuildingCode) to DX FilterString
- [ ] DeviceGridPanel + ServerGridPanel OnNavigate branch: `filterBy:{Col}:{Val}` → `GridControl.FilterString = "[Col] = 'Val'"` with single-quote escaping (doubled `''`)
- [ ] HierarchyTreePanel subscribes to `focus{NodeType}:{code}` payload (Region/Site/Building/Floor/Room/Rack) — walks ParentId chain to expand every ancestor, sets FocusedRowHandle; `_pendingFocus` stashes the request when tree not loaded yet + replayed after ReloadAsync

**Search → entity-grid row focus**
- [ ] DeviceGridPanel OnNavigate `selectId:{guid}:{label}` → matches by hostname (SwitchName) because grid Id column holds switch_guide numeric id; case-insensitive
- [ ] ServerGridPanel OnNavigate `selectId:{guid}:{label}` → matches by Id first, hostname fallback
- [ ] Focus via `FocusedRowHandle = GetRowHandleByListIndex(visible_idx)` to scroll + keyboard-focus the row

**Engine thin-list endpoints** (supporting context-menu uuid resolution)
- [ ] `GET /api/net/vlans` — VlanListRow `(id, vlan_id, display_name, block_code, scope_level, status, version)`, capped at 5000, ORDER BY vlan_id
- [ ] `GET /api/net/links` — LinkListRow `(id, link_code, link_type, device_a, device_b, status, version)` — LEFT JOIN endpoint_order=0/1 hostnames
- [ ] C# ApiClient `ListVlansAsync` + `VlanListRowDto`; `ListLinksAsync` + `LinkListRowDto`
- [ ] `GET /api/net/devices` thin list pre-dates this arc (ListDevicesAsync shipped in Phase 8)

**Tenant context indicator** (commit `8d6113921`)
- [ ] `App.CurrentTenantName` static resolved once during bootstrap via `LookupTenantNameAsync(dsn, tenantId)` — COALESCE(display_name, slug, '')
- [ ] Fetch wrapped in try/catch so tenants-table hiccup doesn't block startup for a display field
- [ ] MainViewModel `CurrentTenantDisplay` — returns resolved name or `(no tenant)` when empty so StringFormat doesn't render half-prefixed
- [ ] MainWindow status bar `StatusBarTenantLabel` — `Tenant: {name}` with tooltip spelling out "every query, mutation and audit call targets this tenant. Reboot to switch."

**Subnet scope_entity_code** (commits `00cda417d` + `24abbc8d4`)
- [ ] CSV column 7 `scope_entity_code` in 8-column header: `subnet_code, display_name, network, vlan_id, pool_code, scope_level, scope_entity_code, status`
- [ ] Compound format per scope_level: Free → empty · Region → REGION_CODE · Site → REGION/SITE · Building → BUILDING_CODE · Floor → BUILDING/FLOOR · Room → BUILDING/FLOOR/ROOM
- [ ] Import: 5 catalog pre-fetch queries + HashMap lookup; validator enforces scope-level ↔ code-shape consistency with precise per-level errors
- [ ] Apply loop binds `scope_entity_id` on INSERT + UPDATE (previously was silently dropped)
- [ ] Export: CASE expression + 6 LEFT JOIN chain rebuilds compound code from scope_entity_id
- [ ] 12 integration tests total: 5 scope happy-path + rejection tests + round-trip `subnet_all_five_scopes_round_trip_through_export`

**VLAN scope_entity_code** (commit `013f01d5b`)
- [ ] CSV column 5 `scope_entity_code` in 8-column header: `vlan_id, display_name, description, scope_level, scope_entity_code, template_code, block_code, status`
- [ ] Compound format per scope_level: Free → empty · Region → REGION_CODE · Site → REGION/SITE · Building → BUILDING_CODE · Device → DEVICE_HOSTNAME
- [ ] Device uses hostname alone (unique per tenant per migration 088)
- [ ] 7 integration tests: `vlan_{region/site/building/device}_scope_resolves*` + `vlan_device_scope_rejects_unknown_hostname` + `vlan_free_scope_rejects_non_empty_code` + `vlan_all_five_scopes_round_trip_through_export`

**Bulk import upsert mode** (commits `d9964d33a` + `7a1c554e5` + `0d7c13391`)
- [ ] `ImportMode::{Create, Upsert}` enum + `?mode=` query param; Create (default) rejects existing keys, Upsert UPDATEs them
- [ ] Covered across all 6 entities: Devices (with version column) / VLANs / Subnets / Servers / Links / DHCP relay targets
- [ ] Pre-fetch existing-rows query returns `(natural_key, id, version)` HashMap for apply-loop branching
- [ ] `dup_check_set` suppression in Upsert mode (validator's "already exists" fires only in Create)
- [ ] Device upsert honours version-column optimistic concurrency; stale CSV version rolls back the whole batch
- [ ] Link upsert DELETEs existing endpoints + INSERTs fresh per CSV row (port_a/port_b on re-import is authoritative)
- [ ] Audit distinguishes action="Created"/"Updated" + `details.mode`
- [ ] Round-trip integration tests prove export → edit → re-import-as-Upsert cycle touches only the edited cell (`bulk_round_trip_integration.rs`)

**Validation rules added this arc** (commits `cc4748f5f` + `90d931eed` + `bbd80fab9`)
- [ ] `link.endpoint_interface_unique_per_device` (Error) — GROUP BY (device_id, interface_name) HAVING count>1; catches port reused by two active links
- [ ] `dhcp_relay_target.unique_per_vlan_ip` (Error) — GROUP BY (vlan_id, server_ip) HAVING count>1; catches CRUD/raw-SQL dupes past the bulk-importer check
- [ ] `device_role.display_name_not_empty` (Warning) — integrity twin of `naming_template_not_empty`; blank display_name renders as empty row in pickers
- [ ] `link.endpoint_devices_resolve` (Error) — LEFT JOIN net.device asserts both endpoints point to a device that still exists (catches orphaned endpoints left behind by raw DELETE on net.device)
- [ ] `subnet.active_subnet_has_pool` (Error) — active subnets without a pool_id; the subnet auto-carver never produces these, but manual INSERT / broken import can
- [ ] `server.active_has_building` (Warning) — parallel to `device.active_requires_building`; active servers should resolve to a building for audit / hierarchy reach
- [ ] `dispatcher_has_arm_for_every_catalog_rule` guardrail test updated on each arc so a new catalog row without a matching `match rule_code` arm fails CI

**Audit activity stats endpoint** (commit `7095c47d4`)
- [ ] `GET /api/net/audit/stats` — per-entity-type activity summary with total_count / distinct_actors / last_seen_at; optional fromAt / toAt ISO-8601 bounds
- [ ] Single SQL pass: GROUP BY entity_type with COUNT + COUNT(DISTINCT actor_user_id) FILTER (WHERE actor_user_id IS NOT NULL) + MAX(created_at); ORDER BY total_count DESC, entity_type ASC
- [ ] Null-safe time bounds via `$2::timestamptz IS NULL OR created_at >= $2` idiom so one query handles all-time + window-bounded calls
- [ ] Surfaced in web client (`NetworkAuditStatsComponent`) with 24h / 7d / 30d preset buttons + double-click drill to entity-type-filtered search

**GIN search indexes** (migration 107_net_search_gin_indexes.sql, commit `5b034abb2`)
- [ ] Six partial GIN indexes (one per search-target entity) on `to_tsvector('english'::regconfig, …)` expressions
- [ ] `::regconfig` cast is load-bearing — the text-overload variant is STABLE and won't key a functional index
- [ ] `WHERE deleted_at IS NULL` partial predicate matches the search query exactly so the planner can use the index
- [ ] search.rs updated in the same commit to use `'english'::regconfig` — byte-for-byte match between query expression and index expression
- [ ] Unit test `every_to_tsvector_uses_regconfig_cast` scans production source (pre-`#[cfg(test)]`) as a guardrail against future drift back to the STABLE variant

### 7.X.17 Web Operator Surface (Phase 10b web client, 2026-04-19+)

Parallel to the WPF operator surface in §7.X.16, Angular pages land
under `apps/web/src/app/modules/network/components/` and consume the
same engine endpoints via `NetworkingEngineService`. Sub-nav lives
on `NetworkDashboardComponent`.

**Search page** (`network-search.component.ts`)
- [ ] Query TextBox + entity-types DxTagBox + limit SpinEdit (1-500 server clamp)
- [ ] Results grouped by entityType; double-click drills to entity detail via routerLink
- [ ] Saved-views sidebar — list / apply / save / delete (X-User-Id scoped; cross-user 404-not-403)

**Validation page** (`network-validation.component.ts`)
- [ ] Run-all + single-rule-run modes (ruleCode text box)
- [ ] Violations grid grouped by severity (Error/Warning/Info)
- [ ] Summary banner: rulesRun / rulesWithFindings / totalViolations

**Scope grants page** (`network-scope-grants.component.ts`)
- [ ] Read-only today — writes flow through WPF ScopeGrantsAdminPanel until the web form component lands
- [ ] Filter bar: user-id (with "Me" button reading `userId` from localStorage) / action / entity-type; DxSelectBox `acceptCustomValue` so future free-text-tolerant engine values surface without schema bump
- [ ] Double-click row → /network/audit/ScopeGrant/:id

**Hierarchy page** (`network-hierarchy.component.ts`)
- [ ] Region → Site → Building → Floor DxTreeList with lazy child loads
- [ ] Reads from Central.Api endpoints (PascalCase) — separate wire shape from the engine (camelCase) handled by TypeScript interface casing

**Pools page** (`network-pools.component.ts`)
- [ ] Three tabs: ASN / VLAN / IP (pools + blocks per family); column set matches WPF PoolAdminPanel

**Bulk page** (`network-bulk.component.ts`, commit `4aa436bce`)
- [ ] Entity combo (Devices / VLANs / Subnets / Servers / Links / DHCP relay targets) — same six the WPF BulkPanel covers
- [ ] Mode combo (create / upsert) + dry-run-only gating (web client forces dryRun=true; Apply is WPF-only for this slice)
- [ ] CSV editor via DxTextArea; Export fills it with rendered CSV from `GET /api/net/{entity}/export`
- [ ] Canonical headers dict mirrors engine `*_COLUMNS` const + WPF CanonicalHeaders; Insert headers button emits for the picked entity
- [ ] Outcomes grid binds to flat ImportRowOutcome rows (rowNumber / ok / identifier / errors-joined); summary shows valid / invalid / totalRows

**Devices grid** (`network-devices.component.ts`, commit `73d29f8ca`)
- [ ] Reads from engine thin list `/api/net/devices` (5000 cap, ORDER BY hostname)
- [ ] Columns: hostname / roleCode / buildingCode / status / version / id; double-click drills to audit timeline for the uuid

**Audit timeline** (`network-audit-timeline.component.ts`)
- [ ] Accepts `/network/audit/:entityType/:entityId` route params
- [ ] No 500-row cap (calls `/api/net/audit/entity/{type}/{id}` — unlimited)
- [ ] Columns: created_at / action / actor / correlation / details (JSON-formatted)

**Audit activity stats** (`network-audit-stats.component.ts`, commit `7095c47d4`)
- [ ] Reads `GET /api/net/audit/stats` summary — one row per entity type
- [ ] Date-range DxDateBox inputs + Last-24h / 7d / 30d preset buttons (parallel to WPF AuditDashboardPanel muscle memory)
- [ ] Columns: entityType / totalCount / distinctActors / lastSeenAt; default sort totalCount DESC
- [ ] Double-click row → `/network/audit-search?entityType=X`

**NetworkingEngineService** (`apps/web/src/app/core/services/networking-engine.service.ts`)
- [ ] Typed methods covering: search / listSavedViews / listDevices / listVlans / listLinks / validateBulk / listAsnPools + listAsnBlocks / listVlanPools + listVlanBlocks / listIpPools / listRegions + listSites + listBuildings + listFloors / listScopeGrants / runValidation / getEntityTimeline / auditStatsByEntityType
- [ ] Interfaces match engine camelCase (SearchResult / SavedView / AuditRow / Violation / ImportRowOutcome / EntityTypeStats / ScopeGrant / DeviceListRow)
- [ ] PascalCase interfaces for Central.Api-sourced rows (AsnPoolRow / AsnBlockRow / VlanPoolRow / VlanBlockRow / IpPoolRow / RegionRow / SiteRow / BuildingRow / FloorRow) — one service, two casings, matching whichever side serves each endpoint

**Routing + sub-nav** (`apps/web/src/app/app.routes.ts` + `network-dashboard.component.ts`)
- [ ] Every new component lazy-loaded via `loadComponent` so unused Phase 10b pages don't inflate the main bundle
- [ ] Sub-nav entries gated through `ModuleRegistryService.isEnabled(...)` for links + routing; others are base-module (`switches`)
- [ ] Routes wrapped in `moduleGuard('switches')` at the parent level; per-page gating for `links` + `routing`

### 7.X.18 Phase 10b extended web surface (commits 2026-04-19+)

Post-initial-launch web slices. Bring the Phase 10b operator surface
to parity with the WPF client across rollups, search facets, change
sets, and per-entity thin grids for the remaining net.* entity types.

**Engine thin lists — servers + subnets** (commit `3d003d58a`)
- [ ] `GET /api/net/servers` — ServerListRow (id, hostname, profileCode, buildingCode, status, version); LEFT JOIN net.server_profile + net.building
- [ ] `GET /api/net/subnets` — SubnetListRow (id, subnetCode, displayName, network::text, scopeLevel, poolCode, vlanTag, status, version); LEFT JOIN net.ip_pool + net.vlan

**Web thin grids — entity coverage** (commits `73d29f8ca` + `826185e37` + `8a385b906`)
- [ ] Devices · VLANs · Servers · Links · Subnets · DHCP relay targets — one DxDataGrid page per entity, all under `/network/{route}`
- [ ] Links grid grouped by linkType by default so P2P / B2B / FW / DMZ / MLAG-Peer / Server-NIC / WAN cohorts are easy to scan; routed at `/network/links-grid` to avoid colliding with the earlier BGP-detail `/network/links`
- [ ] DHCP relay grid: two-phase load (VLANs first so the filter combo + row labels have uuid → tag resolution before the target query completes); rows decorated with vlanLabel; 403 "lacks read:DhcpRelayTarget" surfaced specifically
- [ ] Each grid double-clicks to /network/audit/:entityType/:uuid

**Audit rollup trio** (commits `7095c47d4` + `e0bcda458` + `0e83e7407`)
- [ ] `/api/net/audit/stats` (per-entity-type COUNT + COUNT(DISTINCT actor) + MAX(created_at), optional window)
- [ ] `/api/net/audit/trend` (GROUP BY date_trunc($bucket, created_at); hour / day / week bucketing; optional entityType narrower)
- [ ] `/api/net/audit/top-actors` (per actor_user_id + actor_display; COUNT + COUNT(DISTINCT entity_type) + MAX(created_at); LIMIT default 20, clamp 1..=100; service rows with NULL actor bucket together rather than being dropped)
- [ ] Web audit-stats dashboard: two-column grid (per-entity-type + top-actors) + DxChart trend line above; bucket granularity auto-picked by window length (<= 2d → hour, <= 90d → day, > 90d → week)
- [ ] Web audit-search accepts queryParams: entityType / entityId / action / actorUserId / correlationId — all auto-populate the filter bar on drill from audit-stats / audit-timeline / change-sets

**Audit search export + correlation drill** (commits `77eb96c61` + `3ad5dfca7`)
- [ ] CSV + NDJSON export buttons on the filter bar — reuse current filter state via URLSearchParams + `window.open(url, '_blank')`; engine Content-Disposition: attachment drives the download
- [ ] Audit-timeline correlation_id column renders as clickable link → `/network/audit-search?correlationId=X`; same vocabulary as the actor / entity drills
- [ ] Visible correlation text box on the audit-search filter bar + threaded into listAudit + the export URL

**Search facets** (commit `69991e714`)
- [ ] `GET /api/net/search/facets` — per-entity-type COUNT for a query. One UNION-ALL round trip; every branch uses '::regconfig' cast so the partial GIN indexes from migration 107 match
- [ ] `every_to_tsvector_uses_regconfig_cast` guardrail test still passes with the new facet branches (6 additional to_tsvector calls)
- [ ] No RBAC post-filter on facets (counts are a narrowing hint; drill to /api/net/search still enforces read:entity)
- [ ] Web search page renders a clickable chip bar ("Device(12) · Vlan(4) · Subnet(1)") below the query input — click-to-narrow toggles (clicking an active single-facet filter clears back to "all types")

**Saved views + scope grants CRUD** (commits `2f8594e99` + `d74696308` + `3e1739310` + `3ad5dfca7` + `54791f367`)
- [ ] Saved views: "Save current" button in sidebar (window.prompt for name) + per-row trash + pencil (rename) icons; 409 "name exists" + 412 "stale version" + generic RFC 7807 pass-through
- [ ] Scope grants: row actions column with copy (navigator.clipboard.writeText + fallback) + trash (confirm + 403-specific error); create dialog with ALLOWED_ACTIONS + ALLOWED_SCOPE_TYPES enums
- [ ] Scope grants "Check permission" dialog — dry-runs GET /api/net/scope-grants/check without enforcing; ALLOWED (green) / DENIED (red) result box with matched grant uuid on allow
- [ ] Scope grants + saved views both surface 403 on write attempts with a targeted message ("your user lacks write:ScopeGrant" / "delete:ScopeGrant on this grant")

**Change sets (read-only)** (commit `13945b3ea`)
- [ ] `GET /api/net/change-sets` consumer. Status filter combo (Draft / Submitted / Approved / Rejected / Applied / RolledBack / Cancelled — PascalCase round-trip)
- [ ] Grid grouped by status by default; coloured status badges (applied green, submitted blue, approved purple, rejected red, rolledback amber, cancelled grey, draft neutral)
- [ ] Correlation id column renders as a clickable link → audit-search for that correlation; row double-click does the same drill
- [ ] Writes (submit / approve / apply / cancel / rollback) stay WPF-only until the web approval chrome lands

**Validation rule expansion — batches 7-8** (commits `a5b6e55fa` + `209306ed9`)
- [ ] `subnet.within_parent_pool_cidr` (Error) — uses PG `<<=` operator; equal-CIDR case allowed (single-subnet pool)
- [ ] `link.unique_link_code_active` (Error) — active-only GROUP BY; message lists all colliding uuids
- [ ] `device_role.unique_role_code_per_tenant` (Error) — ignores deleted_at (role picker queries across it)
- [ ] `vlan.scope_entity_resolves` (Error) — non-Free scope must point at a live row in the matching hierarchy table
- [ ] `dhcp_relay_target.vlan_active` (Error) — LEFT JOIN catches soft-deleted + non-Active VLAN refs
- [ ] `subnet.vlan_link_is_active` (Warning) — IP ranges often outlive VLAN tags, so it's a warning not an error

---

## 8. Enterprise SaaS

### 8.1 Multi-Tenancy Foundation

- [ ] Central.Tenancy project (ITenantContext, TenantConnectionFactory, TenantSchemaManager)
- [ ] TenantContext.Default for backward-compatible single-tenant mode
- [ ] Schema name validated (alphanumeric + underscore only)
- [ ] ProvisionTenantAsync creates schema + applies all migrations
- [ ] DropTenantSchemaAsync (refuses to drop public/central_platform)
- [ ] central_platform schema tables: tenants, subscription_plans, tenant_subscriptions, module_catalog, etc.
- [ ] Seed: 3 subscription plans, 8 module catalog entries, 3 release channels
- [ ] Default tenant seeded for backward compatibility
- [ ] Tenancy models — `TenancyTests` (10 tests)
- [ ] TenantResolutionMiddleware extracts tenant_slug from JWT
- [ ] Falls back to X-Tenant header for API key auth
- [ ] Defaults to "default" tenant for backward compatibility
- [ ] ModuleLicenseMiddleware maps API paths to module codes
- [ ] Enterprise tier bypasses module license checks

### 8.2 Registration + Licensing

- [ ] Central.Licensing project
- [ ] RegistrationService: RegisterAsync creates global user + tenant + subscription — `RegistrationTests` (2 tests)
- [ ] Email verification, slug generation, slug uniqueness
- [ ] SubscriptionService: limits, expiry, plan upgrade
- [ ] ModuleLicenseService: IsModuleLicensedAsync, Grant, Revoke
- [ ] LicenseKeyService: RSA-4096 signed license keys — `LicenseKeyTests` (13 tests)
- [ ] Offline validation: public key embedded, verify signature + hardware + expiry — `LicenseKeyTests.ValidateLicense_TamperedPayload_InvalidSignature`

### 8.3 Subscription Management

- [x] Multiple tiers — Free/Pro/Enterprise in `subscription_plans`
- [x] Usage-based billing — `usage_quotas` + `overage_action`
- [x] Upgrade/downgrade flows — `SubscriptionService.UpgradeAsync`
- [x] Proration — `proration_events` + `/api/billing/.../proration`
- [x] Payment method management — `payment_methods` (card/bank/po)
- [x] Admin discount/override — `discount_codes` + `discount_redemptions` + `discount_pct`
- [x] Invoice generation/history — `invoices` + `/api/billing/.../invoices`
- [x] Renewal/cancellation — `cancel_at` + `cancelled_at` + `next_invoice_at`
- [x] Grace periods — `grace_period_ends_at`
- [x] Usage metering + quota enforcement — `usage_quotas` + `CheckLimitsAsync`
- [x] Add-ons/upselling — `subscription_addons` (5 seeded) + `tenant_addons`
- [x] Annual vs monthly — `billing_cycle` + `price_annual` + `annual_discount_pct`
- [x] Corporate billing/POs — `payment_methods.method_type='po'` + `po_number` + `po_expires_at`
- [x] Trial period management — `tenant_subscriptions.is_trial` + `trial_ends_at`

### 8.4 Client Binary Protection

- [ ] Central.Protection project
- [ ] HardwareFingerprint: CPU ID + disk serial + machine name + MAC to SHA256
- [ ] ClientLicenseValidator: RSA public key, offline validation
- [ ] DPAPI-encrypted local cache for 7-day offline grace period
- [ ] CertificatePinningHandler: SHA-256 public key pinning
- [ ] IntegrityChecker: SHA-256 of Central*.dll files at runtime — `IntegrityResultTests` (7 tests)

### 8.5 Auto-Update Manager

- [ ] Central.UpdateClient project
- [ ] UpdateManager: CheckForUpdateAsync, ApplyUpdateAsync, Rollback, RestartApplication
- [ ] SHA-256 checksum verification, backup before overwrite

### 8.6 Environment Routing

- [ ] EnvironmentService singleton manages Live/Test/Dev connection profiles — `EnvironmentServiceTests` (10 tests)
- [ ] Profiles stored in %LocalAppData%/Central/environments.json
- [ ] SwitchTo(name) changes active environment + fires EnvironmentChanged event

### 8.7 Concurrent Editing

- [ ] Central.Collaboration project — `CollaborationTests` (10 tests)
- [ ] PresenceService: JoinEditing, LeaveEditing, DisconnectAll, GetEditors
- [ ] ConflictDetector: row_version comparison, three-way merge
- [ ] Non-overlapping changes auto-merged, overlapping flagged

### 8.8 Item-Level Security (ABAC)

- [ ] Central.Security project — `SecurityTests` (6 tests)
- [ ] SecurityPolicyEngine: CanAccessRow, GetHiddenFields, FilterFields
- [ ] Policies: EntityType, PolicyType (row/field), Effect (allow/deny), Conditions, Priority

### 8.9 Observability

- [ ] Central.Observability project — `ObservabilityTests` (5 tests)
- [ ] CorrelationContext: AsyncLocal<string> for request correlation ID
- [ ] StructuredLogEntry with ToCef() for SIEM integration
- [ ] Level-to-severity mapping

### 8.10 Global Admin Console

- [ ] 5-phase buildout: tenant CRUD, licensing, setup wizard, audit, addresses/contacts
- [ ] Platform-level audit trail in `central_platform.global_admin_audit_log`
- [ ] Permissions: global_admin:read/write/delete/provision
- [ ] Tenant addresses (many-to-one) and tenant contacts (many-to-many)
- [ ] Angular 5-tab admin UI (health, tenants CRUD, users, licenses, infra)
- [ ] WPF 5-panel Global Admin module

### Enterprise Feature Matrix Totals

- [x] User Management (7/7): registration, social login, profile, password reset, MFA, sessions, activity logs
- [x] Address Management (5/5): types, validation, defaults, geolocation, history
- [x] Roles & Permissions (6/6): RBAC, predefined roles, custom roles, assignment levels, inheritance+override, templates
- [x] Teams (6/6): creation, invitations, resources, hierarchy, access controls, activity dashboards
- [x] Groups (5/5): batch permissions, dynamic rules, cross-team, resource sharing, auto-assignment
- [x] Companies/Tenants (7/7): multi-tenant, registration, branding, settings, hierarchy, cross-company access, feature flags
- [x] Security (10/10): encryption, API keys, IP allowlist, SSL, user keys, rate limiting, compliance, backup, SSO, auto-deprovisioning
- [x] Registration (7/7): self-service, invitation, domain verification, trial, approval, onboarding, ToS
- [x] Subscription Management (13/13): see 8.3
- **Total: 66/66 enterprise spec items complete**

---

## 9. AI & Intelligence

### 9.1 Platform-Level AI Providers (Global Admin Only)

- [ ] GET /api/global-admin/ai/providers returns all 8 seeded platform providers (Anthropic Claude, OpenAI, Azure OpenAI, Vertex, Bedrock, Groq, Ollama, LM Studio)
- [ ] POST /api/global-admin/ai/providers creates a new platform provider and appears in list
- [ ] PUT /api/global-admin/ai/providers/{id} updates provider metadata (base URL, display name, enabled flag)
- [ ] DELETE /api/global-admin/ai/providers/{id} soft-deletes a platform provider
- [ ] GET /api/global-admin/ai/models returns all 9 seeded models (Claude Opus 4.7, Sonnet 4.6, Haiku 4.5, GPT-5, GPT-5 Mini, Gemini 2.5 Pro, etc.)
- [ ] POST /api/global-admin/ai/models adds a new model under an existing provider
- [ ] Non-global_admin user receives 403 on every /api/global-admin/ai/* call
- [ ] GET /api/global-admin/ai/features returns the feature catalog (assistant, scoring, dedup, enrichment, churn, calls, etc.)

### 9.2 Tenant BYOK Configuration

- [ ] POST /api/ai/tenant/providers accepts a tenant API key and stores it AES-256-encrypted in `tenant_ai_providers.api_key_enc` (plaintext never round-trips back)
- [ ] GET /api/ai/tenant/providers returns tenant providers with `api_key_enc` redacted (only last-4 / masked form)
- [ ] PUT /api/ai/tenant/providers/{id} updates a tenant provider's display name / enabled / default-model
- [ ] DELETE /api/ai/tenant/providers/{id} removes a tenant-specific BYOK binding
- [ ] POST /api/ai/tenant/providers/{id}/test performs a round-trip call with the stored key and returns success/failure + latency
- [ ] PUT /api/ai/tenant/features maps a feature code (e.g. assistant, lead_scoring) to a specific provider for this tenant
- [ ] GET /api/ai/tenant/features lists the tenant's per-feature provider mapping with fallbacks indicated

### 9.3 Provider Resolution + Usage Logging

- [ ] GET /api/ai/tenant/resolve/{featureCode} returns tenant-configured provider when a tenant mapping exists
- [ ] Falls back to the platform default provider when no tenant mapping exists
- [ ] Returns "none" / 404-style response when neither tenant nor platform provider is configured
- [ ] `resolve_ai_provider()` SQL function returns the same result as the HTTP endpoint (parity check)
- [ ] `TenantAiProviderResolver` caches resolutions for 2 minutes and invalidates on tenant provider update
- [ ] Every AI call writes a row to `ai_usage_log` with feature, provider, model, tokens in/out, cost, latency
- [ ] GET /api/ai/tenant/usage returns aggregated usage (by day/feature/provider) from the auto-aggregation trigger
- [ ] Usage logging honors tenant quota; overage action (warn/block/throttle) fires per tenant config

### 9.4 ML Scoring (Leads, Deals, Accounts, Churn, LTV)

- [ ] GET /api/ai/scores?entity=lead returns scores for leads from `ai_model_scores`
- [ ] GET /api/ai/scores?entity=deal returns scores joined to `crm_deals` ML score columns
- [ ] GET /api/ai/scores?entity=account returns account health/propensity scores
- [ ] POST /api/ai/ml-models registers a new ML model in `ai_ml_models`
- [ ] POST /api/ai/ml-models/{id}/train enqueues a row in `ai_training_jobs` and transitions status queued to running to completed
- [ ] GET /api/ai/next-actions returns entries from `ai_next_best_actions` for the current user's pipeline
- [ ] Lead/deal/account detail views display the ML score column value when present

### 9.5 AI Assistant (Conversations, Messages, Templates, Tools)

- [ ] POST /api/ai/assistant/conversations creates a conversation and returns its id
- [ ] GET /api/ai/assistant/conversations lists conversations for the current user (not other users')
- [ ] POST /api/ai/assistant/conversations/{id}/messages appends a user message and returns assistant response
- [ ] GET /api/ai/assistant/conversations/{id}/messages returns full message thread in order
- [ ] DELETE /api/ai/assistant/conversations/{id} removes conversation + messages
- [ ] GET /api/ai/assistant/templates returns the 4 seeded prompt templates
- [ ] POST /api/ai/assistant/templates creates a new prompt template (admin-only)
- [ ] GET /api/ai/assistant/tools returns the 6 seeded AI tool definitions
- [ ] Assistant calls respect tenant provider mapping for the `assistant` feature

### 9.6 Duplicate Detection + Merge

- [ ] GET /api/ai/duplicates returns candidate duplicates from `v_contact_duplicate_candidates` (pg_trgm similarity)
- [ ] POST /api/ai/duplicates/rules creates a `crm_duplicate_rules` entry (fields + threshold)
- [ ] POST /api/ai/duplicates/merge executes a merge operation and writes to `crm_merge_operations` with before/after snapshot
- [ ] Merge is blocked without `AiDedupMerge` permission

### 9.7 Enrichment (Clearbit/Apollo/etc. + Tenant BYOK)

- [ ] GET /api/ai/enrichment/providers lists the 5 seeded enrichment providers (Clearbit, Apollo, ZoomInfo, PeopleData, Hunter)
- [ ] POST /api/ai/tenant/enrichment-providers stores tenant BYOK enrichment key (AES-256)
- [ ] POST /api/ai/enrichment/jobs enqueues an enrichment job in `crm_enrichment_jobs`
- [ ] GET /api/ai/enrichment/jobs/{id} shows job status + results
- [ ] Enrichment endpoints require `AiEnrichmentRead` / `AiEnrichmentRun` permission

### 9.8 Churn Risk + LTV Prediction

- [ ] GET /api/ai/churn-risks returns churn risk entries from `crm_churn_risks`
- [ ] GET /api/ai/account-ltv returns per-account LTV snapshots from `crm_account_ltv`

### 9.9 Call Recording (Transcript, Sentiment, Topics, Talk Ratio)

- [ ] GET /api/ai/calls returns call recordings with transcript, sentiment, topics, talk ratio
- [ ] POST /api/ai/calls uploads a call recording (or URL) and creates the row in `crm_call_recordings`

### 9.10 Activity Auto-Capture

- [ ] Auto-capture rule matches activity — row inserted into `crm_auto_capture_queue`; queue processor logs the activity
- [ ] 8 new AI-related webhook event types fire (e.g. ai.call.transcribed, ai.churn_risk.raised, ai.duplicate.merged) and appear in `webhook_deliveries`

### AI Migrations + Infrastructure

- [ ] Migration 076_ai_providers.sql applies cleanly; 8 providers + 9 models seeded
- [ ] Migration 077_ai_ml_scoring.sql applies and adds ML score columns to crm_leads/crm_deals/crm_accounts
- [ ] Migration 078_ai_assistant.sql applies; 4 prompt templates + 6 tools seeded
- [ ] Migration 079_ai_dedup_enrichment.sql applies; 5 enrichment providers seeded; `v_contact_duplicate_candidates` view queries successfully with pg_trgm
- [ ] Migration 080_ai_churn_calls.sql applies; 8 webhook event types registered
- [ ] All 5 migrations listed in `infra/k8s/base/kustomization.yaml`

---

## 10. Integrations

### 10.1 Service Desk Sync (ManageEngine)

(See Section 7.7 for canonical ME sync items.)
- [ ] Zoho EU OAuth flow, refresh token rotation, fields_required param
- [ ] Incremental sync via completed_time not synced_at
- [ ] Auth failure shows error toast, integration_log records with duration

### 10.2 Email (Accounts, Templates, Tracking, Providers)

(See Section 7.8 Phase 20 and Section 6.1 EmailEndpoints.)
- [ ] SMTP / IMAP / Exchange / Gmail providers with OAuth token storage
- [ ] Email templates with merge fields
- [ ] Tracking pixel + click redirect (anonymous endpoints)

### 10.3 Webhooks (Subscriptions, Event Types, Deliveries, HMAC)

- [ ] 28 seeded event types (crm.deal.won/lost, crm.lead.converted, etc.) + 8 AI event types + 16 Stage 3 order/subscription events
- [ ] webhook_subscriptions with HMAC secret generation
- [ ] webhook_deliveries with retry + status tracking
- [ ] X-Webhook-Signature HMAC-SHA256 validation (see Section 6.9)
- [ ] deal_won trigger auto-dispatches crm.deal.won

### 10.4 Sync Engine (Bidirectional CRM Sync)

- [ ] Sync Config panel opens from Admin > System > Sync Engine
- [ ] Sync configs grid shows Name, AgentType, Enabled, Direction, Interval, Status
- [ ] Run Sync executes SyncEngine.ExecuteSyncAsync
- [ ] Test Connection checks agent availability
- [ ] ManageEngine agent registered — `SyncEngineTests` (6 tests)
- [ ] 7 field converters registered — `FieldConverterTests` (15 tests)
- [ ] Entity maps / field maps define mapping per config
- [ ] Concurrent sync throttled by SemaphoreSlim
- [ ] Cancel sync via SyncEngine.CancelSync(configId)
- [ ] 6 seeded integration agents (Salesforce, HubSpot, Dynamics, Exchange, Gmail, Pipedrive)
- [ ] 8 sync configs for bidirectional entity sync
- [ ] crm_external_ids + crm_sync_conflicts tables

#### Sync Engine Agents
- [ ] ManageEngine agent (agent_type='manage_engine') — OAuth refresh, paged read, write-back
- [ ] CSV Import agent — `CsvImportAgentTests` (9 tests)
- [ ] REST API agent — `RestApiAgentTests` (6 tests)
- [ ] All 3 agents registered at startup — `AgentRegistrationTests` (3 tests)

#### Sync Models
- [ ] SyncConfig StatusColor: success/failed/running/partial/never — `SyncModelsTests` (9 tests)
- [ ] SyncLogEntry StatusColor
- [ ] SyncEntityMap PropertyChanged
- [ ] SyncFieldMap PropertyChanged
- [ ] SyncConfig PropertyChanged

#### Sync Pipeline Integration
- [ ] Full pipeline with direct mapping — `SyncPipelineTests.DirectMapping`
- [ ] Full pipeline with converters — `SyncPipelineTests.WithConverters`
- [ ] Upsert failure counted as failed record — `SyncPipelineTests.UpsertFailure`
- [ ] Empty source: success with 0 records — `SyncPipelineTests.EmptySource`
- [ ] Multiple entity maps synced concurrently — `SyncPipelineTests.MultipleEntityMaps`

#### Converter Edge Cases
- [ ] Direct: null, int, bool pass through — `ConverterEdgeCaseTests` (11 tests)
- [ ] Constant: always returns expression
- [ ] Combine: empty expression handled
- [ ] Split: null value, invalid expression handled
- [ ] DateFormat: null and invalid string handled
- [ ] Expression: empty value with $value ref
- [ ] Lookup: null value handled

#### Sync Engine Resilience
- [ ] SyncRetry.WithRetryAsync: exponential backoff (1s, 2s, 4s), configurable max retries, 30s cap
- [ ] SyncHashDetector: SHA-256 content hash, skip unchanged records
- [ ] SyncFieldValidator: pre-write validation of required/key fields
- [ ] Dead letter queue: failed records stored with error message, retry count
- [ ] Per-record retry in SyncEngine: 2 retries with 500ms backoff
- [ ] Hash lookup/update callbacks
- [ ] Validation errors routed to dead letter queue (not retried)
- [ ] RecordsSkipped counter for unchanged records

### 10.5 Identity Providers (SAML/OIDC/Entra/Okta/Duo)

(See Section 2.5 for canonical IdP items.)
- [ ] Email-based IdP discovery + domain mapping
- [ ] Claim-to-role mapping via claim_mappings table
- [ ] JIT provisioning of first-time external users
- [ ] auth_events records all login/logout/failed events

---

## 11. Infrastructure & Ops

### 11.1 Kubernetes (7-Node Cluster)

- [ ] Terraform module generates Vagrantfile
- [ ] 1 master + 6 workers on VMware Workstation
- [ ] Ansible roles: common, containerd, k8s-master, k8s-worker, metallb, registry
- [ ] MetalLB L2 advertisement (192.168.56.200-220)
- [ ] Local container registry (NodePort 30500)
- [ ] Calico CNI, Kubeconfig exported
- [ ] PostgreSQL HA StatefulSet (primary + streaming replica, WAL replication, anti-affinity)
- [ ] Redis StatefulSet (AOF persistence, LRU eviction)
- [ ] Central API Deployment (2 replicas, HPA 2-8, PDB)
- [ ] Auth Service Deployment (2 replicas, HPA 2-6, PDB)
- [ ] LoadBalancer services via MetalLB
- [ ] Namespace, RBAC, ResourceQuota, LimitRange, ConfigMap per tenant

### 11.2 Rust Services (7 services)

- [ ] auth-service: MFA, WebAuthn, SAML, OIDC, JWT
- [ ] admin-service: tenant management, setup wizard
- [ ] gateway: reverse proxy, rate limiting, TLS, WebSocket/SignalR
- [ ] task-service: 26 endpoints, SSE stream, batch, search, Redis events
- [ ] storage-service: MinIO/S3, BLAKE3 dedup, multipart upload, pre-signed URLs
- [ ] sync-service: vector clocks, push/pull, conflict resolution
- [ ] audit-service: M365 forensics, GDPR scoring, investigations, evidence export
- [ ] All services have source and build from Cargo workspace
- [ ] tenant-provisioner tool: pg_dump source schema > CREATE DATABASE > restore > migrate > create K8s namespace

### 11.3 Terraform + Terragrunt IaC

- [ ] VPC module — subnets, NAT gateway, IGW, route tables
- [ ] EKS module — cluster + managed node groups, OIDC provider for IRSA
- [ ] RDS module — Aurora PostgreSQL cluster, encryption, parameter group
- [ ] ElastiCache module — Redis replication group, failover
- [ ] ECR module — container registries for all 8 services, lifecycle policies
- [ ] S3 module — media/backup/config buckets, versioning, encryption
- [ ] KMS module — customer-managed keys with rotation
- [ ] Secrets module — DB creds, JWT key, encryption key in Secrets Manager
- [ ] Monitoring module — CloudWatch log groups, alarms, dashboard
- [ ] K8s Service module — reusable Deployment + Service + HPA + PDB
- [ ] Local cluster module — VMware VMs via Vagrant
- [ ] Root terragrunt.hcl — S3 backend, DynamoDB locking, provider generation
- [ ] _envcommon/ — DRY module configs
- [ ] dev/env.hcl — 2 AZs, t3.medium, single NAT, no spot
- [ ] staging/env.hcl — 2 AZs, t3.large, spot enabled
- [ ] prod/env.hcl — 3 AZs, r6g/m6i, spot, read replicas, transit encryption
- [ ] Dependency chain: KMS > VPC > EKS > RDS/ElastiCache > ECR > Secrets

### 11.4 CI/CD (4 GH Actions workflows)

- [ ] GitHub Actions workflow on push to main/develop and PRs
- [ ] Steps: checkout, setup .NET 10, restore, build (x64 Release), test
- [ ] Test results published via dotnet-trx reporter
- [ ] Container build job on main branch only (after tests pass)
- [ ] Podman build + tag + health test in CI
- [ ] Multi-arch builds
- [ ] Backup with retention (workflow)
- [ ] Environment promotion workflow (dev to staging to prod)
- [ ] security-scan workflow with Trivy, Gitleaks, NuGet audit, npm audit

### 11.5 Deployment Containers (Podman builds, K8s deploys)

- [ ] Dockerfile includes Api.Client project reference
- [ ] Dockerfile copies db/migrations/ for auto-apply
- [ ] HEALTHCHECK directive pings /api/health every 30s
- [ ] pod.yaml has postgres + api containers with resource limits
- [ ] pod.yaml uses PG 18 alpine with performance tuning
- [ ] auth-service container in Central Podman pod (port 8081)
- [ ] Redis container in pod (port 6379, session store)
- [ ] secure_auth database created on same PG instance
- [ ] Auth-service V001-V017 migrations applied
- [ ] Seed: default tenant, admin user, roles with Central permission codes
- [ ] setup.sh: k8s-up, k8s-deploy, k8s-status, k8s-psql, k8s-logs, k8s-migrate, k8s-push, k8s-down, build-auth, auth-logs, auth-psql

### 11.6 Monitoring (Prometheus, Grafana, Jaeger, Loki, Alertmanager)

- [ ] Prometheus scrapes /api/health/metrics
- [ ] Grafana dashboards for API / DB / sync engine
- [ ] Jaeger distributed tracing via correlation IDs
- [ ] Loki structured log aggregation
- [ ] Alertmanager rules for high error rate, PgBouncer queue depth, auth lockouts

### 11.7 Backup + Purge

- [ ] pg_dump scheduled via `db_backup` job (migration 042)
- [ ] backup_history table tracks outputs
- [ ] Retention policies per environment
- [ ] Rust `backup-manager` + `backup-service` + `backup-app` tools
- [ ] Soft-delete purge panel (see 7.9)
- [ ] Pre-destructive infra changes require manual pg_dump confirmation

### 11.8 System Tray Manager (Rust)

#### Tray Icon & Status
- [ ] Tray icon appears in Windows system tray on launch
- [ ] Green icon when all 11 services running (HEALTH_ALL)
- [ ] Yellow icon when partial services running (HEALTH_PARTIAL)
- [ ] Red icon when no services running (HEALTH_NONE)
- [ ] Tooltip shows "Central Platform - {health} ({running}/{total})"
- [ ] Status polls K8s every 200ms via kubectl
- [ ] Right-click opens context menu without closing immediately

#### Service Management (per service: central-api, auth, task, storage, sync, audit, admin, gateway)
- [ ] Service state shown in submenu: [UP], [STOPPED], [NOT DEPLOYED]
- [ ] Open in Browser — opens service URL
- [ ] Restart — kubectl rollout restart
- [ ] Stop — kubectl scale --replicas=0
- [ ] Start — kubectl scale --replicas=1
- [ ] View Logs — opens terminal with kubectl logs -f

#### Data Layer
- [ ] psql: central DB — kubectl exec postgres-0 -- psql
- [ ] psql: secure_auth DB — kubectl exec postgres-0 -- psql -d secure_auth
- [ ] Backup (pg_dump) — kubectl exec pg_dump
- [ ] Run Migrations — applies db/migrations/*.sql
- [ ] redis-cli — kubectl exec redis-0 -- redis-cli
- [ ] MinIO Console — opens MinIO web UI

#### Global Admin
- [ ] View Tenants — queries central_platform.tenants
- [ ] Global Users — queries central_platform.global_users
- [ ] Subscriptions — queries tenant_subscriptions + plans
- [ ] Module Licenses — queries tenant_module_licenses
- [ ] New Tenant — redirects to desktop app
- [ ] Provision Schema — redirects to desktop app

#### K8s Infrastructure
- [ ] Nodes — kubectl get nodes -o wide
- [ ] All Pods — kubectl -n central get pods -o wide
- [ ] Services — kubectl -n central get svc
- [ ] HPA (Autoscale) — kubectl -n central get hpa
- [ ] Events — kubectl -n central get events
- [ ] Deploy Manifests — kubectl apply -k infra/k8s/base/

#### VMware VMs
- [ ] Start All — vagrant up (in terminal window)
- [ ] Stop All — vagrant halt
- [ ] Status — vagrant status + kubectl get nodes
- [ ] SSH to k8s-master, k8s-worker-01 through k8s-worker-04

#### Cluster Actions
- [ ] Restart All Services — rollout restart all 8 deployments
- [ ] Stop All Services — scale all to 0
- [ ] Refresh Status — force status re-poll

#### Quick Launch
- [ ] Open Gateway — http://192.168.56.203:8000
- [ ] Open Swagger — http://192.168.56.200:5000/swagger
- [ ] Open Angular — http://localhost:4200
- [ ] Launch Desktop App — Central.exe

#### Tools
- [ ] Tray Manager Logs — internal log viewer
- [ ] Audit Log — view audit events
- [ ] Open Project Folder — opens C:\Development\Central
- [ ] Open Terminal — opens CMD
- [ ] Check for Updates — version manifest check
- [ ] About Central — version info dialog

### 11.9 Engine Services (Cross-Cutting)

#### Mediator + PanelMessageBus
- [ ] Mediator singleton handles all in-process panel messaging with pipeline behaviors — `MediatorTests` (11 tests)
- [ ] MediatorLoggingBehavior logs all messages to debug output
- [ ] MediatorPerformanceBehavior tracks per-message-type counts and avg latency
- [ ] Mediator.GetDiagnostics() returns subscription and message count stats
- [ ] Filtered subscriptions: handlers only called when filter function returns true
- [ ] PanelMessageBus.Publish bridges to Mediator automatically — `PanelMessageBusTests` (7 tests)
- [ ] 4 message types: SelectionChanged, NavigateToPanel, DataModified, RefreshPanel
- [ ] Subscriber ID tracking, message count, multiple subscribers, filters, logging — `MediatorAdvancedTests` (5 tests)

#### Link Engine
- [ ] LinkEngine manages DB-stored LinkRules — `LinkEngineTests` (8 tests)
- [ ] Default link rules: SD Technicians>Requests, Requesters>Requests, Groups>Requests, Devices>Switches, Users>AuthEvents
- [ ] Right-click grid > "Configure Links..." opens LinkCustomizerDialog
- [ ] LinkCustomizerDialog shows source/target panel dropdowns + field names + active toggle
- [ ] Link rules persisted in panel_customizations table

#### Notification + Alert Services
- [ ] NotificationService.Instance singleton with Info/Success/Warning/Error — `NotificationServiceTests` (8 tests)
- [ ] Default toast channel / suppress none / email / both / disabled
- [ ] NotificationService.NotificationReceived event for shell rendering
- [ ] AlertService.PingFailed/PingRecovered/SshFailed/ConfigDrift/BgpPeerDown — `AlertServiceTests` (7 tests)
- [ ] AlertRaised event fires
- [ ] notification_preferences table + 8 event types — `NotificationPreferenceExtendedTests`

#### Email Service
- [ ] EmailService.Instance configurable with SMTP settings — `EmailServiceTests` (6 tests)
- [ ] Configure() accepts host, port, username, password, from address, SSL
- [ ] SendAsync sends text or HTML emails
- [ ] Predefined templates: sync failure alert, auth lockout alert, backup complete
- [ ] SendTestEmailAsync for testing SMTP configuration
- [ ] Non-blocking — email failures don't crash the app

#### Cron Expression Parser
- [ ] CronExpression.Parse various expressions — `CronExpressionTests` (14 tests) + `CronExpressionNextOccurrenceTests` (18 tests) + `CronExpressionExtendedTests` (16 tests) + `CronEdgeCaseTests` (7 tests)
- [ ] Supports: *, ranges (1-5), steps (*/15), lists (1,3,5)
- [ ] GetNextOccurrence skips non-matching months/days/years
- [ ] Matches(DateTime) returns true when time matches
- [ ] TryParse valid returns result, invalid returns false
- [ ] job_schedules.schedule_cron column (migration 051)
- [ ] Jobs with cron expression run when current minute matches

#### Data Validation Service
- [ ] DataValidationService.Instance singleton — `DataValidationServiceTests` (12 tests) + `DataValidationEdgeCaseTests` (22 tests)
- [ ] Required, MinLength, MaxLength, Regex, Range, Custom rules
- [ ] Multiple rules per entity, all errors reported
- [ ] RegisterDefaults() seeds rules for Device, User, SdRequest, Appointment, Country, ReferenceConfig
- [ ] Registered at startup in App.OnStartup

#### Settings Export/Import Service
- [ ] ExportAsync exports user settings as JSON — `SettingsExportTests` (3 tests), `SettingsExportExtendedTests` (7 tests)
- [ ] ExportToFileAsync writes JSON to file
- [ ] ImportFromFile parses exported settings JSON
- [ ] ExportedSettings defaults (empty collections, version, ISO timestamp)
- [ ] ImportFromFile malformed JSON throws

#### CommandGuard + SafeAsync
- [ ] TryEnter/Exit prevents concurrent execution — `CommandGuardTests` (7 tests) + `CommandGuardEdgeCaseTests` (6 tests)
- [ ] RunAsync wraps async actions with automatic TryEnter/Exit
- [ ] Run wraps sync actions with automatic TryEnter/Exit
- [ ] IsRunning tracks current state
- [ ] Applied to: GlobalAdd, GlobalDelete, AddTask, AddSubTask, Refresh, SaveLayout
- [ ] SafeAsync.Run wraps async void handlers with try/catch — `SafeAsyncTests` (4 tests)
- [ ] Exceptions routed to NotificationService.Error
- [ ] SafeAsync.RunGuarded combines CommandGuard + safe exception handling

#### File Management Service
- [ ] FileManagementService singleton: Configure() sets filesystem storage path — `FileManagementServiceTests` (17 tests)
- [ ] ComputeMd5: byte array and Stream overloads
- [ ] ShouldStoreInline: files <= 10MB stored in DB, larger on filesystem
- [ ] GetStoragePath: sharded directory structure, auto-create
- [ ] SaveToFilesystemAsync / ReadFromFilesystemAsync / DeleteFromFilesystem
- [ ] FileRecord with FileSizeDisplay (B, KB, MB, GB)
- [ ] FileVersionRecord model

#### Elsa Workflows Engine
- [ ] Central.Workflows project with Elsa 3.5.3
- [ ] PostgreSQL persistence for both Management and Runtime stores
- [ ] 6 custom activities: UpdateTaskStatus, ValidateTransition, SendNotification, Approval, LogAudit, SetField — `WorkflowActivityTests` (9 tests)
- [ ] TaskStatusTransitionWorkflow built-in
- [ ] Activities use workflow variables (SetVariable)
- [ ] ApprovalActivity uses Elsa bookmarks for suspend/resume
- [ ] Elsa management API at /elsa/api/*

#### GridValidationHelper + PreferenceKeys + PlatformTests
- [ ] GridValidationHelper.Validate all cases — `GridValidationHelperTests` (14 tests)
- [ ] FormatErrors correct/empty
- [ ] PreferenceKeys HideReserved/Theme/DockLayout correct values — `PreferenceKeysTests` (7 tests)
- [ ] Platform singletons exist, permission codes, AuthStates, NotificationEventTypes, PasswordPolicy — `PlatformTests` (11 tests)

---

## 12. Unit Test Summary

Total: **2,229 tests across 164 test classes** (xUnit, .NET 10). 0 failures on unit tests.
Tests live in `tests/dotnet/`. 9 integration tests require live K8s services
(auth-service, task-service, PostgreSQL, Redis) to run — they are skipped locally.

Rough breakdown by area:

| Area | Approx Count | Highlights |
|---|---|---|
| Auth | ~285 | AuthContext, AuthFramework, AuthStates, CredentialEncryptor, IdentityConfig, PasswordHasher, PasswordPolicy, PermissionCodes, PermissionGuard, SecureStringExtension, TotpService, UserTypes, AppUser |
| Api | ~40 | Endpoint smoke tests, Swagger schema validation, middleware pipeline |
| Enterprise | ~60 | Tenancy, Collaboration, Security, Observability, LicenseKey, Registration, IntegrityResult |
| Integration | ~70 | Agent registration, ConverterEdgeCase, CsvImport, FieldConverter, RestApi, SyncEngine, SyncModels, SyncPipeline (9 need live K8s) |
| Models | ~1,050 | TaskItem, DeviceRecord, SwitchRecord, NetworkLink, VlanEntry, RibbonConfig, SdRequest, RoleRecord, AppUser, Integration, Location, Appointment, Kanban, Project, Container, Dashboard, Notification, etc. |
| Services | ~310 | AuditService, CommandGuard, ConfigDiff, CronExpression, DataValidation, EmailService, EnvironmentService, FileManagement, GridValidation, IconOverride, IntegrationService, NotificationService, Platform, PreferenceKeys, SafeAsync, SettingsExport, StartupArgs, UndoService, AlertService, DeployService |
| Shell | ~65 | LinkEngine, Mediator, MediatorAdvanced, PanelCustomization, PanelMessageBus, RibbonBuilder, WidgetCommand |
| Tasks | ~95 | TaskModels, TaskFileParser, SprintAndPlanning, WorkflowActivity, TaskRepositoryIntegration |
| Widgets | small | Toast rendering, ribbon group helpers |

Per-class breakdown details are intentionally omitted here — run `dotnet test` for the
canonical test manifest. The full list lived at the bottom of the previous checklist
and was noise that drifted out of date.

---

## Migrations Reference

Full list of all 80 migrations in `db/migrations/`. Each applies idempotently on a
fresh DB via `MigrationRunner` at startup or `./db/setup.sh` manually.

| #   | File | Purpose |
|-----|------|---------|
| 002 | `builder.sql` | `vlan_templates`; extra columns on `switch_connections` and `bgp_config` |
| 003 | `connectivity.sql` | `management_ip`, `ssh_*`, `last_ping_*`, `last_ssh_*` on switches; `running_configs` table |
| 004 | `ipam_fields.sql` | Additional IPAM device fields |
| 005 | `lookup_values.sql` | `lookup_values` table for dropdown options |
| 006 | `users_roles.sql` | `app_users`, `role_permissions`, `user_settings` tables |
| 007 | `roles_table.sql` | `roles` table for role management UI |
| 008 | `role_sites.sql` | `role_sites` table for per-role site/building access control |
| 009 | `config_ranges.sql` | Config range definitions (reusable number/IP ranges: ASN, VLAN, etc.) |
| 010 | `excel_sheets.sql` | Excel sheet data imported into queryable tables |
| 011 | `view_reserved.sql` | `can_view_reserved` permission + reserved device view |
| 012 | `config_versions.sql` | Config version history for compare |
| 013 | `rename_ipam_to_devices.sql` | Rename module key from 'ipam' to 'devices' in role_permissions |
| 014 | `asn_definitions.sql` | ASN definitions — one row per distinct ASN with description and type |
| 015 | `app_log.sql` | General-purpose application log for errors, warnings, info, audit events |
| 016 | `builder_selections.sql` | Persists config builder toggle state per device |
| 017 | `missing_tables.sql` | Fills in missing tables from initial schema |
| 018 | `switch_model_interfaces.sql` | Maps switch models to their interface layout |
| 019 | `lldp_columns.sql` | LLDP neighbor columns on switch_interfaces |
| 020 | `interface_optics.sql` | Historical optics diagnostics per interface per sync |
| 021 | `p2p_descriptions.sql` | Port descriptions for each side of a P2P link |
| 022 | `vlan_site.sql` | Per-site VLAN data |
| 023 | `bgp_sync.sql` | BGP sync tracking (`fast_external_failover`, `bestpath_multipath_relax`, `last_synced`) |
| 024 | `permissions_v2.sql` | `permissions` table (25 module:action codes), `role_permission_grants`, role priorities |
| 025 | `audit_log_v2.sql` | `audit_log` (append-only JSONB) + soft delete columns on link + device tables |
| 026 | `platform_schema.sql` | `central_platform` schema for cross-tenant global tables |
| 027 | `global_admin.sql` | Global admin user + role + endpoints baseline |
| 028 | `tenant_id.sql` | `tenant_id` column + RLS scaffolding on all public-schema tables |
| 029 | `ribbon_default_panel.sql` | Ribbon page default panel + missing permissions |
| 030 | `migrate_users_to_auth.sql` | Migrate Central app_users to auth-service secure_auth database |
| 031 | `module_catalog_audit_globaladmin.sql` | Module catalog + audit + globaladmin seeds |
| 032 | `global_admin_audit.sql` | `central_platform.global_admin_audit_log` — platform-level audit trail |
| 033 | `global_admin_permissions.sql` | `global_admin:read/write/delete/provision` permissions |
| 034 | `tenant_addresses_contacts.sql` | `tenant_addresses` (many-to-one), `contacts` + `tenant_contacts` (many-to-many) |
| 035 | `api_key_salt.sql` | `salt` column on `api_keys` for per-key salted SHA256 hashing |
| 036 | `companies.sql` | `companies` table with hierarchy |
| 037 | `contacts_v2.sql` | Full CRM contacts + addresses + communications |
| 038 | `teams_departments.sql` | `departments`, `teams`, `team_members` |
| 039 | `addresses_unified.sql` | Polymorphic addresses |
| 040 | `user_profiles.sql` | `user_profiles` + `user_invitations` + `role_templates` |
| 041 | `global_admin_v2.sql` | `tenant_onboarding`, `billing_accounts`, `invoices`, usage metrics, FTS indexes |
| 042 | `groups.sql` | `user_groups`, `group_members`, `group_permissions`, `group_resource_access`, `group_assignment_rules` |
| 043 | `feature_flags.sql` | `feature_flags`, `tenant_feature_flags` (9 seeded flags) |
| 044 | `security_enhancements.sql` | `ip_access_rules`, `user_ssh_keys`, `deprovisioning_rules`/`log`, `terms_of_service`, `domain_verifications` |
| 045 | `team_hierarchy.sql` | `teams.parent_id`, `team_resources`, `team_permissions`, `company_user_roles`, `team_activity` |
| 046 | `address_history.sql` | `address_history` + auto-audit trigger |
| 047 | `permission_inheritance.sql` | `roles.parent_role_id`, `user_permission_overrides`, `v_user_effective_permissions` view |
| 048 | `social_providers.sql` | `social_providers` (Google/Microsoft/GitHub seeded), `user_social_logins`, `oauth_states` |
| 049 | `billing_extended.sql` | Annual pricing, trials, grace periods, addons (5 seeded), discount codes, payment methods, proration, quotas |
| 050 | `password_recovery.sql` | `password_reset_tokens`, `email_verification_tokens`, `app_users.email_verified_at` + `must_change_password` |
| 051 | `seed_data.sql` | Baseline seed data (subscription plans, module catalog, release channels) |
| 052 | `tenant_sizing.sql` | `tenant_connection_map` + dedicated DB tracking + provisioning jobs + auto-upgrade trigger |
| 053 | `rls_timescale_citus.sql` | Per-op RLS policies, TimescaleDB hypertables + compression + continuous aggregates, Citus scaffolding, logical replication publications, sharding threshold function |
| 054 | `crm_core.sql` | CRM accounts, contacts M:N, deal pipeline (5 seeded stages), leads + scoring, unified activities |
| 055 | `crm_quotes_products.sql` | Quote versioning + line items, products + price books + price book entries with volume tiers |
| 056 | `email_integration.sql` | `email_accounts`, `email_templates` (4 seeded), `email_messages`, `email_tracking_events`, `email_attachments` |
| 057 | `crm_dashboards_reports.sql` | `saved_reports`, `forecast_snapshots`, 4 materialized views (revenue/activity/lead_source_roi/account_health), `refresh_crm_dashboards()` + hourly job |
| 058 | `crm_integrations.sql` | Salesforce/HubSpot/Dynamics/Exchange/Gmail/Pipedrive integrations seeded, `sync_configs`, `crm_external_ids`, `crm_sync_conflicts`, `crm_documents`, `crm_document_templates`, `crm_document_approvals` |
| 059 | `crm_webhooks_polish.sql` | `webhook_event_types` (28 seeded), `webhook_subscriptions`, `webhook_deliveries`, deal_won trigger, contact/sd_requester auto-link, switch_guide.crm_account_id, task_projects.crm_deal_id |
| 060 | `crm_campaigns.sql` | `crm_campaigns` + members + costs with auto-cost-recalc trigger |
| 061 | `crm_segments_sequences.sql` | `crm_segments` (static + dynamic), `crm_email_sequences` + steps + enrollments, `crm_landing_pages`, `crm_forms` + form_submissions |
| 062 | `crm_attribution.sql` | `crm_utm_events`, `crm_attribution_touches` (5 models), `generate_attribution()` fn, `crm_campaign_influence` matview, hourly refresh job |
| 063 | `crm_territories.sql` | `crm_territories` + members + rules, territory FK on accounts and leads |
| 064 | `crm_quotas_commissions.sql` | `crm_quotas`, `crm_commission_plans` + tiers + user assignment + payouts |
| 065 | `crm_account_teams.sql` | `crm_opportunity_splits` (100% validation trigger), `crm_account_teams`, `crm_account_plans` + stakeholders, `crm_org_chart_edges` |
| 066 | `crm_forecast_hierarchies.sql` | `crm_forecast_adjustments`, `crm_pipeline_health` matview, `crm_deal_insights` + `generate_deal_insights()` fn |
| 067 | `crm_cpq.sql` | `crm_product_bundles` + components, `crm_pricing_rules`, `crm_discount_approval_matrix` (4 seeded tiers) |
| 068 | `approval_engine.sql` | `approval_requests` + steps + actions + auto-resolve trigger (generic, reusable) |
| 069 | `crm_contracts.sql` | `crm_contract_clauses` library, `crm_contract_templates`, `crm_contracts`, `crm_contract_versions`, `crm_contract_clause_usage`, `crm_contract_milestones`, `crm_contract_renewals` view |
| 070 | `crm_subscriptions_revenue.sql` | `crm_subscriptions` + events with auto-log trigger, `crm_mrr_dashboard` matview, `crm_revenue_schedules` + entries (ASC 606), `generate_revenue_entries()` fn |
| 071 | `crm_orders.sql` | `crm_orders` + order_lines with auto-recalc-totals + auto-create-subscription-from-order-line triggers, 16 new webhook event types |
| 072 | `portals_community.sql` | `portal_users` (customer/partner), `magic_links`, `portal_sessions`, `partner_deal_registrations`, `kb_categories`, `kb_articles` (tsvector FTS), `community_threads` + `community_posts` + `community_votes`, auto-update triggers |
| 073 | `rule_engines.sql` | `validation_rules` (JSONLogic), `workflow_rules` (Elsa-integrated), `rule_execution_log`, universal `broadcast_record_change` trigger attached to 7 CRM tables for pg_notify |
| 074 | `custom_objects_field_security.sql` | `custom_entities`, `custom_fields`, `custom_entity_records` (jsonb + GIN), `custom_field_values`, `custom_relationships`, `field_permissions`, `get_field_permission()` function |
| 075 | `import_commerce.sql` | `import_jobs` + `import_job_rows` with dedup strategies, `shopping_carts` + `cart_items` with auto-recalc trigger, `payments` with Stripe-compatible fields |
| 076 | `ai_providers.sql` | `ai_providers` (8 platform providers seeded), `ai_models` (9 seeded), `tenant_ai_providers`, `tenant_ai_feature_mappings`, `ai_usage_log`, `resolve_ai_provider()` SQL fn |
| 077 | `ai_ml_scoring.sql` | `ai_ml_models`, `ai_training_jobs`, `ai_model_scores`, `ai_next_best_actions` + ML score columns on crm_leads/crm_deals/crm_accounts |
| 078 | `ai_assistant.sql` | `ai_conversations`, `ai_messages`, `ai_prompt_templates` (4 seeded), `ai_tool_definitions` (6 seeded) |
| 079 | `ai_dedup_enrichment.sql` | `crm_duplicate_rules`, `crm_merge_operations`, `crm_enrichment_providers` (5 seeded), `crm_enrichment_jobs`, `v_contact_duplicate_candidates` view (pg_trgm) |
| 080 | `ai_churn_calls.sql` | `crm_churn_risks`, `crm_account_ltv`, `crm_call_recordings`, `crm_auto_capture_queue`, 8 AI-related webhook event types |
| 081 | `desktop_missing_tables.sql` | Hotfix for local dev — tables the desktop expects that were lost in earlier renumber (identity_providers, auth_events, etc.) |
| 082 | `app_users_auth_columns.sql` | `password_changed_at`, `mfa_secret_enc` and other auth columns the desktop PermissionRepository queries |
| 083 | `module_catalog_reconcile.sql` | Reconcile `module_catalog` with the post-consolidation WPF module layout |
| 084 | `net_schema_foundation.sql` | **Networking engine — Phase 1.** `net` schema, universal entity base columns (status, lock_state, version, etc.), `net.entity_status` + `net.lock_state` enums, `net.entity_index` registry |
| 085 | `net_hierarchy.sql` | **Phase 2.** `net.region`, `net.site`, `net.building`, `net.floor`, `net.room`, `net.rack` with parent FKs + per-tenant uniqueness on (parent, code) |
| 086 | `net_pools.sql` | **Phase 3.** 16 pool tables (asn/vlan/mlag/subnet/loopback/etc.) + GIST EXCLUDE on `net.subnet` (no overlap per tenant via `btree_gist` + `inet_ops`) |
| 087 | `net_immunocore_import.sql` | **Phase 3 import.** Seeds Immunocore ASN pool/blocks/allocations, loopback /24 subnets, 63 site VLANs from legacy switches data |
| 088 | `net_devices.sql` | **Phase 4.** 7 device tables (`net.device_role` + 12 role catalog, `net.device`, `net.device_interface`, `net.device_optic`, etc.) |
| 089 | `net_device_import.sql` | **Phase 4 import.** Imports from legacy `switches` with role-prefix disambiguation (hostname contains "CORE" → prefer L1Core over L1SW) |
| 090 | `net_device_dual_write.sql` | **Phase 4 dual-write.** Bidirectional sync trigger between legacy `public.switches` and `net.device` with txn-scoped reentrancy guard |
| 091 | `net_links.sql` | **Phase 5.** 3 unified link tables (`net.link_type` + 7-type catalog, `net.link`, `net.link_endpoint`) replacing P2P/B2B/FW separate tables |
| 092 | `net_link_import.sql` | **Phase 5 import.** Migrates 2,826 legacy P2P/B2B/FW rows into the unified `net.link` model preserving `legacy_link_kind` + `legacy_link_id` |
| 093 | `net_device_naming.sql` | **Chunk A.** `naming_template` column on `net.device_role` with per-role seeds — brace-substitution template engine for hostname conventions |
| 094 | `net_servers.sql` | **Phase 6.** 3 server tables (`net.server`, `net.server_nic`, `net.server_profile`) with `Server4NIC` profile seeded for fan-out to building cores |
| 095 | `net_server_import.sql` | **Phase 6 import.** Imports 160 legacy server rows → 31 unique servers via UNIQUE hostname dedup |
| 096 | `net_server_dual_write.sql` | **Phase 6 dual-write.** Bidirectional sync trigger between legacy `public.servers` and `net.server` |
| 097 | `net_naming_overrides.sql` | **Phase 7a.** `net.naming_template_override` (entity_type + subtype_code + scope_level + scope_entity_id + template) — admin-editable overrides for the naming resolver |
| 100 | `net_enforce_lock_state.sql` | **Phase 8f.** Trigger-enforced HardLock / Immutable on the `net.*` tables — blocks UPDATE / DELETE from any session when lock_state ∈ (HardLock, Immutable) |
| 101 | `net_validation_rules.sql` | **Phase 9a.** `net.tenant_rule_config` for per-tenant severity / enabled overrides (rule SQL lives in Rust; this table is config-only) |
| 102 | `net_cli_flavors.sql` | **Phase 10.** CLI flavor state — tenant.cli_flavor preference + render-history table (`net.device_config_render`) with SHA-256 content-chain for tamper detection |
| 103 | `net_dhcp_relay_targets.sql` | **Phase 10.** `net.dhcp_relay_target` M:N (vlan × server_ip) with priority ordering + linked_ip_address_id |
| 104 | `net_immunocore_seed_gateway_vrrp_dhcp.sql` | **Phase 10.** Seeds Gateway + VRRP VIPs + DHCP relay targets from `public.vrrp_config` + `public.dhcp_relay` so config-gen reaches byte-parity on Immunocore out-of-the-box |
| 105 | `net_scope_grants.sql` | **Phase 10 RBAC foundation.** `net.scope_grant` tuple — (user_id, action, entity_type, scope_type, scope_entity_id); resolver walks hierarchy Region→Site→Building for Device/Server/Building/Site entity types |
| 106 | `net_saved_views.sql` | **Phase 10.** `net.saved_view` per-user named query state for the Search panel — UNIQUE (org, user_id, name); `filters jsonb` unstructured for future facet additions |
| 107 | `net_search_gin_indexes.sql` | **Phase 10.** Six partial GIN indexes (one per search-target entity) on `to_tsvector('english'::regconfig, …)` expressions — backs the dynamic-UNION search with sub-ms index scans; `::regconfig` cast required so the expression is IMMUTABLE |

---
