import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/api_client.dart';
import 'auth.dart';

/// M-G6: sign in. After a successful sign in the router sends the user to the page of their role.
///
/// Wide screens: a dark panel with the institute's mark on the left and the form on the right, like the entrance
/// of a gallery. Phones: the same panel as a compact header above the form.
class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _username = TextEditingController();
  final _password = TextEditingController();
  bool _obscure = true;

  @override
  void dispose() {
    _username.dispose();
    _password.dispose();
    super.dispose();
  }

  // Client-side checks first; server errors come from the auth state
  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    await ref.read(authControllerProvider.notifier).login(_username.text.trim(), _password.text);
  }

  @override
  Widget build(BuildContext context) {
    final wide = MediaQuery.sizeOf(context).width >= AppLayout.wide;
    final form = _form(context);
    return Scaffold(
      backgroundColor: AppColors.wall,
      body: wide
          ? Row(
              children: [
                const Expanded(flex: 5, child: _BrandPanel(wide: true)),
                Expanded(
                  flex: 5,
                  child: Center(
                    child: SingleChildScrollView(
                      padding: const EdgeInsets.all(AppSpacing.xl),
                      child: ConstrainedBox(constraints: const BoxConstraints(maxWidth: 400), child: form),
                    ),
                  ),
                ),
              ],
            )
          : SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const _BrandPanel(wide: false),
                  Center(
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 440),
                      child: Padding(padding: const EdgeInsets.all(AppSpacing.lg), child: form),
                    ),
                  ),
                ],
              ),
            ),
    );
  }

  /// Underlined fields: quieter than boxes, like lines on a form card.
  InputDecoration _decoration(String label, {Widget? suffix}) => InputDecoration(
        labelText: label,
        suffixIcon: suffix,
        filled: false,
        contentPadding: const EdgeInsets.symmetric(vertical: 14),
        border: const UnderlineInputBorder(borderSide: BorderSide(color: AppColors.muted)),
        enabledBorder: const UnderlineInputBorder(borderSide: BorderSide(color: AppColors.muted)),
        focusedBorder: const UnderlineInputBorder(borderSide: BorderSide(color: AppColors.ink, width: 2)),
        errorBorder: const UnderlineInputBorder(borderSide: BorderSide(color: AppColors.danger)),
        focusedErrorBorder: const UnderlineInputBorder(borderSide: BorderSide(color: AppColors.danger, width: 2)),
      );

  Widget _form(BuildContext context) {
    final auth = ref.watch(authControllerProvider);
    final loading = auth.isLoading;
    final error = auth.hasError ? ApiException.from(auth.error!).message : null;
    final text = Theme.of(context).textTheme;

    return Form(
      key: _formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Align(
            alignment: Alignment.centerLeft,
            child: SizedBox(width: 28, height: 2, child: ColoredBox(color: AppColors.brass)),
          ),
          const SizedBox(height: AppSpacing.md),
          Semantics(header: true, child: Text('Sign in', style: text.headlineMedium)),
          const SizedBox(height: AppSpacing.xs),
          Text('Enter competitions and follow your entries.', style: text.bodyMedium!.copyWith(color: AppColors.muted)),
          const SizedBox(height: AppSpacing.xl),
          TextFormField(
            controller: _username,
            enabled: !loading,
            autofillHints: const [AutofillHints.username],
            decoration: _decoration('Username'),
            textInputAction: TextInputAction.next,
            validator: (v) => (v == null || v.trim().isEmpty) ? 'Enter your username' : null,
          ),
          const SizedBox(height: AppSpacing.md),
          TextFormField(
            controller: _password,
            enabled: !loading,
            obscureText: _obscure,
            autofillHints: const [AutofillHints.password],
            decoration: _decoration(
              'Password',
              suffix: IconButton(
                tooltip: _obscure ? 'Show password' : 'Hide password',
                icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                onPressed: () => setState(() => _obscure = !_obscure),
              ),
            ),
            onFieldSubmitted: (_) => _submit(),
            validator: (v) => (v == null || v.isEmpty) ? 'Enter your password' : null,
          ),
          if (error != null) ...[
            const SizedBox(height: AppSpacing.lg),
            Semantics(
              liveRegion: true,
              child: Container(
                padding: const EdgeInsets.all(AppSpacing.md),
                decoration: BoxDecoration(
                  color: AppColors.danger.withValues(alpha: 0.08),
                  borderRadius: AppRadius.mdAll,
                  border: Border.all(color: AppColors.danger.withValues(alpha: 0.4)),
                ),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Icon(Icons.error_outline, color: AppColors.danger, size: 20),
                    const SizedBox(width: AppSpacing.sm),
                    Expanded(child: Text(error, style: text.bodyMedium!.copyWith(color: AppColors.danger))),
                  ],
                ),
              ),
            ),
          ],
          const SizedBox(height: AppSpacing.xl),
          FilledButton(
            onPressed: loading ? null : _submit,
            child: loading
                ? const SizedBox(
                    height: 20,
                    width: 20,
                    child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                  )
                : const Text('SIGN IN'),
          ),
          const SizedBox(height: AppSpacing.sm),
          TextButton(onPressed: () => context.go('/'), child: const Text('CONTINUE AS GUEST')),
        ],
      ),
    );
  }
}

/// Dark panel with the institute's mark, name and a line of welcome, and the way back to the home page.
class _BrandPanel extends StatelessWidget {
  const _BrandPanel({required this.wide});

  final bool wide;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final pad = wide ? AppSpacing.xxl : AppSpacing.lg;

    final content = Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // Back to the home page
        TextButton.icon(
          onPressed: () => context.go('/'),
          style: TextButton.styleFrom(foregroundColor: AppColors.stone, padding: EdgeInsets.zero),
          icon: const Icon(Icons.arrow_back, size: 18),
          label: const Text('HOME'),
        ),
        SizedBox(height: wide ? AppSpacing.xxl * 2 : AppSpacing.lg),
        Image.asset(
          'assets/images/logo_ifa_transparent.png',
          height: wide ? 72 : 48,
          alignment: Alignment.centerLeft,
          excludeFromSemantics: true,
        ),
        SizedBox(height: wide ? AppSpacing.xl : AppSpacing.md),
        const SizedBox(width: 36, height: 2, child: ColoredBox(color: AppColors.brass)),
        const SizedBox(height: AppSpacing.md),
        Semantics(
          header: true,
          child: Text(
            'Institute of\nFine Arts',
            style: (wide ? text.displayMedium : text.headlineLarge)!.copyWith(color: Colors.white),
          ),
        ),
        const SizedBox(height: AppSpacing.sm),
        Text(
          'Where young painters compete, exhibit and are seen.',
          style: text.bodyLarge!.copyWith(color: Colors.white.withValues(alpha: 0.8)),
        ),
      ],
    );

    return ColoredBox(
      color: AppColors.ink,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: EdgeInsets.fromLTRB(pad, wide ? pad : AppSpacing.sm, pad, pad),
          child: wide ? Align(alignment: Alignment.centerLeft, child: content) : content,
        ),
      ),
    );
  }
}
