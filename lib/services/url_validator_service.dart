import '../core/constants/app_constants.dart';

class UrlValidatorService {
  UrlValidatorService._();

  static final UrlValidatorService instance = UrlValidatorService._();

  /// Validates if the URL is valid and secure
  ValidationResult validate(String? url) {
    if (url == null || url.trim().isEmpty) {
      return ValidationResult(
        isValid: false,
        errorKey: 'url_empty',
      );
    }

    final trimmedUrl = url.trim();

    // Check for blocked schemes
    for (final scheme in AppConstants.blockedSchemes) {
      if (trimmedUrl.toLowerCase().startsWith(scheme)) {
        return ValidationResult(
          isValid: false,
          errorKey: 'invalid_url',
        );
      }
    }

    // Check HTTPS requirement
    if (!trimmedUrl.toLowerCase().startsWith(AppConstants.httpsPrefix)) {
      return ValidationResult(
        isValid: false,
        errorKey: 'https_required',
      );
    }

    // Validate URL format
    try {
      final uri = Uri.parse(trimmedUrl);
      if (!uri.hasScheme || uri.host.isEmpty) {
        return ValidationResult(
          isValid: false,
          errorKey: 'invalid_url',
        );
      }
    } catch (_) {
      return ValidationResult(
        isValid: false,
        errorKey: 'invalid_url',
      );
    }

    return ValidationResult(isValid: true);
  }

  /// Quick check if URL is valid
  bool isValid(String? url) {
    return validate(url).isValid;
  }

  /// Normalizes URL (adds https:// if missing, trims whitespace)
  String normalizeUrl(String url) {
    var normalized = url.trim();

    // If URL doesn't have a scheme, add https://
    if (!normalized.contains('://')) {
      normalized = '${AppConstants.httpsPrefix}$normalized';
    }

    // Remove trailing slash
    if (normalized.endsWith('/')) {
      normalized = normalized.substring(0, normalized.length - 1);
    }

    return normalized;
  }

  /// Check if URL is external (different domain)
  bool isExternalUrl(String currentUrl, String newUrl) {
    try {
      final currentUri = Uri.parse(currentUrl);
      final newUri = Uri.parse(newUrl);
      return currentUri.host != newUri.host;
    } catch (_) {
      return true;
    }
  }
}

class ValidationResult {
  final bool isValid;
  final String? errorKey;

  ValidationResult({
    required this.isValid,
    this.errorKey,
  });
}
