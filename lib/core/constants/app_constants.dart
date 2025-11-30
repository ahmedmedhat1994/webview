class AppConstants {
  AppConstants._();

  static const String appName = 'Vopecs POS';
  static const String appVersion = '1.0.0';

  // URL Validation
  static const String httpsPrefix = 'https://';
  static const List<String> blockedSchemes = ['file://', 'about:', 'javascript:'];

  // Animation durations
  static const Duration splashDuration = Duration(seconds: 2);
  static const Duration animationDuration = Duration(milliseconds: 300);
}
