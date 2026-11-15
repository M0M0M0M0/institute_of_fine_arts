import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/tokens.dart';
import '../../shared/tab_scaffold.dart';
import '../auth/auth.dart';

/// Account tab shared by students and staff (M-S7 / M-T6): profile fields and sign out.
/// Basic version; the phase-2 agents may restyle it.
class AccountScreen extends ConsumerWidget {
  const AccountScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = currentUserOf(ref.watch(authControllerProvider));
    final isStudent = user?.role == Role.student;

    Widget row(String label, String? value) => value == null || value.isEmpty
        ? const SizedBox.shrink()
        : ListTile(title: Text(label, style: Theme.of(context).textTheme.bodySmall), subtitle: Text(value));

    return TabScaffold(
      title: 'Account',
      body: ListView(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
        children: [
          if (user != null) ...[
            ListTile(
              title: Text(user.fullName, style: Theme.of(context).textTheme.headlineSmall),
              subtitle: Text('@${user.username}'),
            ),
            row(isStudent ? 'Student code' : 'Staff code', user.code),
            row('Email', user.email),
            if (isStudent) row('Class', user.className) else row('Position', user.position),
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
