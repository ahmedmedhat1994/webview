import 'package:flutter/material.dart';
import '../services/url_storage_service.dart';

class LocaleProvider extends ChangeNotifier {
  Locale _locale = const Locale('en');
  UrlStorageService? _storageService;

  Locale get locale => _locale;
  bool get isArabic => _locale.languageCode == 'ar';

  Future<void> initialize(UrlStorageService storageService) async {
    _storageService = storageService;
    final savedCode = storageService.getLanguageCode();
    _locale = Locale(savedCode);
    notifyListeners();
  }

  Future<void> setLocale(Locale locale) async {
    if (_locale == locale) return;

    _locale = locale;
    await _storageService?.saveLanguageCode(locale.languageCode);
    notifyListeners();
  }

  Future<void> toggleLocale() async {
    final newLocale = isArabic ? const Locale('en') : const Locale('ar');
    await setLocale(newLocale);
  }
}
