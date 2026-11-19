import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/role_shell.dart';

/// Teacher bottom bar: Grading · Account. Teachers grade the entries of closed competitions; the rest of the
/// institute's work (awards, exhibitions) is done by staff.
/// Branch order must match the teacher `StatefulShellRoute` in app/router.dart.
class TeacherShell extends StatelessWidget {
  const TeacherShell({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  static const destinations = [
    ShellDestination(label: 'Grading', icon: Icons.rate_review_outlined, selectedIcon: Icons.rate_review),
    ShellDestination(label: 'Account', icon: Icons.person_outline, selectedIcon: Icons.person),
  ];

  @override
  Widget build(BuildContext context) => RoleShell(navigationShell: navigationShell, destinations: destinations);
}
