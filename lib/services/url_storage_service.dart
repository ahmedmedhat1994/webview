import 'package:shared_preferences/shared_preferences.dart';
import '../core/constants/storage_keys.dart';

class UrlStorageService {
  static UrlStorageService? _instance;
  static SharedPreferences? _prefs;

  UrlStorageService._();

  static Future<UrlStorageService> getInstance() async {
    _instance ??= UrlStorageService._();
    _prefs ??= await SharedPreferences.getInstance();
    // إعادة تحميل البيانات للتأكد من الحصول على أحدث القيم
    await _prefs!.reload();
    return _instance!;
  }

  // Saved URL (main URL)
  Future<bool> saveUrl(String url) async {
    return await _prefs!.setString(StorageKeys.savedUrl, url);
  }

  String? getSavedUrl() {
    return _prefs!.getString(StorageKeys.savedUrl);
  }

  Future<bool> clearSavedUrl() async {
    return await _prefs!.remove(StorageKeys.savedUrl);
  }

  // Last Visited URL
  Future<bool> saveLastVisitedUrl(String url) async {
    return await _prefs!.setString(StorageKeys.lastVisitedUrl, url);
  }

  String? getLastVisitedUrl() {
    return _prefs!.getString(StorageKeys.lastVisitedUrl);
  }

  // Language
  Future<bool> saveLanguageCode(String code) async {
    return await _prefs!.setString(StorageKeys.languageCode, code);
  }

  String getLanguageCode() {
    return _prefs!.getString(StorageKeys.languageCode) ?? 'en';
  }

  // First Launch
  Future<bool> setFirstLaunchDone() async {
    return await _prefs!.setBool(StorageKeys.isFirstLaunch, false);
  }

  bool isFirstLaunch() {
    return _prefs!.getBool(StorageKeys.isFirstLaunch) ?? true;
  }

  // Check if URL exists
  bool hasStoredUrl() {
    final url = getSavedUrl();
    return url != null && url.isNotEmpty;
  }

  // Get URL to load (last visited or saved)
  String? getUrlToLoad() {
    return getLastVisitedUrl() ?? getSavedUrl();
  }
}
