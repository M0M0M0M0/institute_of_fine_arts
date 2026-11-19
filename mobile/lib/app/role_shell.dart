import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

/// One entry of a bottom navigation bar.
class ShellDestination {
  const ShellDestination({required this.label, required this.icon, required this.selectedIcon});

  final String label;
  final IconData icon;
  final IconData selectedIcon;
}

/// Common frame of the guest / student / staff shells: the active tab (a go_router
/// `StatefulNavigationShell`) above a bottom `NavigationBar`.
///
/// Every tab page is a full `Scaffold` that brings its own `AppBar` (see `TabScaffold`).
/// [onSelect] lets a shell override what a tap does (the guest "Sign in" tab leaves the shell).
class RoleShell extends StatelessWidget {
  const RoleShell({super.key, required this.navigationShell, required this.destinations, this.onSelect});

  final StatefulNavigationShell navigationShell;
  final List<ShellDestination> destinations;

  /// Return true when the tap was handled and the branch must not be switched.
  final bool Function(int index)? onSelect;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: navigationShell,
      // Five tabs share the width: the labels keep their size even with a large system font, so "Competitions"
      // stays on one line (icons and the rest of the app still follow the user's font size)
      bottomNavigationBar: MediaQuery.withClampedTextScaling(
        maxScaleFactor: 1.0,
        child: NavigationBar(
          selectedIndex: navigationShell.currentIndex,
          onDestinationSelected: (i) {
            if (onSelect?.call(i) ?? false) return;
            // Tapping the active tab returns to its first page.
            navigationShell.goBranch(i, initialLocation: i == navigationShell.currentIndex);
          },
          destinations: [
            for (final d in destinations)
              NavigationDestination(icon: Icon(d.icon), selectedIcon: Icon(d.selectedIcon), label: d.label),
          ],
        ),
      ),
    );
  }
}
