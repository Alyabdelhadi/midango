/// Where the API lives. Override per build: --dart-define=API_BASE_URL=http://10.0.2.2:5300
const apiBase = String.fromEnvironment('API_BASE_URL', defaultValue: 'http://localhost:5300');
