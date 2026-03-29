@echo off
REM Start the Central API Gateway (Rust)
REM Routes all API traffic to backend services

set GATEWAY_HOST=0.0.0.0
set GATEWAY_PORT=8000
set AUTH_SERVICE_URL=http://localhost:8081
set ADMIN_SERVICE_URL=http://localhost:8080
set AUDIT_SERVICE_URL=http://localhost:8082
set SYNC_SERVICE_URL=http://localhost:8083
set STORAGE_SERVICE_URL=http://localhost:8084
set CENTRAL_API_URL=http://localhost:5000
set AUTH_SERVICE_JWT_SECRET=central-dev-jwt-secret-change-in-production-2026
set RUST_LOG=gateway_service=info,tower_http=info

echo Starting Central API Gateway on port %GATEWAY_PORT%...
echo Routes: auth(8081) admin(8080) audit(8082) sync(8083) storage(8084) central-api(5000)
echo.

"%~dp0..\Secure\SecureAPP\target\release\gateway-service.exe"
