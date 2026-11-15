import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/role_shell.dart';

/// Staff bottom bar: Home · Grading · Awards · Exhibitions · Account.
/// Branch order must match the staff `StatefulShellRoute` in app/router.dart.
class StaffShell extends StatelessWidget {
  const StaffShell({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  static const destinations = [
    ShellDestination(label: 'Home', icon: Icons.home_outlined, selectedIcon: Icons.home),
    ShellDestination(label: 'Grading', icon: Icons.rate_review_outlined, selectedIcon: Icons.rate_review),
    ShellDestination(label: 'Awards', icon: Icons.emoji_events_outlined, selectedIcon: Icons.emoji_events),
    ShellDestination(label: 'Exhibitions', icon: Icons.museum_outlined, selectedIcon: Icons.museum),
    ShellDestination(label: 'Account', icon: Icons.person_outline, selectedIcon: Icons.person),
  ];

  @override
  Widget build(BuildContext context) => RoleShell(navigationShell: navigationShell, destinations: destinations);
}
