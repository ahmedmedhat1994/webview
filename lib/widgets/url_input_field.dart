import 'package:flutter/material.dart';
import '../core/localization/app_localizations.dart';
import '../services/url_validator_service.dart';

class UrlInputField extends StatefulWidget {
  final TextEditingController controller;
  final String? initialValue;
  final ValueChanged<String>? onSubmitted;
  final bool enabled;

  const UrlInputField({
    super.key,
    required this.controller,
    this.initialValue,
    this.onSubmitted,
    this.enabled = true,
  });

  @override
  State<UrlInputField> createState() => _UrlInputFieldState();
}

class _UrlInputFieldState extends State<UrlInputField> {
  String? _errorText;

  @override
  void initState() {
    super.initState();
    if (widget.initialValue != null) {
      widget.controller.text = widget.initialValue!;
    }
  }

  void _validate() {
    final result = UrlValidatorService.instance.validate(widget.controller.text);
    setState(() {
      _errorText = result.isValid ? null : context.tr(result.errorKey ?? 'invalid_url');
    });
  }

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: widget.controller,
      enabled: widget.enabled,
      keyboardType: TextInputType.url,
      textInputAction: TextInputAction.done,
      textDirection: TextDirection.ltr,
      decoration: InputDecoration(
        labelText: context.tr('enter_url'),
        hintText: context.tr('enter_url_hint'),
        errorText: _errorText,
        prefixIcon: const Icon(Icons.link),
        suffixIcon: widget.controller.text.isNotEmpty
            ? IconButton(
                icon: const Icon(Icons.clear),
                onPressed: () {
                  widget.controller.clear();
                  setState(() {
                    _errorText = null;
                  });
                },
              )
            : null,
      ),
      onChanged: (_) {
        if (_errorText != null) {
          _validate();
        }
        setState(() {});
      },
      onSubmitted: (value) {
        _validate();
        if (_errorText == null && widget.onSubmitted != null) {
          widget.onSubmitted!(value);
        }
      },
    );
  }
}
