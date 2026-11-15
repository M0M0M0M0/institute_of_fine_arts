import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../shared/tab_scaffold.dart';
import '../auth/auth.dart';

/// PLACEHOLDER (M-S1) – replaced by the student phase-2 agent.
/// Data is ready: `studentDashboardProvider` in dashboard.dart.
class StudentHomeScreen extends ConsumerWidget {
  const StudentHomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = currentUserOf(ref.watch(authControllerProvider));
    return TabScaffold(
      title: 'Home',
      body: PlaceholderBody(title: 'Welcome, ${user?.firstName ?? 'student'}'),
    );
  }
}
