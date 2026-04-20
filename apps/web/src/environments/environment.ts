export const environment = {
  production: false,
  gatewayUrl: 'http://192.168.56.203:8000',
  authServiceUrl: 'http://localhost:8082',
  taskServiceUrl: 'http://192.168.56.203:8000',
  centralApiUrl: 'http://192.168.56.203:8000',
  /// Networking engine Rust service URL — Phase 10 endpoints
  /// (bulk, search, scope grants, saved views, audit) proxy
  /// through the gateway or hit the service directly.
  networkingEngineUrl: 'http://192.168.56.203:8000',
  defaultTenantId: '00000000-0000-0000-0000-000000000001',
};
