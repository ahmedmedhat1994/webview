import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:printing/printing.dart';
import '../core/constants/app_constants.dart';
import '../core/localization/app_localizations.dart';
import '../services/url_storage_service.dart';
import '../services/url_validator_service.dart';
import '../services/print_service.dart';
import '../providers/locale_provider.dart';
import '../widgets/url_input_field.dart';
import 'webview_screen.dart';

class SettingsScreen extends StatefulWidget {
  const SettingsScreen({super.key});

  @override
  State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
  final _urlController = TextEditingController();
  UrlStorageService? _storageService;
  PrintService? _printService;
  String? _currentUrl;
  bool _isLoading = true;
  bool _isPrintLoading = false;
  List<Printer> _printers = [];

  @override
  void initState() {
    super.initState();
    _loadSettings();
  }

  @override
  void dispose() {
    _urlController.dispose();
    super.dispose();
  }

  Future<void> _loadSettings() async {
    _storageService = await UrlStorageService.getInstance();
    _printService = await PrintService.getInstance();
    _currentUrl = _storageService!.getSavedUrl();
    _urlController.text = _currentUrl ?? '';

    await _loadPrinters();
    setState(() => _isLoading = false);
  }

  Future<void> _loadPrinters() async {
    setState(() => _isPrintLoading = true);
    _printers = await _printService!.refreshPrinters();
    setState(() => _isPrintLoading = false);
  }

  Future<void> _changeUrl() async {
    final newUrl = _urlController.text.trim();

    final result = UrlValidatorService.instance.validate(newUrl);
    if (!result.isValid) {
      _showError(context.tr(result.errorKey ?? 'invalid_url'));
      return;
    }

    final confirmed = await _showConfirmDialog();
    if (!confirmed) return;

    setState(() => _isLoading = true);

    try {
      final normalizedUrl = UrlValidatorService.instance.normalizeUrl(newUrl);
      await _storageService!.saveUrl(normalizedUrl);
      await _storageService!.saveLastVisitedUrl(normalizedUrl);

      if (!mounted) return;

      _showSuccess(context.tr('url_saved'));

      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const WebViewScreen()),
        (route) => false,
      );
    } catch (e) {
      if (!mounted) return;
      _showError(context.tr('something_went_wrong'));
      setState(() => _isLoading = false);
    }
  }

  Future<bool> _showConfirmDialog() async {
    return await showDialog<bool>(
          context: context,
          builder: (context) => AlertDialog(
            title: Text(context.tr('confirm_change_url')),
            content: Text(context.tr('confirm_change_url_message')),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(context).pop(false),
                child: Text(context.tr('cancel')),
              ),
              FilledButton(
                onPressed: () => Navigator.of(context).pop(true),
                child: Text(context.tr('confirm')),
              ),
            ],
          ),
        ) ??
        false;
  }

  Future<void> _selectPrinter(Printer printer) async {
    await _printService!.selectPrinter(printer);
    setState(() {});
    if (mounted) {
      Navigator.of(context).pop();
    }
  }

  Future<void> _testPrint() async {
    setState(() => _isPrintLoading = true);
    final success = await _printService!.testPrint();
    setState(() => _isPrintLoading = false);

    if (mounted) {
      if (success) {
        _showSuccess(context.tr('print_success'));
      } else {
        _showError(context.tr('print_error'));
      }
    }
  }

  void _showPrinterSelector() {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (context) => DraggableScrollableSheet(
        initialChildSize: 0.5,
        minChildSize: 0.3,
        maxChildSize: 0.8,
        expand: false,
        builder: (context, scrollController) => Column(
          children: [
            Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  Text(
                    context.tr('select_printer'),
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  const Spacer(),
                  IconButton(
                    icon: const Icon(Icons.refresh),
                    onPressed: () async {
                      await _loadPrinters();
                      if (mounted) setState(() {});
                    },
                  ),
                ],
              ),
            ),
            const Divider(height: 1),
            Expanded(
              child: _printers.isEmpty
                  ? Center(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          const Icon(Icons.print_disabled, size: 48, color: Colors.grey),
                          const SizedBox(height: 16),
                          Text(context.tr('no_printers_found')),
                          const SizedBox(height: 16),
                          OutlinedButton.icon(
                            onPressed: _loadPrinters,
                            icon: const Icon(Icons.refresh),
                            label: Text(context.tr('refresh_printers')),
                          ),
                        ],
                      ),
                    )
                  : ListView.builder(
                      controller: scrollController,
                      itemCount: _printers.length,
                      itemBuilder: (context, index) {
                        final printer = _printers[index];
                        final isSelected = printer.url == _printService?.settings.printerUrl;
                        return ListTile(
                          leading: Icon(
                            Icons.print,
                            color: isSelected
                                ? Theme.of(context).colorScheme.primary
                                : null,
                          ),
                          title: Text(
                            printer.name,
                            style: TextStyle(
                              fontWeight: isSelected ? FontWeight.bold : null,
                            ),
                          ),
                          subtitle: printer.isDefault ? const Text('Default') : null,
                          trailing: isSelected
                              ? Icon(
                                  Icons.check_circle,
                                  color: Theme.of(context).colorScheme.primary,
                                )
                              : null,
                          onTap: () => _selectPrinter(printer),
                        );
                      },
                    ),
            ),
          ],
        ),
      ),
    );
  }

  void _showError(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: Theme.of(context).colorScheme.error,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  void _showSuccess(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: Colors.green,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final localeProvider = context.watch<LocaleProvider>();

    return Scaffold(
      appBar: AppBar(
        title: Text(context.tr('settings')),
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : ListView(
              padding: const EdgeInsets.all(16),
              children: [
                // Print Settings Section
                _buildPrintSettingsCard(theme),
                const SizedBox(height: 16),

                // URL Section
                _buildUrlCard(theme),
                const SizedBox(height: 16),

                // Language Section
                _buildLanguageCard(theme, localeProvider),
                const SizedBox(height: 16),

                // About Section
                _buildAboutCard(theme),
              ],
            ),
    );
  }

  Widget _buildPrintSettingsCard(ThemeData theme) {
    final settings = _printService?.settings;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.print, color: theme.colorScheme.primary),
                const SizedBox(width: 8),
                Text(
                  context.tr('print_settings'),
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),

            // Selected Printer
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.print_outlined),
              title: Text(context.tr('select_printer')),
              subtitle: Text(
                settings?.printerName ?? context.tr('no_printer_selected'),
                style: TextStyle(
                  color: settings?.printerName != null
                      ? theme.colorScheme.primary
                      : theme.colorScheme.onSurfaceVariant,
                ),
              ),
              trailing: const Icon(Icons.chevron_right),
              onTap: _showPrinterSelector,
            ),

            const Divider(),

            // Printer Type
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.straighten),
              title: Text(context.tr('printer_type')),
              subtitle: Text(_getPrinterTypeText(settings?.printerType)),
              trailing: const Icon(Icons.chevron_right),
              onTap: _showPrinterTypeSelector,
            ),

            const Divider(),

            // Silent Print Toggle
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              secondary: const Icon(Icons.flash_on),
              title: Text(context.tr('silent_print')),
              subtitle: Text(
                context.tr('silent_print_desc'),
                style: theme.textTheme.bodySmall,
              ),
              value: settings?.silentPrint ?? false,
              onChanged: (value) async {
                await _printService!.setSilentPrint(value);
                setState(() {});
              },
            ),

            const SizedBox(height: 16),

            // Test Print Button
            SizedBox(
              width: double.infinity,
              child: OutlinedButton.icon(
                onPressed: _isPrintLoading ? null : _testPrint,
                icon: _isPrintLoading
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.print),
                label: Text(context.tr('test_print')),
              ),
            ),
          ],
        ),
      ),
    );
  }

  String _getPrinterTypeText(PrinterType? type) {
    switch (type) {
      case PrinterType.thermal58:
        return context.tr('thermal_58');
      case PrinterType.thermal80:
        return context.tr('thermal_80');
      case PrinterType.standard:
      default:
        return context.tr('standard_printer');
    }
  }

  void _showPrinterTypeSelector() {
    showModalBottomSheet(
      context: context,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Padding(
              padding: const EdgeInsets.all(16),
              child: Text(
                context.tr('printer_type'),
                style: Theme.of(context).textTheme.titleLarge,
              ),
            ),
            const Divider(height: 1),
            _buildPrinterTypeOption(PrinterType.standard, Icons.description),
            _buildPrinterTypeOption(PrinterType.thermal58, Icons.receipt),
            _buildPrinterTypeOption(PrinterType.thermal80, Icons.receipt_long),
            const SizedBox(height: 16),
          ],
        ),
      ),
    );
  }

  Widget _buildPrinterTypeOption(PrinterType type, IconData icon) {
    final isSelected = _printService?.settings.printerType == type;
    return ListTile(
      leading: Icon(icon),
      title: Text(_getPrinterTypeText(type)),
      trailing: isSelected
          ? Icon(Icons.check_circle, color: Theme.of(context).colorScheme.primary)
          : null,
      onTap: () async {
        await _printService!.setPrinterType(type);
        setState(() {});
        if (mounted) Navigator.of(context).pop();
      },
    );
  }

  Widget _buildUrlCard(ThemeData theme) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.link, color: theme.colorScheme.primary),
                const SizedBox(width: 8),
                Text(
                  context.tr('change_url'),
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            Text(
              '${context.tr('current_url')}:',
              style: theme.textTheme.bodySmall,
            ),
            const SizedBox(height: 4),
            SelectableText(
              _currentUrl ?? '-',
              style: theme.textTheme.bodyMedium?.copyWith(
                color: theme.colorScheme.primary,
              ),
            ),
            const SizedBox(height: 16),
            const Divider(),
            const SizedBox(height: 16),
            Text(
              context.tr('new_url'),
              style: theme.textTheme.bodySmall,
            ),
            const SizedBox(height: 8),
            UrlInputField(
              controller: _urlController,
              onSubmitted: (_) => _changeUrl(),
            ),
            const SizedBox(height: 16),
            SizedBox(
              width: double.infinity,
              child: FilledButton.icon(
                onPressed: _changeUrl,
                icon: const Icon(Icons.save),
                label: Text(context.tr('save')),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildLanguageCard(ThemeData theme, LocaleProvider localeProvider) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.language, color: theme.colorScheme.primary),
                const SizedBox(width: 8),
                Text(
                  context.tr('language'),
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            _buildLanguageOption(
              context: context,
              title: 'English',
              isSelected: !localeProvider.isArabic,
              onTap: () => localeProvider.setLocale(const Locale('en')),
            ),
            const SizedBox(height: 8),
            _buildLanguageOption(
              context: context,
              title: 'العربية',
              isSelected: localeProvider.isArabic,
              onTap: () => localeProvider.setLocale(const Locale('ar')),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildAboutCard(ThemeData theme) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.info_outline, color: theme.colorScheme.primary),
                const SizedBox(width: 8),
                Text(
                  context.tr('about'),
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            _buildInfoRow(context.tr('app_name'), AppConstants.appName),
            const Divider(height: 24),
            _buildInfoRow(context.tr('version'), AppConstants.appVersion),
          ],
        ),
      ),
    );
  }

  Widget _buildLanguageOption({
    required BuildContext context,
    required String title,
    required bool isSelected,
    required VoidCallback onTap,
  }) {
    final theme = Theme.of(context);

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        decoration: BoxDecoration(
          border: Border.all(
            color: isSelected
                ? theme.colorScheme.primary
                : theme.colorScheme.outline,
            width: isSelected ? 2 : 1,
          ),
          borderRadius: BorderRadius.circular(12),
          color: isSelected
              ? theme.colorScheme.primaryContainer.withOpacity(0.3)
              : null,
        ),
        child: Row(
          children: [
            Text(
              title,
              style: theme.textTheme.bodyLarge?.copyWith(
                fontWeight: isSelected ? FontWeight.bold : null,
              ),
            ),
            const Spacer(),
            if (isSelected)
              Icon(Icons.check_circle, color: theme.colorScheme.primary),
          ],
        ),
      ),
    );
  }

  Widget _buildInfoRow(String label, String value) {
    final theme = Theme.of(context);

    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label, style: theme.textTheme.bodyMedium),
        Text(
          value,
          style: theme.textTheme.bodyMedium?.copyWith(
            fontWeight: FontWeight.bold,
          ),
        ),
      ],
    );
  }
}
