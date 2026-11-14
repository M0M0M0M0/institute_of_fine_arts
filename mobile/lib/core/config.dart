/// Địa chỉ Web API.
/// Mặc định là API chạy trên máy (http://localhost:5000 – chính web app, API nằm chung trong đó). Muốn đổi khi chạy:
///   flutter run -d chrome --dart-define=API_BASE_URL=http://192.168.1.10:5000
const apiBaseUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'http://localhost:5000',
);
