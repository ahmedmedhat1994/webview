import 'package:flutter_test/flutter_test.dart';
import 'package:vopecs_pos/services/url_validator_service.dart';

void main() {
  group('URL Validator Tests', () {
    final validator = UrlValidatorService.instance;

    test('Valid HTTPS URL should pass', () {
      final result = validator.validate('https://example.com');
      expect(result.isValid, true);
    });

    test('HTTP URL should fail', () {
      final result = validator.validate('http://example.com');
      expect(result.isValid, false);
      expect(result.errorKey, 'https_required');
    });

    test('Empty URL should fail', () {
      final result = validator.validate('');
      expect(result.isValid, false);
      expect(result.errorKey, 'url_empty');
    });

    test('Null URL should fail', () {
      final result = validator.validate(null);
      expect(result.isValid, false);
      expect(result.errorKey, 'url_empty');
    });

    test('file:// URL should fail', () {
      final result = validator.validate('file:///path/to/file');
      expect(result.isValid, false);
    });

    test('about: URL should fail', () {
      final result = validator.validate('about:blank');
      expect(result.isValid, false);
    });

    test('URL normalization adds https', () {
      final normalized = validator.normalizeUrl('example.com');
      expect(normalized, 'https://example.com');
    });

    test('URL normalization removes trailing slash', () {
      final normalized = validator.normalizeUrl('https://example.com/');
      expect(normalized, 'https://example.com');
    });
  });
}
