import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';

import 'tokens.dart';

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

  void _select(int i) {
    // A light tick when the section changes (motion is not the only feedback)
    if (i != navigationShell.currentIndex) HapticFeedback.selectionClick();
    if (onSelect?.call(i) ?? false) return;
    // Tapping the active tab returns to its first page.
    navigationShell.goBranch(i, initialLocation: i == navigationShell.currentIndex);
  }

  @override
  Widget build(BuildContext context) {
    final wide = MediaQuery.sizeOf(context).width >= AppLayout.wide;
    if (wide) {
      return Scaffold(
        body: Column(
          children: [
            _TopNav(destinations: destinations, selected: navigationShell.currentIndex, onSelect: _select),
            Expanded(child: navigationShell),
          ],
        ),
      );
    }
    return Scaffold(
      body: navigationShell,
      // Five tabs share the width: the labels keep their size even with a large system font, so "Competitions"
      // stays on one line (icons and the rest of the app still follow the user's font size)
      bottomNavigationBar: DecoratedBox(
        decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.line))),
        child: MediaQuery.withClampedTextScaling(
          maxScaleFactor: 1.0,
          child: NavigationBar(
            selectedIndex: navigationShell.currentIndex,
            onDestinationSelected: _select,
            destinations: [
              for (final d in destinations)
                NavigationDestination(icon: Icon(d.icon), selectedIcon: Icon(d.selectedIcon), label: d.label),
            ],
          ),
        ),
      ),
    );
  }
}

/// Museum-style top bar for wide screens: the institute's name on the left, the sections as small capitals on the right.
class _TopNav extends StatelessWidget {
  const _TopNav({required this.destinations, required this.selected, required this.onSelect});

  final List<ShellDestination> destinations;
  final int selected;
  final ValueChanged<int> onSelect;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final gutter = AppLayout.gutterFor(MediaQuery.sizeOf(context).width);
    return Material(
      color: AppColors.wall,
      child: DecoratedBox(
        decoration: const BoxDecoration(border: Border(bottom: BorderSide(color: AppColors.line))),
        child: SafeArea(
          bottom: false,
          child: Padding(
            padding: EdgeInsets.symmetric(horizontal: gutter),
            child: SizedBox(
              height: 72,
              child: Row(
                children: [
                  Text(
                    'INSTITUTE OF FINE ARTS',
                    style: text.titleSmall!.copyWith(letterSpacing: 2.4, fontWeight: FontWeight.w600, color: AppColors.ink),
                  ),
                  const Spacer(),
                  for (var i = 0; i < destinations.length; i++)
                    _NavLink(label: destinations[i].label, active: i == selected, onTap: () => onSelect(i)),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _NavLink extends StatelessWidget {
  const _NavLink({required this.label, required this.active, required this.onTap});

  final String label;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Semantics(
      button: true,
      selected: active,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md),
          child: SizedBox(
            height: 72,
            child: Stack(
              alignment: Alignment.center,
              children: [
                Text(
                  label.toUpperCase(),
                  style: text.labelSmall!.copyWith(
                    fontSize: 12.5,
                    letterSpacing: 1.6,
                    color: active ? AppColors.ink : AppColors.muted,
                  ),
                ),
                // Brass rule under the current section
                if (active)
                  const Positioned(
                    left: 0,
                    right: 0,
                    bottom: 0,
                    child: SizedBox(height: 2, child: ColoredBox(color: AppColors.brass)),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
