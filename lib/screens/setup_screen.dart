import 'package:flutter/material.dart';
import '../core/localization/app_localizations.dart';
import '../services/url_storage_service.dart';
import '../services/url_validator_service.dart';
import '../widgets/url_input_field.dart';
import 'webview_screen.dart';

class SetupScreen extends StatefulWidget {
  const SetupScreen({super.key});

  @override
  State<SetupScreen> createState() => _SetupScreenState();
}

class _SetupScreenState extends State<SetupScreen> {
  final _urlController = TextEditingController();
  bool _isLoading = false;

  @override
  void dispose() {
    _urlController.dispose();
    super.dispose();
  }

  Future<void> _saveAndContinue() async {
    final url = _urlController.text.trim();

    // Validate URL
    final result = UrlValidatorService.instance.validate(url);
    if (!result.isValid) {
      _showError(context.tr(result.errorKey ?? 'invalid_url'));
      return;
    }

    setState(() => _isLoading = true);

    try {
      // Normalize and save URL
      final normalizedUrl = UrlValidatorService.instance.normalizeUrl(url);
      final storageService = await UrlStorageService.getInstance();
      await storageService.saveUrl(normalizedUrl);
      await storageService.setFirstLaunchDone();

      if (!mounted) return;

      // Navigate to WebView
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(
          builder: (_) => const WebViewScreen(),
        ),
      );
    } catch (e) {
      if (!mounted) return;
      _showError(context.tr('something_went_wrong'));
    } finally {
      if (mounted) {
        setState(() => _isLoading = false);
      }
    }
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

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: 48),
              // Logo
              Container(
                padding: const EdgeInsets.all(24),
                decoration: BoxDecoration(
                  color: theme.colorScheme.primaryContainer,
                  shape: BoxShape.circle,
                ),
                child: Icon(
                  Icons.point_of_sale,
                  size: 64,
                  color: theme.colorScheme.primary,
                ),
              ),
              const SizedBox(height: 32),
              // Welcome text
              Text(
                context.tr('welcome'),
                style: theme.textTheme.headlineMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 8),
              Text(
                context.tr('app_name'),
                style: theme.textTheme.titleLarge?.copyWith(
                  color: theme.colorScheme.primary,
                ),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 48),
              // URL Input
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(
                        context.tr('enter_url'),
                        style: theme.textTheme.titleMedium,
                      ),
                      const SizedBox(height: 16),
                      UrlInputField(
                        controller: _urlController,
                        enabled: !_isLoading,
                        onSubmitted: (_) => _saveAndContinue(),
                      ),
                      const SizedBox(height: 8),
                      Text(
                        context.tr('https_required'),
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: theme.colorScheme.onSurfaceVariant,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 24),
              // Save Button
              FilledButton.icon(
                onPressed: _isLoading ? null : _saveAndContinue,
                icon: _isLoading
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : const Icon(Icons.arrow_forward),
                label: Text(context.tr('save_and_continue')),
                style: FilledButton.styleFrom(
                  padding: const EdgeInsets.symmetric(vertical: 16),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
