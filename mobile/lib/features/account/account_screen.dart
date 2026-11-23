import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/tokens.dart';
import '../../core/api_client.dart';
import '../../shared/tab_scaffold.dart';
import '../auth/auth.dart';

/// Full profile of the signed-in user. The login response lacks email / code / class / position,
/// so they are read from `GET /api/auth/me`; on any error the basic login data is shown.
final accountProfileProvider = FutureProvider.autoDispose<AppUser?>((ref) async {
  final dio = ref.watch(dioProvider);
  try {
    final res = await dio.get<Map<String, dynamic>>('/api/auth/me');
    return AppUser.fromJson(res.data!);
  } catch (_) {
    return null;
  }
});

/// Up to two initials of a full name ("Nguyen Van An" -> "NA").
String initialsOf(String fullName) {
  final parts = fullName.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
  if (parts.isEmpty) return '?';
  if (parts.length == 1) return parts.first[0].toUpperCase();
  return (parts.first[0] + parts.last[0]).toUpperCase();
}

/// Account tab shared by students (M-S7) and staff (M-T6): avatar, name, code, email,
/// class (students) or position (staff), and Sign out.
class AccountScreen extends ConsumerWidget {
  const AccountScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final signedIn = currentUserOf(ref.watch(authControllerProvider));
    final user = ref.watch(accountProfileProvider).value ?? signedIn;
    final isStudent = (user ?? signedIn)?.role == Role.student;
    final text = Theme.of(context).textTheme;
    final gutter = AppLayout.gutterFor(MediaQuery.sizeOf(context).width);

    // One fact: small-caps name on the left, value on the right, a hairline above (like the competition details)
    Widget row(String label, String? value) => value == null || value.isEmpty
        ? const SizedBox.shrink()
        : DecoratedBox(
            decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.line))),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.baseline,
                textBaseline: TextBaseline.alphabetic,
                children: [
                  SizedBox(
                    width: 150,
                    child: Text(
                      label.toUpperCase(),
                      style: text.labelSmall!.copyWith(color: AppColors.muted, letterSpacing: 1.4),
                    ),
                  ),
                  Expanded(child: Text(value, style: text.bodyLarge!.copyWith(fontWeight: FontWeight.w500))),
                ],
              ),
            ),
          );

    return TabScaffold(
      title: 'Account',
      body: ListView(
        padding: EdgeInsets.fromLTRB(gutter, AppSpacing.lg, gutter, AppSpacing.xl),
        children: [
          // The page stays a readable column even on a wide screen
          Align(
            alignment: Alignment.topLeft,
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 560),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (user != null) ...[
                    Row(
                      children: [
                        Semantics(
                          label: 'Avatar of ${user.fullName}',
                          excludeSemantics: true,
                          child: Container(
                            width: 72,
                            height: 72,
                            alignment: Alignment.center,
                            color: AppColors.ink,
                            child: Text(
                              initialsOf(user.fullName),
                              style: text.headlineSmall!.copyWith(color: AppColors.stone, letterSpacing: 1),
                            ),
                          ),
                        ),
                        const SizedBox(width: AppSpacing.md),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Semantics(header: true, child: Text(user.fullName, style: text.headlineMedium)),
                              Text('@${user.username}', style: text.bodyMedium!.copyWith(color: AppColors.muted)),
                            ],
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.xl),
                    row(isStudent ? 'Student code' : 'Staff code', user.code),
                    row('Email', user.email),
                    if (isStudent) row('Class', user.className) else row('Position', user.position),
                    const Divider(height: 1),
                  ],
                  const SizedBox(height: AppSpacing.xl),
                  OutlinedButton.icon(
                    onPressed: () => ref.read(authControllerProvider.notifier).logout(),
                    icon: const Icon(Icons.logout, size: 18),
                    label: const Text('SIGN OUT'),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
