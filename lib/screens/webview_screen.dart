import 'dart:async';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_inappwebview/flutter_inappwebview.dart';
import '../core/localization/app_localizations.dart';
import '../services/url_storage_service.dart';
import '../services/connectivity_service.dart';
import '../widgets/loading_indicator.dart';
import '../widgets/error_widget.dart';
import 'settings_screen.dart';

class WebViewScreen extends StatefulWidget {
  const WebViewScreen({super.key});

  @override
  State<WebViewScreen> createState() => _WebViewScreenState();
}

class _WebViewScreenState extends State<WebViewScreen> with WidgetsBindingObserver {
  InAppWebViewController? _controller;
  UrlStorageService? _storageService;

  bool _isLoading = true;
  bool _hasError = false;
  bool _isPrinting = false;
  bool _isFabExpanded = false;
  String _currentUrl = '';
  String _homeUrl = '';
  int _loadingProgress = 0;

  StreamSubscription<bool>? _connectivitySubscription;

  InAppWebViewSettings get _settings => InAppWebViewSettings(
    useShouldOverrideUrlLoading: true,
    mediaPlaybackRequiresUserGesture: false,
    javaScriptEnabled: true,
    javaScriptCanOpenWindowsAutomatically: true,
    supportZoom: true,
    allowsInlineMediaPlayback: !Platform.isWindows,
    iframeAllowFullscreen: true,
  );

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _initServices();
    _listenToConnectivity();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _connectivitySubscription?.cancel();
    _saveCurrentUrl();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.paused) {
      _saveCurrentUrl();
    }
  }

  void _listenToConnectivity() {
    _connectivitySubscription = ConnectivityService.instance.onConnectivityChanged.listen((isConnected) {
      if (isConnected && _hasError) {
        _reload();
      }
    });
  }

  Future<void> _initServices() async {
    try {
      _storageService = await UrlStorageService.getInstance();

      // مسح الـ cache عند فتح التطبيق (تخطي على Windows لتجنب المشاكل)
      if (!Platform.isWindows) {
        await _clearCache();
      }

      final url = _storageService!.getUrlToLoad();
      _homeUrl = _storageService!.getSavedUrl() ?? '';

      debugPrint('WebView URL to load: $url');

      if (url == null || url.isEmpty) {
        debugPrint('No URL found, showing error');
        if (mounted) setState(() => _hasError = true);
        return;
      }

      _currentUrl = url;
      if (mounted) setState(() {});
    } catch (e) {
      debugPrint('Error initializing services: $e');
      if (mounted) setState(() => _hasError = true);
    }
  }

  /// مسح cache الـ WebView
  Future<void> _clearCache() async {
    try {
      // تخطي على Windows لأنها قد تسبب مشاكل
      if (Platform.isWindows) return;
      await InAppWebViewController.clearAllCache();
      debugPrint('WebView cache cleared');
    } catch (e) {
      debugPrint('Failed to clear cache: $e');
    }
  }

  Future<void> _saveCurrentUrl() async {
    if (_currentUrl.isNotEmpty) {
      await _storageService?.saveLastVisitedUrl(_currentUrl);
    }
  }

  Future<void> _reload() async {
    setState(() {
      _isLoading = true;
      _hasError = false;
    });
    await _controller?.reload();
  }

  void _openSettings() {
    setState(() => _isFabExpanded = false);
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const SettingsScreen(),
      ),
    );
  }

  /// العودة للصفحة الرئيسية
  Future<void> _goHome() async {
    setState(() => _isFabExpanded = false);
    if (_homeUrl.isNotEmpty && _controller != null) {
      await _controller!.loadUrl(
        urlRequest: URLRequest(url: WebUri(_homeUrl)),
      );
    }
  }

  void _toggleFab() {
    setState(() => _isFabExpanded = !_isFabExpanded);
  }

  /// طباعة الصفحة الحالية باستخدام WebView المدمج
  Future<void> _printCurrentPage() async {
    if (_controller == null || _isPrinting) return;

    setState(() => _isPrinting = true);

    // Timeout لإلغاء اللودينج بعد 10 ثواني لو مفيش استجابة
    Timer? timeoutTimer;
    timeoutTimer = Timer(const Duration(seconds: 10), () {
      if (mounted && _isPrinting) {
        setState(() => _isPrinting = false);
        _showMessage(context.tr('print_timeout') ?? 'Print timeout', isError: true);
      }
    });

    try {
      // استخدام طباعة WebView المدمجة
      final printJobController = await _controller!.printCurrentPage();

      if (printJobController != null) {
        // انتظار اكتمال الطباعة
        printJobController.onComplete = (completed, error) async {
          timeoutTimer?.cancel();
          if (mounted) {
            setState(() => _isPrinting = false);
            if (completed && error == null) {
              _showMessage(context.tr('print_success'), isError: false);
            }
          }
        };
      } else {
        timeoutTimer?.cancel();
        if (mounted) {
          setState(() => _isPrinting = false);
        }
      }
    } catch (e) {
      timeoutTimer?.cancel();
      debugPrint('Print error: $e');
      if (mounted) {
        setState(() => _isPrinting = false);
        _showMessage(context.tr('print_error'), isError: true);
      }
    }
  }

  /// طباعة HTML مباشرة (للفواتير من POS)
  Future<void> _printHtml(String html) async {
    if (_controller == null || _isPrinting) return;

    setState(() => _isPrinting = true);

    Timer? timeoutTimer;
    timeoutTimer = Timer(const Duration(seconds: 15), () {
      if (mounted && _isPrinting) {
        setState(() => _isPrinting = false);
        _showMessage(context.tr('print_timeout') ?? 'Print timeout', isError: true);
      }
    });

    try {
      // حقن HTML في div مخفي ثم طباعته
      // تهريب الـ HTML لتجنب مشاكل الـ quotes
      final escapedHtml = html
          .replaceAll('\\', '\\\\')
          .replaceAll("'", "\\'")
          .replaceAll('\n', '\\n')
          .replaceAll('\r', '\\r');

      await _controller!.evaluateJavascript(source: '''
        (function() {
          // إزالة أي print container سابق
          var oldContainer = document.getElementById('flutter-print-container');
          if (oldContainer) oldContainer.remove();

          // إنشاء container للطباعة
          var container = document.createElement('div');
          container.id = 'flutter-print-container';
          container.innerHTML = '$escapedHtml';

          // إضافة CSS للطباعة
          var style = document.createElement('style');
          style.textContent = '@media print { body > *:not(#flutter-print-container) { display: none !important; } #flutter-print-container { display: block !important; } } @media screen { #flutter-print-container { display: none; } }';
          container.appendChild(style);

          document.body.appendChild(container);
        })();
      ''');

      // انتظار قليل ثم الطباعة
      await Future.delayed(const Duration(milliseconds: 300));

      final printJobController = await _controller!.printCurrentPage();

      if (printJobController != null) {
        printJobController.onComplete = (completed, error) async {
          timeoutTimer?.cancel();
          // تنظيف container بعد الطباعة
          await _controller?.evaluateJavascript(source: '''
            var c = document.getElementById('flutter-print-container');
            if (c) c.remove();
          ''');
          if (mounted) {
            setState(() => _isPrinting = false);
            if (completed && error == null) {
              _showMessage(context.tr('print_success'), isError: false);
            }
          }
        };
      } else {
        timeoutTimer?.cancel();
        // تنظيف container
        await _controller?.evaluateJavascript(source: '''
          var c = document.getElementById('flutter-print-container');
          if (c) c.remove();
        ''');
        if (mounted) {
          setState(() => _isPrinting = false);
        }
      }
    } catch (e) {
      timeoutTimer?.cancel();
      debugPrint('Print HTML error: $e');
      // تنظيف container
      await _controller?.evaluateJavascript(source: '''
        var c = document.getElementById('flutter-print-container');
        if (c) c.remove();
      ''');
      if (mounted) {
        setState(() => _isPrinting = false);
        _showMessage(context.tr('print_error'), isError: true);
      }
    }
  }

  void _showMessage(String message, {bool isError = false}) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: isError ? Theme.of(context).colorScheme.error : Colors.green,
        behavior: SnackBarBehavior.floating,
        duration: const Duration(seconds: 2),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Stack(
          children: [
            _buildBody(),
            // Overlay للإغلاق عند الضغط خارج القائمة
            if (_isFabExpanded)
              Positioned.fill(
                child: GestureDetector(
                  onTap: () => setState(() => _isFabExpanded = false),
                  child: Container(color: Colors.transparent),
                ),
              ),
          ],
        ),
      ),
      floatingActionButton: _buildExpandableFab(),
      floatingActionButtonLocation: FloatingActionButtonLocation.endFloat,
    );
  }

  Widget _buildExpandableFab() {
    final theme = Theme.of(context);

    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        // الأزرار المخفية
        AnimatedContainer(
          duration: const Duration(milliseconds: 200),
          curve: Curves.easeOut,
          height: _isFabExpanded ? 120 : 0,
          child: AnimatedOpacity(
            duration: const Duration(milliseconds: 150),
            opacity: _isFabExpanded ? 1 : 0,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                // زر الصفحة الرئيسية
                _buildMiniAction(
                  icon: Icons.home_rounded,
                  label: context.tr('home'),
                  onTap: _goHome,
                ),
                const SizedBox(height: 8),
                // زر الإعدادات
                _buildMiniAction(
                  icon: Icons.settings_rounded,
                  label: context.tr('settings'),
                  onTap: _openSettings,
                ),
                const SizedBox(height: 12),
              ],
            ),
          ),
        ),
        // الزر الرئيسي
        FloatingActionButton.small(
          onPressed: _toggleFab,
          tooltip: context.tr('menu'),
          backgroundColor: theme.colorScheme.primaryContainer.withOpacity(0.95),
          child: AnimatedRotation(
            turns: _isFabExpanded ? 0.125 : 0,
            duration: const Duration(milliseconds: 200),
            child: Icon(
              _isFabExpanded ? Icons.close_rounded : Icons.apps_rounded,
              color: theme.colorScheme.primary,
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildMiniAction({
    required IconData icon,
    required String label,
    required VoidCallback onTap,
  }) {
    final theme = Theme.of(context);

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(24),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          decoration: BoxDecoration(
            color: theme.colorScheme.surface.withOpacity(0.95),
            borderRadius: BorderRadius.circular(24),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withOpacity(0.1),
                blurRadius: 8,
                offset: const Offset(0, 2),
              ),
            ],
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                label,
                style: theme.textTheme.labelMedium?.copyWith(
                  fontWeight: FontWeight.w500,
                ),
              ),
              const SizedBox(width: 8),
              Container(
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  color: theme.colorScheme.primaryContainer,
                  shape: BoxShape.circle,
                ),
                child: Icon(
                  icon,
                  size: 18,
                  color: theme.colorScheme.primary,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildBody() {
    if (_hasError) {
      return NoConnectionWidget(onRetry: _reload);
    }

    if (_storageService == null || _currentUrl.isEmpty) {
      return LoadingIndicator(message: context.tr('loading'));
    }

    return Stack(
      children: [
        InAppWebView(
          initialUrlRequest: URLRequest(url: WebUri(_currentUrl)),
          initialSettings: _settings,
          onWebViewCreated: (controller) {
            debugPrint('WebView created successfully');
            _controller = controller;

            // إضافة JavaScript handler للطباعة
            controller.addJavaScriptHandler(
              handlerName: 'printHandler',
              callback: (args) async {
                // إذا تم إرسال HTML، نطبعه مباشرة
                if (args.isNotEmpty && args[0] != null && args[0].toString().isNotEmpty) {
                  await _printHtml(args[0].toString());
                } else {
                  // fallback: طباعة الصفحة الحالية
                  await _printCurrentPage();
                }
                return true;
              },
            );
          },
          onLoadStart: (controller, url) {
            debugPrint('WebView load started: $url');
            setState(() {
              _isLoading = true;
              _hasError = false;
              if (url != null) {
                _currentUrl = url.toString();
              }
            });
          },
          onProgressChanged: (controller, progress) {
            setState(() => _loadingProgress = progress);
          },
          onLoadStop: (controller, url) async {
            debugPrint('WebView load stopped: $url');
            if (url != null) {
              _currentUrl = url.toString();
            }

            // حقن JavaScript للتقاط window.print()
            await controller.evaluateJavascript(source: '''
              (function() {
                // حفظ الدالة الأصلية
                var originalPrint = window.print;

                // استبدال window.print
                window.print = function() {
                  // إرسال طلب للتطبيق
                  if (window.flutter_inappwebview) {
                    window.flutter_inappwebview.callHandler('printHandler');
                  } else {
                    // fallback للمتصفح العادي
                    originalPrint.call(window);
                  }
                };
              })();
            ''');

            setState(() {
              _isLoading = false;
            });
          },
          onReceivedError: (controller, request, error) {
            debugPrint('WebView error: ${error.description}');
            if (request.isForMainFrame ?? false) {
              setState(() {
                _isLoading = false;
                _hasError = true;
              });
            }
          },
          onConsoleMessage: (controller, consoleMessage) {
            debugPrint('WebView Console: ${consoleMessage.message}');
          },
          shouldOverrideUrlLoading: (controller, navigationAction) async {
            return NavigationActionPolicy.ALLOW;
          },
          // التقاط طلب الطباعة - استخدام الطباعة المدمجة
          onPrintRequest: (controller, url, printJobController) async {
            // السماح بالطباعة الافتراضية للـ WebView
            return false;
          },
        ),
        // شريط التحميل
        if (_isLoading)
          Positioned(
            top: 0,
            left: 0,
            right: 0,
            child: LinearProgressIndicator(
              value: _loadingProgress / 100,
              minHeight: 3,
            ),
          ),
        // مؤشر الطباعة المتطور
        if (_isPrinting)
          Container(
            color: Colors.black.withOpacity(0.7),
            child: Center(
              child: Container(
                margin: const EdgeInsets.all(32),
                padding: const EdgeInsets.symmetric(horizontal: 40, vertical: 32),
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [
                      Theme.of(context).colorScheme.surface,
                      Theme.of(context).colorScheme.surface.withOpacity(0.95),
                    ],
                  ),
                  borderRadius: BorderRadius.circular(20),
                  boxShadow: [
                    BoxShadow(
                      color: Theme.of(context).colorScheme.primary.withOpacity(0.3),
                      blurRadius: 30,
                      spreadRadius: 5,
                    ),
                  ],
                ),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    // أيقونة الطابعة مع تأثير
                    Container(
                      padding: const EdgeInsets.all(20),
                      decoration: BoxDecoration(
                        color: Theme.of(context).colorScheme.primaryContainer.withOpacity(0.3),
                        shape: BoxShape.circle,
                      ),
                      child: Icon(
                        Icons.print_rounded,
                        size: 48,
                        color: Theme.of(context).colorScheme.primary,
                      ),
                    ),
                    const SizedBox(height: 24),
                    // شريط التحميل المتحرك
                    SizedBox(
                      width: 200,
                      child: ClipRRect(
                        borderRadius: BorderRadius.circular(10),
                        child: LinearProgressIndicator(
                          minHeight: 6,
                          backgroundColor: Theme.of(context).colorScheme.primaryContainer.withOpacity(0.3),
                          valueColor: AlwaysStoppedAnimation<Color>(
                            Theme.of(context).colorScheme.primary,
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(height: 20),
                    // النص
                    Text(
                      context.tr('printing'),
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w600,
                        color: Theme.of(context).colorScheme.onSurface,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Please wait...',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: Theme.of(context).colorScheme.onSurface.withOpacity(0.6),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }
}
