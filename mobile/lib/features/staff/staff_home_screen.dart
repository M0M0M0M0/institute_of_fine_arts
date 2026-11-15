import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../shared/tab_scaffold.dart';
import '../auth/auth.dart';

/// PLACEHOLDER (M-T1) – replaced by the staff phase-2 agent.
/// Data is ready: `staffDashboardProvider` in dashboard.dart.
class StaffHomeScreen extends ConsumerWidget {
  const StaffHomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = currentUserOf(ref.watch(authControllerProvider));
    return TabScaffold(
      title: 'Home',
      body: PlaceholderBody(title: 'Welcome, ${user?.firstName ?? 'staff'}'),
    );
  }
}
