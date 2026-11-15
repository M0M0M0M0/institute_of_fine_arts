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

    Widget row(IconData icon, String label, String? value) => value == null || value.isEmpty
        ? const SizedBox.shrink()
        : ListTile(
            leading: Icon(icon, color: AppColors.muted),
            title: Text(label, style: text.bodySmall),
            subtitle: Text(value, style: text.bodyLarge),
          );

    return TabScaffold(
      title: 'Account',
      body: ListView(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.lg),
        children: [
          if (user != null) ...[
            Center(
              child: Semantics(
                label: 'Avatar of ${user.fullName}',
                excludeSemantics: true,
                child: CircleAvatar(
                  radius: 40,
                  backgroundColor: AppColors.ink,
                  foregroundColor: Colors.white,
                  child: Text(initialsOf(user.fullName), style: text.headlineSmall!.copyWith(color: Colors.white)),
                ),
              ),
            ),
            const SizedBox(height: AppSpacing.md),
            Text(user.fullName, style: text.headlineSmall, textAlign: TextAlign.center),
            Text('@${user.username}', style: text.bodyMedium, textAlign: TextAlign.center),
            const SizedBox(height: AppSpacing.lg),
            const Divider(height: 1),
            row(Icons.badge_outlined, isStudent ? 'Student code' : 'Staff code', user.code),
            row(Icons.mail_outline, 'Email', user.email),
            if (isStudent)
              row(Icons.school_outlined, 'Class', user.className)
            else
              row(Icons.work_outline, 'Position', user.position),
          ],
          const Divider(height: AppSpacing.xl),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md),
            child: OutlinedButton.icon(
              onPressed: () => ref.read(authControllerProvider.notifier).logout(),
              icon: const Icon(Icons.logout),
              label: const Text('Sign out'),
            ),
          ),
        ],
      ),
    );
  }
}
