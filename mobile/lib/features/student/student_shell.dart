import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/role_shell.dart';

/// Student bottom bar: Home · Competitions · My entries · Exhibitions · Account.
/// Branch order must match the student `StatefulShellRoute` in app/router.dart.
class StudentShell extends StatelessWidget {
  const StudentShell({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  static const destinations = [
    ShellDestination(label: 'Home', icon: Icons.home_outlined, selectedIcon: Icons.home),
    ShellDestination(label: 'Competitions', icon: Icons.palette_outlined, selectedIcon: Icons.palette),
    ShellDestination(label: 'My entries', icon: Icons.image_outlined, selectedIcon: Icons.image),
    ShellDestination(label: 'Exhibitions', icon: Icons.museum_outlined, selectedIcon: Icons.museum),
    ShellDestination(label: 'Account', icon: Icons.person_outline, selectedIcon: Icons.person),
  ];

  @override
  Widget build(BuildContext context) => RoleShell(navigationShell: navigationShell, destinations: destinations);
}
