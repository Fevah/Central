@echo off
REM Start the Task Service (Rust)
REM High-performance task management — Hansoft/P4 Plan clone

set TASK_SERVICE_HOST=0.0.0.0
set TASK_SERVICE_PORT=8085
set DATABASE_URL=postgres://central:central@localhost:5432/central
set RUST_LOG=task_service=info,sqlx=warn

echo Starting Task Service on port %TASK_SERVICE_PORT%...
"%~dp0..\Secure\SecureAPP\target\release\task-service.exe"
