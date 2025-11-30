import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:printing/printing.dart';
import 'package:pdf/pdf.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// أنواع الطابعات المدعومة
enum PrinterType {
  standard,  // طابعة عادية A4
  thermal58, // طابعة حرارية 58mm
  thermal80, // طابعة حرارية 80mm
}

/// إعدادات الطباعة
class PrintSettings {
  final String? printerName;
  final String? printerUrl;
  final PrinterType printerType;
  final bool silentPrint;
  final PdfPageFormat pageFormat;

  PrintSettings({
    this.printerName,
    this.printerUrl,
    this.printerType = PrinterType.standard,
    this.silentPrint = false,
    this.pageFormat = PdfPageFormat.a4,
  });

  /// تحويل نوع الطابعة لحجم الورق
  static PdfPageFormat getPageFormat(PrinterType type) {
    switch (type) {
      case PrinterType.thermal58:
        return const PdfPageFormat(58 * PdfPageFormat.mm, double.infinity);
      case PrinterType.thermal80:
        return const PdfPageFormat(80 * PdfPageFormat.mm, double.infinity);
      case PrinterType.standard:
        return PdfPageFormat.a4;
    }
  }

  Map<String, dynamic> toJson() => {
    'printerName': printerName,
    'printerUrl': printerUrl,
    'printerType': printerType.index,
    'silentPrint': silentPrint,
  };

  factory PrintSettings.fromJson(Map<String, dynamic> json) => PrintSettings(
    printerName: json['printerName'],
    printerUrl: json['printerUrl'],
    printerType: PrinterType.values[json['printerType'] ?? 0],
    silentPrint: json['silentPrint'] ?? false,
  );
}

/// خدمة الطباعة المتكاملة
class PrintService {
  static PrintService? _instance;
  static SharedPreferences? _prefs;

  PrintSettings _settings = PrintSettings();
  Printer? _selectedPrinter;
  List<Printer> _availablePrinters = [];

  PrintService._();

  static Future<PrintService> getInstance() async {
    _instance ??= PrintService._();
    _prefs ??= await SharedPreferences.getInstance();
    await _instance!._loadSettings();
    return _instance!;
  }

  // Getters
  PrintSettings get settings => _settings;
  Printer? get selectedPrinter => _selectedPrinter;
  List<Printer> get availablePrinters => _availablePrinters;
  bool get hasPrinter => _selectedPrinter != null || _settings.printerUrl != null;

  /// تحميل الإعدادات المحفوظة
  Future<void> _loadSettings() async {
    final printerName = _prefs!.getString('printer_name');
    final printerUrl = _prefs!.getString('printer_url');
    final printerType = _prefs!.getInt('printer_type') ?? 0;
    final silentPrint = _prefs!.getBool('silent_print') ?? false;

    _settings = PrintSettings(
      printerName: printerName,
      printerUrl: printerUrl,
      printerType: PrinterType.values[printerType],
      silentPrint: silentPrint,
    );

    // محاولة إيجاد الطابعة المحفوظة
    if (printerUrl != null) {
      await refreshPrinters();
      _selectedPrinter = _availablePrinters.where(
        (p) => p.url == printerUrl
      ).firstOrNull;
    }
  }

  /// حفظ الإعدادات
  Future<void> saveSettings(PrintSettings settings) async {
    _settings = settings;
    await _prefs!.setString('printer_name', settings.printerName ?? '');
    await _prefs!.setString('printer_url', settings.printerUrl ?? '');
    await _prefs!.setInt('printer_type', settings.printerType.index);
    await _prefs!.setBool('silent_print', settings.silentPrint);
  }

  /// تحديث قائمة الطابعات المتاحة
  Future<List<Printer>> refreshPrinters() async {
    _availablePrinters = await Printing.listPrinters();
    return _availablePrinters;
  }

  /// اختيار طابعة
  Future<void> selectPrinter(Printer printer) async {
    _selectedPrinter = printer;
    _settings = PrintSettings(
      printerName: printer.name,
      printerUrl: printer.url,
      printerType: _settings.printerType,
      silentPrint: _settings.silentPrint,
    );
    await saveSettings(_settings);
  }

  /// تعيين نوع الطابعة
  Future<void> setPrinterType(PrinterType type) async {
    _settings = PrintSettings(
      printerName: _settings.printerName,
      printerUrl: _settings.printerUrl,
      printerType: type,
      silentPrint: _settings.silentPrint,
    );
    await saveSettings(_settings);
  }

  /// تفعيل/تعطيل الطباعة الصامتة
  Future<void> setSilentPrint(bool silent) async {
    _settings = PrintSettings(
      printerName: _settings.printerName,
      printerUrl: _settings.printerUrl,
      printerType: _settings.printerType,
      silentPrint: silent,
    );
    await saveSettings(_settings);
  }

  /// طباعة من bytes (صورة أو PDF)
  Future<bool> printBytes(Uint8List bytes, {String jobName = 'Print Job'}) async {
    try {
      if (_settings.silentPrint && _selectedPrinter != null) {
        // طباعة صامتة مباشرة
        return await Printing.directPrintPdf(
          printer: _selectedPrinter!,
          onLayout: (_) async => bytes,
          name: jobName,
        );
      } else {
        // طباعة مع dialog
        return await Printing.layoutPdf(
          onLayout: (_) async => bytes,
          name: jobName,
          format: PrintSettings.getPageFormat(_settings.printerType),
        );
      }
    } catch (e) {
      debugPrint('Print error: $e');
      return false;
    }
  }

  /// طباعة صفحة HTML (من WebView)
  Future<bool> printHtml(String html, {String jobName = 'Receipt'}) async {
    try {
      if (_settings.silentPrint && _selectedPrinter != null) {
        // تحويل HTML إلى PDF ثم طباعة صامتة
        final pdfBytes = await Printing.convertHtml(
          format: PrintSettings.getPageFormat(_settings.printerType),
          html: html,
        );
        return await Printing.directPrintPdf(
          printer: _selectedPrinter!,
          onLayout: (_) async => pdfBytes,
          name: jobName,
        );
      } else {
        // طباعة مع dialog
        return await Printing.layoutPdf(
          onLayout: (_) async => await Printing.convertHtml(
            format: PrintSettings.getPageFormat(_settings.printerType),
            html: html,
          ),
          name: jobName,
        );
      }
    } catch (e) {
      debugPrint('Print HTML error: $e');
      return false;
    }
  }

  /// طباعة اختبارية
  Future<bool> testPrint() async {
    const testHtml = '''
    <html>
      <head>
        <style>
          body { font-family: Arial; text-align: center; padding: 20px; }
          h1 { color: #333; }
          .info { margin: 10px 0; color: #666; }
          .success { color: green; font-size: 24px; margin: 20px 0; }
        </style>
      </head>
      <body>
        <h1>🖨️ Vopecs POS</h1>
        <div class="success">✓ Test Print Successful</div>
        <div class="info">Printer is configured correctly</div>
        <div class="info">الطابعة تعمل بشكل صحيح</div>
      </body>
    </html>
    ''';

    return await printHtml(testHtml, jobName: 'Test Print');
  }

  /// مسح إعدادات الطابعة
  Future<void> clearPrinter() async {
    _selectedPrinter = null;
    _settings = PrintSettings();
    await _prefs!.remove('printer_name');
    await _prefs!.remove('printer_url');
    await _prefs!.remove('printer_type');
    await _prefs!.remove('silent_print');
  }
}
