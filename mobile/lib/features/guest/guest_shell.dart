import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/role_shell.dart';
import '../auth/auth.dart';
import '../home/home_screen.dart' show homePathFor;

/// Guest bottom bar: Home · Competitions · Gallery · Sign in.
/// The first three tabs are branches of the guest `StatefulShellRoute`; "Sign in" opens `/login`
/// (a signed-in user who lands on a guest page sees "My page" there instead).
class GuestShell extends ConsumerWidget {
  const GuestShell({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = currentUserOf(ref.watch(authControllerProvider));
    return RoleShell(
      navigationShell: navigationShell,
      destinations: [
        const ShellDestination(label: 'Home', icon: Icons.home_outlined, selectedIcon: Icons.home),
        const ShellDestination(label: 'Competitions', icon: Icons.palette_outlined, selectedIcon: Icons.palette),
        const ShellDestination(label: 'Gallery', icon: Icons.photo_library_outlined, selectedIcon: Icons.photo_library),
        ShellDestination(
          label: user == null ? 'Sign in' : 'My page',
          icon: Icons.login,
          selectedIcon: Icons.login,
        ),
      ],
      onSelect: (i) {
        if (i != 3) return false;
        context.go(user == null ? '/login' : homePathFor(user.role));
        return true;
      },
    );
  }
}
